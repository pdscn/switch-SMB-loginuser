using System;
using System.Drawing;
using System.Windows.Forms;
using SmbUserSwitch.Core;

namespace SmbUserSwitch.UI
{
    /// <summary>
    /// 账号编辑对话框。字段文案与主界面列表表头保持一致：
    /// 账号 / 登录名 / 盘符 / 密码 / 备注。
    /// </summary>
    public class AccountEditForm : Form
    {
        AppleTextBox _txtName, _txtUser, _txtPassword, _txtRemote, _txtNote;
        ComboBox _cmbDrive;
        CheckBox _chkSavePassword;
        AppleButton _btnOk, _btnCancel;

        public AccountProfile Result;
        string _sessionOnlyPassword;

        public AccountEditForm(string serverAddress, AccountProfile existing)
        {
            Result = existing == null ? new AccountProfile() : existing.Clone();

            Text = existing == null ? "新增账号" : "编辑账号";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            // 高度 = 上边距 + 卡片1(288) + 间距(12) + 卡片2(176) + 间距(16) + 按钮(34) + 下边距(20)
            ClientSize = new Size(470, 566);
            Font = Theme.Body(9f);
            BackColor = Theme.WindowBg;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;

            BuildUi(serverAddress, existing);

            AcceptButton = null;   // 自绘按钮，不用系统默认按钮机制
            CancelButton = null;
            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
                else if (e.KeyCode == Keys.Enter && !(ActiveControl is TextBox)) { OnOk(null, EventArgs.Empty); }
            };
        }

        void BuildUi(string serverAddress, AccountProfile existing)
        {
            int pad = 20;
            int cardW = ClientSize.Width - pad * 2;
            int y = pad;

            // ===== 卡片：账号信息 =====
            CardPanel card = new CardPanel();
            card.Location = new Point(pad, y);
            card.Size = new Size(cardW, 288);
            Controls.Add(card);

            int innerX = 20;
            int fieldW = cardW - innerX * 2;
            int cy = 18;
            int labelH = 18;
            int fieldH = 34;
            int gap = 14;

            // 账号（与列表表头「账号」一致）
            AddLabel(card, "账号", innerX, cy);
            cy += labelH + 4;
            _txtName = AddField(card, innerX, cy, fieldW);
            _txtName.Text = Result.Name ?? "";
            _txtName.Placeholder = "显示在列表里的名字，如 admin";
            cy += fieldH + gap;

            // 登录名
            AddLabel(card, "登录名", innerX, cy);
            cy += labelH + 4;
            _txtUser = AddField(card, innerX, cy, fieldW);
            _txtUser.Text = Result.UserName ?? "";
            _txtUser.Placeholder = @"服务器上的用户名，如 administrator";
            cy += fieldH + gap;

            // 密码
            AddLabel(card, "密码", innerX, cy);
            cy += labelH + 4;
            _txtPassword = AddField(card, innerX, cy, fieldW);
            _txtPassword.UseSystemPasswordChar = true;
            _txtPassword.Placeholder = Result.HasSavedPassword
                ? "留空表示沿用已保存的密码"
                : "请输入密码";
            cy += fieldH + 6;

            _chkSavePassword = new CheckBox();
            _chkSavePassword.Text = "保存密码（DPAPI 加密，仅当前 Windows 账户可解密）";
            _chkSavePassword.Font = Theme.Body(8.5f);
            _chkSavePassword.ForeColor = Theme.TextSecondary;
            _chkSavePassword.BackColor = Color.Transparent;
            _chkSavePassword.AutoSize = true;
            _chkSavePassword.Location = new Point(innerX, cy);
            _chkSavePassword.Checked = Result.HasSavedPassword;
            card.Controls.Add(_chkSavePassword);

            // ===== 卡片：共享与盘符 =====
            CardPanel card2 = new CardPanel();
            card2.Location = new Point(pad, card.Bottom + 12);
            card2.Size = new Size(cardW, 176);
            Controls.Add(card2);

            int cy2 = 18;

            AddLabel(card2, "共享路径", innerX, cy2);
            cy2 += labelH + 4;
            _txtRemote = AddField(card2, innerX, cy2, fieldW);
            _txtRemote.Text = string.IsNullOrEmpty(Result.RemotePath)
                ? serverAddress + @"\"
                : Result.RemotePath;
            _txtRemote.Placeholder = @"如 \\192.168.11.13\share";
            cy2 += fieldH + gap;

            // 盘符 + 备注并排
            int half = (fieldW - 12) / 2;
            AddLabel(card2, "盘符", innerX, cy2);
            AddLabel(card2, "备注", innerX + half + 12, cy2);
            cy2 += labelH + 4;

            _cmbDrive = new ComboBox();
            _cmbDrive.Location = new Point(innerX, cy2);
            _cmbDrive.Size = new Size(half, fieldH);
            _cmbDrive.FlatStyle = FlatStyle.Flat;
            _cmbDrive.Font = Theme.Body(9.5f);
            _cmbDrive.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbDrive.BackColor = Theme.FillSecondary;
            _cmbDrive.ForeColor = Theme.TextPrimary;
            _cmbDrive.Items.Add("(不映射)");
            for (char c = 'A'; c <= 'Z'; c++) _cmbDrive.Items.Add(c + ":");
            int idx = 0;
            string cur = SmbSessionManager.NormalizeDrive(Result.DriveLetter);
            if (!string.IsNullOrEmpty(cur))
            {
                int found = _cmbDrive.Items.IndexOf(cur);
                if (found >= 0) idx = found;
            }
            _cmbDrive.SelectedIndex = idx;
            card2.Controls.Add(_cmbDrive);

            _txtNote = AddField(card2, innerX + half + 12, cy2, half);
            _txtNote.Text = Result.Note ?? "";
            _txtNote.Placeholder = "可选";

            // ===== 底部按钮 =====
            _btnOk = new AppleButton();
            _btnOk.Text = "保存";
            _btnOk.Kind = AppleButtonKind.Primary;
            _btnOk.Size = new Size(88, 34);
            _btnOk.Location = new Point(ClientSize.Width - pad - 88, ClientSize.Height - pad - 34);
            _btnOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnOk.Click += OnOk;
            Controls.Add(_btnOk);

            _btnCancel = new AppleButton();
            _btnCancel.Text = "取消";
            _btnCancel.Kind = AppleButtonKind.Secondary;
            _btnCancel.Size = new Size(88, 34);
            _btnCancel.Location = new Point(_btnOk.Left - 8 - 88, _btnOk.Top);
            _btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnCancel.Click += delegate(object s, EventArgs e) { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_btnCancel);
        }

