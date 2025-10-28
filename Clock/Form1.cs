using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Globalization;

namespace ClockApp
{
    public partial class Form1 : Form
    {
        // Windows API
        [DllImport("user32.dll", SetLastError = true)]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;

        // リサイズ判定用
        private const int WM_NCHITTEST = 0x0084;
        private const int HTBOTTOMRIGHT = 17;
        private const int GripSize = 18;   // 右下の有効領域ピクセル

        private Timer timer;

        private enum Mode { Clock, Countdown, Target }
        private Mode currentMode = Mode.Clock;

        private CultureInfo currentCulture = new CultureInfo("ja-JP");

        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        // メニュー参照
        private ToolStripMenuItem languageJaMenuItem;
        private ToolStripMenuItem alwaysOnTopMenuItem;
        private ToolStripMenuItem enableDraggingMenuItem;
        private ToolStripMenuItem transparencyKeyMenuItem;
        private ToolStripMenuItem changeTextColorMenuItem;
        private ToolStripMenuItem changeOpacityMenuItem;
        private ToolStripMenuItem changeTitleTextMenuItem;
        private ToolStripMenuItem changeTitleFontSizeMenuItem;
        private ToolStripMenuItem changeTimeFontSizeMenuItem;
        private ToolStripMenuItem showCurrentTimeMenuItem;
        private ToolStripMenuItem setTimerDurationMenuItem;
        private ToolStripMenuItem setTimerTargetTimeMenuItem;
        private ToolStripMenuItem exitAppMenuItem;

        private bool draggingEnabled = true;
        private NotifyIcon trayIcon;
        private ContextMenuStrip contextMenu;

        private int remainingSeconds = 0;
        private DateTime targetTime;

        // リサイズとフォント拡縮用の基準値
        private bool isUserResizing = false;
        private Size baseClientSize;
        private float baseTitleFontSize;
        private float baseTimeFontSize;

        public Form1()
        {
            InitializeComponent();
            InitializeFormSettings();
            InitializeContextMenu();
            InitializeTrayIcon();
            InitializeDragEvents();

            // 基準値を記録（デザイナ初期サイズとフォント）
            baseClientSize = this.ClientSize;
            baseTitleFontSize = lblTitle.Font.Size;
            baseTimeFontSize = lblTime.Font.Size;

            // 右下ドラッグでのリサイズ監視
            this.ResizeBegin += (s, e) => { isUserResizing = true; };
            this.ResizeEnd += (s, e) => { isUserResizing = false; UpdateFontsBySize(); CenterLabels(); };

            StartClockMode();
            UpdateTitleWithDateAmPm();

            CenterLabels();
            this.SizeChanged += new EventHandler(Form1_SizeChanged);

            // 既定チェック
            languageJaMenuItem.Checked = true;
            alwaysOnTopMenuItem.Checked = true;
            enableDraggingMenuItem.Checked = true;
            transparencyKeyMenuItem.Checked = false; // 透過オフ既定

            UpdateMenuTexts();
            ApplyTransparencySetting(); // 既定適用
        }

        private void InitializeFormSettings()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
            this.Opacity = 0.75;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ResizeRedraw = true; // リサイズ時の再描画
        }

