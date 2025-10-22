using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace ClockApp
{
    public partial class Form1 : Form
    {
        // Windows API の関数宣言
        [DllImport("user32.dll", SetLastError = true)]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;

        private Timer timer;
        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;
        private ToolStripMenuItem alwaysOnTopMenuItem;
        private ToolStripMenuItem transparencyKeyMenuItem;
        private ToolStripMenuItem enableDraggingMenuItem;
        private bool draggingEnabled = true;
        private NotifyIcon trayIcon;
        private ContextMenuStrip contextMenu;

        public Form1()
        {
            InitializeComponent();
            InitializeFormSettings();
            InitializeClock();
            InitializeContextMenu();
            InitializeDragEvents();
            InitializeTrayIcon();

            // タイトルに起動時の日付をセット
            SetInitialDate();

            CenterLabels();
            this.SizeChanged += new EventHandler(Form1_SizeChanged);

            // Set the initial states of the menu items
            alwaysOnTopMenuItem.Checked = true;
            transparencyKeyMenuItem.Checked = true;
            enableDraggingMenuItem.Checked = true;
        }

        private void SetInitialDate()
        {
            // 現在の日付と午前・午後を取得してフォーマット
            string date = DateTime.Now.ToString("MM/dd");
            string amPm = DateTime.Now.ToString("tt", new System.Globalization.CultureInfo("ja-JP"));
            lblTitle.Text = $"{date}/{amPm}";
        }

        private void InitializeFormSettings()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Black;
            this.Opacity = 0.75;
            this.StartPosition = FormStartPosition.CenterScreen;
        }

        private void InitializeClock()
        {
            timer = new Timer();
            timer.Interval = 1000;
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            lblTime.Text = DateTime.Now.ToString("HH:mm:ss");
            CenterLabels();
        }

        private void InitializeContextMenu()
        {
            contextMenu = new ContextMenuStrip();

            alwaysOnTopMenuItem = new ToolStripMenuItem("常に手前に表示");
            alwaysOnTopMenuItem.CheckOnClick = true;
            alwaysOnTopMenuItem.CheckedChanged += AlwaysOnTopMenuItem_CheckedChanged;
            contextMenu.Items.Add(alwaysOnTopMenuItem);

            transparencyKeyMenuItem = new ToolStripMenuItem("黒背景を表示");
            transparencyKeyMenuItem.CheckOnClick = true;
            transparencyKeyMenuItem.CheckedChanged += TransparencyKeyMenuItem_CheckedChanged;
            contextMenu.Items.Add(transparencyKeyMenuItem);

            enableDraggingMenuItem = new ToolStripMenuItem("ドラッグ移動を有効");
            enableDraggingMenuItem.CheckOnClick = true;
            enableDraggingMenuItem.CheckedChanged += EnableDraggingMenuItem_CheckedChanged;
            contextMenu.Items.Add(enableDraggingMenuItem);

            ToolStripMenuItem changeOpacity = new ToolStripMenuItem("透明度を数値で指定");
            changeOpacity.Click += ChangeOpacity_Click;
            contextMenu.Items.Add(changeOpacity);

            ToolStripMenuItem changeTitleText = new ToolStripMenuItem("タイトル文を変更");
            changeTitleText.Click += ChangeTitleText_Click;
            contextMenu.Items.Add(changeTitleText);

            ToolStripMenuItem changeTitleFontSize = new ToolStripMenuItem("タイトルの大きさフォントを変更");
            changeTitleFontSize.Click += ChangeTitleFontSize_Click;
            contextMenu.Items.Add(changeTitleFontSize);

            ToolStripMenuItem changeTimeFontSize = new ToolStripMenuItem("時計の大きさフォントを変更");
            changeTimeFontSize.Click += ChangeTimeFontSize_Click;
            contextMenu.Items.Add(changeTimeFontSize);

            ToolStripMenuItem showCurrentTime = new ToolStripMenuItem("現在の時刻を表示");
            showCurrentTime.Click += ShowCurrentTime_Click;
            contextMenu.Items.Add(showCurrentTime);

            ToolStripMenuItem setTimerDuration = new ToolStripMenuItem("タイマーを設定（残り時間指定）");
            setTimerDuration.Click += SetTimerDuration_Click;
            contextMenu.Items.Add(setTimerDuration);

            ToolStripMenuItem setTimerTargetTime = new ToolStripMenuItem("タイマーを設定（時間を指定して逆算）");
            setTimerTargetTime.Click += SetTimerTargetTime_Click;
            contextMenu.Items.Add(setTimerTargetTime);

            ToolStripMenuItem exitApp = new ToolStripMenuItem("アプリの終了");
            exitApp.Click += ExitApp_Click;
            contextMenu.Items.Add(exitApp);

            this.ContextMenuStrip = contextMenu;
        }

        private void InitializeTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Text = "Clock App",
                Icon = this.Icon,
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
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.Hide();
                }
            };
        }

        private void ShowCurrentTime_Click(object sender, EventArgs e)
        {
            timer.Tick -= Timer_Tick;
            timer.Tick += Timer_Tick;
            Timer_Tick(sender, e);
        }

        private void ChangeTitleText_Click(object sender, EventArgs e)
        {
            using (Form textInputDialog = new Form())
            {
                textInputDialog.Text = "タイトル文字を変更";
                textInputDialog.Size = new Size(300, 120);

                TextBox textBox = new TextBox();
                textBox.Location = new Point(10, 10);
                textBox.Width = 260;
                textBox.Text = lblTitle.Text;
                textInputDialog.Controls.Add(textBox);

                Button btnOk = new Button();
                btnOk.Text = "OK";
                btnOk.DialogResult = DialogResult.OK;
                btnOk.Location = new Point(10, 50);
                textInputDialog.Controls.Add(btnOk);

                Button btnCancel = new Button();
                btnCancel.Text = "キャンセル";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Location = new Point(100, 50);
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
                timerDialog.Text = "タイマーの設定（分）";
                timerDialog.Size = new Size(290, 120);
                NumericUpDown numericUpDown = new NumericUpDown();
                numericUpDown.Maximum = 1440;
                numericUpDown.Minimum = 1;
                numericUpDown.Value = 1;
                numericUpDown.Location = new Point(10, 10);
                timerDialog.Controls.Add(numericUpDown);

                Button btnOk = new Button();
                btnOk.Text = "OK";
                btnOk.DialogResult = DialogResult.OK;
                btnOk.Location = new Point(10, 50);
                timerDialog.Controls.Add(btnOk);

                Button btnCancel = new Button();
                btnCancel.Text = "キャンセル";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Location = new Point(100, 50);
                timerDialog.Controls.Add(btnCancel);

                if (timerDialog.ShowDialog() == DialogResult.OK)
                {
                    int duration = (int)numericUpDown.Value * 60;
                    timer.Stop();
                    timer = new Timer();
                    timer.Interval = 1000;
                    timer.Tick += (s, args) =>
                    {
                        if (duration > 0)
                        {
                            duration--;
                            lblTime.Text = TimeSpan.FromSeconds(duration).ToString(@"hh\:mm\:ss");
                        }
                        else
                        {
                            timer.Stop();
                            MessageBox.Show("指定時刻です");
                        }
                        CenterLabels();
                    };
                    timer.Start();
                }
            }
        }

        private void SetTimerTargetTime_Click(object sender, EventArgs e)
        {
            using (Form timePickerDialog = new Form())
            {
                timePickerDialog.Text = "逆算時間の設定";
                timePickerDialog.Size = new Size(290, 120);
                DateTimePicker dateTimePicker = new DateTimePicker();
                dateTimePicker.Format = DateTimePickerFormat.Time;
                dateTimePicker.ShowUpDown = true;
                dateTimePicker.Location = new Point(10, 10);
                timePickerDialog.Controls.Add(dateTimePicker);

                Button btnOk = new Button();
                btnOk.Text = "OK";
                btnOk.DialogResult = DialogResult.OK;
                btnOk.Location = new Point(10, 50);
                timePickerDialog.Controls.Add(btnOk);

                Button btnCancel = new Button();
                btnCancel.Text = "キャンセル";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Location = new Point(100, 50);
                timePickerDialog.Controls.Add(btnCancel);

                if (timePickerDialog.ShowDialog() == DialogResult.OK)
                {
                    DateTime targetTime = dateTimePicker.Value;
                    timer.Stop();
                    timer = new Timer();
                    timer.Interval = 1000;
                    timer.Tick += (s, args) =>
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
                            MessageBox.Show("指定時刻です");
                        }
                        CenterLabels();
                    };
                    timer.Start();
                }
            }
        }

        private void ChangeOpacity_Click(object sender, EventArgs e)
        {
            using (Form opacityDialog = new Form())
            {
                opacityDialog.Text = "透過設定";
                opacityDialog.Size = new Size(300, 160);
                TrackBar trackBar = new TrackBar();
                trackBar.Minimum = 0;
                trackBar.Maximum = 100;
                trackBar.Value = (int)(this.Opacity * 100);
                trackBar.TickFrequency = 10;
                trackBar.Location = new Point(10, 10);
                trackBar.Width = 260;
                opacityDialog.Controls.Add(trackBar);

                Label label = new Label();
                label.Text = $"Opacity: {trackBar.Value}%";
                label.Location = new Point(10, 60);
                opacityDialog.Controls.Add(label);

                trackBar.Scroll += (s, args) =>
                {
                    label.Text = $"Opacity: {trackBar.Value}%";
                };

                Button btnOk = new Button();
                btnOk.Text = "OK";
                btnOk.DialogResult = DialogResult.OK;
                btnOk.Location = new Point(10, 90);
                opacityDialog.Controls.Add(btnOk);

                Button btnCancel = new Button();
                btnCancel.Text = "キャンセル";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Location = new Point(100, 90);
                opacityDialog.Controls.Add(btnCancel);

                if (opacityDialog.ShowDialog() == DialogResult.OK)
                {
                    this.Opacity = trackBar.Value / 100.0;
                }
            }
        }

        private void TransparencyKeyMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            this.TransparencyKey = transparencyKeyMenuItem.Checked ? Color.WhiteSmoke : Color.Black;
        }

        private void AlwaysOnTopMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            this.TopMost = alwaysOnTopMenuItem.Checked;
        }

        private void EnableDraggingMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            draggingEnabled = enableDraggingMenuItem.Checked;

            if (!draggingEnabled)
            {
                // クリックスルーを有効にする
                int currentStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
                SetWindowLong(this.Handle, GWL_EXSTYLE, currentStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            }
            else
            {
                // クリックスルーを無効にする
                int currentStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
                SetWindowLong(this.Handle, GWL_EXSTYLE, currentStyle & ~WS_EX_TRANSPARENT);
            }
        }

        private void ExitApp_Click(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
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

        private void CenterLabels()
        {
            lblTitle.Left = (this.ClientSize.Width - lblTitle.Width) / 2;
            lblTime.Left = (this.ClientSize.Width - lblTime.Width) / 2;
            lblTitle.Top = (this.ClientSize.Height - lblTitle.Height - lblTime.Height) / 2;
            lblTime.Top = lblTitle.Top + lblTitle.Height;
            AdjustFormSize();
        }

        private void AdjustFormSize()
        {
            int newWidth = Math.Max(lblTitle.Width, lblTime.Width) + 40;
            int newHeight = lblTitle.Height + lblTime.Height + 40;
            this.ClientSize = new Size(newWidth, newHeight);
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            CenterLabels();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
