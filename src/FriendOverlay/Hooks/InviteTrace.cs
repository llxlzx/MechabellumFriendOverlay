using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppGameRiver;
using Il2CppGameRiver.Client;
using MelonLoader;

namespace FriendOverlay.Hooks
{
    /// <summary>
    /// Throw-away probe for the 0.3.6 invite work. The overlay has to create rooms the way the native
    /// 「邀请参与的战斗类型」 window does, but the IL2CPP interop assemblies carry no method bodies, so the
    /// (GameMode, MatchMode, isPrivate) tuple behind each button and whatever 组队匹配 calls can only be
    /// read off a live click. Delete this file once the table is captured.
    /// </summary>
    public static class InviteTrace
    {
        private const string Tag = "[InviteTrace] ";

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            var window = typeof(CreateRoomByInviteWindow);

            foreach (var name in new[]
                     {
                         "Btn1v1OnClicked",
                         "Btn2v2OnClicked",
                         "BtnSurviveOnClicked",
                         "BtnChaosFactionOnClicked",
                         "BtnChaosFaction2V2OnClicked",
                         "BtnRift1v1OnClicked",
                         "BtnRift2v2OnClicked",
                         "MatchBtnOnClicked",
                         "CancelBtnOnClicked",
                     })
            {
                PatchClick(harmony, window, name);
            }

            Patch(harmony,
                AccessTools.Method(window, "CreateRoom", new[] { typeof(GameMode), typeof(MatchMode) }),
                nameof(WindowCreateRoom));

            var lobby = typeof(LobbyProxy);

            Patch(harmony, FindCreateRoomByMode(lobby), nameof(LobbyCreateRoomByMode));
            Patch(harmony, FindCreateRoomByRequest(lobby), nameof(LobbyCreateRoomByRequest));

            Patch(harmony,
                AccessTools.Method(lobby, "InviteUserJoin", new[] { typeof(ulong), typeof(bool) }),
                nameof(InviteUserJoin));

            Patch(harmony,
                AccessTools.Method(lobby, "TryRequestInvite", new[] { typeof(ulong), typeof(string), typeof(bool) }),
                nameof(TryRequestInvite));

            Patch(harmony, AccessTools.Method(lobby, "OnResponseCreateRoom"), nameof(OnResponseCreateRoom), postfix: true);

            Patch(harmony,
                AccessTools.Method(typeof(TeamProxy), "RequestTeamInvite", new[] { typeof(ulong) }),
                nameof(RequestTeamInvite));

            Patch(harmony, AccessTools.Method(typeof(MatchMakerProxy), "JoinMatch"), nameof(JoinMatch));

            Patch(harmony,
                AccessTools.Method(typeof(FriendBtnListWindow), "InviteRoomBtnOnClicked"),
                nameof(NativeInviteBtn));

            MelonLogger.Msg(Tag + "armed. Open native friends (F8), invite someone, click each battle type once.");
        }

        /// <summary>
        /// Overload picked by shape rather than by naming the delegate type: the callback parameter is a
        /// nested IL2CPP delegate whose C# name has drifted between game builds.
        /// </summary>
        private static MethodInfo? FindCreateRoomByMode(Type lobby) =>
            AccessTools.GetDeclaredMethods(lobby)
                .FirstOrDefault(m => m.Name == "CreateRoom" &&
                                     m.GetParameters().Length == 4 &&
                                     m.GetParameters()[0].ParameterType == typeof(GameMode));

        private static MethodInfo? FindCreateRoomByRequest(Type lobby) =>
            AccessTools.GetDeclaredMethods(lobby)
                .FirstOrDefault(m => m.Name == "CreateRoom" &&
                                     m.GetParameters().Length == 2 &&
                                     m.GetParameters()[0].ParameterType.Name == "RequestCreateRoom");

        private static void PatchClick(HarmonyLib.Harmony harmony, Type window, string name)
        {
            var target = AccessTools.Method(window, name);
            if (target == null)
            {
                MelonLogger.Msg(Tag + "no such handler: " + name);
                return;
            }

            // One shared prefix cannot tell the handlers apart, so the label is baked into a finalizer
            // over the same method via a per-name closure patch.
            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(InviteTrace), nameof(ClickPrefix))));
                MelonLogger.Msg(Tag + "patched " + name);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(Tag + "patch failed " + name + ": " + ex.Message);
            }
        }

        private static void Patch(HarmonyLib.Harmony harmony, MethodBase? target, string handler, bool postfix = false)
        {
            if (target == null)
            {
                MelonLogger.Msg(Tag + "target missing for " + handler);
                return;
            }

            try
            {
                var patch = new HarmonyMethod(AccessTools.Method(typeof(InviteTrace), handler));
                if (postfix)
                    harmony.Patch(target, postfix: patch);
                else
                    harmony.Patch(target, prefix: patch);

                MelonLogger.Msg(Tag + "patched " + target.DeclaringType?.Name + "." + target.Name);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(Tag + "patch failed " + target.Name + ": " + ex.Message);
            }
        }

        private static void ClickPrefix(MethodBase __originalMethod)
        {
            MelonLogger.Msg(Tag + "CLICK " + __originalMethod.Name);
        }

        private static void WindowCreateRoom(GameMode gameMode, MatchMode matchMode)
        {
            MelonLogger.Msg(Tag + "window.CreateRoom gameMode=" + gameMode + "(" + (int)gameMode + ")" +
                            " matchMode=" + matchMode + "(" + (int)matchMode + ")");
        }

        private static void LobbyCreateRoomByMode(GameMode gameMode, MatchMode matchMode, bool isPrivate)
        {
            MelonLogger.Msg(Tag + "LobbyProxy.CreateRoom gameMode=" + gameMode + "(" + (int)gameMode + ")" +
                            " matchMode=" + matchMode + "(" + (int)matchMode + ")" +
                            " isPrivate=" + isPrivate);
        }

        private static void LobbyCreateRoomByRequest()
        {
            MelonLogger.Msg(Tag + "LobbyProxy.CreateRoom(RequestCreateRoom) used instead of the mode overload");
        }

        private static void InviteUserJoin(ulong userid, bool discord)
        {
            MelonLogger.Msg(Tag + "InviteUserJoin userid=" + userid + " discord=" + discord);
        }

        private static void TryRequestInvite(ulong userid, bool discord)
        {
            MelonLogger.Msg(Tag + "TryRequestInvite userid=" + userid + " discord=" + discord);
        }

        private static void OnResponseCreateRoom()
        {
            MelonLogger.Msg(Tag + "OnResponseCreateRoom");
        }

        private static void RequestTeamInvite(ulong target)
        {
            MelonLogger.Msg(Tag + "TeamProxy.RequestTeamInvite target=" + target);
        }

        private static void JoinMatch()
        {
            MelonLogger.Msg(Tag + "MatchMakerProxy.JoinMatch");
        }

        private static void NativeInviteBtn()
        {
            MelonLogger.Msg(Tag + "FriendBtnListWindow.InviteRoomBtnOnClicked");
        }
    }
}