        private void ResetTimer()
        {
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= Timer_Tick_Clock;
                timer.Tick -= Timer_Tick_Countdown;
                timer.Tick -= Timer_Tick_Target;
                timer.Dispose();
            }
            timer = new Timer { Interval = 1000 };
        }

        private void StartClockMode()
        {
            currentMode = Mode.Clock;
            ResetTimer();
            timer.Tick += Timer_Tick_Clock;
            timer.Start();
            lblTime.Text = DateTime.Now.ToString("HH:mm:ss", currentCulture);
            UpdateTitleWithDateAmPm();
            CenterLabels();
        }

        private void StartCountdownMode(int seconds)
        {
            currentMode = Mode.Countdown;
            remainingSeconds = Math.Max(0, seconds);
            ResetTimer();
            timer.Tick += Timer_Tick_Countdown;
            timer.Start();
            Timer_Tick_Countdown(this, EventArgs.Empty);
        }

        private void StartTargetMode(DateTime target)
        {
            currentMode = Mode.Target;
            targetTime = target;
            ResetTimer();
            timer.Tick += Timer_Tick_Target;
            timer.Start();
            Timer_Tick_Target(this, EventArgs.Empty);
        }

        private void Timer_Tick_Clock(object sender, EventArgs e)
        {
            lblTime.Text = DateTime.Now.ToString("HH:mm:ss", currentCulture);
            CenterLabels();
        }

        private void Timer_Tick_Countdown(object sender, EventArgs e)
        {
            if (remainingSeconds > 0)
            {
                remainingSeconds--;
                lblTime.Text = TimeSpan.FromSeconds(remainingSeconds).ToString(@"hh\:mm\:ss");
            }
            else
            {
                timer.Stop();
                lblTime.Text = "00:00:00";
                MessageBox.Show(languageJaMenuItem.Checked ? "指定時刻です" : "Time is up");
            }
            CenterLabels();
        }

        private void Timer_Tick_Target(object sender, EventArgs e)
        {
            TimeSpan remaining = targetTime - DateTime.Now;
            if (remaining.TotalSeconds > 0)
            {
                lblTime.Text = remaining.ToString(@"hh\:mm\:ss");
            }
            else
            {
                timer.Stop();
                lblTime.Text = "00:00:00";
                MessageBox.Show(languageJaMenuItem.Checked ? "指定時刻です" : "Time is up");
            }
            CenterLabels();
        }

        private void InitializeContextMenu()
        {
            contextMenu = new ContextMenuStrip();

            // 並び順（指定どおり）
            languageJaMenuItem = new ToolStripMenuItem("日本語表記") { CheckOnClick = true };
            languageJaMenuItem.CheckedChanged += LanguageJaMenuItem_CheckedChanged;
            contextMenu.Items.Add(languageJaMenuItem);
            contextMenu.Items.Add(new ToolStripSeparator());

            alwaysOnTopMenuItem = new ToolStripMenuItem("常に手前に表示") { CheckOnClick = true };
            alwaysOnTopMenuItem.CheckedChanged += AlwaysOnTopMenuItem_CheckedChanged;
            contextMenu.Items.Add(alwaysOnTopMenuItem);

            enableDraggingMenuItem = new ToolStripMenuItem("ドラッグ移動を有効") { CheckOnClick = true };
            enableDraggingMenuItem.CheckedChanged += EnableDraggingMenuItem_CheckedChanged;
            contextMenu.Items.Add(enableDraggingMenuItem);

            transparencyKeyMenuItem = new ToolStripMenuItem("背景を透過（白煙色）") { CheckOnClick = true };
            transparencyKeyMenuItem.CheckedChanged += TransparencyKeyMenuItem_CheckedChanged;
            contextMenu.Items.Add(transparencyKeyMenuItem);

            changeTextColorMenuItem = new ToolStripMenuItem("文字色を変更");
            changeTextColorMenuItem.Click += ChangeTextColor_Click;
            contextMenu.Items.Add(changeTextColorMenuItem);

            changeOpacityMenuItem = new ToolStripMenuItem("透明度を数値で指定");
            changeOpacityMenuItem.Click += ChangeOpacity_Click;
            contextMenu.Items.Add(changeOpacityMenuItem);

            changeTitleTextMenuItem = new ToolStripMenuItem("タイトル文を変更");
            changeTitleTextMenuItem.Click += ChangeTitleText_Click;
            contextMenu.Items.Add(changeTitleTextMenuItem);

            changeTitleFontSizeMenuItem = new ToolStripMenuItem("タイトルの大きさフォントを変更");
            changeTitleFontSizeMenuItem.Click += ChangeTitleFontSize_Click;
            contextMenu.Items.Add(changeTitleFontSizeMenuItem);

            changeTimeFontSizeMenuItem = new ToolStripMenuItem("時計の大きさフォントを変更");
            changeTimeFontSizeMenuItem.Click += ChangeTimeFontSize_Click;
            contextMenu.Items.Add(changeTimeFontSizeMenuItem);

            showCurrentTimeMenuItem = new ToolStripMenuItem("現在の時刻を表示");
            showCurrentTimeMenuItem.Click += ShowCurrentTime_Click;
            contextMenu.Items.Add(showCurrentTimeMenuItem);

            setTimerDurationMenuItem = new ToolStripMenuItem("タイマーを設定（残り時間指定）");
            setTimerDurationMenuItem.Click += SetTimerDuration_Click;
            contextMenu.Items.Add(setTimerDurationMenuItem);

            setTimerTargetTimeMenuItem = new ToolStripMenuItem("タイマーを設定（時間を指定して逆算）");
            setTimerTargetTimeMenuItem.Click += SetTimerTargetTime_Click;
            contextMenu.Items.Add(setTimerTargetTimeMenuItem);

            exitAppMenuItem = new ToolStripMenuItem("アプリの終了");
            exitAppMenuItem.Click += ExitApp_Click;
            contextMenu.Items.Add(exitAppMenuItem);

            this.ContextMenuStrip = contextMenu;
        }

        private void InitializeTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Text = "Clock App",
                Icon = this.Icon,              // デザイナ設定のアイコン
                ContextMenuStrip = contextMenu,
                Visible = true
            };

            trayIcon.DoubleClick += (sender, args) =>
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
            };

            this.Resize += (sender, args) =>
            {
                if (this.WindowState == FormWindowState.Minimized) this.Hide();
            };
        }

        private void ShowCurrentTime_Click(object sender, EventArgs e) => StartClockMode();

        private void ChangeTitleText_Click(object sender, EventArgs e)
        {
            using (Form textInputDialog = new Form())
            {
                textInputDialog.Text = languageJaMenuItem.Checked ? "タイトル文字を変更" : "Change title text";
                textInputDialog.Size = new Size(300, 120);

                TextBox textBox = new TextBox { Location = new Point(10, 10), Width = 260, Text = lblTitle.Text };
                textInputDialog.Controls.Add(textBox);

                Button btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(10, 50) };
                Button btnCancel = new Button { Text = languageJaMenuItem.Checked ? "キャンセル" : "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(100, 50) };
                textInputDialog.Controls.Add(btnOk);
                textInputDialog.Controls.Add(btnCancel);

                if (textInputDialog.ShowDialog() == DialogResult.OK)
                {
                    lblTitle.Text = textBox.Text;
                    CenterLabels();
                }
            }
        }

        private void ChangeTitleFontSize_Click(object sender, EventArgs e)
        {
            using (FontDialog fontDialog = new FontDialog())
            {
                fontDialog.Font = lblTitle.Font;
                if (fontDialog.ShowDialog() == DialogResult.OK)
                {
                    lblTitle.Font = fontDialog.Font;
                    CenterLabels();
                }
            }
        }

        private void ChangeTimeFontSize_Click(object sender, EventArgs e)
        {
            using (FontDialog fontDialog = new FontDialog())
            {
                fontDialog.Font = lblTime.Font;
                if (fontDialog.ShowDialog() == DialogResult.OK)
                {
                    lblTime.Font = fontDialog.Font;
                    CenterLabels();
                    AdjustFormSize();
                }
            }
        }

        private void SetTimerDuration_Click(object sender, EventArgs e)
        {
            using (Form timerDialog = new Form())
            {
                timerDialog.Text = languageJaMenuItem.Checked ? "タイマーの設定（分）" : "Set timer (minutes)";
                timerDialog.Size = new Size(290, 120);

                NumericUpDown numericUpDown = new NumericUpDown
                {
                    Maximum = 1440,
                    Minimum = 1,
                    Value = 1,
                    Location = new Point(10, 10)
                };
                timerDialog.Controls.Add(numericUpDown);

                Button btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(10, 50) };
                Button btnCancel = new Button { Text = languageJaMenuItem.Checked ? "キャンセル" : "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(100, 50) };
                timerDialog.Controls.Add(btnOk);
                timerDialog.Controls.Add(btnCancel);

                if (timerDialog.ShowDialog() == DialogResult.OK)
                {
                    int duration = (int)numericUpDown.Value * 60;
                    StartCountdownMode(duration);
                }
            }
        }

        private void SetTimerTargetTime_Click(object sender, EventArgs e)
        {
            using (Form timePickerDialog = new Form())
            {
                timePickerDialog.Text = languageJaMenuItem.Checked ? "逆算時間の設定" : "Set target time";
                timePickerDialog.Size = new Size(290, 120);

                DateTimePicker dateTimePicker = new DateTimePicker
                {
                    Format = DateTimePickerFormat.Time,
                    ShowUpDown = true,
                    Location = new Point(10, 10)
                };
                timePickerDialog.Controls.Add(dateTimePicker);

                Button btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(10, 50) };
                Button btnCancel = new Button { Text = languageJaMenuItem.Checked ? "キャンセル" : "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(100, 50) };
                timePickerDialog.Controls.Add(btnOk);
                timePickerDialog.Controls.Add(btnCancel);

                if (timePickerDialog.ShowDialog() == DialogResult.OK)
                {
                    DateTime pick = dateTimePicker.Value;
                    DateTime target = DateTime.Today.AddHours(pick.Hour).AddMinutes(pick.Minute).AddSeconds(pick.Second);
                    if (target <= DateTime.Now) target = target.AddDays(1);
                    StartTargetMode(target);
                }
            }
        }

        private void ChangeOpacity_Click(object sender, EventArgs e)
        {
            using (Form opacityDialog = new Form())
            {
                opacityDialog.Text = languageJaMenuItem.Checked ? "透過設定" : "Opacity";
                opacityDialog.Size = new Size(300, 160);

                TrackBar trackBar = new TrackBar
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = (int)(this.Opacity * 100),
                    TickFrequency = 10,
                    Location = new Point(10, 10),
                    Width = 260
                };
                opacityDialog.Controls.Add(trackBar);

                Label label = new Label { Text = languageJaMenuItem.Checked ? $"不透明度: {trackBar.Value}%" : $"Opacity: {trackBar.Value}%", Location = new Point(10, 60) };
                opacityDialog.Controls.Add(label);

                trackBar.Scroll += (s, args) =>
                {
                    label.Text = languageJaMenuItem.Checked ? $"不透明度: {trackBar.Value}%" : $"Opacity: {trackBar.Value}%";
                };

                Button btnOk = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(10, 90) };
                Button btnCancel = new Button { Text = languageJaMenuItem.Checked ? "キャンセル" : "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(100, 90) };
                opacityDialog.Controls.Add(btnOk);
                opacityDialog.Controls.Add(btnCancel);

                if (opacityDialog.ShowDialog() == DialogResult.OK)
                {
                    this.Opacity = trackBar.Value / 100.0;
                }
            }
        }

        private void ChangeTextColor_Click(object sender, EventArgs e)
        {
            using (var dlg = new ColorDialog())
            {
                dlg.AllowFullOpen = true;
                dlg.FullOpen = true;
                dlg.Color = lblTime.ForeColor;

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    if (this.TransparencyKey != Color.Empty &&
                        dlg.Color.ToArgb() == this.TransparencyKey.ToArgb())
                    {
                        dlg.Color = ControlPaint.Dark(dlg.Color);
                    }
                    lblTitle.ForeColor = dlg.Color;
                    lblTime.ForeColor = dlg.Color;
                }
            }
        }

        private void TransparencyKeyMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            ApplyTransparencySetting();
        }

        private void ApplyTransparencySetting()
        {
            if (transparencyKeyMenuItem.Checked)
            {
                this.BackColor = Color.WhiteSmoke;
                this.TransparencyKey = Color.WhiteSmoke;
            }
            else
            {
                this.TransparencyKey = Color.Empty;
                this.BackColor = Color.Black;
            }
        }

        private void AlwaysOnTopMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            this.TopMost = alwaysOnTopMenuItem.Checked;
        }

        private void EnableDraggingMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            draggingEnabled = enableDraggingMenuItem.Checked;

            int currentStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            if (!draggingEnabled)
            {
                SetWindowLong(this.Handle, GWL_EXSTYLE, currentStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            }
            else
            {
                SetWindowLong(this.Handle, GWL_EXSTYLE, currentStyle & ~WS_EX_TRANSPARENT);
            }
        }

        private void ExitApp_Click(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        private void LanguageJaMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            currentCulture = languageJaMenuItem.Checked ? new CultureInfo("ja-JP") : new CultureInfo("en-US");
            UpdateMenuTexts();
            UpdateTitleWithDateAmPm();

            if (currentMode == Mode.Clock) StartClockMode();
            else CenterLabels();
        }

        private void UpdateMenuTexts()
        {
            bool ja = languageJaMenuItem.Checked;

            languageJaMenuItem.Text = ja ? "日本語表記" : "Japanese labels";
            alwaysOnTopMenuItem.Text = ja ? "常に手前へ表示" : "Always on top";
            enableDraggingMenuItem.Text = ja ? "ドラッグ移動有効" : "Enable drag move";
            transparencyKeyMenuItem.Text = ja ? "背景透過" : "Transparent BG";
            changeTextColorMenuItem.Text = ja ? "文字色変更" : "Change text color";
            changeOpacityMenuItem.Text = ja ? "透明度を数値指定" : "Set opacity";
            changeTitleTextMenuItem.Text = ja ? "タイトル文を変更" : "Change title text";
            changeTitleFontSizeMenuItem.Text = ja ? "タイトルの大きさフォントを変更" : "Change title font";
            changeTimeFontSizeMenuItem.Text = ja ? "時計の大きさフォントを変更" : "Change time font";
            showCurrentTimeMenuItem.Text = ja ? "現在の時刻を表示" : "Show current time";
            setTimerDurationMenuItem.Text = ja ? "タイマーを設定（残り時間指定）" : "Set timer (duration)";
            setTimerTargetTimeMenuItem.Text = ja ? "タイマーを設定（時間を指定して逆算）" : "Set timer (target time)";
            exitAppMenuItem.Text = ja ? "アプリの終了" : "Exit";

            this.Text = ja ? "時計" : "Clock";
            trayIcon.Text = ja ? "時計" : "Clock App";
        }

        private void UpdateTitleWithDateAmPm()
        {
            string date = DateTime.Now.ToString("MM/dd", currentCulture);
            string amPm = DateTime.Now.ToString("tt", currentCulture);
            lblTitle.Text = $"{date}/{amPm}";
            CenterLabels();
        }

        private void InitializeDragEvents()
        {
            this.lblTime.MouseDown += new MouseEventHandler(this.lblTime_MouseDown);
            this.lblTime.MouseMove += new MouseEventHandler(this.lblTime_MouseMove);
            this.lblTime.MouseUp += new MouseEventHandler(this.lblTime_MouseUp);
            this.lblTitle.MouseDown += new MouseEventHandler(this.lblTime_MouseDown);
            this.lblTitle.MouseMove += new MouseEventHandler(this.lblTime_MouseMove);
            this.lblTitle.MouseUp += new MouseEventHandler(this.lblTime_MouseUp);
        }

        private void lblTime_MouseDown(object sender, MouseEventArgs e)
        {
            if (draggingEnabled)
            {
                dragging = true;
                dragCursorPoint = Cursor.Position;
                dragFormPoint = this.Location;
            }
        }

        private void lblTime_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging && draggingEnabled)
            {
                Point diff = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(diff));
            }
        }

        private void lblTime_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }

        // 右下ドラッグでのリサイズ判定
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                // クライアント座標に変換
                Point p = PointToClient(Cursor.Position);
                if (p.X >= this.ClientSize.Width - GripSize &&
                    p.Y >= this.ClientSize.Height - GripSize)
                {
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        // フォントをウィンドウ高さに比例させる
        private void UpdateFontsBySize()
        {
            if (baseClientSize.Height <= 0) return;
            float scale = (float)this.ClientSize.Height / baseClientSize.Height;

            float newTitle = Math.Max(8f, baseTitleFontSize * scale);
            float newTime = Math.Max(8f, baseTimeFontSize * scale);

            lblTitle.Font = new Font(lblTitle.Font.FontFamily, newTitle, lblTitle.Font.Style, GraphicsUnit.Point);
            lblTime.Font = new Font(lblTime.Font.FontFamily, newTime, lblTime.Font.Style, GraphicsUnit.Point);
        }

        private void CenterLabels()
        {
            lblTitle.Left = (this.ClientSize.Width - lblTitle.Width) / 2;
            lblTime.Left = (this.ClientSize.Width - lblTime.Width) / 2;
            lblTitle.Top = (this.ClientSize.Height - lblTitle.Height - lblTime.Height) / 2;
            lblTime.Top = lblTitle.Top + lblTitle.Height;

            // 自動サイズはユーザリサイズ中は抑止
            if (!isUserResizing)
            {
                AdjustFormSize();
            }
        }

        private void AdjustFormSize()
        {
            int newWidth = Math.Max(lblTitle.Width, lblTime.Width) + 40;
            int newHeight = lblTitle.Height + lblTime.Height + 40;
            this.ClientSize = new Size(newWidth, newHeight);
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            if (isUserResizing) UpdateFontsBySize();
            CenterLabels();
        }

        private void Form1_Load(object sender, EventArgs e) { }
        private void Form1_Load_1(object sender, EventArgs e) { }
    }
}
