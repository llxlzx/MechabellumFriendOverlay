using System.Runtime.InteropServices;
using System.Text;
using FriendOverlay.Core;

namespace FriendOverlay.Hooks
{
    /// <summary>
    /// Steam clears the library button only when this process disappears before the
    /// overlay restart loop. ExitProcess hangs in DLL detach and kills any fallback
    /// thread first. The syscall number is read from ntdll on disk, then invoked
    /// through a stub so an in-memory hook cannot swallow it.
    /// </summary>
    public static class QuitStallHooks
    {
        private const uint MemCommit = 0x1000;
        private const uint MemReserve = 0x2000;
        private const uint PageReadWrite = 0x04;
        private const uint PageExecuteRead = 0x20;

        private static int _exitOnce;
        private static NtTerminateDelegate? _terminate;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int NtTerminateDelegate(IntPtr processHandle, uint exitStatus);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocationType, uint protect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint newProtect, out uint oldProtect);

        public static void Apply()
        {
            PrepareSyscall();
            // Foreground so the runtime does not abort this thread during shutdown.
            var watcher = new Thread(WatchPlayerLog)
            {
                IsBackground = false,
                Name = "FriendOverlay.QuitLog"
            };
            watcher.Start();
            MelonLoader.MelonLogger.Msg("[FriendOverlay] quit stall guard installed");
        }

        /// <summary>
        /// Ends this process. Called when Player.log shows the quit marker, and again
        /// from OnApplicationQuit after preferences are saved.
        /// </summary>
        public static void EndThisProcess()
        {
            if (Interlocked.Exchange(ref _exitOnce, 1) != 0)
                return;

            if (_terminate == null)
            {
                Note("syscall id unavailable");
                return;
            }

            Note("syscall exit");
            var status = _terminate(GetCurrentProcess(), 0);
            Note("syscall status=" + status);
        }

        private static void PrepareSyscall()
        {
            if (!TryReadSyscallId(out var id))
            {
                Note("syscall id unavailable");
                return;
            }

            var stub = new byte[]
            {
                0x4C, 0x8B, 0xD1,
                0xB8, (byte)id, (byte)(id >> 8), (byte)(id >> 16), (byte)(id >> 24),
                0x0F, 0x05,
                0xC3
            };
            var memory = VirtualAlloc(IntPtr.Zero, (UIntPtr)stub.Length, MemCommit | MemReserve, PageReadWrite);
            if (memory == IntPtr.Zero)
            {
                Note("syscall id unavailable");
                return;
            }

            Marshal.Copy(stub, 0, memory, stub.Length);
            if (!VirtualProtect(memory, (UIntPtr)stub.Length, PageExecuteRead, out _))
            {
                Note("syscall id unavailable");
                return;
            }

            _terminate = Marshal.GetDelegateForFunctionPointer<NtTerminateDelegate>(memory);
            Note("syscall ready id=" + id.ToString("X"));
        }

