using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace SmbUserSwitch.Core
{
    /// <summary>
    /// 配置与凭据的持久化。
    /// 密码用 DPAPI（CurrentUser 范围）加密后以 Base64 存入 XML ——
    /// 换 Windows 账户或换机器都无法解密，这正是我们想要的隔离性。
    /// 存储位置：%APPDATA%\SmbUserSwitch\config.xml
    /// </summary>
    public static class CredentialStore
    {
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SmbUserSwitch.v1.dpapi");

        public static string ConfigDir
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SmbUserSwitch");
                return dir;
            }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(ConfigDir, "config.xml"); }
        }

        // ---------- 加解密 ----------

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            byte[] enc = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }

        public static string Unprotect(string cipherBase64)
        {
            if (string.IsNullOrEmpty(cipherBase64)) return "";
            try
            {
                byte[] dec = ProtectedData.Unprotect(
                    Convert.FromBase64String(cipherBase64), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch (CryptographicException)
            {
                // 换了 Windows 账户 / 配置文件损坏
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        // ---------- 读写配置 ----------

        public static AppConfig Load()
        {
            AppConfig cfg = new AppConfig();
            string path = ConfigPath;
            if (!File.Exists(path)) return cfg;

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlElement root = doc.DocumentElement;
                if (root == null) return cfg;

                cfg.LastServer = Attr(root, "lastServer", "");
                cfg.AutoOpenExplorer = Attr(root, "autoOpenExplorer", "false") == "true";
                cfg.MinimizeToTray = Attr(root, "minimizeToTray", "true") == "true";

                XmlNodeList serverNodes = root.SelectNodes("server");
                if (serverNodes != null)
                {
                    foreach (XmlNode sn in serverNodes)
                    {
                        XmlElement se = sn as XmlElement;
                        if (se == null) continue;

                        ServerProfile sp = new ServerProfile();
                        sp.Address = Attr(se, "address", "");
                        sp.LastAccount = Attr(se, "lastAccount", "");
                        if (string.IsNullOrEmpty(sp.Address)) continue;

                        XmlNodeList acctNodes = se.SelectNodes("account");
                        if (acctNodes != null)
                        {
                            foreach (XmlNode an in acctNodes)
                            {
                                XmlElement ae = an as XmlElement;
                                if (ae == null) continue;

                                AccountProfile ap = new AccountProfile();
                                ap.Name = Attr(ae, "name", "");
                                ap.UserName = Attr(ae, "userName", "");
                                ap.PasswordEnc = Attr(ae, "password", "");
                                ap.DriveLetter = Attr(ae, "driveLetter", "");
                                ap.RemotePath = Attr(ae, "remotePath", "");
                                ap.Note = Attr(ae, "note", "");
                                if (!string.IsNullOrEmpty(ap.Name)) sp.Accounts.Add(ap);
                            }
                        }
                        cfg.Servers.Add(sp);
                    }
                }
            }
            catch (Exception)
            {
                // 配置损坏：先把坏文件改名留存，再退回空配置。
                // 早先直接返回空配置，用户下一次保存就会把损坏文件永久覆盖掉 ——
                // 里面存着的账号配置就此不可恢复。
                TryQuarantine(path);
                return new AppConfig();
            }
            return cfg;
        }

        /// <summary>把无法解析的配置改名留存，避免被后续保存覆盖。</summary>
        static void TryQuarantine(string path)
        {
            try
            {
                string bad = path + ".corrupt-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss");
                if (File.Exists(bad)) File.Delete(bad);
                File.Move(path, bad);
            }
            catch (Exception)
            {
                // 留存失败也不能影响启动
            }
        }

        public static void Save(AppConfig cfg)
        {
            Directory.CreateDirectory(ConfigDir);
            string tmp = ConfigPath + ".tmp";

            XmlWriterSettings ws = new XmlWriterSettings();
            ws.Indent = true;
            ws.Encoding = new UTF8Encoding(false);

            using (XmlWriter w = XmlWriter.Create(tmp, ws))
            {
                w.WriteStartDocument();
                w.WriteStartElement("smbUserSwitch");
                w.WriteAttributeString("version", "1");
                w.WriteAttributeString("lastServer", cfg.LastServer ?? "");
                w.WriteAttributeString("autoOpenExplorer", cfg.AutoOpenExplorer ? "true" : "false");
                w.WriteAttributeString("minimizeToTray", cfg.MinimizeToTray ? "true" : "false");

                foreach (ServerProfile sp in cfg.Servers)
                {
                    w.WriteStartElement("server");
                    w.WriteAttributeString("address", sp.Address ?? "");
                    w.WriteAttributeString("lastAccount", sp.LastAccount ?? "");

                    foreach (AccountProfile ap in sp.Accounts)
                    {
                        w.WriteStartElement("account");
                        w.WriteAttributeString("name", ap.Name ?? "");
                        w.WriteAttributeString("userName", ap.UserName ?? "");
                        w.WriteAttributeString("password", ap.PasswordEnc ?? "");
                        w.WriteAttributeString("driveLetter", ap.DriveLetter ?? "");
                        w.WriteAttributeString("remotePath", ap.RemotePath ?? "");
                        w.WriteAttributeString("note", ap.Note ?? "");
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                }

                w.WriteEndElement();
                w.WriteEndDocument();
            }

            // 原子替换。
            // 不能先 Delete 再 Move —— 那两步之间崩溃会直接丢掉整个配置。
            // File.Replace 是单次原子操作，且会把旧文件留成 .bak 作为退路。
            if (File.Exists(ConfigPath))
            {
                File.Replace(tmp, ConfigPath, ConfigPath + ".bak", true);
            }
            else
            {
                File.Move(tmp, ConfigPath);
            }
        }

        static string Attr(XmlElement e, string name, string def)
        {
            if (e == null) return def;
            string v = e.GetAttribute(name);
            return string.IsNullOrEmpty(v) ? def : v;
        }
    }
}
