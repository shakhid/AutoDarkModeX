using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoDarkMode
{
    class AppImage : Base
    {
        Image img;
        public AppImage()
        {
            try { img = Image.FromStream(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.png")); } catch { }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (img == null) return;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawImage(img, new Rectangle(0, 0, Width, Height));
        }
    }

    class AboutForm : Form
    {
        static int P(double v) { return Pal.P(v); }

        public AboutForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(P(340), P(176));
            BackColor = Pal.Bg;
            Font = new Font("Segoe UI", 9f);
            DoubleBuffered = true;
            KeyPreview = true;
            Text = L.T("about");

            // icon left, name and version next to it
            var icon = new AppImage();
            icon.SetBounds(P(20), P(20), P(52), P(52));
            Controls.Add(icon);
            Controls.Add(Left(86, 22, 234, 26, L.T("title"), 13f, true, false));
            Controls.Add(Left(86, 48, 234, 20, L.T("version") + " " + MainForm.VersionText, 9f, false, true));

            var desc = Left(20, 84, 300, 38, L.T("about_desc"), 9f, false, true);
            desc.Wrap = true;
            Controls.Add(desc);

            Controls.Add(Left(20, 134, 200, 26, "© 2026 Shax", 9f, false, true));
            var ok = new Btn { Primary = true, Font = new Font("Segoe UI Semibold", 9f), Text = L.T("close") };
            ok.SetBounds(P(220), P(130), P(100), P(32));
            ok.Click += delegate { Close(); };
            Controls.Add(ok);
            KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) Close(); };
        }

        Lbl Left(double x, double y, double w, double h, string text, float size, bool bold, bool sub)
        {
            var l = new Lbl { Text = text, Sub = sub, Font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size) };
            l.SetBounds(P(x), P(y), P(w), P(h));
            return l;
        }

        Lbl MakeLbl(double x, double y, double w, double h, string text, float size, bool bold, bool sub)
        {
            var l = new Lbl { Text = text, Sub = sub, CenterAlign = true, Font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size) };
            l.SetBounds(P(x), P(y), P(w), P(h));
            return l;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            MainForm.StyleTitleBar(Handle, true);
        }
    }
}
