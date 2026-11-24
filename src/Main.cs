using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AutoDarkMode
{
    class Plan
    {
        public bool Light, NextLight, HasNext, HasWindow, AllLight;
        public DateTime Next;
        public TimeSpan LightFrom, DarkFrom;
        public int Polar; // 0 none, 1 polar day, 2 polar night
    }

    class MainForm : Form
    {
        const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string AppName = "AutoDarkModeX";
        const int TitleH = 40;
        const int WinW = 400;                       // window width (96 dpi units)
        const int HeightWallOff = 534, HeightWallOn = 644;
        int WinH = HeightWallOff;

        public const string Copyright = "Copyright (c) 2026 Created by Shax";
        public const string VersionText = AppVersion.Text;

        Settings S;
        NotifyIcon tray;
        System.Threading.Timer bgTimer;   // background check: independent of the (possibly hidden) window
        readonly object evalLock = new object();
        ContextMenuStrip trayMenu, langMenu;
        ToolStripMenuItem miOpen, miLight, miAuto, miDark, miExit;
        ToolStripMenuItem[] miLang = new ToolStripMenuItem[L.Codes.Length];
        CapBtn btnMin, btnClose;
        Card statusCard, schedCard, optCard;
        ThemeIcon icon;
        Lbl lblState, lblNext, lnkAbout, wallLbl;
        Card wallCard;
        Toggle wallTg;
        WallThumb thLight, thDark;
        Btn btnLang, btnRefresh;
        Segmented theme, sched;
        LocRow loc;
        Glyph gSun, gMoon;
        TimeBox tLight, tDark;
        Timeline line;
        Lbl[] optLbl = new Lbl[4];
        Toggle[] optTg = new Toggle[4];
        ToolTip tips = new ToolTip();

        bool? lastDesired;
        bool curLight, shownLight, exiting, startHidden, loading, hintShown, detecting, dwmRound, micaOn;
        DateTime locFailUntil = DateTime.MinValue;
        Font titleFont;

        static int P(double v) { return Pal.P(v); }
        static Font Fnt(float size, bool bold) { return new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size); }

        public MainForm(bool hidden)
        {
            S = Settings.Load();
            // autostart (--tray): run unseen in the background - no window, no taskbar button.
            // The tray icon is shown only when the tray option is on; the window is opened
            // again by starting the program (desktop shortcut / Start menu).
            startHidden = hidden;
            if (S.Lang < 0) S.Lang = L.Detect();
            L.Lang = S.Lang;
            S.Startup = StartupEnabled();

            curLight = ReadLight(); shownLight = curLight;
            Pal.Set(!curLight);

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            if (S.WinX != int.MinValue && OnScreen(S.WinX, S.WinY))
            {
                StartPosition = FormStartPosition.Manual;
                Location = new Point(S.WinX, S.WinY);
            }
            ClientSize = new Size(P(WinW), P(WinH));
            BackColor = Pal.Bg;
            Font = Fnt(9f, false);
            titleFont = Fnt(9f, false);
            DoubleBuffered = true;

            BuildUI();
            BuildTray();
            LoadValues();
            ApplyTexts();

            // Every 10 s, on a background thread: switch the theme if needed (registry + broadcast need no window),
            // then refresh the window on the UI thread. This keeps working while the window is hidden.
            bgTimer = new System.Threading.Timer(delegate
            {
                try { ApplySchedule(false); } catch { }
                OnUi(delegate { Evaluate(false); });
            }, null, 10000, 10000);
            // After waking up, unlocking or a clock change, check the theme again. Windows may still be
            // starting the taskbar right after logon / resume, so the theme is re-sent a few times.
            SystemEvents.PowerModeChanged += delegate (object s, PowerModeChangedEventArgs e)
            {
                if (e.Mode == PowerModes.Resume) { ReapplyLater(3000, 10000, 30000, 90000); OnUi(delegate { Evaluate(false); }); }
            };
            SystemEvents.SessionSwitch += delegate (object s, SessionSwitchEventArgs e)
            {
                if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.SessionLogon || e.Reason == SessionSwitchReason.ConsoleConnect)
                { ReapplyLater(2000, 8000, 30000); OnUi(delegate { Evaluate(false); }); }
            };
            SystemEvents.UserPreferenceChanged += delegate (object s, UserPreferenceChangedEventArgs e)
            {
                if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color || e.Category == UserPreferenceCategory.VisualStyle)
                    OnUi(delegate { ApplyPalette(); });
            };
            SystemEvents.TimeChanged += delegate { OnUi(delegate { Evaluate(false); }); };

            Evaluate(true);
            ApplyPalette();
            ReapplyLater(15000, 60000);
            if (!S.LocSet) Detect(!hidden);
        }

        // ---------- borderless window ----------
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // WS_CAPTION | WS_MINIMIZEBOX: Windows animates minimize / restore and minimizes from the taskbar.
                // No WS_SYSMENU / WS_MAXIMIZEBOX: without them Windows draws no system caption buttons
                // (they would show through the Mica background). The caption itself is hidden in WndProc.
                if (Environment.OSVersion.Platform == PlatformID.Win32NT) cp.Style |= 0x00C00000 | 0x00020000;
                else cp.Style |= 0x00020000;
                cp.Style &= ~(0x00080000 | 0x00010000);
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCCALCSIZE = 0x0083, WM_NCACTIVATE = 0x0086;
            if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                m.Result = IntPtr.Zero;   // the whole window is client area: no system caption or frame
                return;
            }
            if (m.Msg == WM_NCACTIVATE)
            {
                // stop Windows from painting the hidden caption when focus changes
                m.LParam = (IntPtr)(-1);
            }
            base.WndProc(ref m);
        }

        // With the caption style WinForms would add caption height to the window; keep it exactly client-sized.
        protected override void SetClientSizeCore(int x, int y)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT && IsHandleCreated) { Size = new Size(x, y); return; }
            base.SetClientSizeCore(x, y);
        }

        protected override void OnShown(EventArgs e) { base.OnShown(e); FitWindow(); }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Normal && IsHandleCreated) FitWindow();
        }

        void FitWindow()
        {
            var want = new Size(P(WinW), P(WinH));
            if (Size != want) Size = want;
        }

        // the title bar must stay reachable: at least part of it on some screen
        bool OnScreen(int x, int y)
        {
            var title = new Rectangle(x + P(40), y, P(200), P(30));
            foreach (Screen sc in Screen.AllScreens) if (sc.WorkingArea.IntersectsWith(title)) return true;
            return false;
        }

        void SavePosition()
        {
            if (!Visible || WindowState != FormWindowState.Normal) return;
            if (S.WinX == Left && S.WinY == Top) return;
            S.WinX = Left; S.WinY = Top; S.Save();
        }

        protected override void OnResizeEnd(EventArgs e) { base.OnResizeEnd(e); SavePosition(); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (!micaOn) { base.OnPaintBackground(e); return; }
            // GDI black = fully transparent on the extended frame, so Mica shows through
            IntPtr hdc = e.Graphics.GetHdc();
            try { var r = new Native.RECT { L = -20000, T = -20000, R = 20000, B = 20000 }; Native.FillRect(hdc, ref r, Native.GetStockObject(4)); }
            finally { e.Graphics.ReleaseHdc(hdc); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && e.Y < P(TitleH))
            {
                try { Native.ReleaseCapture(); Native.SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); } catch { }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = micaOn ? TextRenderingHint.AntiAliasGridFit : TextRenderingHint.ClearTypeGridFit;
            float isz = Pal.F(15);
            var ib = new RectangleF(Pal.F(14), Pal.F(20) - isz / 2f, isz, isz);
            if (curLight) Gfx.Sun(g, ib, Pal.Accent); else Gfx.MoonOutline(g, ib, Pal.Accent, Pal.F(1.7));
            using (var sf = new StringFormat(StringFormat.GenericTypographic) { LineAlignment = StringAlignment.Center })
            using (var b = new SolidBrush(Pal.Text))
                g.DrawString(Text, titleFont, b, new RectangleF(Pal.F(38), Pal.F(20) - Pal.F(12), Pal.F(240), Pal.F(24)), sf);
            if (!dwmRound && !micaOn)
                using (var pen = new Pen(Pal.Border)) g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        // ---------- UI construction ----------
        T Add<T>(Control parent, T c, double x, double y, double w, double h) where T : Control
        {
            c.Location = new Point(P(x), P(y));
            c.Size = new Size(P(w), P(h));
            parent.Controls.Add(c);
            return c;
        }

        Lbl MakeLbl(Control parent, double x, double y, double w, double h, float size, bool sub, bool bold)
        {
            var l = Add(parent, new Lbl(), x, y, w, h);
            l.Font = Fnt(size, bold); l.Sub = sub;
            return l;
        }

        void BuildUI()
        {
            // title bar buttons
            btnMin = Add(this, new CapBtn(), 306, 5, 42, 30);
            btnClose = Add(this, new CapBtn { IsClose = true }, 352, 5, 42, 30);
            btnMin.Click += delegate { MinimizeWindow(); };
            btnClose.Click += delegate { Close(); };

            // ---- status card ----
            statusCard = Add(this, new Card(), 12, 42, 376, 104);
            icon = Add(statusCard, new ThemeIcon(), 14, 12, 36, 36);
            lblState = MakeLbl(statusCard, 60, 8, 236, 26, 12.5f, false, true);
            lblNext = MakeLbl(statusCard, 60, 33, 240, 16, 8f, true, false);
            btnLang = Add(statusCard, new Btn { Chevron = true }, 310, 14, 52, 28);
            btnLang.Font = Fnt(8.5f, true);
            btnLang.Click += delegate { langMenu.Show(btnLang, new Point(btnLang.Width - langMenu.PreferredSize.Width, btnLang.Height + P(4))); };
            theme = Add(statusCard, new Segmented(), 14, 58, 348, 34);
            theme.Font = Fnt(9f, true);
            theme.Icons = new[] { Ico.Sun, Ico.None, Ico.Moon };
            theme.Changed += delegate
            {
                if (loading) return;
                S.Theme = theme.Sel; S.Save(); lastDesired = null; Evaluate(true);
            };

            // ---- schedule card ----
            schedCard = Add(this, new Card(), 12, 154, 376, 130);
            sched = Add(schedCard, new Segmented(), 14, 12, 348, 29);
            sched.Font = Fnt(8.5f, true);
            sched.Changed += delegate
            {
                if (loading) return;
                S.Mode = sched.Sel; S.Save(); ShowMode(); Evaluate(true);
            };

            loc = Add(schedCard, new LocRow(), 8, 46, 316, 30);
            loc.Font = Fnt(9f, false); loc.SubFont = Fnt(8f, false);
            loc.Click += delegate { EditCoords(); };
            btnRefresh = Add(schedCard, new Btn { Icon = Ico.Refresh }, 332, 46, 30, 30);
            btnRefresh.Click += delegate { Detect(); };

            gSun = Add(schedCard, new Glyph { Kind = Ico.Sun }, 14, 54, 14, 14);
            tLight = Add(schedCard, new TimeBox(), 34, 45, 140, 32);
            gMoon = Add(schedCard, new Glyph { Kind = Ico.Moon }, 202, 54, 14, 14);
            tDark = Add(schedCard, new TimeBox(), 222, 45, 140, 32);
            tLight.Changed += delegate { if (loading) return; S.LH = tLight.Hour; S.LM = tLight.Minute; S.Save(); Evaluate(true); };
            tDark.Changed += delegate { if (loading) return; S.DH = tDark.Hour; S.DM = tDark.Minute; S.Save(); Evaluate(true); };

            line = Add(schedCard, new Timeline(), 14, 82, 348, 42);
            line.Font = Fnt(8f, false); line.SmallFont = Fnt(8f, false);

            // ---- options card ----
            optCard = Add(this, new Card(), 12, 292, 376, 156);
            optCard.Rows = 4; optCard.RowH = P(38); optCard.RowTop = P(2);
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                optLbl[i] = MakeLbl(optCard, 14, 2 + i * 38 + 6, 280, 26, 9f, false, false);
                optTg[i] = Add(optCard, new Toggle(), 362 - 40, 2 + i * 38 + 9, 40, 21);
                optTg[i].Changed += delegate { OptionChanged(idx); };
            }

            // ---- wallpaper card (off by default; the photos show only when it is on) ----
            wallCard = Add(this, new Card(), 12, 456, 376, 44);
            wallLbl = MakeLbl(wallCard, 14, 8, 280, 26, 9f, false, false);
            wallTg = Add(wallCard, new Toggle(), 322, 11, 40, 21);
            wallTg.Changed += delegate { WallToggled(); };
            thLight = Add(wallCard, new WallThumb { Light = true }, 14, 44, 169, 95);
            thDark = Add(wallCard, new WallThumb { Light = false }, 193, 44, 169, 95);
            thLight.Click += delegate { PickWallpaper(true); };
            thDark.Click += delegate { PickWallpaper(false); };

            // ---- footer ----
            lnkAbout = MakeLbl(this, 200, 454, 188, 20, 8.5f, false, false);
            lnkAbout.Link = true; lnkAbout.RightAlign = true; lnkAbout.Cursor = Cursors.Hand;
            lnkAbout.Click += delegate { ShowAbout(); };

            // language menu
            langMenu = new ContextMenuStrip { ShowImageMargin = false, ShowCheckMargin = true };
            langMenu.Font = Fnt(9.5f, false);
            foreach (int i in L.MenuOrder)
            {
                int idx = i;
                miLang[i] = new ToolStripMenuItem(L.Names[i]);
                miLang[i].Padding = new Padding(0, P(3), 0, P(3));
                miLang[i].Click += delegate { SetLang(idx); };
                langMenu.Items.Add(miLang[i]);
            }
        }

        void SetLang(int i)
        {
            S.Lang = i; L.Lang = i; S.Save();
            ApplyTexts();
        }

        void ShowAbout()
        {
            using (var f = new AboutForm()) f.ShowDialog(this);
        }

        void EditCoords()
        {
            using (var f = new CoordForm(S.Lat, S.Lon, S.City))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                S.Lat = f.Lat; S.Lon = f.Lon; S.City = Geo.Clean(f.PlaceName); S.LocSrc = S.City.Length > 0 ? 3 : 0; S.LocSet = true; S.Save();
                locFailUntil = DateTime.MinValue;
                Evaluate(S.Mode == 0);
            }
        }

        void ShowMode()
        {
            bool sun = S.Mode == 0;
            loc.Visible = btnRefresh.Visible = sun;
            gSun.Visible = gMoon.Visible = tLight.Visible = tDark.Visible = !sun;
        }

        void LoadValues()
        {
            loading = true;
            theme.Sel = S.Theme;
            sched.Sel = S.Mode;
            tLight.Hour = S.LH; tLight.Minute = S.LM;
            tDark.Hour = S.DH; tDark.Minute = S.DM;
            optTg[0].Checked = S.SysTheme; optTg[1].Checked = S.AppTheme;
            optTg[2].Checked = S.Startup; optTg[3].Checked = S.Tray;
            wallTg.Checked = S.Wall;
            ShowMode();
            ShowWallUI();
            loading = false;
        }

        void ApplyTexts()
        {
            Text = L.T("title");
            btnLang.Text = L.Codes[S.Lang];
            for (int i = 0; i < L.Codes.Length; i++) miLang[i].Checked = i == S.Lang;
            theme.Items = new[] { L.T("light"), L.T("auto"), L.T("dark") };
            theme.Invalidate();
            sched.Items = new[] { L.T("mode_sun"), L.T("mode_time") };
            sched.Invalidate();
            optLbl[0].Text = L.T("opt_system"); optLbl[1].Text = L.T("opt_apps");
            optLbl[2].Text = L.T("opt_startup"); optLbl[3].Text = L.T("opt_tray");
            lnkAbout.Text = L.T("about");
            miOpen.Text = L.T("tray_open"); miLight.Text = L.T("light"); miAuto.Text = L.T("auto");
            miDark.Text = L.T("dark"); miExit.Text = L.T("tray_exit");
            tips.SetToolTip(btnRefresh, L.T("refresh_tip"));
            tips.SetToolTip(btnMin, L.T("minimize"));
            wallLbl.Text = L.T("wall");
            thLight.Caption = L.T("light"); thDark.Caption = L.T("dark");
            thLight.Invalidate(); thDark.Invalidate();
            tips.SetToolTip(thLight, L.T("wall_tip")); tips.SetToolTip(thDark, L.T("wall_tip"));
            tips.SetToolTip(btnClose, L.T("close"));
            UpdateStatus(Compute(DateTime.Now));
            Invalidate(new Rectangle(0, 0, ClientSize.Width, P(TitleH)));
        }

        // ---------- Tray ----------
        void BuildTray()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Font = Fnt(9.5f, false);
            miOpen = new ToolStripMenuItem("Open"); miOpen.Click += delegate { ShowFromTray(); };
            miOpen.Font = Fnt(9.5f, true);
            miLight = new ToolStripMenuItem("Light"); miLight.Click += delegate { SetTheme(0); };
            miAuto = new ToolStripMenuItem("Auto"); miAuto.Click += delegate { SetTheme(1); };
            miDark = new ToolStripMenuItem("Dark"); miDark.Click += delegate { SetTheme(2); };
            miExit = new ToolStripMenuItem("Exit"); miExit.Click += delegate { exiting = true; Close(); };
            trayMenu.Items.AddRange(new ToolStripItem[] { miOpen, new ToolStripSeparator(), miLight, miAuto, miDark, new ToolStripSeparator(), miExit });
            tray = new NotifyIcon();
            tray.ContextMenuStrip = trayMenu;
            tray.Icon = MakeIcon(curLight);
            tray.Visible = S.Tray;
            tray.MouseClick += delegate (object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowFromTray(); };
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { Icon = tray.Icon; }
        }

        void SetTheme(int t)
        {
            S.Theme = t; S.Save(); lastDesired = null;
            loading = true; theme.Sel = t; loading = false;
            Evaluate(true);
        }

        public void ShowFromTray()
        {
            Evaluate(false);
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        // "—" always minimizes to the taskbar; the tray option only affects the close button
        void MinimizeWindow()
        {
            WindowState = FormWindowState.Minimized;
        }

        void ShowHint()
        {
            if (hintShown) return;
            hintShown = true;
            tray.ShowBalloonTip(2000, L.T("title"), L.T("tray_hint"), ToolTipIcon.Info);
        }

        static Icon MakeIcon(bool light)
        {
            using (var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                Art.Draw(g, light, new RectangleF(0, 0, 32, 32));
                IntPtr h = bmp.GetHicon();
                try { using (var tmp = Icon.FromHandle(h)) return (Icon)tmp.Clone(); }
                finally { try { Native.DestroyIcon(h); } catch { } }
            }
        }

        // ---------- Window behaviour ----------
        protected override void SetVisibleCore(bool value)
        {
            if (startHidden)
            {
                value = false;
                if (!IsHandleCreated) CreateHandle();
                startHidden = false;
            }
            base.SetVisibleCore(value);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            FitWindow();
            ApplyTitleBar();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SavePosition();
            if (!exiting && S.Tray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                ShowHint();
            }
            else
            {
                tray.Visible = false;
                tray.Dispose();
            }
            base.OnFormClosing(e);
        }

        static int Rgb(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        void ApplyTitleBar()
        {
            StyleTitleBar(Handle, IsHandleCreated);
            if (!IsHandleCreated) return;
            try
            {
                int round = 2; // DWMWCP_ROUND (Windows 11)
                dwmRound = Native.DwmSetWindowAttribute(Handle, 33, ref round, 4) == 0;
                int border = Rgb(Pal.Border);
                Native.DwmSetWindowAttribute(Handle, 34, ref border, 4);
            }
            catch { dwmRound = false; }
            ApplyMica();
        }

        // Mica background on Windows 11 22H2+ (build 22621); "mica=0" in settings.ini turns it off
        void ApplyMica()
        {
            bool want = S.Mica && WinBuild() >= 22621;
            bool ok = false;
            if (want)
            {
                try
                {
                    var m = new Native.MARGINS { L = -1, R = -1, T = -1, B = -1 };
                    int backdrop = 2; // DWMSBT_MAINWINDOW (Mica)
                    ok = Native.DwmExtendFrameIntoClientArea(Handle, ref m) == 0 &&
                         Native.DwmSetWindowAttribute(Handle, 38, ref backdrop, 4) == 0;
                }
                catch { ok = false; }
            }
            micaOn = ok; Pal.Mica = ok;
        }

        static int WinBuild()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    int b; return k != null && int.TryParse(Convert.ToString(k.GetValue("CurrentBuildNumber")), out b) ? b : 0;
                }
            }
            catch { return 0; }
        }

        public static void StyleTitleBar(IntPtr h, bool ready)
        {
            if (!ready) return;
            try
            {
                int d = Pal.Dark ? 1 : 0;
                Native.DwmSetWindowAttribute(h, 20, ref d, 4);   // immersive dark title bar
                int cap = Rgb(Pal.Bg), txt = Rgb(Pal.Text);
                Native.DwmSetWindowAttribute(h, 35, ref cap, 4); // caption colour (Win 11)
                Native.DwmSetWindowAttribute(h, 36, ref txt, 4); // caption text colour (Win 11)
            }
            catch { }
        }

        void ApplyPalette()
        {
            Pal.Set(!curLight);
            BackColor = Pal.Bg;
            ApplyTitleBar();
            trayMenu.Renderer = new MenuRenderer();
            langMenu.Renderer = new MenuRenderer();
            trayMenu.BackColor = langMenu.BackColor = Pal.Card;
            Invalidate(true);
            var old = tray.Icon;
            tray.Icon = MakeIcon(curLight);
            if (old != null && old != Icon) old.Dispose();
        }

        // ---------- Theme logic ----------
        bool ReadLight()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(PersonalizeKey))
                {
                    bool useApps = S == null || S.AppTheme || !S.SysTheme;
                    object v = k.GetValue(useApps ? "AppsUseLightTheme" : "SystemUsesLightTheme");
                    return v == null || Convert.ToInt32(v) != 0;
                }
            }
            catch { return true; }
        }

        // true when every part of Windows we control already has the wanted theme
        bool Matches(bool light)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(PersonalizeKey))
                {
                    if (k == null) return false;
                    int want = light ? 1 : 0;
                    if (S.AppTheme && Convert.ToInt32(k.GetValue("AppsUseLightTheme", 1)) != want) return false;
                    if (S.SysTheme && Convert.ToInt32(k.GetValue("SystemUsesLightTheme", 1)) != want) return false;
                    return true;
                }
            }
            catch { return false; }
        }

        void WriteTheme(bool light)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(PersonalizeKey))
                {
                    int v = light ? 1 : 0;
                    if (S.AppTheme) k.SetValue("AppsUseLightTheme", v, RegistryValueKind.DWord);
                    if (S.SysTheme) k.SetValue("SystemUsesLightTheme", v, RegistryValueKind.DWord);
                }
            }
            catch { }
            ThreadPool.QueueUserWorkItem(delegate
            {
                UIntPtr res;
                try { Native.SendMessageTimeout((IntPtr)0xffff, 0x001A, UIntPtr.Zero, "ImmersiveColorSet", 2, 3000, out res); } catch { }
            });
        }

        Plan Compute(DateTime now)
        {
            var p = new Plan();
            TimeSpan ls, ds;
            if (S.Mode == 0)
            {
                DateTime rise, sunset;
                int r = Sun.Times(now.Date, S.Lat, S.Lon, out rise, out sunset);
                if (r == 1) { p.Polar = 1; p.Light = true; p.AllLight = true; return p; }
                if (r == 2) { p.Polar = 2; p.Light = false; return p; }
                ls = rise.TimeOfDay; ds = sunset.TimeOfDay;
            }
            else
            {
                ls = new TimeSpan(S.LH, S.LM, 0); ds = new TimeSpan(S.DH, S.DM, 0);
            }
            p.LightFrom = ls; p.DarkFrom = ds;
            if (ls == ds) { p.Light = true; p.AllLight = true; return p; }
            p.HasWindow = true;
            TimeSpan t = now.TimeOfDay;
            bool light = ls < ds ? (t >= ls && t < ds) : (t >= ls || t < ds);
            p.Light = light; p.NextLight = !light; p.HasNext = true;
            DateTime n = now.Date + (light ? ds : ls);
            if (n <= now) n = n.AddDays(1);
            p.Next = n;
            return p;
        }

        // Keeps Windows on the wanted theme: the schedule in Auto, the fixed theme in Light / Dark.
        // Besides switching at sunrise / sunset, it also puts the theme back if Windows (or anything
        // else) changed it, e.g. right after the computer was turned on.
        // Puts Windows on the wanted theme. Safe to call from any thread.
        Plan ApplySchedule(bool force)
        {
            lock (evalLock)
            {
                Plan p = Compute(DateTime.Now);
                bool desired = S.Theme == 1 ? p.Light : S.Theme == 0;
                bool changed = !lastDesired.HasValue || lastDesired.Value != desired;
                if (force || changed || !Matches(desired))
                {
                    bool wasOk = Matches(desired);
                    lastDesired = desired;
                    // wallpaper first: setting it makes Explorer refresh the desktop and the taskbar, and if that
                    // happens right after the theme notice the taskbar can keep the old colours
                    if (S.Wall) Wallpaper.Apply(desired ? S.WallLight : S.WallDark);
                    WriteTheme(desired);
                    if (changed && !force) ReapplyLater(5000, 30000);
                    else if (changed || !wasOk) CheckTaskbarLater(2500, 7000, 15000);
                }
                return p;
            }
        }

        void Evaluate(bool force)
        {
            Plan p = ApplySchedule(force);
            curLight = ReadLight();
            if (curLight != shownLight) { shownLight = curLight; ApplyPalette(); }
            UpdateStatus(p);
        }

        void OnUi(Action a)
        {
            try { if (IsHandleCreated) BeginInvoke(a); } catch { }
        }

        // one-shot forced re-apply after the given delays (ms)
        // One-shot re-sends on background timers (they work while the window is hidden):
        // write + notify again, then make sure the taskbar really follows.
        readonly List<System.Threading.Timer> oneShots = new List<System.Threading.Timer>();
        void ReapplyLater(params int[] delays)
        {
            foreach (int ms in delays)
            {
                System.Threading.Timer t = null;
                t = new System.Threading.Timer(delegate
                {
                    try { ApplySchedule(true); Thread.Sleep(1500); CheckTaskbar(); } catch { }
                    OnUi(delegate { Evaluate(false); });
                    lock (oneShots) { oneShots.Remove(t); }
                    t.Dispose();
                }, null, ms, Timeout.Infinite);
                lock (oneShots) oneShots.Add(t);
            }
        }

        // Only looks at the taskbar (and repaints it if needed) after the given delays (ms).
        void CheckTaskbarLater(params int[] delays)
        {
            foreach (int ms in delays)
            {
                System.Threading.Timer t = null;
                t = new System.Threading.Timer(delegate
                {
                    try { CheckTaskbar(); } catch { }
                    lock (oneShots) { oneShots.Remove(t); }
                    t.Dispose();
                }, null, ms, Timeout.Infinite);
                lock (oneShots) oneShots.Add(t);
            }
        }

        // The taskbar (Explorer) sometimes misses the "theme changed" notice, e.g. right after waking from
        // sleep: the setting is dark but the taskbar stays light. If its colour does not match, flip the
        // taskbar setting to the other value and back - that always makes Explorer repaint it.
        void CheckTaskbar()
        {
            if (!S.SysTheme || !lastDesired.HasValue) return;
            bool want = lastDesired.Value;
            bool? light = TaskbarLooksLight();
            if (!light.HasValue || light.Value == want) return;
            lock (evalLock)
            {
                SetSystemTheme(!want); Notify(); Thread.Sleep(700);
                SetSystemTheme(want); Notify();
            }
        }

        static void SetSystemTheme(bool light)
        {
            try { using (var k = Registry.CurrentUser.CreateSubKey(PersonalizeKey)) k.SetValue("SystemUsesLightTheme", light ? 1 : 0, RegistryValueKind.DWord); } catch { }
        }

        static void Notify()
        {
            UIntPtr res;
            try { Native.SendMessageTimeout((IntPtr)0xffff, 0x001A, UIntPtr.Zero, "ImmersiveColorSet", 2, 3000, out res); } catch { }
        }

        // Samples a few pixels of the taskbar's empty area: light (true), dark (false) or unknown (null,
        // e.g. hidden taskbar, locked screen, not Windows).
        static bool? TaskbarLooksLight()
        {
            IntPtr dc = IntPtr.Zero;
            try
            {
                IntPtr bar = Native.FindWindow("Shell_TrayWnd", null);
                Native.RECT r;
                if (bar == IntPtr.Zero || !Native.IsWindowVisible(bar) || !Native.GetWindowRect(bar, out r)) return null;
                var screen = SystemInformation.VirtualScreen;
                int w = r.R - r.L, h = r.B - r.T;
                if (w <= 0 || h <= 0) return null;
                var pts = new List<Point>();
                foreach (double f in new[] { 0.1, 0.17, 0.24, 0.76, 0.8 })
                    pts.Add(w >= h ? new Point(r.L + (int)(w * f), r.T + Math.Min(4, h / 2)) : new Point(r.L + Math.Min(4, w / 2), r.T + (int)(h * f)));
                dc = Native.GetDC(IntPtr.Zero);
                var lum = new List<double>();
                foreach (var p in pts)
                {
                    if (!screen.Contains(p)) continue;
                    uint c = Native.GetPixel(dc, p.X, p.Y);
                    if (c == 0xFFFFFFFF) continue;
                    double R = c & 0xFF, G = (c >> 8) & 0xFF, B = (c >> 16) & 0xFF;
                    lum.Add(0.2126 * R + 0.7152 * G + 0.0722 * B);
                }
                if (lum.Count < 3) return null;
                lum.Sort();
                double med = lum[lum.Count / 2];
                if (med > 150) return true;
                if (med < 100) return false;
                return null;
            }
            catch { return null; }
            finally { if (dc != IntPtr.Zero) Native.ReleaseDC(IntPtr.Zero, dc); }
        }

        void UpdateStatus(Plan p)
        {
            lblState.Text = L.T(curLight ? "light" : "dark");
            icon.Light = curLight; icon.Invalidate();

            if (S.Theme != 1) lblNext.Text = L.T("auto_off");
            else if (p.Polar == 1) lblNext.Text = L.T("polar_day");
            else if (p.Polar == 2) lblNext.Text = L.T("polar_night");
            else if (p.HasNext)
            {
                string at = p.Next.ToString("HH:mm", CultureInfo.InvariantCulture);
                lblNext.Text = string.Format(L.T("next"), L.T(p.NextLight ? "light" : "dark"), at, L.Duration(p.Next - DateTime.Now));
            }
            else lblNext.Text = "";

            // location line
            if (detecting) { loc.Place = L.T("detecting"); loc.Coords = ""; loc.Busy = true; }
            else
            {
                loc.Busy = false;
                loc.Place = DateTime.Now < locFailUntil ? L.T("detect_fail") : (S.City.Length > 0 ? S.City : L.T(S.LocSrc == 1 ? "my_location" : "custom_loc"));
                string coords = S.Lat.ToString("0.00", CultureInfo.InvariantCulture) + ", " + S.Lon.ToString("0.00", CultureInfo.InvariantCulture);
                loc.Coords = S.City.Length > 0 ? "" : coords;
                if (tips.GetToolTip(loc) != coords + "\n" + L.T("loc_tip")) tips.SetToolTip(loc, coords + "\n" + L.T("loc_tip"));
            }
            loc.Invalidate();

            line.HasWindow = p.HasWindow; line.AllLight = p.AllLight;
            line.LightFrom = p.LightFrom; line.DarkFrom = p.DarkFrom;
            line.Invalidate();

            miLight.Checked = S.Theme == 0; miAuto.Checked = S.Theme == 1; miDark.Checked = S.Theme == 2;
            if (tray != null) tray.Text = L.T("title") + " — " + L.T(curLight ? "light" : "dark");
        }

        // ---------- Options & location ----------
        // ---- wallpaper ----
        void ShowWallUI()
        {
            bool on = S.Wall;
            wallCard.Height = P(on ? 152 : 44);
            thLight.Visible = thDark.Visible = on;
            WinH = on ? HeightWallOn : HeightWallOff;
            lnkAbout.Top = P(WinH - 28);
            if (IsHandleCreated) FitWindow(); else ClientSize = new Size(P(WinW), P(WinH));
            if (on) { thLight.SetImage(S.WallLight); thDark.SetImage(S.WallDark); }
        }

        void WallToggled()
        {
            if (loading) return;
            S.Wall = wallTg.Checked;
            if (S.Wall)
            {
                // first time: start with the wallpaper Windows shows now, for both themes
                string cur = null;
                if (S.WallLight.Length == 0 || !System.IO.File.Exists(S.WallLight) || S.WallDark.Length == 0 || !System.IO.File.Exists(S.WallDark))
                    cur = Wallpaper.Current();
                try
                {
                    if (cur != null && (S.WallLight.Length == 0 || !System.IO.File.Exists(S.WallLight))) S.WallLight = Wallpaper.Store(cur, true, S.WallLight);
                    if (cur != null && (S.WallDark.Length == 0 || !System.IO.File.Exists(S.WallDark))) S.WallDark = Wallpaper.Store(cur, false, S.WallDark);
                }
                catch { }
            }
            S.Save();
            ShowWallUI();
            if (S.Wall && lastDesired.HasValue) { Wallpaper.Apply(lastDesired.Value ? S.WallLight : S.WallDark); CheckTaskbarLater(2500, 7000); }
        }

        void PickWallpaper(bool light)
        {
            string picked;
            using (var f = new WallPickForm(light))
            {
                if (f.ShowDialog(this) != DialogResult.OK || string.IsNullOrEmpty(f.Picked)) return;
                picked = f.Picked;
            }
            try
            {
                if (light) S.WallLight = Wallpaper.Store(picked, true, S.WallLight);
                else S.WallDark = Wallpaper.Store(picked, false, S.WallDark);
            }
            catch { return; }
            S.Save();
            (light ? thLight : thDark).SetImage(light ? S.WallLight : S.WallDark);
            // the photo for the theme that is on now is shown right away
            if (S.Wall && lastDesired.HasValue && lastDesired.Value == light) { Wallpaper.Apply(light ? S.WallLight : S.WallDark); CheckTaskbarLater(2500, 7000); }
        }

        void OptionChanged(int i)
        {
            if (loading) return;
            switch (i)
            {
                case 0: S.SysTheme = optTg[0].Checked; break;
                case 1: S.AppTheme = optTg[1].Checked; break;
                case 2: S.Startup = optTg[2].Checked; SetStartup(S.Startup); break;
                case 3: S.Tray = optTg[3].Checked; tray.Visible = S.Tray; break;
            }
            S.Save();
            if (i < 2) Evaluate(true);
        }

        const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        // On only when the Run entry exists AND it is not switched off in Task Manager → Startup apps
        static bool StartupEnabled()
        {
            try
            {
                using (var rk = Registry.CurrentUser.OpenSubKey(RunKey))
                    if (rk == null || rk.GetValue(AppName) == null) return false;
                using (var ak = Registry.CurrentUser.OpenSubKey(ApprovedKey))
                {
                    var b = ak == null ? null : ak.GetValue(AppName) as byte[];
                    return b == null || b.Length == 0 || (b[0] & 1) == 0;   // 02 = enabled, 03 = disabled
                }
            }
            catch { return false; }
        }

        static void SetStartup(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (on) k.SetValue(AppName, "\"" + Application.ExecutablePath + "\" --tray");
                    else k.DeleteValue(AppName, false);
                }
                // clear a "disabled" mark left by Task Manager, so turning it on here really turns it on
                using (var ak = Registry.CurrentUser.OpenSubKey(ApprovedKey, true))
                    if (ak != null) ak.DeleteValue(AppName, false);
            }
            catch { }
        }

        // Windows location service only. If it is off, offer its settings or the city search.
        // interactive = false (e.g. autostart) never shows the question.
        void Detect() { Detect(true); }
        void Detect(bool interactive)
        {
            if (detecting) return;
            SetDetecting(true);
            ThreadPool.QueueUserWorkItem(delegate
            {
                double la, lo; string place;
                bool ok = Geo.Windows(out la, out lo, out place);
                OnUi(delegate
                {
                    if (ok) { SetDetecting(false); SaveLocation(la, lo, place, 1); return; }
                    if (!interactive || !Visible) { SetDetecting(false); DetectFailed(); return; }
                    SetDetecting(false);
                    bool find;
                    using (var f = new AskLocForm()) find = f.ShowDialog(this) == DialogResult.OK;
                    if (find) EditCoords(); else UpdateStatus(Compute(DateTime.Now));
                });
            });
        }

        void SetDetecting(bool on)
        {
            detecting = on;
            btnRefresh.Enabled = !on;
            UpdateStatus(Compute(DateTime.Now));
        }

        void SaveLocation(double la, double lo, string place, int src)
        {
            S.Lat = la; S.Lon = lo; S.City = Geo.Clean(place); S.LocSrc = src; S.LocSet = true; S.Save();
            locFailUntil = DateTime.MinValue;
            Evaluate(S.Mode == 0);
        }

        void DetectFailed()
        {
            locFailUntil = DateTime.Now.AddSeconds(8);
            UpdateStatus(Compute(DateTime.Now));
        }
    }

    // shown when Windows location is off: open its settings or search a city
    class AskLocForm : Form
    {
        static int P(double v) { return Pal.P(v); }
        public AskLocForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(P(460), P(190));
            BackColor = Pal.Bg;
            Font = new Font("Segoe UI", 9.5f);
            Text = L.T("geo_title");
            KeyPreview = true;

            var msg = new Lbl { Text = L.T("geo_off"), Wrap = true, Font = new Font("Segoe UI", 9.5f) };
            msg.SetBounds(P(20), P(18), P(420), P(104));
            Controls.Add(msg);

            var settings = new Btn { Text = L.T("geo_settings"), Font = new Font("Segoe UI", 9f) };
            settings.SetBounds(P(20), P(136), P(160), P(34));
            settings.Click += delegate { try { System.Diagnostics.Process.Start("ms-settings:privacy-location"); } catch { } };
            var cancel = new Btn { Text = L.T("cancel"), Font = new Font("Segoe UI", 9f) };
            cancel.SetBounds(P(188), P(136), P(104), P(34));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            var ip = new Btn { Primary = true, Text = L.T("find_city"), Font = new Font("Segoe UI Semibold", 9f) };
            ip.SetBounds(P(300), P(136), P(140), P(34));
            ip.Click += delegate { DialogResult = DialogResult.OK; Close(); };
            Controls.Add(settings); Controls.Add(cancel); Controls.Add(ip);
            KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); } };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            MainForm.StyleTitleBar(Handle, true);
        }
    }

    // location dialog: search a city by name, or enter coordinates by hand
    class CoordForm : Form
    {
        public double Lat, Lon;
        public string PlaceName = "";
        Field fCity, fLat, fLon;
        Btn btnFind, ok;
        ResultList list;
        bool setting, searching;
        static int P(double v) { return Pal.P(v); }

        public CoordForm(double lat, double lon, string place)
        {
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(P(420), P(392));
            BackColor = Pal.Bg;
            Font = new Font("Segoe UI", 9.5f);
            Text = L.T("geo_title");
            KeyPreview = true;

            fCity = MakeField(20, 20, 292, 38, 120);
            fCity.Prefix = L.T("city");
            btnFind = new Btn { Primary = true, Text = L.T("find"), Font = new Font("Segoe UI Semibold", 9.5f) };
            btnFind.SetBounds(P(320), P(20), P(80), P(38));
            btnFind.Click += delegate { DoSearch(); };
            Controls.Add(btnFind);

            list = new ResultList { Font = new Font("Segoe UI Semibold", 9.5f), SubFont = new Font("Segoe UI", 8.5f), RowH = P(36) };
            list.SetBounds(P(20), P(68), P(380), P(4 + 5 * 36));
            list.Message = L.T("search_hint");
            list.Picked += delegate { Pick(list.Items[list.Selected]); };
            Controls.Add(list);

            var or = new Lbl { Text = L.T("or_coords"), Sub = true, Font = new Font("Segoe UI", 8.5f) };
            or.SetBounds(P(22), P(260), P(376), P(20));
            Controls.Add(or);
            fLat = MakeField(20, 284, 184, 38, 12);
            fLon = MakeField(216, 284, 184, 38, 12);
            fLat.Prefix = L.T("lat"); fLon.Prefix = L.T("lon");
            fLat.Box.TextChanged += delegate { if (!setting) { PlaceName = ""; list.Selected = -1; list.Invalidate(); } Validate2(); };
            fLon.Box.TextChanged += delegate { if (!setting) { PlaceName = ""; list.Selected = -1; list.Invalidate(); } Validate2(); };

            var cancel = new Btn { Text = L.T("cancel"), Font = new Font("Segoe UI", 9.5f) };
            cancel.SetBounds(P(20), P(338), P(184), P(36));
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            ok = new Btn { Primary = true, Text = L.T("save"), Font = new Font("Segoe UI Semibold", 9.5f) };
            ok.SetBounds(P(216), P(338), P(184), P(36));
            ok.Click += delegate { if (Validate2()) { DialogResult = DialogResult.OK; Close(); } };
            Controls.Add(cancel); Controls.Add(ok);

            setting = true;
            fLat.Box.Text = lat.ToString("0.####", CultureInfo.InvariantCulture);
            fLon.Box.Text = lon.ToString("0.####", CultureInfo.InvariantCulture);
            setting = false;
            PlaceName = place ?? "";
            Validate2();

            KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
                else if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    if (fCity.Box.Focused) DoSearch();
                    else if (Validate2()) { DialogResult = DialogResult.OK; Close(); }
                }
            };
            Shown += delegate { fCity.Box.Focus(); };
        }

        Field MakeField(double x, double y, double w, double h, int max)
        {
            var f = new Field();
            f.SetBounds(P(x), P(y), P(w), P(h));
            f.Font = new Font("Segoe UI", 9.5f);
            f.Box.Font = new Font("Segoe UI", 10.5f);
            f.Box.MaxLength = max;
            f.Box.BackColor = Pal.Field; f.Box.ForeColor = Pal.Text;
            Controls.Add(f);
            return f;
        }

        void DoSearch()
        {
            string q = fCity.Box.Text.Trim();
            if (q.Length < 2 || searching) return;
            searching = true; btnFind.Enabled = false;
            list.Items.Clear(); list.Selected = -1; list.Message = L.T("searching"); list.Invalidate();
            string lang = L.Iso[L.Lang];
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<Place> found = null; bool failed = false;
                try { found = Geo.Search(q, lang); } catch { failed = true; }
                try { BeginInvoke((Action)delegate { ShowResults(found, failed); }); } catch { }
            });
        }

        // also used by tests
        public void ShowResults(List<Place> found, bool failed)
        {
            searching = false; btnFind.Enabled = true;
            list.Items = found ?? new List<Place>();
            if (list.Items.Count > 5) list.Items.RemoveRange(5, list.Items.Count - 5);
            list.Message = failed ? L.T("search_fail") : L.T("not_found");
            list.Selected = -1;
            list.Invalidate();
            if (list.Items.Count == 1) { list.Selected = 0; Pick(list.Items[0]); }
        }

        void Pick(Place p)
        {
            setting = true;
            fLat.Box.Text = p.Lat.ToString("0.####", CultureInfo.InvariantCulture);
            fLon.Box.Text = p.Lon.ToString("0.####", CultureInfo.InvariantCulture);
            setting = false;
            PlaceName = p.Short;
            Validate2();
        }

        static bool Parse(string s, double min, double max, out double v)
        {
            return double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= min && v <= max;
        }

        bool Validate2()
        {
            if (fLat == null || fLon == null) return false;
            bool a = Parse(fLat.Box.Text, -90, 90, out Lat);
            bool b = Parse(fLon.Box.Text, -180, 180, out Lon);
            fLat.Invalid = !a; fLon.Invalid = !b;
            fLat.Invalidate(); fLon.Invalidate();
            if (ok != null) ok.Enabled = a && b;
            return a && b;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            MainForm.StyleTitleBar(Handle, true);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); } catch { }
            bool created;
            var ev = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\AutoDarkModeX_Show", out created);
            bool hidden = Array.IndexOf(args, "--tray") >= 0;
            if (!created) { if (!hidden) ev.Set(); return; }

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate { };   // keep running quietly instead of showing an error dialog
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Pal.InitScale();
            var form = new MainForm(hidden);
            var th = new Thread(delegate ()
            {
                while (true)
                {
                    ev.WaitOne();
                    try { form.BeginInvoke((Action)form.ShowFromTray); } catch { }
                }
            });
            th.IsBackground = true;
            th.Start();
            Application.Run(form);
        }
    }
}
