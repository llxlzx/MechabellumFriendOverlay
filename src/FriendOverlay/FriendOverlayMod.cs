using FriendOverlay.Compat;
using FriendOverlay.Core;
using FriendOverlay.Data;
using FriendOverlay.Hooks;
using FriendOverlay.State;
using FriendOverlay.UI;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(FriendOverlay.FriendOverlayMod), "FriendOverlay", "0.3.5", "MechabellumFriendOverlay")]
[assembly: MelonGame("GameRiver", "Mechabellum")]

namespace FriendOverlay
{
    public sealed class FriendOverlayMod : MelonMod
    {
        private MelonPreferences_Category? _prefs;
        private MelonPreferences_Entry<bool>? _prefOverlayDefault;
        private MelonPreferences_Entry<int>? _prefSort;
        private MelonPreferences_Entry<int>? _prefHotkey;
        private MelonPreferences_Entry<bool>? _prefAnimations;
        private MelonPreferences_Entry<float>? _prefUiScale;
        private MelonPreferences_Entry<bool>? _prefUseGameFont;
        private MelonPreferences_Entry<bool>? _prefTransparentNative;
        private MelonPreferences_Entry<bool>? _prefCollapseJoinable;
        private MelonPreferences_Entry<bool>? _prefCollapseBusy;
        private MelonPreferences_Entry<bool>? _prefCollapseOffline;
        private MelonPreferences_Entry<bool>? _prefCollapsePinned;
        private MelonPreferences_Entry<string>? _prefPinned;
        private MelonPreferences_Entry<float>? _prefWinX;
        private MelonPreferences_Entry<float>? _prefWinY;
        private MelonPreferences_Entry<float>? _prefWinW;
        private MelonPreferences_Entry<float>? _prefWinH;

        public override void OnInitializeMelon()
        {
            LoadPreferences();
            OverlaySession.PersistPreferences = SavePreferences;

            var probe = TypeProbe.Probe();
            if (!probe.Ok)
            {
                OverlaySession.Degraded = true;
                LoggerInstance.Warning("FriendOverlay disabled (fail-open). Native friends UI untouched. " + probe.Message);
                return;
            }

            // Optional surfaces are probed before the hooks so hook installation can consult them.
            Capabilities.Probe();

            try
            {
                FriendPanelHooks.Apply(HarmonyInstance);
                LoggerInstance.Msg("FriendOverlay hooks applied. Overlay default=" + OverlaySession.PreferOverlayDefault);
            }
            catch (System.Exception ex)
            {
                OverlaySession.Degraded = true;
                LoggerInstance.Error("Harmony patch failed; fail-open. " + ex);
                return;
            }

            // Probe only, and isolated: a drifted signature here must not disable the whole mod.
            try
            {
                InviteTrace.Apply(HarmonyInstance);
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Warning("InviteTrace skipped: " + ex.Message);
            }
        }

        public override void OnUpdate()
        {
            // The shield swallows every uGUI click, so it must never outlive the session even if
            // the panel went away without one of our hooks firing.
            if (OverlaySession.Panel == null && InputShield.Active)
                InputShield.Destroy();

            if (OverlaySession.Degraded)
                return;

            FriendListService.Tick();
            FansListService.Tick();
            ImguiFriendOverlay.UpdateInput();

            if (OverlaySession.Panel != null && Input.GetKeyDown(OverlaySession.ToggleHotkey))
                OverlaySession.ToggleMode();
        }

        public override void OnGUI()
        {
            if (OverlaySession.Degraded)
                return;

            ImguiFriendOverlay.Draw();
        }

        public override void OnApplicationQuit()
        {
            SavePreferences();
        }

