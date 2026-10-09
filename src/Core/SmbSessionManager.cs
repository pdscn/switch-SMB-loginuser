using System;
using System.Collections.Generic;
using System.Text;
using SmbUserSwitch.Native;

namespace SmbUserSwitch.Core
{
    /// <summary>切换结果。</summary>
    public class SwitchResult
    {
        public bool Success;
        public int ErrorCode;          // 0 表示成功
        public string Message;         // 面向用户的中文说明
        public bool NeedsForceConfirm; // 需要用户确认强制断开
        public List<ConnectionInfo> Blocking; // 占用中的连接

        public static SwitchResult Ok(string msg)
        {
            SwitchResult r = new SwitchResult();
            r.Success = true;
            r.ErrorCode = 0;
            r.Message = msg;
            return r;
        }

        public static SwitchResult Fail(int code, string msg)
        {
            SwitchResult r = new SwitchResult();
            r.Success = false;
            r.ErrorCode = code;
            r.Message = msg;
            return r;
        }
    }

    /// <summary>
    /// SMB 会话的枚举 / 断开 / 连接。
    ///
    /// 核心约束：Windows 重定向器在「每个登录会话 + 每个服务器名」下只保留一套凭据，
    /// 用不同用户名连同一台服务器会直接返回 1219。所以「切换用户」必须先拆掉旧会话。
    /// </summary>
    public static class SmbSessionManager
    {
        /// <summary>枚举指向该服务器的全部连接（含无盘符的 IPC$）。</summary>
        public static List<ConnectionInfo> ListConnections(string serverAddress)
        {
            return ConnectionEnumerator.ForServer(serverAddress);
        }

        /// <summary>当前生效的账号名（从已有连接反推），无连接时返回 ""。</summary>
        public static string CurrentUser(string serverAddress)
        {
            List<ConnectionInfo> conns = ListConnections(serverAddress);
            // 优先取有盘符的那条，它的用户名最能代表"当前在用谁"
            foreach (ConnectionInfo c in conns)
            {
                if (c.HasDriveLetter && !string.IsNullOrEmpty(c.UserName)) return c.UserName;
            }
            foreach (ConnectionInfo c in conns)
            {
                if (!string.IsNullOrEmpty(c.UserName)) return c.UserName;
            }
            return "";
        }

        /// <summary>
        /// 断开该服务器上的全部连接。
        /// force=false 时若有文件被占用会返回 2401；调用方据此询问用户后再用 force=true 重试。
        /// </summary>
        public static SwitchResult Disconnect(string serverAddress, bool force)
        {
            return Disconnect(serverAddress, force, false);
        }

        /// <param name="thorough">
        /// true 时额外盲断 IPC$，用于"我要换账号"的场景 —— 此时旧会话本来就要丢。
        /// false 时只断看得见的连接，避免拆掉用户正在用的隐藏会话。
        /// </param>
        public static SwitchResult Disconnect(string serverAddress, bool force, bool thorough)
        {
            string server = AppConfig.NormalizeServer(serverAddress);
            List<ConnectionInfo> conns = ListConnections(server);

            // 先断有盘符的映射，最后断 IPC$：顺序反过来的话，盘符映射会先失效
            List<ConnectionInfo> ordered = new List<ConnectionInfo>();
            List<ConnectionInfo> ipcOnly = new List<ConnectionInfo>();
            foreach (ConnectionInfo c in conns)
            {
                if (IsIpc(c)) ipcOnly.Add(c);
                else ordered.Add(c);
            }
            ordered.AddRange(ipcOnly);

            int blocked = 0;
            List<ConnectionInfo> blockers = new List<ConnectionInfo>();
            StringBuilder errs = new StringBuilder();

            foreach (ConnectionInfo c in ordered)
            {
                // WNetCancelConnection2 对名字的要求是不对称的，用错名字不会报错、
                // 只会返回 2250 然后什么都不做：
                //   有盘符的连接  → 必须用本地名 "S:"
                //   无盘符的连接  → 必须用远程名 "\\server\share"
                string target = c.HasDriveLetter ? c.LocalName : c.RemoteName;

                int rc = Mpr.WNetCancelConnection2(target, 0, force);
                if (rc == Mpr.NO_ERROR) continue;

                if (rc == 2401)
                {
                    blocked++;
                    blockers.Add(c);
                    continue;
                }
                errs.Append(target).Append(": ").Append(Win32Error.Format(rc)).Append("; ");
            }

            if (blocked > 0)
            {
                SwitchResult r = SwitchResult.Fail(2401,
                    "有 " + blocked + " 个连接正被程序占用，无法断开。");
                r.NeedsForceConfirm = true;
                r.Blocking = blockers;
                return r;
            }

            if (errs.Length > 0)
                return SwitchResult.Fail(0, "断开时出现问题：" + errs.ToString());

            // 兜底：NetUseEnum 偶尔看不到某些隐式会话，直接盲断 IPC$。
            // 不存在时返回 2250（未建立连接），无害。
            //
            // 但这属于"看不见的连接"，所以只在明确要换账号（thorough）时才做。
            // 普通的"断开连接"操作保持保守，避免拆掉用户正在用的隐藏会话。
            if (thorough)
                Mpr.WNetCancelConnection2(server + @"\IPC$", 0, true);

            // 回读校验：WNetCancelConnection2 的返回值不足以判断结果
            // （用错名字时它返回 2250 却什么都没做），必须重新枚举确认。
            List<ConnectionInfo> remaining = ListConnections(server);
            if (remaining.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (ConnectionInfo c in remaining) sb.Append("\n    ").Append(c.RemoteName);
                SwitchResult r = SwitchResult.Fail(2401,
                    "断开后仍有 " + remaining.Count + " 个连接残留，可能正被程序占用：" + sb.ToString());
                r.NeedsForceConfirm = !force;
                r.Blocking = remaining;
                return r;
            }

            return SwitchResult.Ok(conns.Count == 0 ? "当前无连接。" : "已断开 " + conns.Count + " 个连接。");
        }