        void AddLabel(Control parent, string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = Theme.Body(9f);
            l.ForeColor = Theme.TextSecondary;
            l.BackColor = Color.Transparent;
            l.AutoSize = true;
            l.Location = new Point(x, y);
            parent.Controls.Add(l);
        }

        AppleTextBox AddField(Control parent, int x, int y, int w)
        {
            AppleTextBox t = new AppleTextBox();
            t.Location = new Point(x, y);
            t.Size = new Size(w, 34);
            t.Font = Theme.Body(9.5f);
            parent.Controls.Add(t);
            return t;
        }

        void OnOk(object sender, EventArgs e)
        {
            string name = _txtName.Text.Trim();
            string user = _txtUser.Text.Trim();
            string remote = _txtRemote.Text.Trim();

            if (name.Length == 0) { Warn("请填写「账号」。"); _txtName.Focus(); return; }
            if (user.Length == 0) { Warn("请填写「登录名」。"); _txtUser.Focus(); return; }
            if (remote.Length == 0 || !remote.StartsWith(@"\\"))
            {
                Warn(@"「共享路径」必须以 \\ 开头，例如 \\192.168.11.13\share");
                _txtRemote.Focus();
                return;
            }

            Result.Name = name;
            Result.UserName = user;
            Result.RemotePath = remote;
            Result.Note = _txtNote.Text.Trim();
            Result.DriveLetter = _cmbDrive.SelectedIndex <= 0
                ? "" : _cmbDrive.SelectedItem.ToString();

            string typed = _txtPassword.Text;
            if (typed.Length > 0)
            {
                if (_chkSavePassword.Checked)
                {
                    Result.PasswordEnc = CredentialStore.Protect(typed);
                    _sessionOnlyPassword = null;
                }
                else
                {
                    Result.PasswordEnc = "";
                    _sessionOnlyPassword = typed;
                }
            }
            else if (!_chkSavePassword.Checked)
            {
                Result.PasswordEnc = "";
                _sessionOnlyPassword = null;
            }
            // 留空 + 勾选保存 → 沿用原有 PasswordEnc

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>本次会话临时密码（未勾选"保存密码"时使用，不落盘）。</summary>
        public string SessionOnlyPassword
        {
            get { return _sessionOnlyPassword; }
        }

        void Warn(string msg)
        {
            MessageBox.Show(this, msg, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
