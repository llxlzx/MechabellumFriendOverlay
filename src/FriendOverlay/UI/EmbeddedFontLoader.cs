using System;
using System.IO;
using System.Runtime.InteropServices;
using MelonLoader;
using UnityEngine;

namespace FriendOverlay.UI
{
    /// <summary>
    /// Extracts embedded Noto Sans SC Medium, privately registers it with GDI, and builds a Unity Font
    /// via <see cref="Font.Internal_CreateDynamicFont"/> (spike-proven on this IL2CPP build).
    /// </summary>
    internal static class EmbeddedFontLoader
    {
        private const uint FrPrivate = 0x10;
        private const string ResourceName = "FriendOverlay.Fonts.NotoSansSC-Medium.otf";
        private const string FileName = "NotoSansSC-Medium.otf";
        private const string OflResourceName = "FriendOverlay.Fonts.OFL.txt";

        private static string? _extractedPath;
        private static bool _registered;

        public static Font? TryCreateNoto(int size = 16)
        {
            try
            {
                if (!EnsureExtractedAndRegistered())
                    return null;

                var font = new Font();
                Font.Internal_CreateDynamicFont(
                    font,
                    new[]
                    {
                        "Noto Sans SC Medium",
                        "NotoSansSC-Medium",
                        "Source Han Sans SC Medium",
                        "Noto Sans SC",
                        "Source Han Sans SC",
                    },
                    size);

                if (!LooksUsable(font, "钢铁ABC"))
                    return null;

                return font;
            }
            catch
            {
                return null;
            }
        }

        public static Font? TryCreateYahei(int size = 16) =>
            TryCreateSystem(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑", "Arial" },
                "钢铁ABC",
                size);

        public static Font? TryCreateSystem(string[] names, string probeSample, int size = 16)
        {
            if (names == null || names.Length == 0)
                return null;

            try
            {
                var font = new Font();
                Font.Internal_CreateDynamicFont(font, names, size);

                if (!LooksUsable(font, probeSample))
                    return null;

                return font;
            }
            catch
            {
                return null;
            }
        }

        public static void ShutdownBestEffort()
        {
            if (!_registered || string.IsNullOrEmpty(_extractedPath))
                return;

            try
            {
                RemoveFontResourceEx(_extractedPath, FrPrivate, IntPtr.Zero);
            }
            catch
            {
                // ignore
            }

            _registered = false;
        }

        private static bool EnsureExtractedAndRegistered()
        {
            if (_registered && !string.IsNullOrEmpty(_extractedPath) && File.Exists(_extractedPath))
                return true;

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MechabellumFriendOverlay",
                "Fonts");
            Directory.CreateDirectory(dir);

            var otfPath = Path.Combine(dir, FileName);
            var oflPath = Path.Combine(dir, "OFL.txt");

            var needExtract = !File.Exists(otfPath) || new FileInfo(otfPath).Length < 1000;
            if (!needExtract)
            {
                try
                {
                    var asm = typeof(EmbeddedFontLoader).Assembly;
                    using var stream = asm.GetManifestResourceStream(ResourceName);
                    if (stream != null && stream.Length != new FileInfo(otfPath).Length)
                        needExtract = true;
                }
                catch
                {
                    // keep existing file
                }
            }

            if (needExtract && !ExtractResource(ResourceName, otfPath))
                return false;

            if (!File.Exists(oflPath))
                ExtractResource(OflResourceName, oflPath);

            var added = AddFontResourceEx(otfPath, FrPrivate, IntPtr.Zero);
            if (added == 0)
            {
                MelonLogger.Warning("[FriendOverlay] AddFontResourceEx failed path=" + otfPath +
                                    " err=" + Marshal.GetLastWin32Error());
                return false;
            }

            _extractedPath = otfPath;
            _registered = true;
            return true;
        }

        private static bool ExtractResource(string resourceName, string destPath)
        {
            var asm = typeof(EmbeddedFontLoader).Assembly;
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                MelonLogger.Warning("[FriendOverlay] missing embedded resource " + resourceName);
                return false;
            }

            var tmp = destPath + ".partial";
            using (var fs = File.Create(tmp))
                stream.CopyTo(fs);

            if (File.Exists(destPath))
                File.Delete(destPath);
            File.Move(tmp, destPath);
            return true;
        }

        private static bool LooksUsable(Font? font, string sample)
        {
            if (font == null || font.Pointer == IntPtr.Zero)
                return false;

            try
            {
                if (font.material == null)
                    return false;

                font.RequestCharactersInTexture(sample, 16, FontStyle.Normal);
                var ok = 0;
                foreach (var ch in sample)
                {
                    if (font.HasCharacter(ch))
                        ok++;
                }

                return ok >= sample.Length;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("gdi32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

        [DllImport("gdi32", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);
    }
}