        /// <summary>
        /// 断开本机全部网络连接（不限服务器）。
        /// force=false 时若有文件被占用会返回 2401 并列出占用项。
        /// </summary>
        public static SwitchResult DisconnectAll(bool force)
        {
            List<ConnectionInfo> conns = ConnectionEnumerator.Enumerate();

            // 先断有盘符的映射，最后断 IPC$ 这类无盘符会话
            List<ConnectionInfo> ordered = new List<ConnectionInfo>();
            List<ConnectionInfo> tail = new List<ConnectionInfo>();
            foreach (ConnectionInfo c in conns)
            {
                if (c.HasDriveLetter) ordered.Add(c);
                else tail.Add(c);
            }
            ordered.AddRange(tail);

            int blocked = 0;
            List<ConnectionInfo> blockers = new List<ConnectionInfo>();
            StringBuilder errs = new StringBuilder();

            foreach (ConnectionInfo c in ordered)
            {
                // 名字规则同 Disconnect：有盘符用本地名，无盘符用远程名
                string target = c.HasDriveLetter ? c.LocalName : c.RemoteName;

                int rc = Mpr.WNetCancelConnection2(target, 0, force);
                if (rc == Mpr.NO_ERROR) continue;

                if (rc == 2401)
                {
                    blocked++;
                    blockers.Add(c);
                    continue;
                }
                errs.Append(target).Append(": ").Append(Win32Error.Format(rc)).Append("; ");
            }

            if (blocked > 0)
            {
                SwitchResult r = SwitchResult.Fail(2401,
                    "有 " + blocked + " 个连接正被程序占用，无法断开。");
                r.NeedsForceConfirm = true;
                r.Blocking = blockers;
                return r;
            }

            if (errs.Length > 0)
                return SwitchResult.Fail(0, "断开时出现问题：" + errs.ToString());

            // 回读校验，理由同 Disconnect
            List<ConnectionInfo> remaining = ConnectionEnumerator.Enumerate();
            if (remaining.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (ConnectionInfo c in remaining)
                    sb.Append("\n    ").Append(c.RemoteName);
                SwitchResult r = SwitchResult.Fail(2401,
                    "断开后仍有 " + remaining.Count + " 个连接残留，可能正被程序占用：" + sb.ToString());
                r.NeedsForceConfirm = !force;
                r.Blocking = remaining;
                return r;
            }

            return SwitchResult.Ok(conns.Count == 0
                ? "当前没有任何网络连接。"
                : "已断开全部 " + conns.Count + " 个连接。");
        }

        /// <summary>用指定凭据连接。serverAddress 形如 "\\192.168.11.13"，remotePath 为完整 UNC。</summary>
        public static SwitchResult Connect(AccountProfile acct, string serverAddress)
        {
            return Connect(acct, serverAddress, null);
        }

