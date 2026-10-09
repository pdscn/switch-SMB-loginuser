using System;
using System.Collections.Generic;

namespace SmbUserSwitch.Core
{
    /// <summary>一个可切换的账号。</summary>
    public class AccountProfile
    {
        public string Name;          // 显示名，如 "admin"
        public string UserName;      // 登录名，如 "administrator" 或 "192.168.11.13\admin"
        public string PasswordEnc;   // DPAPI 密文（Base64），未保存密码时为 null/空
        public string DriveLetter;   // "S:" 或 ""（不映射盘符）
        public string RemotePath;    // 完整 UNC，如 "\\192.168.11.13\share"
        public string Note;          // 备注

        public bool HasSavedPassword
        {
            get { return !string.IsNullOrEmpty(PasswordEnc); }
        }

        public AccountProfile Clone()
        {
            AccountProfile c = new AccountProfile();
            c.Name = this.Name;
            c.UserName = this.UserName;
            c.PasswordEnc = this.PasswordEnc;
            c.DriveLetter = this.DriveLetter;
            c.RemotePath = this.RemotePath;
            c.Note = this.Note;
            return c;
        }
    }

    /// <summary>一台服务器下的全部账号配置。</summary>
    public class ServerProfile
    {
        public string Address;       // "\\192.168.11.13"
        public string LastAccount;   // 上次成功使用的账号名
        public List<AccountProfile> Accounts;

        public ServerProfile()
        {
            Accounts = new List<AccountProfile>();
        }

        public AccountProfile Find(string name)
        {
            foreach (AccountProfile a in Accounts)
            {
                if (string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) return a;
            }
            return null;
        }
    }

    /// <summary>整个配置文件。</summary>
    public class AppConfig
    {
        public List<ServerProfile> Servers;
        public string LastServer;
        public bool AutoOpenExplorer;   // 切换成功后自动打开资源管理器
        public bool MinimizeToTray;     // 关闭窗口时最小化到托盘而非退出

        public AppConfig()
        {
            Servers = new List<ServerProfile>();
            LastServer = "";
            AutoOpenExplorer = false;
            MinimizeToTray = true;
        }

        public ServerProfile FindServer(string address)
        {
            if (string.IsNullOrEmpty(address)) return null;
            string norm = NormalizeServer(address);
            foreach (ServerProfile s in Servers)
            {
                if (string.Equals(NormalizeServer(s.Address), norm, StringComparison.OrdinalIgnoreCase))
                    return s;
            }
            return null;
        }

        public ServerProfile GetOrCreateServer(string address)
        {
            ServerProfile s = FindServer(address);
            if (s != null) return s;
            s = new ServerProfile();
            s.Address = NormalizeServer(address);
            Servers.Add(s);
            return s;
        }

        /// <summary>把 "192.168.11.13"、"\\\\192.168.11.13"、"\\\\192.168.11.13\\" 统一成 "\\\\192.168.11.13"。</summary>
        public static string NormalizeServer(string address)
        {
            if (string.IsNullOrEmpty(address)) return "";
            string a = address.Trim();
            a = a.TrimEnd('\\', '/');
            if (!a.StartsWith(@"\\")) a = @"\\" + a.TrimStart('\\', '/');
            return a;
        }
    }
}
