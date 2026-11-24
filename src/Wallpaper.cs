using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AutoDarkMode
{
    // Desktop wallpaper per theme. Chosen photos are copied into %APPDATA%\AutoDarkModeX\wallpapers,
    // so they keep working when the original file is moved or deleted.
    static class Wallpaper
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool SystemParametersInfo(int action, int param, string value, int flags);
        const int SPI_SETDESKWALLPAPER = 0x0014, SPIF_UPDATEINIFILE = 1, SPIF_SENDCHANGE = 2;

        public static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".bmp" };

        public static string Folder
        {
            get
            {
                string d = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoDarkModeX"), "wallpapers");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        // The wallpaper Windows shows now: its file, or Windows' own copy of it (e.g. after the original was removed).
        public static string Current()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                {
                    string p = k == null ? null : k.GetValue("WallPaper") as string;
                    if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
                }
            }
            catch { }
            string t = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
            return File.Exists(t) ? t : null;
        }

        // The last wallpapers chosen in Windows settings ("Recent images"), newest first.
        public static List<string> Recent()
        {
            var list = new List<string>();
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers"))
                    if (k != null)
                        for (int i = 0; i < 5; i++)
                        {
                            string p = k.GetValue("BackgroundHistoryPath" + i) as string;
                            if (!string.IsNullOrEmpty(p) && File.Exists(p) && !list.Contains(p)) list.Add(p);
                        }
            }
            catch { }
            return list;
        }

        // Copies a photo into the program's folder for the light or the dark slot; returns the new path.
        // Each copy gets a new name: Windows caches wallpapers by path.
        public static string Store(string source, bool light, string previous)
        {
            string ext = Path.GetExtension(source).ToLowerInvariant();
            if (Array.IndexOf(Extensions, ext) < 0) ext = ".jpg";   // e.g. Windows' TranscodedWallpaper (JPEG without extension)
            string dest = Path.Combine(Folder, (light ? "light-" : "dark-") + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ext);
            File.Copy(source, dest, true);
            try
            {
                if (!string.IsNullOrEmpty(previous) && File.Exists(previous) &&
                    Path.GetDirectoryName(Path.GetFullPath(previous)).Equals(Folder, StringComparison.OrdinalIgnoreCase))
                    File.Delete(previous);
            }
            catch { }
            return dest;
        }

        // Sets the wallpaper unless Windows already shows this file. Fit mode stays as chosen in Windows.
        public static void Apply(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                {
                    string cur = k == null ? null : k.GetValue("WallPaper") as string;
                    if (string.Equals(cur, path, StringComparison.OrdinalIgnoreCase)) return;
                }
            }
            catch { }
            try { SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE); } catch { }
        }

        // A small copy of the photo for the preview, cropped to fill w x h; the file is not kept open.
        public static Bitmap Thumb(string path, int w, int h)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || w <= 0 || h <= 0) return null;
            try
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                using (var img = Image.FromStream(ms))
                {
                    var bmp = new Bitmap(w, h);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        float scale = Math.Max((float)w / img.Width, (float)h / img.Height);
                        float sw = w / scale, sh = h / scale;
                        g.DrawImage(img, new RectangleF(0, 0, w, h), new RectangleF((img.Width - sw) / 2f, (img.Height - sh) / 2f, sw, sh), GraphicsUnit.Pixel);
                    }
                    return bmp;
                }
            }
            catch { return null; }
        }
    }

    // Preview tile: the photo with a "Light" / "Dark" chip; click to choose another photo.
    class WallThumb : Base
    {
        Bitmap img;
        public bool Light, ShowChip = true;
        public string Caption = "";
        bool hover;

        public WallThumb() { Cursor = Cursors.Hand; }

        public void SetImage(string path)
        {
            var old = img;
            img = Wallpaper.Thumb(path, Math.Max(1, Width), Math.Max(1, Height));
            if (old != null) old.Dispose();
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            if (img != null)
                using (var path = Gfx.Round(r, Pal.F(8)))
                {
                    var old = g.Clip; g.SetClip(path, CombineMode.Intersect);
                    g.DrawImage(img, 0, 0, Width, Height);
                    g.Clip = old;
                }
            else
            {
                Gfx.Fill(g, r, Pal.F(8), Pal.Field);
                using (var pen = Gfx.RoundPen(Pal.Sub, Pal.F(1.8)))
                {
                    float cx = Width / 2f, cy = Height / 2f - (ShowChip ? Pal.F(8) : 0), s = Pal.F(8);
                    g.DrawLine(pen, cx - s, cy, cx + s, cy);
                    g.DrawLine(pen, cx, cy - s, cx, cy + s);
                }
            }
            Gfx.Stroke(g, r, Pal.F(8), hover ? Pal.Accent : Gfx.Mix(Pal.Card, Pal.Text, 0.15f), hover ? 2f : 1f);

            if (ShowChip)
            {
                float ch = Pal.F(22);
                using (var f = new Font("Segoe UI Semibold", 8f))
                {
                    float tw = g.MeasureString(Caption, f, 999, StringFormat.GenericTypographic).Width;
                    float isz = Pal.F(11), cw = Pal.F(8) + isz + Pal.F(5) + tw + Pal.F(10);
                    var chip = new RectangleF(Pal.F(6), Height - ch - Pal.F(6), cw, ch);
                    Gfx.Fill(g, chip, ch / 2f, Color.FromArgb(200, 20, 20, 24));
                    var ib = new RectangleF(chip.X + Pal.F(8), chip.Y + (ch - isz) / 2f, isz, isz);
                    if (Light) Gfx.Sun(g, ib, Color.White); else Gfx.Moon(g, ib, Color.White, Color.FromArgb(20, 20, 24));
                    using (var b = new SolidBrush(Color.White))
                    using (var sf = new StringFormat(StringFormat.GenericTypographic) { LineAlignment = StringAlignment.Center })
                        g.DrawString(Caption, f, b, new RectangleF(ib.Right + Pal.F(5), chip.Y, tw + 4, ch), sf);
                }
            }
            if (hover && img != null && ShowChip)
            {   // "change" badge
                var pr = new RectangleF(Width - Pal.F(30), Pal.F(6), Pal.F(24), Pal.F(24));
                Gfx.Fill(g, pr, Pal.F(12), Color.FromArgb(200, 20, 20, 24));
                using (var pen = Gfx.RoundPen(Color.White, Pal.F(1.5)))
                {
                    float x0 = pr.X + Pal.F(7), y0 = pr.Bottom - Pal.F(7), x1 = pr.Right - Pal.F(7), y1 = pr.Y + Pal.F(7);
                    g.DrawLine(pen, x0 + Pal.F(1), y0 - Pal.F(1), x1, y1);          // pencil body
                    g.DrawLine(pen, x0, y0, x0 + Pal.F(3), y0);                     // tip
                    g.DrawLine(pen, x0, y0, x0, y0 - Pal.F(3));
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && img != null) { img.Dispose(); img = null; }
            base.Dispose(disposing);
        }
    }

    // Choose a photo: Windows' recent wallpapers, or any file (starts in Windows' own wallpaper folder).
    class WallPickForm : Form
    {
        public string Picked;
        static int P(double v) { return Pal.P(v); }

        public WallPickForm(bool light)
        {
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(P(552), P(172));
            BackColor = Pal.Bg;
            Font = new Font("Segoe UI", 9f);
            Text = L.T(light ? "wall_for_light" : "wall_for_dark");
            KeyPreview = true;

            var lbl = new Lbl { Text = L.T("recent"), Sub = true, Font = new Font("Segoe UI", 9f) };
            lbl.SetBounds(P(20), P(12), P(400), P(22));
            Controls.Add(lbl);

            var recent = Wallpaper.Recent();
            if (recent.Count == 0)
            {
                var none = new Lbl { Text = L.T("no_recent"), Sub = true, Font = new Font("Segoe UI", 9f) };
                none.SetBounds(P(20), P(40), P(400), P(60));
                Controls.Add(none);
            }
            for (int i = 0; i < recent.Count; i++)
            {
                string path = recent[i];
                var t = new WallThumb { ShowChip = false };
                t.SetBounds(P(20 + i * 104), P(38), P(96), P(64));
                Controls.Add(t);
                t.SetImage(path);
                t.Click += delegate { Picked = path; DialogResult = DialogResult.OK; Close(); };
            }

            var browse = new Btn { Text = L.T("browse"), Font = new Font("Segoe UI", 9f) };
            browse.SetBounds(P(20), P(120), P(140), P(34));
            browse.Click += delegate { Browse(); };
            var cancel = new Btn { Text = L.T("cancel"), Font = new Font("Segoe UI", 9f) };
            cancel.SetBounds(P(412), P(120), P(120), P(34));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(browse); Controls.Add(cancel);
            KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
        }

        void Browse()
        {
            using (var d = new OpenFileDialog())
            {
                d.Title = Text;
                d.Filter = L.T("images") + "|*.jpg;*.jpeg;*.png;*.bmp";
                string web = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Web\Wallpaper");
                if (Directory.Exists(web)) d.InitialDirectory = web;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                Picked = d.FileName;
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            MainForm.StyleTitleBar(Handle, true);
        }
    }
}
