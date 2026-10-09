using System;
using System.Runtime.InteropServices;

namespace SmbUserSwitch.Native
{
    /// <summary>
    /// NETRESOURCE 结构。用 class（而非 struct）以便字段为 null 时传 NULL 指针。
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public class NETRESOURCE
    {
        public int dwScope;
        public int dwType;
        public int dwDisplayType;
        public int dwUsage;
        public string lpLocalName;
        public string lpRemoteName;
        public string lpComment;
        public string lpProvider;
    }

    public static class Mpr
    {
        // dwScope
        public const int RESOURCE_CONNECTED = 0x00000001;
        public const int RESOURCE_GLOBALNET = 0x00000002;
        public const int RESOURCE_REMEMBERED = 0x00000003;

        // dwType
        public const int RESOURCETYPE_ANY = 0x00000000;
        public const int RESOURCETYPE_DISK = 0x00000001;

        // dwDisplayType
        public const int RESOURCEDISPLAYTYPE_SHARE = 0x00000003;

        // dwUsage
        public const int RESOURCEUSAGE_CONNECTABLE = 0x00000001;
        public const int RESOURCEUSAGE_CONTAINER = 0x00000002;

        // WNetAddConnection2 dwFlags
        public const int CONNECT_UPDATE_PROFILE = 0x00000001;
        public const int CONNECT_TEMPORARY = 0x00000004;

        public const int NO_ERROR = 0;
        public const int ERROR_MORE_DATA = 234;
        public const int ERROR_NOT_CONNECTED = 2250;

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        public static extern int WNetAddConnection2(
            NETRESOURCE lpNetResource, string lpPassword, string lpUsername, int dwFlags);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        public static extern int WNetCancelConnection2(
            string lpName, int dwFlags, bool fForce);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        public static extern int WNetOpenEnum(
            int dwScope, int dwType, int dwUsage, NETRESOURCE lpNetResource, out IntPtr lphEnum);

        // 必须显式 Unicode：默认 CharSet.Ansi 会绑到 WNetEnumResourceA，
        // 缓冲区里就是单字节字符串，按 UTF-16 读出来全是乱码。
        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        public static extern int WNetEnumResource(
            IntPtr hEnum, ref int lpcCount, IntPtr lpBuffer, ref int lpBufferSize);

        [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
        public static extern int WNetCloseEnum(IntPtr hEnum);
    }

    public static class Win32Error
    {
        /// <summary>把 Win32 错误码翻译成中文说明。</summary>
        public static string Describe(int code)
        {
            switch (code)
            {
                case 0: return "成功";
                case 53: return "找不到网络路径（服务器不可达或共享名错误）";
                case 67: return "找不到网络名（共享名可能拼写错误）";
                case 85: return "本地设备名已在使用中（该盘符已被占用）";
                case 86: return "指定的网络密码不正确";
                case 1203: return "网络连接被中断（服务器不可达或拒绝连接）";
                case 1219: return "凭据冲突：本机已有到该服务器的其他用户名的连接，需先断开（错误 1219）";
                case 1326: return "登录失败：用户名或密码错误";
                case 1327: return "登录失败：该账号不允许空密码，或账号被限制";
                case 1331: return "登录失败：该账号已被禁用";
                case 1909: return "账号已被锁定";
                case 2250: return "未建立网络连接";
                case 2401: return "有文件正在被使用，无法断开连接（错误 2401）";
                default: return "未知错误";
            }
        }

        public static string Format(int code)
        {
            return "(" + code + ") " + Describe(code);
        }
    }
}
