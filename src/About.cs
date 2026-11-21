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
            ClientSize = new Size(P(340), P(222));
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

            // update check: button + status
            btnUpd = new Btn { Font = new Font("Segoe UI", 9f), Text = L.T("check_updates") };
            btnUpd.SetBounds(P(20), P(130), P(164), P(32));
            btnUpd.Click += delegate { UpdateClick(); };
            Controls.Add(btnUpd);
            lblUpd = Left(192, 130, 128, 32, "", 8.5f, false, true);
            lblUpd.Wrap = false;
            Controls.Add(lblUpd);

            Controls.Add(Left(20, 180, 200, 26, "© 2026 Shax", 9f, false, true));
            var ok = new Btn { Primary = true, Font = new Font("Segoe UI Semibold", 9f), Text = L.T("close") };
            ok.SetBounds(P(220), P(176), P(100), P(32));
            ok.Click += delegate { Close(); };
            Controls.Add(ok);
            KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) Close(); };
        }

        Btn btnUpd;
        Lbl lblUpd;
        ToolTip tips = new ToolTip();
        Updater.Release found;
        bool busy;

        void UpdateClick()
        {
            if (busy) return;
            if (found != null && found.Installer.Length > 0) { Install(); return; }
            if (found != null) { OpenPage(found.Page); return; }
            busy = true; btnUpd.Enabled = false; lblUpd.Text = L.T("checking");
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                Updater.Release r = null; bool fail = false;
                try { r = Updater.Latest(); } catch (Exception ex) { fail = true; if (Updater.LastError.Length == 0) Updater.LastError = ex.Message; }
                OnUi(delegate
                {
                    busy = false; btnUpd.Enabled = true;
                    if (fail || r == null || r.Ver == new Version(0, 0, 0, 0)) { lblUpd.Text = L.T("update_fail"); tips.SetToolTip(lblUpd, Updater.LastError); return; }
                    tips.SetToolTip(lblUpd, "");
                    if (r.Ver <= Updater.Current) { lblUpd.Text = L.T("up_to_date"); return; }
                    found = r;
                    lblUpd.Text = string.Format(L.T("update_avail"), r.Tag.TrimStart('v', 'V'));
                    btnUpd.Primary = true;
                    btnUpd.Text = L.T("update_now");
                    btnUpd.Invalidate();
                });
            });
        }

        // download the installer from the release and start it; it closes the running program itself
        void Install()
        {
            busy = true; btnUpd.Enabled = false; lblUpd.Text = L.T("downloading");
            var r = found;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string file = null;
                string err = "";
                try { file = Updater.Download(r); } catch (Exception ex) { err = ex.Message; }
                OnUi(delegate
                {
                    busy = false; btnUpd.Enabled = true;
                    if (file == null) { lblUpd.Text = L.T("update_fail"); tips.SetToolTip(lblUpd, err); OpenPage(r.Page); return; }
                    try { System.Diagnostics.Process.Start(file); Close(); }
                    catch { lblUpd.Text = L.T("update_fail"); }
                });
            });
        }

        static void OpenPage(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://github.com/")) url = "https://github.com/" + Updater.Repo + "/releases";
            try { System.Diagnostics.Process.Start(url); } catch { }
        }

        void OnUi(Action a)
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); } catch { }
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
