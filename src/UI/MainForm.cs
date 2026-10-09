using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using SmbUserSwitch.Core;
using SmbUserSwitch.Native;

namespace SmbUserSwitch.UI
{
    public class MainForm : Form
    {
        AppConfig _cfg;
        TrayIcon _tray;

        ComboBox _cmbServer;
        AccountList _list;
        CardPanel _cardServer;
        CardPanel _cardList;
        AppleButton _btnSwitch, _btnDisconnect, _btnDisconnectAll, _btnAdd, _btnEdit, _btnDelete;
        AppleButton _btnCheck;
        CheckBox _chkAutoOpen;

        Panel _statusBar;
        string _statusText = "";
        string _statusDetail = "";
        Color _statusDot = Theme.TextSecondary;

        System.Windows.Forms.Timer _refreshTimer;

        Dictionary<string, string> _sessionPasswords =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        bool _reallyExit;
        bool _busy;

        // 忙碌指示：定时器驱动状态栏重绘，让省略号循环跳动
        System.Windows.Forms.Timer _busyTick;
        int _busyDots;

        public MainForm()
        {
            _cfg = CredentialStore.Load();

            Text = "SMB 用户切换";
            Font = Theme.Body(9f);
            // 固定尺寸：布局是按像素手工算的，允许缩放反而会让锚点和 LayoutDynamic 打架
            ClientSize = new Size(780, 540);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;   // 全自绘，手工按 DPI 缩放
            BackColor = Theme.WindowBg;
            DoubleBuffered = true;

            BuildUi();

            _tray = new TrayIcon(this);

            _busyTick = new System.Windows.Forms.Timer();
            _busyTick.Interval = 350;
            _busyTick.Tick += delegate(object s, EventArgs e)
            {
                _busyDots = (_busyDots + 1) % 3;
                InvalidateStatus();
            };

            _refreshTimer = new System.Windows.Forms.Timer();
            _refreshTimer.Interval = 5000;
            _refreshTimer.Tick += delegate(object s, EventArgs e) { RefreshConnectionStatus(); };
            _refreshTimer.Start();

            LoadServersIntoCombo();
            ReloadAccounts();
            RefreshConnectionStatus();
        }

        // ---------------- UI 构建 ----------------

        void BuildUi()
        {
            int pad = 18;

            // ===== 顶部：服务器卡片 =====
            _cardServer = new CardPanel();
            _cardServer.Location = new Point(pad, pad);
            _cardServer.Size = new Size(ClientSize.Width - pad * 2, 76);
            _cardServer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_cardServer);

            Label lblServer = new Label();
            lblServer.Text = "服务器";
            lblServer.Font = Theme.Body(9f);
            lblServer.ForeColor = Theme.TextSecondary;
            lblServer.AutoSize = true;
            lblServer.BackColor = Color.Transparent;
            lblServer.Location = new Point(18, 16);
            _cardServer.Controls.Add(lblServer);

            _cmbServer = new ComboBox();
            _cmbServer.Location = new Point(18, 38);
            _cmbServer.Width = 300;
            _cmbServer.FlatStyle = FlatStyle.Flat;
            _cmbServer.Font = Theme.Body(10f);
            _cmbServer.DropDownStyle = ComboBoxStyle.DropDown;
            _cmbServer.BackColor = Theme.FillSecondary;
            _cmbServer.ForeColor = Theme.TextPrimary;
            _cmbServer.SelectedIndexChanged += delegate(object s, EventArgs e) { OnServerChanged(); };
            _cmbServer.Leave += delegate(object s, EventArgs e) { OnServerChanged(); };
            _cardServer.Controls.Add(_cmbServer);

            _btnCheck = new AppleButton();
            _btnCheck.Text = "检测连接";
            _btnCheck.Kind = AppleButtonKind.Secondary;
            _btnCheck.Location = new Point(328, 37);
            _btnCheck.Size = new Size(92, 30);
            _btnCheck.Click += delegate(object s, EventArgs e) { RefreshConnectionStatus(); ReloadAccounts(); };
            _cardServer.Controls.Add(_btnCheck);