        private static bool TryReadSyscallId(out uint id)
        {
            id = 0;
            try
            {
                var path = Path.Combine(Environment.SystemDirectory, "ntdll.dll");
                var data = File.ReadAllBytes(path);
                if (data.Length < 0x40)
                    return false;

                var pe = BitConverter.ToInt32(data, 0x3C);
                if (pe <= 0 || pe + 24 >= data.Length)
                    return false;
                if (data[pe] != (byte)'P' || data[pe + 1] != (byte)'E')
                    return false;

                var coff = pe + 4;
                var sections = BitConverter.ToUInt16(data, coff + 2);
                var sizeOpt = BitConverter.ToUInt16(data, coff + 16);
                var optional = coff + 20;
                if (BitConverter.ToUInt16(data, optional) != 0x20B)
                    return false;

                var exportRva = BitConverter.ToUInt32(data, optional + 112);
                var sectionTable = optional + sizeOpt;

                int RvaToOffset(uint rva)
                {
                    for (var i = 0; i < sections; i++)
                    {
                        var entry = sectionTable + i * 40;
                        if (entry + 24 > data.Length)
                            return -1;
                        var virtualSize = BitConverter.ToUInt32(data, entry + 8);
                        var virtualAddress = BitConverter.ToUInt32(data, entry + 12);
                        var rawSize = BitConverter.ToUInt32(data, entry + 16);
                        var raw = BitConverter.ToUInt32(data, entry + 20);
                        var span = Math.Max(virtualSize, rawSize);
                        if (span == 0)
                            continue;
                        if (rva >= virtualAddress && rva < virtualAddress + span)
                            return (int)(raw + (rva - virtualAddress));
                    }

                    return -1;
                }

                var export = RvaToOffset(exportRva);
                if (export < 0 || export + 40 > data.Length)
                    return false;

                var nameCount = BitConverter.ToInt32(data, export + 24);
                var functions = RvaToOffset(BitConverter.ToUInt32(data, export + 28));
                var names = RvaToOffset(BitConverter.ToUInt32(data, export + 32));
                var ordinals = RvaToOffset(BitConverter.ToUInt32(data, export + 36));
                if (functions < 0 || names < 0 || ordinals < 0 || nameCount <= 0)
                    return false;

                var target = Encoding.ASCII.GetBytes("NtTerminateProcess");
                for (var i = 0; i < nameCount; i++)
                {
                    var nameRva = BitConverter.ToUInt32(data, names + i * 4);
                    var name = RvaToOffset(nameRva);
                    if (name < 0 || name + target.Length + 1 > data.Length)
                        continue;

                    var match = true;
                    for (var j = 0; j < target.Length; j++)
                    {
                        if (data[name + j] != target[j])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (!match || data[name + target.Length] != 0)
                        continue;

                    var ordinal = BitConverter.ToUInt16(data, ordinals + i * 2);
                    var functionRva = BitConverter.ToUInt32(data, functions + ordinal * 4);
                    var function = RvaToOffset(functionRva);
                    if (function < 0)
                        return false;
                    return TryParsePrologue(data, function, out id);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Accepts the short stub, or the same stub with Windows' SharedUserData gate
        /// between mov eax and syscall. The id is the imm32 in that mov. Anything else
        /// is refused.
        /// </summary>
        private static bool TryParsePrologue(byte[] data, int offset, out uint id)
        {
            id = 0;
            if (offset < 0 || offset + 11 > data.Length)
                return false;
            if (data[offset] != 0x4C || data[offset + 1] != 0x8B || data[offset + 2] != 0xD1 || data[offset + 3] != 0xB8)
                return false;

            id = BitConverter.ToUInt32(data, offset + 4);
            if (data[offset + 8] == 0x0F && data[offset + 9] == 0x05 && data[offset + 10] == 0xC3)
                return true;

            byte[] gate =
            {
                0xF6, 0x04, 0x25, 0x08, 0x03, 0xFE, 0x7F, 0x01, 0x75, 0x03, 0x0F, 0x05, 0xC3
            };
            if (offset + 8 + gate.Length > data.Length)
                return false;
            for (var i = 0; i < gate.Length; i++)
            {
                if (data[offset + 8 + i] != gate[i])
                    return false;
            }

            return true;
        }

        private static void WatchPlayerLog()
        {
            var path = PlayerLogPath();
            var offset = FileLength(path);

            while (true)
            {
                Thread.Sleep(200);
                if (Environment.HasShutdownStarted)
                {
                    Note("shutdown started");
                    EndThisProcess();
                    return;
                }

                try
                {
                    var length = FileLength(path);
                    if (length < 0)
                    {
                        offset = 0;
                        continue;
                    }

                    if (length < offset)
                        offset = 0;
                    if (length <= offset)
                        continue;

                    var count = (int)Math.Min(length - offset, 512 * 1024);
                    var buffer = new byte[count];
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        stream.Seek(offset, SeekOrigin.Begin);
                        count = stream.Read(buffer, 0, count);
                    }

                    offset = length;
                    if (!QuitStallGuard.TailHasQuitMarker(buffer, count))
                        continue;

                    Note("quit marker seen");
                    EndThisProcess();
                    return;
                }
                catch
                {
                    // The log is rewritten while Unity still has it open. The next tick retries.
                }
            }
        }

        private static string PlayerLogPath()
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = Path.GetDirectoryName(local) ?? local;
            return Path.Combine(root, "LocalLow", "GameRiver", "Mechabellum", "Player.log");
        }

        private static long FileLength(string path)
        {
            try
            {
                return File.Exists(path) ? new FileInfo(path).Length : -1;
            }
            catch
            {
                return -1;
            }
        }

        private static string NoteDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MechabellumFriendOverlay");
        }

        private static string NoteFilePath() => Path.Combine(NoteDirectory(), "quit-stall.log");

        private static void Note(string message)
        {
            try
            {
                Directory.CreateDirectory(NoteDirectory());
                File.AppendAllText(
                    NoteFilePath(),
                    DateTime.Now.ToString("HH:mm:ss.fff ") + message + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
                // The note is diagnostic. Ending the process does not depend on it.
            }
        }
    }
}
