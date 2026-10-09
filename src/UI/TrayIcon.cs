using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;
using SmbUserSwitch.Core;

namespace SmbUserSwitch.UI
{
    /// <summary>
    /// 托盘图标。图标在运行时用 GDI+ 画出来，避免依赖外部 .ico 资源，
    /// 保证编译产物是真正意义上的单个 exe。
    /// </summary>
    public class TrayIcon : IDisposable
    {
        NotifyIcon _icon;
        MainForm _owner;

        public TrayIcon(MainForm owner)
        {
            _owner = owner;

            _icon = new NotifyIcon();
            _icon.Icon = BuildIcon(Color.FromArgb(0x2D, 0x7D, 0xD2));
            _icon.Text = "SMB 用户切换";
            _icon.Visible = true;
            _icon.DoubleClick += delegate(object s, EventArgs e) { _owner.ShowFromTray(); };
        }

        // 字体是 GDI 资源，必须复用而不是每次重建。
        // 早先的版本每 5 秒（刷新定时器）重建一次菜单并 new 一次 Font，
        // 旧菜单 Dispose 时字体不会立刻释放 —— GDI 对象会持续堆积直到进程耗尽配额。
        Font _menuFont;
        Font _menuFontBold;

        /// <summary>菜单签名：内容没变就不重建，避免无谓的 GDI 分配与闪烁。</summary>
        string _lastSignature;

        public void UpdateMenu(AppConfig cfg, string currentServer, string currentUser)
        {
            string signature = BuildSignature(cfg, currentServer, currentUser);
            if (signature == _lastSignature && _icon.ContextMenuStrip != null) return;
            _lastSignature = signature;

            if (_menuFont == null) _menuFont = new Font("Microsoft YaHei UI", 9f);
            if (_menuFontBold == null) _menuFontBold = new Font(_menuFont, FontStyle.Bold);

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = _menuFont;

            ToolStripMenuItem header = new ToolStripMenuItem(
                string.IsNullOrEmpty(currentServer) ? "未选择服务器" : currentServer);
            header.Enabled = false;
            menu.Items.Add(header);

            ServerProfile sp = string.IsNullOrEmpty(currentServer) ? null : cfg.FindServer(currentServer);
            if (sp != null && sp.Accounts.Count > 0)
            {
                menu.Items.Add(new ToolStripSeparator());
                foreach (AccountProfile acct in sp.Accounts)
                {
                    AccountProfile captured = acct;
                    bool isCurrent = IsCurrentUser(currentUser, acct.UserName);

                    ToolStripMenuItem item = new ToolStripMenuItem();
                    item.Text = (isCurrent ? "● " : "   ") + acct.Name +
                                (string.IsNullOrEmpty(acct.DriveLetter) ? "" : "   [" + acct.DriveLetter + "]");
                    if (isCurrent) item.Font = _menuFontBold;
                    item.Click += delegate(object s, EventArgs e) { _owner.SwitchToAccount(captured); };
                    menu.Items.Add(item);
                }

                menu.Items.Add(new ToolStripSeparator());
                ToolStripMenuItem disc = new ToolStripMenuItem("断开当前连接");
                disc.Click += delegate(object s, EventArgs e) { _owner.DisconnectCurrent(); };
                menu.Items.Add(disc);
            }

            // 「全部断开」不依赖当前是否选中了服务器，始终可用
            if (sp == null || sp.Accounts.Count == 0) menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem discAll = new ToolStripMenuItem("全部断开");
            discAll.Click += delegate(object s, EventArgs e) { _owner.DisconnectEverything(); };
            menu.Items.Add(discAll);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem open = new ToolStripMenuItem("打开主窗口");
            open.Click += delegate(object s, EventArgs e) { _owner.ShowFromTray(); };
            menu.Items.Add(open);

            ToolStripMenuItem exit = new ToolStripMenuItem("退出");
            exit.Click += delegate(object s, EventArgs e) { _owner.ExitApp(); };
            menu.Items.Add(exit);

            ContextMenuStrip old = _icon.ContextMenuStrip;
            _icon.ContextMenuStrip = menu;
            if (old != null) old.Dispose();
        }

        static bool IsCurrentUser(string currentUser, string accountUser)
        {
            if (string.IsNullOrEmpty(currentUser) || string.IsNullOrEmpty(accountUser)) return false;
            return currentUser.EndsWith("\\" + accountUser, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(currentUser, accountUser, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>把菜单的可见内容压成一个字符串，用于判断是否需要重建。</summary>
        static string BuildSignature(AppConfig cfg, string currentServer, string currentUser)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(currentServer).Append('|').Append(currentUser).Append('|');
            ServerProfile sp = string.IsNullOrEmpty(currentServer) ? null : cfg.FindServer(currentServer);
            if (sp != null)
            {
                foreach (AccountProfile a in sp.Accounts)
                {
                    sb.Append(a.Name).Append('\u0001')
                      .Append(a.UserName).Append('\u0001')
                      .Append(a.DriveLetter).Append('\u0002');
                }
            }
            return sb.ToString();
        }

        public void ShowBalloon(string title, string text, bool warning)
        {
            try
            {
                _icon.BalloonTipTitle = title;
                _icon.BalloonTipText = text;
                _icon.BalloonTipIcon = warning ? ToolTipIcon.Warning : ToolTipIcon.Info;
                _icon.ShowBalloonTip(4000);
            }
            catch (Exception) { /* 托盘气泡失败不该影响主流程 */ }
        }

        /// <summary>画一个圆角方块 + 两个小人的图标。</summary>
        static Icon BuildIcon(Color color)
        {
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    using (SolidBrush bg = new SolidBrush(color))
                    using (GraphicsPath path = RoundedRect(new Rectangle(1, 1, 30, 30), 7))
                    {
                        g.FillPath(bg, path);
                    }

                    using (SolidBrush fg = new SolidBrush(Color.White))
                    {
                        // 后面的人
                        g.FillEllipse(fg, 8, 8, 9, 9);
                        g.FillPie(fg, 4, 17, 17, 18, 180, 180);
                        // 前面的人
                        g.FillEllipse(fg, 16, 10, 9, 9);
                        g.FillPie(fg, 12, 19, 17, 18, 180, 180);
                    }
                }

                IntPtr h = bmp.GetHicon();
                try
                {
                    using (Icon tmp = Icon.FromHandle(h))
                    {
                        // 必须克隆，否则 FromHandle 的图标在句柄释放后失效
                        return (Icon)tmp.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(h);
                }
            }
        }

        static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr handle);

        public void Dispose()
        {
            if (_icon != null)
            {
                _icon.Visible = false;
                if (_icon.ContextMenuStrip != null) _icon.ContextMenuStrip.Dispose();
                _icon.Dispose();
                _icon = null;
            }
            if (_menuFont != null) { _menuFont.Dispose(); _menuFont = null; }
            if (_menuFontBold != null) { _menuFontBold.Dispose(); _menuFontBold = null; }
        }
    }
}