            _chkAutoOpen = new CheckBox();
            _chkAutoOpen.Text = "切换后自动打开";
            _chkAutoOpen.Font = Theme.Body(9f);
            _chkAutoOpen.ForeColor = Theme.TextPrimary;
            _chkAutoOpen.BackColor = Color.Transparent;
            _chkAutoOpen.AutoSize = true;
            _chkAutoOpen.Location = new Point(436, 42);
            _chkAutoOpen.Checked = _cfg.AutoOpenExplorer;
            _chkAutoOpen.CheckedChanged += delegate(object s, EventArgs e)
            {
                _cfg.AutoOpenExplorer = _chkAutoOpen.Checked;
                SaveConfig();
            };
            _cardServer.Controls.Add(_chkAutoOpen);

            // ===== 中部：账号列表卡片 =====
            _cardList = new CardPanel();
            _cardList.Location = new Point(pad, _cardServer.Bottom + 14);
            _cardList.Size = new Size(ClientSize.Width - pad * 2, 0);
            _cardList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_cardList);

            Label lblAccounts = new Label();
            lblAccounts.Text = "账号";
            lblAccounts.Font = Theme.BodyBold(10f);
            lblAccounts.ForeColor = Theme.TextPrimary;
            lblAccounts.AutoSize = true;
            lblAccounts.BackColor = Color.Transparent;
            lblAccounts.Location = new Point(18, 12);
            _cardList.Controls.Add(lblAccounts);

            _list = new AccountList();
            _list.Location = new Point(8, 38);
            _list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _list.SelectionChanged += delegate(object s, EventArgs e) { UpdateButtons(); };
            _list.ItemActivated += delegate(object s, EventArgs e) { OnSwitch(null, EventArgs.Empty); };
            _cardList.Controls.Add(_list);

            // ===== 底部：按钮区 =====
            Panel actions = new Panel();
            actions.BackColor = Color.Transparent;
            actions.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            actions.Location = new Point(pad, 0);
            actions.Size = new Size(ClientSize.Width - pad * 2, 44);
            Controls.Add(actions);

            int x = 0, gap = 8, bh = 34;
            _btnSwitch = AddBtn(actions, "切换到此账号", x, ref x, 128, bh, AppleButtonKind.Primary, OnSwitch); x += gap;
            _btnDisconnect = AddBtn(actions, "断开当前", x, ref x, 92, bh, AppleButtonKind.Secondary, OnDisconnect); x += gap;
            _btnDisconnectAll = AddBtn(actions, "全部断开", x, ref x, 92, bh, AppleButtonKind.Secondary, OnDisconnectAll); x += gap + 12;
            _btnAdd = AddBtn(actions, "新增", x, ref x, 76, bh, AppleButtonKind.Secondary, OnAdd); x += gap;
            _btnEdit = AddBtn(actions, "编辑", x, ref x, 76, bh, AppleButtonKind.Secondary, OnEdit); x += gap;
            _btnDelete = AddBtn(actions, "删除", x, ref x, 76, bh, AppleButtonKind.Danger, OnDelete);

            _actions = actions;

            // ===== 状态栏 =====
            _statusBar = new Panel();
            _statusBar.BackColor = Color.Transparent;
            _statusBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _statusBar.Location = new Point(pad, 0);
            _statusBar.Size = new Size(ClientSize.Width - pad * 2, 26);
            _statusBar.Paint += OnStatusPaint;
            Controls.Add(_statusBar);

