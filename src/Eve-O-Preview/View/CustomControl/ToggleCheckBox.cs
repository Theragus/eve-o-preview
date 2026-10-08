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
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EveOPreview.View.CustomControl;

/// <summary>
/// A CheckBox drawn as a Windows 11 style toggle switch. It keeps the full CheckBox behaviour
/// (Checked, CheckedChanged, keyboard, accessibility, designer binding); only the painting differs.
/// Colours come from SystemColors so the control follows light and dark mode; the "on" track uses
/// the application's gold accent. .NET 11 ships a built-in toggle appearance; this covers .NET 10.
/// </summary>
public class ToggleCheckBox : CheckBox
{
    private static readonly Color Accent = Color.FromArgb(212, 175, 55);
    private const int TrackWidth = 40;
    private const int TrackHeight = 20;
    private const int Gap = 8;
    private bool _hover;

    public ToggleCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        Cursor = Cursors.Hand;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        return new Size(TrackWidth + Gap + text.Width + 2, Math.Max(TrackHeight, text.Height) + 2);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var back = new SolidBrush(Parent?.BackColor ?? BackColor)) g.FillRectangle(back, ClientRectangle);

        var track = new Rectangle(1, (Height - TrackHeight) / 2, TrackWidth, TrackHeight);
        Color trackFill = !Enabled ? SystemColors.ControlDark
            : Checked ? (_hover ? ControlPaint.Light(Accent, 0.15f) : Accent)
            : (_hover ? SystemColors.ControlLight : SystemColors.Control);
        Color trackBorder = Checked ? trackFill : SystemColors.ControlDarkDark;
        using (var path = RoundedRectangle(track, TrackHeight / 2))
        {
            using var fill = new SolidBrush(trackFill);
            g.FillPath(fill, path);
            using var pen = new Pen(trackBorder);
            g.DrawPath(pen, path);
        }

        const int knobSize = TrackHeight - 8;
        int knobX = Checked ? track.Right - knobSize - 4 : track.Left + 4;
        var knob = new Rectangle(knobX, track.Top + 4, knobSize, knobSize);
        using (var knobBrush = new SolidBrush(Checked ? Color.FromArgb(20, 20, 22) : SystemColors.ControlText))
            g.FillEllipse(knobBrush, knob);

        var textBounds = new Rectangle(TrackWidth + Gap + 1, 0, Math.Max(0, Width - TrackWidth - Gap - 1), Height);
        TextRenderer.DrawText(g, Text, Font, textBounds, Enabled ? ForeColor : SystemColors.GrayText,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, new Rectangle(0, 0, Width - 1, Height - 1));
    }

    private static GraphicsPath RoundedRectangle(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
