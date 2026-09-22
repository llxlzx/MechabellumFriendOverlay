using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FriendOverlay.Core;
using MelonLoader;

namespace FriendOverlay.Hooks
{
    /// <summary>
    /// Steam stays in-game until Mechabellum.exe exits. The game logs
    /// OnApplicationWantsToQuit and then stalls inside that callback, so a new
    /// thread started from the callback never runs. This watcher is started at
    /// load, and it is the thread that ends the process.
    /// </summary>
    public static class QuitStallHooks
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        public static void Apply()
        {
            var watcher = new Thread(WatchPlayerLog)
            {
                IsBackground = true,
                Name = "FriendOverlay.QuitLog"
            };
            watcher.Start();
            MelonLogger.Msg("[FriendOverlay] quit stall guard installed");
        }

        private static void WatchPlayerLog()
        {
            var path = PlayerLogPath();
            var offset = FileLength(path);

            while (true)
            {
                Thread.Sleep(400);
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
                    Thread.Sleep(QuitStallGuard.AcceptedQuitGraceMs);
                    Note("ending process");
                    EndGameProcess();
                    return;
                }
                catch
                {
                    // The log is rewritten while Unity still has it open. The next tick retries.
                }
            }
        }

        private static void EndGameProcess()
        {
            try
            {
                foreach (var handler in Process.GetProcessesByName("UnityCrashHandler64"))
                {
                    try
                    {
                        handler.Kill();
                    }
                    catch
                    {
                        // Steam clears once the game exe itself is gone.
                    }
                }
            }
            catch
            {
                // Listing processes can fail during teardown. Still end this process.
            }

            TerminateProcess(Process.GetCurrentProcess().Handle, 0);
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

        private static void Note(string message)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MechabellumFriendOverlay");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, "quit-stall.log"),
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