            ApplyDpiLayout();
        }

        Panel _actions;

        AppleButton AddBtn(Control parent, string text, int x, ref int cursor,
                           int w, int h, AppleButtonKind kind, EventHandler onClick)
        {
            AppleButton b = new AppleButton();
            b.Text = text;
            b.Kind = kind;
            b.Location = new Point(x, 0);
            b.Size = new Size(w, h);
            b.Click += onClick;
            parent.Controls.Add(b);
            cursor = x + w;
            return b;
        }

        /// <summary>
        /// 按当前 DPI 排一次版。窗口尺寸固定，所以只需在构造和 DPI 变化时调用，
        /// 不必挂在 Resize 上（那样反而会与锚点重复计算）。
        /// </summary>
        void LayoutDynamic()
        {
            int pad = 18;
            int bottomPad = 14;
            int statusH = 26;
            int actionsH = 44;

            _statusBar.Location = new Point(pad, ClientSize.Height - bottomPad - statusH);
            _statusBar.Size = new Size(ClientSize.Width - pad * 2, statusH);

            _actions.Location = new Point(pad, _statusBar.Top - 8 - actionsH);
            _actions.Size = new Size(ClientSize.Width - pad * 2, actionsH);

            foreach (Control c in _actions.Controls)
                c.Top = _actions.Height - c.Height;

            _cardList.Size = new Size(
                ClientSize.Width - pad * 2,
                Math.Max(60, _actions.Top - _cardList.Top - 14));

            _list.Size = new Size(
                _cardList.Width - 16,
                Math.Max(30, _cardList.Height - 38 - 12));
        }

        /// <summary>固定窗口尺寸，但按系统 DPI 缩放，避免高分屏下窗口过小。</summary>
        void ApplyDpiLayout()
        {
            int w = Theme.Scale(this, 780);
            int h = Theme.Scale(this, 540);
            // 受限于屏幕工作区，别让窗口超出可视范围
            Rectangle wa = Screen.FromControl(this).WorkingArea;
            if (w > wa.Width) w = wa.Width;
            if (h > wa.Height) h = wa.Height;
            ClientSize = new Size(w, h);

            _cardServer.Size = new Size(ClientSize.Width - 36, Theme.Scale(this, 76));
            _cmbServer.Width = Theme.Scale(this, 300);
            _btnCheck.Location = new Point(Theme.Scale(this, 328), Theme.Scale(this, 37));
            _chkAutoOpen.Location = new Point(Theme.Scale(this, 436), Theme.Scale(this, 42));

            _cardList.Location = new Point(18, _cardServer.Bottom + 14);
            LayoutDynamic();
        }

        // ---------------- 状态栏绘制 ----------------

        void InvalidateStatus()
        {
            if (_statusBar != null && !_statusBar.IsDisposed) _statusBar.Invalidate();
        }

        void OnStatusPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int x = 0;
            int cy = _statusBar.Height / 2;

            // 状态圆点
            using (SolidBrush b = new SolidBrush(_statusDot))
                g.FillEllipse(b, x, cy - 4, 8, 8);
            x += 16;

            using (Font f = Theme.Body(9f))
            using (SolidBrush b = new SolidBrush(Theme.TextPrimary))
            using (StringFormat sf = new StringFormat())
            {
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                sf.Trimming = StringTrimming.EllipsisCharacter;

                string text = _statusText;
                if (_busy)
                {
                    // 省略号按时间循环 1→2→3，给出"正在进行"的动感
                    int dots = (int)((Environment.TickCount / 350) % 3) + 1;
                    text = "处理中" + new string('.', dots);
                }

                SizeF sz = g.MeasureString(text, f);
                g.DrawString(text, f, b, new RectangleF(x, 0, sz.Width + 4, _statusBar.Height), sf);
                x += (int)sz.Width + 12;

                if (!string.IsNullOrEmpty(_statusDetail) && !_busy)
                {
                    using (SolidBrush db = new SolidBrush(Theme.TextSecondary))
                    {
                        SizeF ds = g.MeasureString(_statusDetail, f);
                        g.DrawString(_statusDetail, f, db,
                            new RectangleF(x, 0, Math.Max(0, _statusBar.Width - x), _statusBar.Height), sf);
                    }
                }
            }
        }

        // ---------------- 服务器与账号 ----------------

        void LoadServersIntoCombo()
        {
            _cmbServer.Items.Clear();
            foreach (ServerProfile sp in _cfg.Servers) _cmbServer.Items.Add(sp.Address);

            if (_cmbServer.Items.Count == 0)
                _cmbServer.Text = @"\\192.168.11.13";
            else
            {
                string pick = _cfg.LastServer;
                if (string.IsNullOrEmpty(pick) || !_cmbServer.Items.Contains(pick))
                    pick = _cmbServer.Items[0].ToString();
                _cmbServer.SelectedItem = pick;
            }
        }

        string CurrentServer
        {
            get { return AppConfig.NormalizeServer(_cmbServer.Text); }
        }

        void OnServerChanged()
        {
            string addr = CurrentServer;
            if (string.IsNullOrEmpty(addr) || addr == @"\\") return;
            if (!_cmbServer.Items.Contains(addr)) _cmbServer.Items.Add(addr);
            _cfg.LastServer = addr;
            ReloadAccounts();
            RefreshConnectionStatus();
        }

        void ReloadAccounts()
        {
            string addr = CurrentServer;
            ServerProfile sp = string.IsNullOrEmpty(addr) ? null : _cfg.FindServer(addr);

            List<AccountList.Row> rows = new List<AccountList.Row>();
            string currentUser = string.IsNullOrEmpty(addr) ? "" : SmbSessionManager.CurrentUser(addr);

            if (sp != null)
            {
                foreach (AccountProfile a in sp.Accounts)
                {
                    AccountList.Row r = new AccountList.Row();
                    r.Account = a;
                    r.Name = a.Name;
                    r.Login = a.UserName;
                    r.Drive = string.IsNullOrEmpty(a.DriveLetter) ? "—" : a.DriveLetter;
                    r.Password = a.HasSavedPassword ? "已保存"
                        : (_sessionPasswords.ContainsKey(Key(addr, a.Name)) ? "本次" : "无");
                    r.Note = a.Note ?? "";
                    r.Server = addr;
                    r.IsCurrent = !string.IsNullOrEmpty(currentUser) &&
                        (currentUser.EndsWith("\\" + a.UserName, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(currentUser, a.UserName, StringComparison.OrdinalIgnoreCase));
                    rows.Add(r);
                }
            }

            _list.SetRows(rows);
            UpdateButtons();
            _tray.UpdateMenu(_cfg, addr, currentUser);
        }

        void UpdateButtons()
        {
            bool has = _list.Selected != null;
            _btnEdit.Enabled = has && !_busy;
            _btnDelete.Enabled = has && !_busy;
            _btnSwitch.Enabled = has && !_busy;
            _btnDisconnect.Enabled = !_busy;
            _btnDisconnectAll.Enabled = !_busy;
            _btnAdd.Enabled = !_busy;
            _btnCheck.Enabled = !_busy;
        }

        AccountProfile SelectedAccount
        {
            get { return _list.Selected == null ? null : _list.Selected.Account; }
        }

        static string Key(string server, string account)
        {
            return (server ?? "") + "|" + (account ?? "");
        }

        // ---------------- 连接状态 ----------------

        void RefreshConnectionStatus()
        {
            string addr = CurrentServer;
            if (string.IsNullOrEmpty(addr) || addr == @"\\") return;

            List<ConnectionInfo> conns = SmbSessionManager.ListConnections(addr);

            if (conns.Count == 0)
            {
                SetStatus("未连接", addr, Theme.TextSecondary);
                return;
            }

            string user = SmbSessionManager.CurrentUser(addr);
            List<string> drives = new List<string>();
            foreach (ConnectionInfo c in conns)
                if (c.HasDriveLetter) drives.Add(c.LocalName);

            string main = "已连接" + (string.IsNullOrEmpty(user) ? "" : " · " + user);
            string detail = drives.Count > 0
                ? string.Join("、", drives.ToArray())
                : "仅 IPC$ 会话";

            SetStatus(main, detail, Theme.Success);
            _tray.UpdateMenu(_cfg, addr, user);
        }

        void SetStatus(string text, string detail, Color dot)
        {
            _statusText = text;
            _statusDetail = detail;
            _statusDot = dot;
            InvalidateStatus();
        }

        // ---------------- 忙碌指示 ----------------

        void SetBusy(bool busy)
        {
            _busy = busy;
            if (busy)
            {
                // 忙碌期间用定时器持续重绘状态栏，让省略号动起来；
                // 不忙时立刻停表，不空转占 CPU。
                _busyDots = 0;
                _busyTick.Start();
            }
            else
            {
                _busyTick.Stop();
                _busyDots = 0;
            }
            UpdateButtons();
            InvalidateStatus();
        }

        // ---------------- 操作 ----------------

        void OnSwitch(object sender, EventArgs e)
        {
            AccountProfile a = SelectedAccount;
            if (a == null) return;
            SwitchToAccount(a);
        }

        public void SwitchToAccount(AccountProfile acct)
        {
            if (_busy) return;
            string addr = CurrentServer;
            if (string.IsNullOrEmpty(addr) || addr == @"\\")
            {
                Warn("请先填写服务器地址。");
                return;
            }

            string password = null;
            if (!acct.HasSavedPassword)
            {
                string k = Key(addr, acct.Name);
                if (!_sessionPasswords.ContainsKey(k))
                {
                    Warn("账号「" + acct.Name + "」没有保存密码。\n请先双击编辑该账号并输入密码。");
                    return;
                }
                password = _sessionPasswords[k];
            }

            DoSwitch(acct, addr, password, false);
        }

        void DoSwitch(AccountProfile acct, string addr, string password, bool force)
        {
            SetBusy(true);
            Cursor = Cursors.WaitCursor;
            try
            {
                SwitchResult r = SmbSessionManager.SwitchTo(acct, addr, force, password);

                if (!r.Success && r.NeedsForceConfirm)
                {
                    Cursor = Cursors.Default;
                    SetBusy(false);
                    if (ConfirmForce(r, "连接被占用", "强制断开并继续切换")) DoSwitch(acct, addr, password, true);
                    return;
                }

                if (r.Success)
                {
                    ServerProfile sp = _cfg.GetOrCreateServer(addr);
                    sp.LastAccount = acct.Name;
                    _cfg.LastServer = addr;
                    SaveConfig();

                    SetStatus("切换成功 · " + acct.UserName,
                        string.IsNullOrEmpty(acct.DriveLetter) ? acct.RemotePath : acct.DriveLetter,
                        Theme.Success);
                    _tray.ShowBalloon("切换成功", r.Message, false);

                    if (_cfg.AutoOpenExplorer)
                    {
                        string target = string.IsNullOrEmpty(acct.DriveLetter)
                            ? acct.RemotePath : acct.DriveLetter + "\\";
                        TryOpen(target);
                    }
                    ReloadAccounts();
                    RefreshConnectionStatus();
                }
                else
                {
                    SetStatus("切换失败", DescribeShort(r.ErrorCode), Theme.Danger);
                    MessageBox.Show(this, r.Message, "切换失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    RefreshConnectionStatus();
                }
            }
            finally
            {
                Cursor = Cursors.Default;
                SetBusy(false);
            }
        }

        static string DescribeShort(int code)
        {
            switch (code)
            {
                case 1219: return "凭据冲突";
                case 1326: case 86: return "认证失败";
                case 53: case 67: return "路径不可达";
                case 85: return "盘符被占用";
                case 2401: return "文件被占用";
                default: return code == 0 ? "" : "错误 " + code;
            }
        }

        void OnDisconnect(object sender, EventArgs e)
        {
            if (_busy) return;
            string addr = CurrentServer;
            if (string.IsNullOrEmpty(addr) || addr == @"\\") return;
            DoDisconnect(addr, false);
        }

        /// <summary>供托盘菜单调用。</summary>
        public void DisconnectCurrent()
        {
            OnDisconnect(null, EventArgs.Empty);
        }

        /// <summary>供托盘菜单调用：全部断开（仍需用户确认）。</summary>
        public void DisconnectEverything()
        {
            OnDisconnectAll(null, EventArgs.Empty);
        }

        void DoDisconnect(string addr, bool force)
        {
            SetBusy(true);
            try
            {
                SwitchResult r = SmbSessionManager.Disconnect(addr, force, true);

                if (!r.Success && r.NeedsForceConfirm)
                {
                    SetBusy(false);
                    if (ConfirmForce(r, "连接被占用", "强制断开")) DoDisconnect(addr, true);
                    return;
                }

                if (r.Success)
                {
                    SetStatus("已断开", addr, Theme.TextSecondary);
                }
                else
                {
                    SetStatus("断开失败", DescribeShort(r.ErrorCode), Theme.Danger);
                    MessageBox.Show(this, r.Message, "断开失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                ReloadAccounts();
                RefreshConnectionStatus();
            }
            finally
            {
                SetBusy(false);
            }
        }

        // ---- 全部断开 ----

        void OnDisconnectAll(object sender, EventArgs e)
        {
            if (_busy) return;

            List<ConnectionInfo> all = ConnectionEnumerator.Enumerate();
            if (all.Count == 0)
            {
                SetStatus("当前没有任何网络连接", "", Theme.TextSecondary);
                return;
            }

            string detail = "";
            foreach (ConnectionInfo c in all)
                detail += "\n    " + (c.HasDriveLetter ? c.LocalName + "  →  " : "") + c.RemoteName;

            if (MessageBox.Show(this,
                    "将断开本机全部 " + all.Count + " 个网络连接（不限于当前服务器）：" + detail +
                    "\n\n正在使用这些共享的程序可能会报错。确定继续吗？",
                    "全部断开", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            DoDisconnectAll(false);
        }

        void DoDisconnectAll(bool force)
        {
            SetBusy(true);
            try
            {
                SwitchResult r = SmbSessionManager.DisconnectAll(force);

                if (!r.Success && r.NeedsForceConfirm)
                {
                    SetBusy(false);
                    if (ConfirmForce(r, "连接被占用", "强制断开全部")) DoDisconnectAll(true);
                    return;
                }

                if (r.Success)
                {
                    SetStatus("已断开全部连接", r.Message, Theme.TextSecondary);
                }
                else
                {
                    SetStatus("全部断开失败", DescribeShort(r.ErrorCode), Theme.Danger);
                    MessageBox.Show(this, r.Message, "全部断开失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                ReloadAccounts();
                RefreshConnectionStatus();
            }
            finally
            {
                SetBusy(false);
            }
        }

        bool ConfirmForce(SwitchResult r, string title, string action)
        {
            string detail = "";
            foreach (ConnectionInfo c in r.Blocking)
                detail += "\n    " + c.RemoteName + (c.HasDriveLetter ? "  (" + c.LocalName + ")" : "");

            return MessageBox.Show(this,
                r.Message + detail +
                "\n\n强制断开可能导致这些程序报错或丢失未保存的数据。" +
                "\n建议先关闭相关窗口再试。\n\n是否" + action + "？",
                title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        void OnAdd(object sender, EventArgs e)
        {
            string addr = CurrentServer;
            if (string.IsNullOrEmpty(addr) || addr == @"\\")
            {
                Warn("请先填写服务器地址，例如 \\\\192.168.11.13");
                return;
            }

            using (AccountEditForm f = new AccountEditForm(addr, null))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;

                ServerProfile sp = _cfg.GetOrCreateServer(addr);
                if (sp.Find(f.Result.Name) != null)
                {
                    Warn("已存在同名账号：" + f.Result.Name);
                    return;
                }
                sp.Accounts.Add(f.Result);
                if (!string.IsNullOrEmpty(f.SessionOnlyPassword))
                    _sessionPasswords[Key(addr, f.Result.Name)] = f.SessionOnlyPassword;

                _cfg.LastServer = addr;
                SaveConfig();
                ReloadAccounts();
            }
        }

        void OnEdit(object sender, EventArgs e)
        {
            AccountProfile a = SelectedAccount;
            if (a == null) return;
            string addr = CurrentServer;

            using (AccountEditForm f = new AccountEditForm(addr, a))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;

                ServerProfile sp = _cfg.GetOrCreateServer(addr);
                int idx = sp.Accounts.IndexOf(a);
                if (idx < 0) return;

                AccountProfile clash = sp.Find(f.Result.Name);
                if (clash != null && clash != a)
                {
                    Warn("已存在同名账号：" + f.Result.Name);
                    return;
                }

                // 改名时清掉旧名字下的临时密码，否则它会一直留在内存里，
                // 而且日后新建同名账号会莫名其妙继承这个旧密码。
                if (!string.Equals(a.Name, f.Result.Name, StringComparison.OrdinalIgnoreCase))
                    _sessionPasswords.Remove(Key(addr, a.Name));

                sp.Accounts[idx] = f.Result;

                string k = Key(addr, f.Result.Name);
                if (!string.IsNullOrEmpty(f.SessionOnlyPassword))
                    _sessionPasswords[k] = f.SessionOnlyPassword;
                else
                    _sessionPasswords.Remove(k);

                SaveConfig();
                ReloadAccounts();
            }
        }

        void OnDelete(object sender, EventArgs e)
        {
            AccountProfile a = SelectedAccount;
            if (a == null) return;

            if (MessageBox.Show(this, "确定要删除账号「" + a.Name + "」吗？",
                    "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                != DialogResult.Yes) return;

            string addr = CurrentServer;
            ServerProfile sp = _cfg.FindServer(addr);
            if (sp != null)
            {
                sp.Accounts.Remove(a);
                if (sp.LastAccount == a.Name) sp.LastAccount = "";
                _sessionPasswords.Remove(Key(addr, a.Name));
                SaveConfig();
                ReloadAccounts();
            }
        }

        void TryOpen(string path)
        {
            try { Process.Start("explorer.exe", "\"" + path + "\""); }
            catch (Exception) { }
        }

        void SaveConfig()
        {
            try { CredentialStore.Save(_cfg); }
            catch (Exception ex) { Warn("保存配置失败：" + ex.Message); }
        }

        void Warn(string msg)
        {
            MessageBox.Show(this, msg, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // ---------------- 托盘交互 ----------------

        public void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        public void ExitApp()
        {
            _reallyExit = true;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_reallyExit && _cfg.MinimizeToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                _tray.ShowBalloon("仍在后台运行", "程序已最小化到托盘，右键托盘图标可快速切换账号。", false);
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_refreshTimer != null) _refreshTimer.Dispose();
                if (_busyTick != null) _busyTick.Dispose();
                if (_tray != null) _tray.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
