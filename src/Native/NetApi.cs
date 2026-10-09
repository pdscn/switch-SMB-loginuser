using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SmbUserSwitch.Native
{
    /// <summary>USE_INFO_2 —— NetUseEnum level 2 的条目。</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct USE_INFO_2
    {
        public IntPtr ui2_local;        // 本地名（盘符），IPC$ 等无盘符连接为空
        public IntPtr ui2_remote;       // \\server\share
        public IntPtr ui2_password;
        public int ui2_status;
        public int ui2_asg_type;
        public int ui2_refcount;
        public int ui2_usecount;
        public IntPtr ui2_username;
        public IntPtr ui2_domainname;
    }

    public static class NetApi
    {
        public const int NERR_Success = 0;
        public const int ERROR_MORE_DATA = 234;

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        public static extern int NetUseEnum(
            string servername, int level, out IntPtr bufptr,
            int prefmaxlen, out int entriesread, out int totalentries, ref int resume_handle);

        [DllImport("netapi32.dll")]
        public static extern int NetApiBufferFree(IntPtr buf);
    }

    /// <summary>一条当前生效的网络连接。</summary>
    public class ConnectionInfo
    {
        public string LocalName;    // "Z:" 或 ""（无盘符）
        public string RemoteName;   // "\\server\share"
        public string UserName;     // "LIU-PC\test" 或 ""
        public bool HasDriveLetter { get { return !string.IsNullOrEmpty(LocalName); } }

        public string ShareName
        {
            get
            {
                if (string.IsNullOrEmpty(RemoteName)) return "";
                int i = RemoteName.LastIndexOf('\\');
                return i >= 0 ? RemoteName.Substring(i + 1) : RemoteName;
            }
        }
    }

    public static class ConnectionEnumerator
    {
        /// <summary>
        /// 枚举本机全部网络连接。
        /// 用 NetUseEnum 而不是 WNetEnumResource —— 后者看不到无盘符的隐藏 IPC$ 会话，
        /// 而 IPC$ 恰恰是触发错误 1219 的常见元凶。
        /// </summary>
        public static List<ConnectionInfo> Enumerate()
        {
            List<ConnectionInfo> list = new List<ConnectionInfo>();
            IntPtr buf;
            int read, total = 0, resume = 0;
            int rc = NetApi.NetUseEnum(null, 2, out buf, -1, out read, out total, ref resume);
            if (rc != NetApi.NERR_Success) return list;

            try
            {
                int stride = Marshal.SizeOf(typeof(USE_INFO_2));
                for (int i = 0; i < read; i++)
                {
                    IntPtr p = (IntPtr)(buf.ToInt64() + i * stride);
                    USE_INFO_2 u = (USE_INFO_2)Marshal.PtrToStructure(p, typeof(USE_INFO_2));

                    ConnectionInfo ci = new ConnectionInfo();
                    ci.LocalName = Str(u.ui2_local);
                    ci.RemoteName = Str(u.ui2_remote);

                    string domain = Str(u.ui2_domainname);
                    string user = Str(u.ui2_username);
                    if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(user))
                        ci.UserName = domain + "\\" + user;
                    else if (!string.IsNullOrEmpty(user))
                        ci.UserName = user;
                    else
                        ci.UserName = "";

                    if (!string.IsNullOrEmpty(ci.RemoteName)) list.Add(ci);
                }
            }
            finally
            {
                NetApi.NetApiBufferFree(buf);
            }
            return list;
        }

        /// <summary>筛出指向指定服务器的全部连接（含 IPC$）。</summary>
        public static List<ConnectionInfo> ForServer(string serverAddress)
        {
            string prefix = serverAddress.TrimEnd('\\') + "\\";
            List<ConnectionInfo> result = new List<ConnectionInfo>();
            foreach (ConnectionInfo c in Enumerate())
            {
                if (c.RemoteName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    result.Add(c);
            }
            return result;
        }

        static string Str(IntPtr p)
        {
            return p == IntPtr.Zero ? "" : (Marshal.PtrToStringUni(p) ?? "");
        }
    }
}
