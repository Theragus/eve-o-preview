//Eve-O Preview Plus is a program designed to deliver quality of life tooling. Primarily but not limited to enabling rapid window foreground and focus changes for the online game Eve Online.
//Copyright (C) 2026  Aura Asuna
//
//This program is free software: you can redistribute it and/or modify
//it under the terms of the GNU General Public License as published by
//the Free Software Foundation, either version 3 of the License, or
//(at your option) any later version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program.  If not, see <https://www.gnu.org/licenses/>.


using System;
using System.Drawing;
using Microsoft.Win32;

namespace EveOPreview.View.CustomControl;

/// <summary>
/// Colours shared by the themed settings controls. The accent is the user's Windows accent colour
/// (Settings > Personalization > Colors), read once from the DWM registry value as ABGR. Windows
/// stores very dark or near-grey accents when "automatic from wallpaper" picks one; those would be
/// invisible against the dark theme, so anything without enough brightness or saturation falls back
/// to the Windows default blue. IsDark derives from the current SystemColors so it matches whatever
/// Application.SetColorMode resolved to.
/// </summary>
internal static class ThemeColors
{
    private static readonly Lazy<Color> AccentValue = new(ReadWindowsAccent);

    public static Color Accent => AccentValue.Value;

    public static bool IsDark => SystemColors.Control.GetBrightness() < 0.5f;

    private static Color ReadWindowsAccent()
    {
        try
        {
            if (OperatingSystem.IsWindows() &&
                Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AccentColor", null) is int abgr)
            {
                var accent = Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
                if (accent.GetBrightness() >= 0.3f && accent.GetBrightness() <= 0.85f && accent.GetSaturation() >= 0.3f) return accent;
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException || ex is System.IO.IOException) { }
        return Color.FromArgb(0, 120, 212);
    }
}
