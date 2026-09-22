using System.Diagnostics;
using FriendOverlay.Core;
using HarmonyLib;
using MelonLoader;

namespace FriendOverlay.Hooks
{
    /// <summary>
    /// Patches GameLauncher.OnApplicationWantsToQuit. If that callback accepts quit and the
    /// process is still alive after a short grace, or if the callback never returns, end
    /// Mechabellum.exe so Steam drops the running state.
    /// </summary>
    public static class QuitStallHooks
    {
        private static int _generation;

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            var type = AccessTools.TypeByName("Il2CppGameRiver.GameLauncher");
            var method = AccessTools.Method(type, "OnApplicationWantsToQuit");
            if (method == null)
            {
                MelonLogger.Warning("[FriendOverlay] quit stall guard skipped: OnApplicationWantsToQuit missing");
                return;
            }

            harmony.Patch(
                method,
                prefix: new HarmonyMethod(typeof(QuitStallHooks), nameof(Prefix)),
                postfix: new HarmonyMethod(typeof(QuitStallHooks), nameof(Postfix)));
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

        private static void Schedule(int generation, QuitStallGuard.Decision decision)
        {
            if (!decision.ForceExit)
                return;

            var thread = new Thread(() =>
            {
                Thread.Sleep(decision.DelayMs);
                if (Volatile.Read(ref _generation) != generation)
                    return;

                MelonLogger.Warning("[FriendOverlay] quit stalled; ending Mechabellum.exe so Steam clears the running state");
                EndGameProcess();
            })
            {
                IsBackground = true,
                Name = "FriendOverlay.QuitStall"
            };
            thread.Start();
        }

        private static void EndGameProcess()
        {
            var self = Process.GetCurrentProcess();
            var gameDir = Path.GetDirectoryName(self.MainModule?.FileName);
            foreach (var handler in Process.GetProcessesByName("UnityCrashHandler64"))
            {
                try
                {
                    var path = handler.MainModule?.FileName;
                    if (string.IsNullOrEmpty(gameDir) || string.IsNullOrEmpty(path))
                        continue;
                    if (!path.StartsWith(gameDir, StringComparison.OrdinalIgnoreCase))
                        continue;
                    handler.Kill();
                }
                catch
                {
                    // The crash handler may already be gone. Steam clears once the game exe is gone.
                }
            }

            try
            {
                self.Kill();
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[FriendOverlay] force exit failed: " + ex.Message);
            }
        }
    }
}