        private void LoadPreferences()
        {
            _prefs = MelonPreferences.CreateCategory("FriendOverlay", "Friend Overlay");
            _prefOverlayDefault = _prefs.CreateEntry("PreferOverlayDefault", true, "Default to overlay when friends open");
            _prefSort = _prefs.CreateEntry("SortKey", (int)FriendSortKey.Default, "Sort key");
            _prefHotkey = _prefs.CreateEntry("ToggleHotkey", (int)KeyCode.F8, "Toggle overlay/native hotkey");
            _prefAnimations = _prefs.CreateEntry("Animations", true, "Fade, pulse and hover animations");
            _prefUiScale = _prefs.CreateEntry("UiScale", 0f, "UI scale (0 = auto from screen height)");
            _prefUseGameFont = _prefs.CreateEntry("UseGameFont", false, "Borrow the in-game font instead of the default GUI font");
            _prefTransparentNative = _prefs.CreateEntry("TransparentNativePanel", true, "Hide the native panel via CanvasGroup instead of deactivating it");
            _prefCollapseJoinable = _prefs.CreateEntry("CollapseJoinable", false, "Collapse the online/joinable section");
            _prefCollapseBusy = _prefs.CreateEntry("CollapseBusy", false, "Collapse the online/in-battle section");
            _prefCollapseOffline = _prefs.CreateEntry("CollapseOffline", false, "Collapse the offline section");
            _prefCollapsePinned = _prefs.CreateEntry("CollapsePinned", false, "Collapse the pinned section");
            _prefPinned = _prefs.CreateEntry("PinnedUserIds", string.Empty, "Comma separated pinned user ids (max 20, local only)");
            _prefWinX = _prefs.CreateEntry("WindowX", 0f, "Overlay window X");
            _prefWinY = _prefs.CreateEntry("WindowY", 0f, "Overlay window Y");
            _prefWinW = _prefs.CreateEntry("WindowW", 0f, "Overlay window width");
            _prefWinH = _prefs.CreateEntry("WindowH", 0f, "Overlay window height");

            OverlaySession.PreferOverlayDefault = _prefOverlayDefault.Value;
            OverlaySession.ToggleHotkey = (KeyCode)_prefHotkey.Value;
            OverlaySession.UseTransparentHide = _prefTransparentNative.Value;

            ImguiFriendOverlay.CurrentSort = (FriendSortKey)_prefSort.Value;
            ImguiFriendOverlay.CollapsedJoinable = _prefCollapseJoinable.Value;
            ImguiFriendOverlay.CollapsedBusy = _prefCollapseBusy.Value;
            ImguiFriendOverlay.CollapsedOffline = _prefCollapseOffline.Value;
            ImguiFriendOverlay.CollapsedPinned = _prefCollapsePinned.Value;
            PinStore.Load(_prefPinned.Value);

            Anim.Enabled = _prefAnimations.Value;
            Theme.UserScale = _prefUiScale.Value;
            GameAssets.UseGameFont = _prefUseGameFont.Value;

            if (_prefWinW.Value > 1f && _prefWinH.Value > 1f)
            {
                ImguiFriendOverlay.WindowRect = new Rect(
                    _prefWinX.Value,
                    _prefWinY.Value,
                    _prefWinW.Value,
                    _prefWinH.Value);
            }
        }

        private void SavePreferences()
        {
            if (_prefs == null || _prefSort == null)
                return;

            _prefOverlayDefault!.Value = OverlaySession.PreferOverlayDefault;
            _prefSort.Value = (int)ImguiFriendOverlay.CurrentSort;
            _prefHotkey!.Value = (int)OverlaySession.ToggleHotkey;
            _prefCollapseJoinable!.Value = ImguiFriendOverlay.CollapsedJoinable;
            _prefCollapseBusy!.Value = ImguiFriendOverlay.CollapsedBusy;
            _prefCollapseOffline!.Value = ImguiFriendOverlay.CollapsedOffline;
            _prefCollapsePinned!.Value = ImguiFriendOverlay.CollapsedPinned;
            _prefPinned!.Value = PinStore.Csv;

            var rect = ImguiFriendOverlay.WindowRect;
            _prefWinX!.Value = rect.x;
            _prefWinY!.Value = rect.y;
            _prefWinW!.Value = rect.width;
            _prefWinH!.Value = rect.height;

            MelonPreferences.Save();
        }
    }
}
