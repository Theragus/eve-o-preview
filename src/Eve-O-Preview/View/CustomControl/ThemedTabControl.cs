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
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.View.CustomControl;

/// <summary>
/// TabControl that paints its whole surface itself: strip, tabs, page border, an accent bar on
/// the selected tab, a hover highlight, and an icon glyph per tab. The native control leaves the
/// strip and border in classic light colours and cannot draw icons without an ImageList, which is
/// why everything is drawn here with SystemColors so it follows light and dark mode.
/// Glyphs come from Segoe Fluent Icons (Windows 11) or Segoe MDL2 Assets (Windows 10); they are
/// looked up by tab caption, so a tab without a known caption simply has no icon.
/// </summary>
public class ThemedTabControl : TabControl
{
    private static readonly Dictionary<string, string> Glyphs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["General"] = "",        // Settings
        ["Thumbnail"] = "",      // Pictures
        ["Zoom"] = "",           // Zoom
        ["Overlay"] = "",        // Font
        ["Active Clients"] = "", // People
        ["Cycle Groups"] = "",   // Sync
        ["FPS / Audio"] = "",    // Volume
        ["Profiles"] = "",       // Contact
        ["About"] = "",          // Info
    };

    private static readonly Lazy<Font> GlyphFont = new(() =>
    {
        foreach (string family in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
        {
            try { return new Font(family, 12f, FontStyle.Regular, GraphicsUnit.Point); }
            catch (ArgumentException) { }
        }
        return null;
    });

    private int _hoverIndex = -1;

    public ThemedTabControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int index = -1;
        for (int i = 0; i < TabCount; i++)
        {
            if (GetTabRect(i).Contains(e.Location)) { index = i; break; }
        }
        if (index != _hoverIndex) { _hoverIndex = index; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex != -1) { _hoverIndex = -1; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(SystemColors.Control);

        if (SelectedTab != null)
        {
            Rectangle page = SelectedTab.Bounds;
            page.Inflate(1, 1);
            using var border = new Pen(SystemColors.ControlDark);
            g.DrawRectangle(border, page);
        }

        using var captionFont = new Font(Font.FontFamily, Font.Size + 0.5f, FontStyle.Bold, GraphicsUnit.Point);
        for (int i = 0; i < TabCount; i++)
        {
            Rectangle bounds = GetTabRect(i);
            bool selected = i == SelectedIndex;
            Color fill = selected ? SystemColors.Control
                : i == _hoverIndex ? SystemColors.ControlLight
                : SystemColors.ControlDark;
            using (var brush = new SolidBrush(fill)) g.FillRectangle(brush, bounds);

            if (selected)
            {
                // Accent bar on the outer edge in the Windows accent colour.
                Rectangle bar = Alignment == TabAlignment.Left
                    ? new Rectangle(bounds.X, bounds.Y + 6, 3, bounds.Height - 12)
                    : new Rectangle(bounds.X + 6, bounds.Y, bounds.Width - 12, 3);
                using var accent = new SolidBrush(ThemeColors.Accent);
                g.FillRectangle(accent, bar);
            }

            Color text = selected ? SystemColors.ControlText : SystemColors.GrayText;
            int left = bounds.X + 12;
            Font glyphFont = GlyphFont.Value;
            if (glyphFont != null && Glyphs.TryGetValue(TabPages[i].Text, out string glyph))
            {
                var glyphBounds = new Rectangle(left, bounds.Y, 22, bounds.Height);
                TextRenderer.DrawText(g, glyph, glyphFont, glyphBounds, selected ? ThemeColors.Accent : text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
                left += 28;
            }
            var captionBounds = new Rectangle(left, bounds.Y, bounds.Right - left - 4, bounds.Height);
            TextRenderer.DrawText(g, TabPages[i].Text, captionFont, captionBounds, text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }
}
