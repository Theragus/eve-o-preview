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

using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.View.CustomControl;

/// <summary>
/// TabControl that paints its whole surface itself. The native control only lets DrawItem draw the
/// tabs; the strip behind them and the page border stay in the classic light colours, which shows
/// as a grey band when the application follows the Windows dark theme. Painting in UserPaint mode
/// with SystemColors keeps the strip, tabs and border consistent in both light and dark mode. Tab
/// text is still drawn by the owner's DrawItem handler.
/// </summary>
public class ThemedTabControl : TabControl
{
    private static readonly Color Accent = Color.FromArgb(212, 175, 55);

    public ThemedTabControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(SystemColors.Control);

        if (SelectedTab != null)
        {
            Rectangle page = SelectedTab.Bounds;
            page.Inflate(1, 1);
            using var border = new Pen(SystemColors.ControlDark);
            e.Graphics.DrawRectangle(border, page);
        }

        for (int i = 0; i < TabCount; i++)
        {
            Rectangle bounds = GetTabRect(i);
            bool selected = i == SelectedIndex;
            using (var fill = new SolidBrush(selected ? SystemColors.Control : SystemColors.ControlDark))
            {
                e.Graphics.FillRectangle(fill, bounds);
            }
            if (selected)
            {
                // Accent bar on the outer edge, matching the preview context menu's gold.
                Rectangle bar = Alignment == TabAlignment.Left
                    ? new Rectangle(bounds.X, bounds.Y, 3, bounds.Height)
                    : new Rectangle(bounds.X, bounds.Y, bounds.Width, 3);
                using var accent = new SolidBrush(Accent);
                e.Graphics.FillRectangle(accent, bar);
            }
            OnDrawItem(new DrawItemEventArgs(e.Graphics, Font, bounds, i, selected ? DrawItemState.Selected : DrawItemState.None));
        }
    }

    protected override void OnSelectedIndexChanged(System.EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        Invalidate();
    }
}
