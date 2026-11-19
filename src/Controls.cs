using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AutoDarkMode
{
    // ---------- Palette (follows the current Windows theme) ----------
    static class Pal
    {
        public static bool Dark, Mica;
        public static float K = 1f;
        public static Color Bg, Card, Field, Track, Text, Sub, Accent, AccentText, Line, Border, SunBar, Moon;

        public static int P(double v) { return (int)Math.Round(v * K); }
        public static float F(double v) { return (float)(v * K); }

        public static void InitScale()
        {
            try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) K = g.DpiX / 96f; } catch { }
        }

        public static void Set(bool dark)
        {
            Dark = dark;
            if (dark)
            {
                Bg = Color.FromArgb(32, 32, 32); Card = Color.FromArgb(43, 43, 43); Field = Color.FromArgb(56, 56, 56);
                Track = Color.FromArgb(31, 31, 31);
                Text = Color.FromArgb(245, 245, 245); Sub = Color.FromArgb(158, 158, 163);
                Accent = Color.FromArgb(96, 205, 255); AccentText = Color.FromArgb(16, 16, 16);
                Line = Color.FromArgb(54, 54, 54); Border = Color.FromArgb(70, 70, 70);
                SunBar = Color.FromArgb(246, 198, 84); Moon = Color.FromArgb(200, 192, 255);
            }
            else
            {
                Bg = Color.FromArgb(243, 243, 243); Card = Color.White; Field = Color.FromArgb(238, 238, 240);
                Track = Color.FromArgb(240, 240, 242);
                Text = Color.FromArgb(27, 27, 27); Sub = Color.FromArgb(100, 100, 106);
                Accent = Color.FromArgb(0, 103, 192); AccentText = Color.White;
                Line = Color.FromArgb(230, 230, 230); Border = Color.FromArgb(210, 210, 210);
                SunBar = Color.FromArgb(242, 172, 28); Moon = Color.FromArgb(110, 98, 200);
            }
            Color a;
            if (SystemAccent(dark, out a))
            {
                Accent = a;
                double lum = (0.2126 * a.R + 0.7152 * a.G + 0.0722 * a.B) / 255.0;
                AccentText = lum > 0.55 ? Color.FromArgb(16, 16, 16) : Color.White;
            }
        }

        // Windows accent colour: the shade Windows itself uses in dark (Light 2) or light (Dark 1) mode
        static bool SystemAccent(bool dark, out Color c)
        {
            c = Color.Empty;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent"))
                {
                    var b = k == null ? null : k.GetValue("AccentPalette") as byte[];
                    if (b == null || b.Length < 32) return false;
                    int i = (dark ? 1 : 4) * 4;
                    c = Color.FromArgb(b[i], b[i + 1], b[i + 2]);
                    return true;
                }
            }
            catch { return false; }
        }
    }

    static class Gfx
    {
        public static GraphicsPath Round(RectangleF r, float rad)
        {
            float d = Math.Max(1f, Math.Min(rad * 2, Math.Min(r.Width, r.Height)));
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, RectangleF r, float rad, Color c)
        {
            using (var p = Round(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
        }

        public static void Stroke(Graphics g, RectangleF r, float rad, Color c, float w)
        {
            using (var p = Round(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static Pen RoundPen(Color c, float w)
        {
            var p = new Pen(c, w);
            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round; p.LineJoin = LineJoin.Round;
            return p;
        }

        // ----- small line icons -----
        public static void Sun(Graphics g, RectangleF b, Color c)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Math.Min(b.Width, b.Height), cx = b.X + b.Width / 2f, cy = b.Y + b.Height / 2f;
            float r = s * 0.2f;
            using (var br = new SolidBrush(c)) g.FillEllipse(br, cx - r, cy - r, r * 2, r * 2);
            using (var pen = RoundPen(c, Math.Max(1.2f, s * 0.09f)))
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4;
                    float co = (float)Math.Cos(a), sn = (float)Math.Sin(a);
                    g.DrawLine(pen, cx + co * s * 0.32f, cy + sn * s * 0.32f, cx + co * s * 0.46f, cy + sn * s * 0.46f);
                }
        }

        public static void Moon(Graphics g, RectangleF b, Color c, Color bg)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Math.Min(b.Width, b.Height), cx = b.X + b.Width / 2f, cy = b.Y + b.Height / 2f;
            float r = s * 0.40f;
            using (var br = new SolidBrush(c)) g.FillEllipse(br, cx - r, cy - r, r * 2, r * 2);
            float r2 = s * 0.33f, ox = cx + s * 0.19f, oy = cy - s * 0.15f;
            using (var br = new SolidBrush(bg)) g.FillEllipse(br, ox - r2, oy - r2, r2 * 2, r2 * 2);
        }

        public static void MoonOutline(Graphics g, RectangleF b, Color c, float w)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Math.Min(b.Width, b.Height), cx = b.X + b.Width / 2f, cy = b.Y + b.Height / 2f;
            float r = s * 0.42f, r2 = s * 0.34f, ox = cx + s * 0.20f, oy = cy - s * 0.16f;
            using (var outer = new GraphicsPath())
            using (var inner = new GraphicsPath())
            {
                outer.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                inner.AddEllipse(ox - r2, oy - r2, r2 * 2, r2 * 2);
                using (var reg = new Region(outer))
                {
                    reg.Exclude(inner);
                    // draw the crescent outline: outer arc outside the cut + inner arc inside the disc
                    var old = g.Clip;
                    using (var outside = new Region(new RectangleF(b.X - 4, b.Y - 4, b.Width + 8, b.Height + 8)))
                    {
                        outside.Exclude(inner);
                        g.SetClip(outside, CombineMode.Replace);
                        using (var pen = RoundPen(c, w)) g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                        g.SetClip(outer, CombineMode.Replace);
                        using (var pen = RoundPen(c, w)) g.DrawEllipse(pen, ox - r2, oy - r2, r2 * 2, r2 * 2);
                    }
                    g.Clip = old;
                }
            }
        }

        public static void Pin(Graphics g, RectangleF b, Color c, float w)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Math.Min(b.Width, b.Height), cx = b.X + b.Width / 2f;
            float r = s * 0.32f, cy = b.Y + s * 0.40f;
            using (var p = new GraphicsPath())
            {
                p.AddArc(cx - r, cy - r, r * 2, r * 2, 140, 260);
                p.AddLine(cx + r * (float)Math.Cos(40 * Math.PI / 180), cy + r * (float)Math.Sin(40 * Math.PI / 180), cx, b.Y + s * 0.94f);
                p.CloseFigure();
                using (var pen = RoundPen(c, w)) g.DrawPath(pen, p);
            }
            float ir = s * 0.11f;
            using (var pen = RoundPen(c, w)) g.DrawEllipse(pen, cx - ir, cy - ir, ir * 2, ir * 2);
        }

        public static void Refresh(Graphics g, RectangleF b, Color c, float w)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Math.Min(b.Width, b.Height), cx = b.X + b.Width / 2f, cy = b.Y + b.Height / 2f, r = s * 0.36f;
            // clockwise arc with a gap at the top right, arrow at the top pointing clockwise
            float start = -30, sweep = 290;
            using (var pen = RoundPen(c, w)) g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, start, sweep);
            double a = (start + sweep) * Math.PI / 180;
            float ex = cx + r * (float)Math.Cos(a), ey = cy + r * (float)Math.Sin(a);
            double dx = -Math.Sin(a), dy = Math.Cos(a);           // direction of travel
            float h = s * 0.24f;
            float tx = ex + (float)dx * h * 0.35f, ty = ey + (float)dy * h * 0.35f;
            using (var pen = RoundPen(c, w))
                for (int k = -1; k <= 1; k += 2)
                {
                    double ang = Math.Atan2(-dy, -dx) + k * 0.75;
                    g.DrawLine(pen, tx, ty, tx + (float)Math.Cos(ang) * h, ty + (float)Math.Sin(ang) * h);
                }
        }

        public static void Chevron(Graphics g, PointF center, float size, Color c, float w) { Chevron(g, center, size, c, w, false); }
        public static void Chevron(Graphics g, PointF center, float size, Color c, float w, bool up)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float k = up ? -1 : 1;
            using (var pen = RoundPen(c, w))
                g.DrawLines(pen, new[] { new PointF(center.X - size, center.Y - k * size / 2f), new PointF(center.X, center.Y + k * size / 2f), new PointF(center.X + size, center.Y - k * size / 2f) });
        }
    }

    static class Art
    {
        public static readonly Color SunC = Color.FromArgb(255, 193, 7);
        public static readonly Color MoonC = Color.FromArgb(200, 192, 255);

        // tray icon artwork
        public static void Draw(Graphics g, bool light, RectangleF box)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Math.Min(box.Width, box.Height);
            float cx = box.X + box.Width / 2f, cy = box.Y + box.Height / 2f;
            if (light) { Gfx.Sun(g, box, SunC); return; }
            float r = s * 0.40f;
            using (var b = new SolidBrush(MoonC)) g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2);
            float r2 = s * 0.33f, ox = cx + s * 0.19f, oy = cy - s * 0.15f;
            g.CompositingMode = CompositingMode.SourceCopy;
            using (var b = new SolidBrush(Color.Transparent)) g.FillEllipse(b, ox - r2, oy - r2, r2 * 2, r2 * 2);
            g.CompositingMode = CompositingMode.SourceOver;
        }
    }

    class Base : Control
    {
        public Base()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }
    }

    class Card : Base
    {
        public int Rows, RowH, RowTop;
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Gfx.Fill(g, r, Pal.F(12), Pal.Card);
            Gfx.Stroke(g, r, Pal.F(12), Pal.Line, 1f);
            using (var pen = new Pen(Pal.Line))
                for (int i = 1; i < Rows; i++)
                    g.DrawLine(pen, Pal.P(18), RowTop + i * RowH, Width - Pal.P(18), RowTop + i * RowH);
        }
    }

    class Lbl : Base
    {
        public bool Sub, RightAlign, CenterAlign, Link, Wrap;
        bool hover;
        public Lbl() { }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); if (Link) { hover = true; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (Link) { hover = false; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            // on the Mica window background ClearType has no opaque pixels to blend with: use grayscale
            g.TextRenderingHint = Pal.Mica && Parent is Form ? TextRenderingHint.AntiAliasGridFit : TextRenderingHint.ClearTypeGridFit;
            Color c = Link ? (hover ? Gfx.Mix(Pal.Accent, Pal.Text, 0.25f) : Pal.Accent) : (Sub ? Pal.Sub : Pal.Text);
            using (var sf = new StringFormat(StringFormat.GenericTypographic))
            using (var b = new SolidBrush(c))
            {
                sf.LineAlignment = Wrap ? StringAlignment.Near : StringAlignment.Center;
                sf.Alignment = CenterAlign ? StringAlignment.Center : (RightAlign ? StringAlignment.Far : StringAlignment.Near);
                sf.Trimming = StringTrimming.EllipsisCharacter;
                if (!Wrap) sf.FormatFlags |= StringFormatFlags.NoWrap;
                g.DrawString(Text, Font, b, new RectangleF(0, 0, Width, Height), sf);
            }
        }
    }

    class ThemeIcon : Base
    {
        public bool Light;
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Gfx.Fill(g, new RectangleF(0, 0, Width - 1, Height - 1), Pal.F(12), Pal.Field);
            float pad = Width * 0.27f;
            var box = new RectangleF(pad, pad, Width - 2 * pad, Height - 2 * pad);
            if (Light) Gfx.Sun(g, box, Art.SunC); else Gfx.Moon(g, box, Pal.Moon, Pal.Field);
        }
    }

    // icon kinds used across the UI
    enum Ico { None, Sun, Moon, Pin, Refresh }

    class Glyph : Base
    {
        public Ico Kind;
        public Color? Tint;
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var b = new RectangleF(0, 0, Width, Height);
            Color c = Tint ?? Pal.Sub;
            switch (Kind)
            {
                case Ico.Sun: Gfx.Sun(g, b, c); break;
                case Ico.Moon: Gfx.Moon(g, b, c, Pal.Card); break;
                case Ico.Pin: Gfx.Pin(g, b, c, Pal.F(1.6)); break;
                case Ico.Refresh: Gfx.Refresh(g, b, c, Pal.F(1.8)); break;
            }
        }
    }

    class Segmented : Base
    {
        public string[] Items = new string[0];
        public Ico[] Icons;
        public event EventHandler Changed;
        int sel, hover = -1;
        public int Sel { get { return sel; } set { if (sel != value) { sel = value; Invalidate(); } } }

        public Segmented() { Cursor = Cursors.Hand; }

        float Pad() { return Pal.F(4); }
        float SegW() { return (Width - 2 * Pad()) / Math.Max(1, Items.Length); }
        int Hit(int x) { int i = (int)((x - Pad()) / SegW()); return Math.Max(0, Math.Min(Items.Length - 1, i)); }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int h = Hit(e.X); if (h != hover) { hover = h; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || Items.Length == 0) return;
            int i = Hit(e.X);
            if (i != sel) { sel = i; Invalidate(); if (Changed != null) Changed(this, EventArgs.Empty); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            Gfx.Fill(g, r, Pal.F(10), Pal.Track);
            int n = Items.Length; if (n == 0) return;
            float pad = Pad(), w = SegW();
            for (int i = 0; i < n; i++)
            {
                var seg = new RectangleF(pad + i * w, pad, w, Height - 1 - 2 * pad);
                Color bg = Pal.Track;
                if (i == sel) { Gfx.Fill(g, seg, Pal.F(8), Pal.Accent); bg = Pal.Accent; }
                else if (i == hover) { bg = Gfx.Mix(Pal.Track, Pal.Text, 0.07f); Gfx.Fill(g, seg, Pal.F(8), bg); }
                Color fg = i == sel ? Pal.AccentText : Pal.Text;
                Ico ic = Icons != null && i < Icons.Length ? Icons[i] : Ico.None;
                SizeF ts = g.MeasureString(Items[i], Font, 999, StringFormat.GenericTypographic);
                float isz = ic == Ico.None ? 0 : Pal.F(13), gap = ic == Ico.None ? 0 : Pal.F(6);
                float total = isz + gap + ts.Width;
                float x0 = seg.X + (seg.Width - total) / 2f, cy = seg.Y + seg.Height / 2f;
                if (ic == Ico.Sun) Gfx.Sun(g, new RectangleF(x0, cy - isz / 2f, isz, isz), fg);
                else if (ic == Ico.Moon) Gfx.Moon(g, new RectangleF(x0, cy - isz / 2f, isz, isz), fg, bg);
                using (var sf = new StringFormat(StringFormat.GenericTypographic) { LineAlignment = StringAlignment.Center })
                using (var b = new SolidBrush(fg))
                {
                    sf.FormatFlags |= StringFormatFlags.NoWrap;
                    g.DrawString(Items[i], Font, b, new RectangleF(x0 + isz + gap, seg.Y, ts.Width + 4, seg.Height), sf);
                }
            }
        }
    }

    class Toggle : Base
    {
        bool on;
        public event EventHandler Changed;
        public bool Checked { get { return on; } set { on = value; Invalidate(); } }
        public Toggle() { Cursor = Cursors.Hand; }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            on = !on; Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(1, 1, Width - 2, Height - 2);
            float rad = r.Height / 2f;
            if (on) Gfx.Fill(g, r, rad, Pal.Accent); else Gfx.Stroke(g, r, rad, Pal.Sub, Pal.F(1.3));
            float d = Height * (on ? 0.62f : 0.52f);
            float m = (Height - d) / 2f;
            float x = on ? Width - d - m - Pal.F(1) : m + Pal.F(1);
            using (var b = new SolidBrush(on ? Pal.AccentText : Pal.Sub)) g.FillEllipse(b, x, m, d, d);
        }
    }

    class Btn : Base
    {
        public bool Primary;
        public Ico Icon;
        public bool Chevron;
        bool hover, down;
        public Btn() { Cursor = Cursors.Hand; }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; down = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); down = true; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); down = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            Color fill = Primary ? Pal.Accent : Pal.Field;
            Color txt = Primary ? Pal.AccentText : Pal.Text;
            if (!Enabled) { fill = Gfx.Mix(fill, Pal.Card, 0.5f); txt = Pal.Sub; }
            else if (down) fill = Gfx.Mix(fill, Pal.Text, 0.14f);
            else if (hover) fill = Gfx.Mix(fill, Pal.Text, 0.07f);
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Gfx.Fill(g, r, Pal.F(9), fill);
            if (!Primary) Gfx.Stroke(g, r, Pal.F(9), Gfx.Mix(fill, Pal.Text, 0.06f), 1f);
            if (Icon == Ico.Refresh)
            {
                float s = Math.Min(Width, Height) * 0.46f;
                Gfx.Refresh(g, new RectangleF((Width - s) / 2f, (Height - s) / 2f, s, s), txt, Pal.F(1.8));
                return;
            }
            float chw = Chevron ? Pal.F(14) : 0;
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            using (var b = new SolidBrush(txt))
                g.DrawString(Text, Font, b, new RectangleF(Pal.P(6), 0, Width - Pal.P(12) - chw, Height), sf);
            if (Chevron)
                Gfx.Chevron(g, new PointF(Width - Pal.F(18), Height / 2f + Pal.F(1)), Pal.F(3.5), txt, Pal.F(1.5));
        }
    }

    // caption buttons of the custom title bar
    class CapBtn : Base
    {
        public bool IsClose;
        bool hover;
        public CapBtn() { }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fg = Pal.Text;
            if (hover)
            {
                Color bg = IsClose ? Color.FromArgb(196, 43, 28) : Gfx.Mix(Pal.Bg, Pal.Text, 0.09f);
                Gfx.Fill(g, new RectangleF(0, 0, Width - 1, Height - 1), Pal.F(6), bg);
                if (IsClose) fg = Color.White;
            }
            float cx = Width / 2f, cy = Height / 2f, s = Pal.F(7.5);
            using (var pen = new Pen(fg, Pal.F(1.2)))
            {
                if (IsClose)
                {
                    g.DrawLine(pen, cx - s, cy - s, cx + s, cy + s);
                    g.DrawLine(pen, cx - s, cy + s, cx + s, cy - s);
                }
                else g.DrawLine(pen, cx - s - 1, cy, cx + s + 1, cy);
            }
        }
    }

    // list of found cities in the location dialog
    class ResultList : Base
    {
        public System.Collections.Generic.List<Place> Items = new System.Collections.Generic.List<Place>();
        public string Message = "";
        public int Selected = -1;
        public int RowH = 40;
        public Font SubFont;
        public event EventHandler Picked;
        int hover = -1;
        public ResultList() { Cursor = Cursors.Hand; }
        int Hit(int y) { int i = y / Math.Max(1, RowH); return i >= 0 && i < Items.Count ? i : -1; }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int h = Hit(e.Y); if (h != hover) { hover = h; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = -1; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int i = Hit(e.Y);
            if (e.Button == MouseButtons.Left && i >= 0) { Selected = i; Invalidate(); if (Picked != null) Picked(this, EventArgs.Empty); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Gfx.Fill(g, r, Pal.F(10), Pal.Card);
            Gfx.Stroke(g, r, Pal.F(10), Pal.Line, 1f);
            using (var sf = new StringFormat(StringFormat.GenericTypographic) { LineAlignment = StringAlignment.Center })
            {
                sf.FormatFlags |= StringFormatFlags.NoWrap; sf.Trimming = StringTrimming.EllipsisCharacter;
                if (Items.Count == 0)
                {
                    sf.Alignment = StringAlignment.Center;
                    using (var b = new SolidBrush(Pal.Sub)) g.DrawString(Message, SubFont ?? Font, b, new RectangleF(0, 0, Width, Height), sf);
                    return;
                }
                for (int i = 0; i < Items.Count; i++)
                {
                    var row = new RectangleF(Pal.F(4), i * RowH + Pal.F(4), Width - Pal.F(8), RowH - Pal.F(4));
                    if (row.Bottom > Height) break;
                    bool sel = i == Selected;
                    if (sel) Gfx.Fill(g, row, Pal.F(7), Pal.Accent);
                    else if (i == hover) Gfx.Fill(g, row, Pal.F(7), Gfx.Mix(Pal.Card, Pal.Text, 0.07f));
                    Color fg = sel ? Pal.AccentText : Pal.Text, sub = sel ? Pal.AccentText : Pal.Sub;
                    float x = row.X + Pal.F(10);
                    SizeF ns = g.MeasureString(Items[i].Name, Font, 9999, sf);
                    float nw = Math.Min(ns.Width + 2, row.Width * 0.6f);
                    using (var b = new SolidBrush(fg)) g.DrawString(Items[i].Name, Font, b, new RectangleF(x, row.Y, nw, row.Height), sf);
                    using (var b = new SolidBrush(sub))
                        g.DrawString(Items[i].Region, SubFont ?? Font, b, new RectangleF(x + nw + Pal.F(8), row.Y + Pal.F(1), row.Right - x - nw - Pal.F(14), row.Height), sf);
                }
            }
        }
    }

    // location line: pin, place name, coordinates; clickable to edit coordinates
    class LocRow : Base
    {
        public string Place = "", Coords = "";
        public bool Busy;
        bool hover;
        public Font SubFont;
        public LocRow() { Cursor = Cursors.Hand; }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            if (hover) Gfx.Fill(g, new RectangleF(0, 0, Width - 1, Height - 1), Pal.F(9), Gfx.Mix(Pal.Card, Pal.Text, 0.05f));
            float isz = Pal.F(20);
            Gfx.Pin(g, new RectangleF(Pal.F(2), (Height - isz) / 2f, isz, isz), Pal.Sub, Pal.F(1.6));
            float x = Pal.F(32);
            using (var sf = new StringFormat(StringFormat.GenericTypographic) { LineAlignment = StringAlignment.Center })
            {
                sf.FormatFlags |= StringFormatFlags.NoWrap; sf.Trimming = StringTrimming.EllipsisCharacter;
                SizeF ps = g.MeasureString(Place, Font, 9999, sf);
                float maxw = Width - x - Pal.F(4);
                using (var b = new SolidBrush(Busy ? Pal.Sub : Pal.Text))
                    g.DrawString(Place, Font, b, new RectangleF(x, 0, Math.Min(ps.Width + 2, maxw), Height), sf);
                float x2 = x + ps.Width + Pal.F(7);
                if (Coords.Length > 0 && x2 < Width - Pal.F(30))
                    using (var b = new SolidBrush(Pal.Sub))
                        g.DrawString(Coords, SubFont ?? Font, b, new RectangleF(x2, Pal.F(1), Width - x2, Height), sf);
            }
        }
    }

    // 24 h bar: light period in yellow, a marker for "now", labels under it
    class Timeline : Base
    {
        public bool HasWindow, AllLight;
        public TimeSpan LightFrom, DarkFrom;
        public Font SmallFont;
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            float barY = Pal.F(10), barH = Pal.F(6), w = Width - 1;
            Gfx.Fill(g, new RectangleF(0, barY, w, barH), barH / 2f, Pal.Field);
            Func<TimeSpan, float> X = t => (float)(t.TotalMinutes / 1440.0) * w;
            if (AllLight) Gfx.Fill(g, new RectangleF(0, barY, w, barH), barH / 2f, Pal.SunBar);
            else if (HasWindow)
            {
                float a = X(LightFrom), b = X(DarkFrom);
                if (a < b) Gfx.Fill(g, new RectangleF(a, barY, b - a, barH), barH / 2f, Pal.SunBar);
                else
                {
                    Gfx.Fill(g, new RectangleF(a, barY, w - a, barH), barH / 2f, Pal.SunBar);
                    Gfx.Fill(g, new RectangleF(0, barY, b, barH), barH / 2f, Pal.SunBar);
                }
            }
            float nx = X(DateTime.Now.TimeOfDay);
            using (var b = new SolidBrush(Pal.Text))
                g.FillRectangle(b, nx - Pal.F(1.25), barY - Pal.F(7), Pal.F(2.5), barH + Pal.F(14));

            // labels
            float ly = barY + barH + Pal.F(8), lh = Pal.F(18);
            var f = SmallFont ?? Font;
            using (var sf = new StringFormat(StringFormat.GenericTypographic) { LineAlignment = StringAlignment.Center })
            using (var sb = new SolidBrush(Pal.Sub))
            using (var tb = new SolidBrush(Pal.Text))
            {
                sf.FormatFlags |= StringFormatFlags.NoWrap;
                sf.Alignment = StringAlignment.Near;
                g.DrawString("00:00", f, sb, new RectangleF(0, ly, w, lh), sf);
                sf.Alignment = StringAlignment.Far;
                g.DrawString("24:00", f, sb, new RectangleF(0, ly, w, lh), sf);
                if (HasWindow || AllLight)
                {
                    string mid = AllLight ? "24 h" : Fmt(LightFrom) + "  →  " + Fmt(DarkFrom);
                    SizeF ms = g.MeasureString(mid, f, 999, StringFormat.GenericTypographic);
                    float isz = Pal.F(12), gap = Pal.F(6), total = isz + gap + ms.Width;
                    float x0 = (w - total) / 2f;
                    Gfx.Sun(g, new RectangleF(x0, ly + (lh - isz) / 2f, isz, isz), Pal.Text);
                    sf.Alignment = StringAlignment.Near;
                    g.DrawString(mid, f, tb, new RectangleF(x0 + isz + gap, ly, ms.Width + 4, lh), sf);
                }
            }
        }
        static string Fmt(TimeSpan t) { return ((int)t.TotalHours % 24).ToString("00") + ":" + t.Minutes.ToString("00"); }
    }

    class Field : Base
    {
        public TextBox Box;
        public bool Invalid;
        string prefix = "";
        public string Prefix { get { return prefix; } set { prefix = value; Place(); Invalidate(); } }

        public Field()
        {
            Box = new TextBox();
            Box.BorderStyle = BorderStyle.None;
            Box.MaxLength = 12;
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
            Controls.Add(Box);
        }

        int PrefixW() { return prefix.Length == 0 ? 0 : TextRenderer.MeasureText(prefix, Font, new Size(999, 99), TextFormatFlags.NoPadding).Width; }

        public void Place()
        {
            if (Box == null) return;
            int x = Pal.P(12) + PrefixW() + Pal.P(8);
            Box.SetBounds(x, Math.Max(0, (Height - Box.Height) / 2), Math.Max(10, Width - x - Pal.P(10)), Box.Height);
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Place(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Place(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Gfx.Fill(g, r, Pal.F(8), Pal.Field);
            Color border = Invalid ? Color.FromArgb(232, 76, 76) : (Box.Focused ? Pal.Accent : Pal.Line);
            Gfx.Stroke(g, r, Pal.F(8), border, Box.Focused || Invalid ? 1.6f : 1f);
            using (var sf = new StringFormat(StringFormat.GenericTypographic) { LineAlignment = StringAlignment.Center })
            using (var b = new SolidBrush(Pal.Sub))
                g.DrawString(prefix, Font, b, new RectangleF(Pal.P(12), 0, Width, Height), sf);
        }
    }

    class TimeBox : Base
    {
        public int Hour, Minute;
        int part;
        public event EventHandler Changed;
        Font big = new Font("Segoe UI Semibold", 10f);

        public TimeBox() { SetStyle(ControlStyles.Selectable, true); TabStop = true; Cursor = Cursors.Hand; }

        int ArrowW() { return Pal.P(26); }

        void Step(int dir)
        {
            int minStep = (Control.ModifierKeys & Keys.Control) != 0 ? 1 : 5;
            if (part == 0) Hour = (Hour + dir + 24) % 24;
            else Minute = ((Minute + dir * minStep) % 60 + 60) % 60;
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); Focus();
            if (e.X > Width - ArrowW()) Step(e.Y < Height / 2 ? 1 : -1);
            else part = e.X < (Width - ArrowW()) / 2 ? 0 : 1;
            Invalidate();
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!Focused) { Focus(); part = e.X < (Width - ArrowW()) / 2 ? 0 : 1; }
            Step(e.Delta > 0 ? 1 : -1);
        }
        protected override bool IsInputKey(Keys k)
        {
            return k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right || base.IsInputKey(k);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Up) { Step(1); e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { Step(-1); e.Handled = true; }
            else if (e.KeyCode == Keys.Left) { part = 0; Invalidate(); e.Handled = true; }
            else if (e.KeyCode == Keys.Right) { part = 1; Invalidate(); e.Handled = true; }
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            Gfx.Fill(g, r, Pal.F(9), Pal.Field);
            Gfx.Stroke(g, r, Pal.F(9), Focused ? Pal.Accent : Gfx.Mix(Pal.Field, Pal.Text, 0.06f), Focused ? 1.6f : 1f);
            float aw = ArrowW(), cx = (Width - aw) / 2f;
            Color hc = Focused && part == 0 ? Pal.Accent : Pal.Text;
            Color mc = Focused && part == 1 ? Pal.Accent : Pal.Text;
            using (var sf = new StringFormat(StringFormat.GenericTypographic) { LineAlignment = StringAlignment.Center })
            {
                sf.Alignment = StringAlignment.Far;
                using (var b = new SolidBrush(hc)) g.DrawString(Hour.ToString("00"), big, b, new RectangleF(0, 0, cx - Pal.F(4), Height), sf);
                sf.Alignment = StringAlignment.Center;
                using (var b = new SolidBrush(Pal.Sub)) g.DrawString(":", big, b, new RectangleF(cx - Pal.F(4), 0, Pal.F(8), Height), sf);
                sf.Alignment = StringAlignment.Near;
                using (var b = new SolidBrush(mc)) g.DrawString(Minute.ToString("00"), big, b, new RectangleF(cx + Pal.F(4), 0, cx, Height), sf);
            }
            float ax = Width - aw / 2f - Pal.F(2);
            Gfx.Chevron(g, new PointF(ax, Height * 0.36f), Pal.F(3.5), Pal.Sub, Pal.F(1.4), true);
            Gfx.Chevron(g, new PointF(ax, Height * 0.64f), Pal.F(3.5), Pal.Sub, Pal.F(1.4), false);
        }
    }

    // dark / light menus for the tray and the language picker
    class MenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Pal.Card; } }
        public override Color ImageMarginGradientBegin { get { return Pal.Card; } }
        public override Color ImageMarginGradientMiddle { get { return Pal.Card; } }
        public override Color ImageMarginGradientEnd { get { return Pal.Card; } }
        public override Color MenuBorder { get { return Pal.Border; } }
        public override Color MenuItemBorder { get { return Gfx.Mix(Pal.Card, Pal.Text, 0.10f); } }
        public override Color MenuItemSelected { get { return Gfx.Mix(Pal.Card, Pal.Text, 0.10f); } }
        public override Color MenuItemSelectedGradientBegin { get { return MenuItemSelected; } }
        public override Color MenuItemSelectedGradientEnd { get { return MenuItemSelected; } }
        public override Color SeparatorDark { get { return Pal.Line; } }
        public override Color SeparatorLight { get { return Pal.Line; } }
        public override Color CheckBackground { get { return Pal.Field; } }
        public override Color CheckSelectedBackground { get { return Pal.Field; } }
        public override Color CheckPressedBackground { get { return Pal.Field; } }
    }

    class MenuRenderer : ToolStripProfessionalRenderer
    {
        public MenuRenderer() : base(new MenuColors()) { RoundedEdges = false; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Pal.Text : Pal.Sub;
            base.OnRenderItemText(e);
        }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = e.ImageRectangle;
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, s = Math.Min(r.Width, r.Height) * 0.28f;
            using (var pen = Gfx.RoundPen(Pal.Accent, Math.Max(1.5f, s * 0.35f)))
                g.DrawLines(pen, new[] { new PointF(cx - s, cy), new PointF(cx - s * 0.3f, cy + s * 0.7f), new PointF(cx + s, cy - s * 0.7f) });
        }
    }
}