        /// <param name="overridePassword">
        /// 非 null 时直接用它，忽略配置里存的密文。
        /// 用于"不保存密码"模式下由用户当场输入的密码。
        /// </param>
        public static SwitchResult Connect(AccountProfile acct, string serverAddress, string overridePassword)
        {
            string server = AppConfig.NormalizeServer(serverAddress);
            string remote = string.IsNullOrEmpty(acct.RemotePath)
                ? server
                : acct.RemotePath.Trim();

            if (!remote.StartsWith(@"\\"))
                return SwitchResult.Fail(67, "共享路径必须以 \\\\ 开头，当前为：" + remote);

            string local = NormalizeDrive(acct.DriveLetter);

            NETRESOURCE nr = new NETRESOURCE();
            nr.dwScope = Mpr.RESOURCE_GLOBALNET;
            nr.dwType = Mpr.RESOURCETYPE_DISK;
            nr.dwDisplayType = Mpr.RESOURCEDISPLAYTYPE_SHARE;
            nr.dwUsage = Mpr.RESOURCEUSAGE_CONNECTABLE;
            nr.lpLocalName = string.IsNullOrEmpty(local) ? null : local;
            nr.lpRemoteName = remote;
            nr.lpComment = null;
            nr.lpProvider = null;

            string password;
            if (overridePassword != null)
            {
                password = overridePassword;
            }
            else
            {
                password = CredentialStore.Unprotect(acct.PasswordEnc);
                if (password == null)
                    return SwitchResult.Fail(1326,
                        "无法解密已保存的密码（可能更换了 Windows 账户）。请重新编辑该账号并输入密码。");
            }

            // CONNECT_TEMPORARY：不写入用户配置文件。
            // 若用 CONNECT_UPDATE_PROFILE，连接会被持久化，下次登录时自动重连并再次触发 1219。
            int rc = Mpr.WNetAddConnection2(nr, password, acct.UserName, Mpr.CONNECT_TEMPORARY);

            if (rc == Mpr.NO_ERROR)
            {
                string where = string.IsNullOrEmpty(local) ? remote : local + "  →  " + remote;
                return SwitchResult.Ok("已连接：" + where + "（账号 " + acct.UserName + "）");
            }

            // 注意：这里**故意不做**任何"自动断开再重试"。
            // Connect 是纯连接操作，不该有破坏性副作用 —— 早先的版本在遇到 1219 时
            // 会顺手盲断 IPC$ 再重试，结果一次失败的连接尝试就把用户正在用的会话拆了。
            // 断开旧会话是 SwitchTo/Disconnect 的职责，调用方显式决定。

            string hint = "";
            if (rc == 1219)
                hint = " 请关闭正在访问该共享的程序（资源管理器窗口、Office、播放器等）后重试。";
            else if (rc == 85)
                hint = " 请在账号设置里换一个盘符。";
            else if (rc == 53 || rc == 67)
                hint = " 请检查服务器地址与共享名是否正确、网络是否可达。";

            return SwitchResult.Fail(rc, "连接失败：" + Win32Error.Format(rc) + hint);
        }

        /// <summary>
        /// 完整切换流程：断开旧会话 → 用新凭据连接。
        /// 若断开时被占用，返回 NeedsForceConfirm=true，调用方确认后再调一次并传 force=true。
        /// </summary>
        public static SwitchResult SwitchTo(AccountProfile acct, string serverAddress, bool force)
        {
            return SwitchTo(acct, serverAddress, force, null);
        }

        public static SwitchResult SwitchTo(AccountProfile acct, string serverAddress, bool force, string overridePassword)
        {
            string server = AppConfig.NormalizeServer(serverAddress);

            // thorough=true：换账号时旧会话无论如何都要丢，把隐藏的 IPC$ 也一并断干净，
            // 否则新凭据连接时会撞上 1219。
            SwitchResult dis = Disconnect(server, force, true);
            if (!dis.Success) return dis;

            return Connect(acct, server, overridePassword);
        }

        /// <summary>把 "S" / "s:" / "S:\" 统一成 "S:"；空值返回 ""。</summary>
        public static string NormalizeDrive(string drive)
        {
            if (string.IsNullOrEmpty(drive)) return "";
            string d = drive.Trim().TrimEnd('\\', ':').Trim().ToUpperInvariant();
            if (d.Length == 0) return "";
            if (d.Length > 1) return "";   // 非法输入
            if (d[0] < 'A' || d[0] > 'Z') return "";
            return d + ":";
        }

        static bool IsIpc(ConnectionInfo c)
        {
            return c.RemoteName.EndsWith(@"\IPC$", StringComparison.OrdinalIgnoreCase);
        }
    }
}
