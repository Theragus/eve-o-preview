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
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EveOPreview.View.CustomControl;

/// <summary>
/// A toggle switch with the CheckBox surface the form and designer use (Checked, CheckState,
/// CheckedChanged, AutoSize, UseVisualStyleBackColor). It derives from Control rather than
/// CheckBox on purpose: ButtonBase keeps its own dark-mode painting and XOR focus cues, which left
/// coloured fragments around the switches. Everything here is painted from SystemColors plus the
/// accent, so it follows light and dark mode. .NET 11 ships a built-in toggle appearance; this
/// covers .NET 10.
/// </summary>
[DefaultEvent(nameof(CheckedChanged)), DefaultProperty(nameof(Checked)), DefaultBindingProperty(nameof(Checked))]
public class ToggleCheckBox : Control
{
    private const int TrackWidth = 40;
    private const int TrackHeight = 20;
    private const int Gap = 8;
    private bool _checked;
    private bool _hover;

    public event EventHandler CheckedChanged;

    public ToggleCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.StandardClick | ControlStyles.Selectable, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    [Category("Appearance"), DefaultValue(false)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Two-state only; kept so designer code written for CheckBox still compiles.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CheckState CheckState
    {
        get => _checked ? CheckState.Checked : CheckState.Unchecked;
        set => Checked = value != CheckState.Unchecked;
    }

    /// <summary>Accepted for CheckBox compatibility; the switch always paints its parent's background.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool UseVisualStyleBackColor { get; set; } = true;

    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set { base.AutoSize = value; if (value) Size = GetPreferredSize(Size.Empty); }
    }

    [Browsable(true), EditorBrowsable(EditorBrowsableState.Always), DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text
    {
        get => base.Text;
        set { base.Text = value; if (AutoSize) Size = GetPreferredSize(Size.Empty); Invalidate(); }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size text = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        return new Size(TrackWidth + Gap + text.Width + 4, Math.Max(TrackHeight, text.Height) + 4);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (AutoSize) Size = GetPreferredSize(Size.Empty);
    }

    protected override void OnClick(EventArgs e)
    {
        if (Enabled) { Focus(); Checked = !Checked; }
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space && Enabled) { Checked = !Checked; e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var back = new SolidBrush(Parent?.BackColor ?? SystemColors.Control);
        e.Graphics.FillRectangle(back, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var track = new Rectangle(2, (Height - TrackHeight) / 2, TrackWidth, TrackHeight);
        Color accent = ThemeColors.Accent;
        Color trackFill = !Enabled ? SystemColors.ControlDark
            : _checked ? (_hover ? ControlPaint.Light(accent, 0.2f) : accent)
            : (_hover ? SystemColors.ControlLight : SystemColors.Control);
        Color trackBorder = _checked ? trackFill : SystemColors.ControlDarkDark;
        using (var path = RoundedRectangle(track, TrackHeight / 2))
        {
            using var fill = new SolidBrush(trackFill);
            g.FillPath(fill, path);
            using var pen = new Pen(trackBorder);
            g.DrawPath(pen, path);
        }

        const int knobSize = TrackHeight - 8;
        int knobX = _checked ? track.Right - knobSize - 4 : track.Left + 4;
        var knob = new Rectangle(knobX, track.Top + 4, knobSize, knobSize);
        // Windows draws the "on" knob in the theme's background colour: black on dark, white on light.
        using (var knobBrush = new SolidBrush(_checked ? (ThemeColors.IsDark ? Color.Black : Color.White) : SystemColors.ControlText))
            g.FillEllipse(knobBrush, knob);

        var textBounds = new Rectangle(TrackWidth + Gap + 2, 0, Math.Max(0, Width - TrackWidth - Gap - 2), Height);
        TextRenderer.DrawText(g, Text, Font, textBounds, Enabled ? ForeColor : SystemColors.GrayText,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(SystemColors.GrayText) { DashStyle = DashStyle.Dot };
            g.DrawRectangle(focus, 0, 0, Width - 1, Height - 1);
        }
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ToggleAccessibleObject(this);

    private sealed class ToggleAccessibleObject : ControlAccessibleObject
    {
        private readonly ToggleCheckBox _owner;
        public ToggleAccessibleObject(ToggleCheckBox owner) : base(owner) { _owner = owner; }
        public override AccessibleRole Role => AccessibleRole.CheckButton;
        public override AccessibleStates State => base.State | (_owner.Checked ? AccessibleStates.Checked : AccessibleStates.None);
        public override string DefaultAction => "Toggle";
        public override void DoDefaultAction() => _owner.Checked = !_owner.Checked;
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
