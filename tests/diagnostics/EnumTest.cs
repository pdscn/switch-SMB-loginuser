using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using SmbUserSwitch.Core;
using SmbUserSwitch.Native;

class EnumTest
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHARE_INFO_1 { public IntPtr n; public int t; public IntPtr r; }

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    static extern int NetShareEnum(string srv, int lvl, out IntPtr buf, int max,
        out int read, out int total, ref int resume);
    [DllImport("netapi32.dll")] static extern int NetApiBufferFree(IntPtr b);

    const string Server = @"\192.168.11.13";

    static void Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        AppConfig cfg = CredentialStore.Load();
        ServerProfile sp = cfg.FindServer(Server);
        AccountProfile good = null;
        foreach (AccountProfile a in sp.Accounts)
            if (a.UserName == "liushuai") good = a;

        string pwd = CredentialStore.Unprotect(good.PasswordEnc);
        SmbSessionManager.Disconnect(Server, true, true);

        Console.WriteLine("用 [" + good.UserName + "] 建立认证会话...");
        NETRESOURCE nr = new NETRESOURCE();
        nr.dwScope = Mpr.RESOURCE_GLOBALNET; nr.dwType = Mpr.RESOURCETYPE_DISK;
        nr.dwDisplayType = Mpr.RESOURCEDISPLAYTYPE_SHARE;
        nr.dwUsage = Mpr.RESOURCEUSAGE_CONNECTABLE;
        nr.lpRemoteName = Server + @"\IPC$";
        int rc = Mpr.WNetAddConnection2(nr, pwd, good.UserName, Mpr.CONNECT_TEMPORARY);
        Console.WriteLine("  rc=" + rc + " " + (rc == 0 ? "成功" : Win32Error.Describe(rc)));
        if (rc != 0) return;

        Console.WriteLine();
        Console.WriteLine("=== 枚举共享列表 ===");
        IntPtr buf; int read, total = 0, resume = 0;
        int e = NetShareEnum("192.168.11.13", 1, out buf, -1, out read, out total, ref resume);
        Console.WriteLine("NetShareEnum rc=" + e + " 数量=" + read);
        if (e == 0)
        {
            int stride = Marshal.SizeOf(typeof(SHARE_INFO_1));
            for (int i = 0; i < read; i++)
            {
                IntPtr p = (IntPtr)(buf.ToInt64() + i * stride);
                SHARE_INFO_1 s = (SHARE_INFO_1)Marshal.PtrToStructure(p, typeof(SHARE_INFO_1));
                string nm = s.n == IntPtr.Zero ? "" : Marshal.PtrToStringUni(s.n);
                string rm = s.r == IntPtr.Zero ? "" : Marshal.PtrToStringUni(s.r);
                Console.WriteLine("  " + (nm ?? "").PadRight(22) + " type=" + s.t + "  " + rm);
            }
            NetApiBufferFree(buf);
        }
        SmbSessionManager.Disconnect(Server, true, true);
        Console.WriteLine();
        Console.WriteLine("DONE");
    }
}
