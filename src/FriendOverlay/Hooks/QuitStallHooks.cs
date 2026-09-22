using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FriendOverlay.Core;
using HarmonyLib;
using MelonLoader;

namespace FriendOverlay.Hooks
{
    /// <summary>
    /// Steam stays in-game until Mechabellum.exe exits. The game logs
    /// OnApplicationWantsToQuit and then stalls inside that callback, so a Harmony
    /// postfix on the callback itself never runs. A side thread watches the player
    /// log for that line and ends the process.
    /// </summary>
    public static class QuitStallHooks
    {
        private static int _generation;
        private static int _armed;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            try
            {
                var type = AccessTools.TypeByName("Il2CppGameRiver.GameLauncher");
                var method = AccessTools.Method(type, "OnApplicationWantsToQuit");
                if (method != null)
                {
                    harmony.Patch(
                        method,
                        prefix: new HarmonyMethod(typeof(QuitStallHooks), nameof(Prefix)),
                        postfix: new HarmonyMethod(typeof(QuitStallHooks), nameof(Postfix)));
                }

                var debug = AccessTools.TypeByName("UnityEngine.Debug");
                if (debug != null)
                {
                    foreach (var logWarning in AccessTools.GetDeclaredMethods(debug))
                    {
                        if (logWarning.Name != "LogWarning")
                            continue;
                        harmony.Patch(logWarning, postfix: new HarmonyMethod(typeof(QuitStallHooks), nameof(LogWarningPostfix)));
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[FriendOverlay] quit stall hooks: " + ex.Message);
            }

            var watcher = new Thread(WatchPlayerLog)
            {
                IsBackground = true,
                Name = "FriendOverlay.QuitLog"
            };
            watcher.Start();
            MelonLogger.Msg("[FriendOverlay] quit stall guard installed");
        }

        public static void Prefix()
        {
            var generation = Interlocked.Increment(ref _generation);
            Schedule(generation, QuitStallGuard.OnEnter());
        }

        public static void Postfix(bool __result)
        {
            var generation = Interlocked.Increment(ref _generation);
            Schedule(generation, QuitStallGuard.OnReturned(__result));
        }

        public static void LogWarningPostfix(object[] __args)
        {
            if (__args == null || __args.Length == 0 || __args[0] == null)
                return;

            var text = __args[0].ToString();
            if (text != null && text.IndexOf(QuitStallGuard.QuitMarker, StringComparison.Ordinal) >= 0)
                ArmFromQuitMarker();
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
                    if (QuitStallGuard.TailHasQuitMarker(buffer, count))
                        ArmFromQuitMarker();
                }
                catch
                {
                    // The log is rewritten while Unity still has it open. The next tick retries.
                }
            }
        }

        private static void ArmFromQuitMarker()
        {
            if (Interlocked.Exchange(ref _armed, 1) != 0)
                return;

            Note("quit marker seen");
            var thread = new Thread(() =>
            {
                Thread.Sleep(QuitStallGuard.AcceptedQuitGraceMs);
                Note("ending process");
                EndGameProcess();
            })
            {
                IsBackground = true,
                Name = "FriendOverlay.QuitStall"
            };
            thread.Start();
        }

        private static void Schedule(int generation, QuitStallGuard.Decision decision)
        {
            if (!decision.ForceExit)
                return;

            var thread = new Thread(() =>
            {
                Thread.Sleep(decision.DelayMs);
                if (Volatile.Read(ref _generation) != generation)
                    return;

                Note("harmony quit stall");
                EndGameProcess();
            })
            {
                IsBackground = true,
                Name = "FriendOverlay.QuitStallHarmony"
            };
            thread.Start();
        }

        private static void EndGameProcess()
        {
            try
            {
                var self = Process.GetCurrentProcess();
                var gameDir = Path.GetDirectoryName(self.MainModule?.FileName);
                foreach (var handler in Process.GetProcessesByName("UnityCrashHandler64"))
                {
                    try
                    {
                        var handlerPath = handler.MainModule?.FileName;
                        if (string.IsNullOrEmpty(gameDir) || string.IsNullOrEmpty(handlerPath))
                            continue;
                        if (!handlerPath.StartsWith(gameDir, StringComparison.OrdinalIgnoreCase))
                            continue;
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
                // Reading the module path can fail during teardown. Still end this process.
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
