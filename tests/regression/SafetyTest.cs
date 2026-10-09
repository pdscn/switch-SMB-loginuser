using System;
using System.Collections.Generic;
using System.Text;
using SmbUserSwitch.Core;
using SmbUserSwitch.Native;

class SafetyTest
{
    static void Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        string server = @"\\localhost";

        Console.WriteLine("### 建立一条测试用 IPC$ 会话（模拟用户正在使用中）");
        int rc = Mpr.WNetAddConnection2(MakeNr(server + @"\IPC$"), "", null, Mpr.CONNECT_TEMPORARY);
        Console.WriteLine("  建立会话 rc = " + rc + " " + WinError(rc));
        if (rc != 0)
        {
            Console.WriteLine("  无法建立测试会话，跳过后续验证。");
            return;
        }

        Console.WriteLine("  当前连接数: " + SmbSessionManager.ListConnections(server).Count);

        Console.WriteLine();
        Console.WriteLine("### 关键验证：失败的 Connect 不得破坏已有会话");
        AccountProfile bad = new AccountProfile();
        bad.Name = "bad";
        bad.UserName = "definitely_wrong_user";
        bad.RemotePath = server + @"\IPC$";
        bad.DriveLetter = "";

        SwitchResult r = SmbSessionManager.Connect(bad, server, "wrong-password-xyz");
        Console.WriteLine("  连接结果: Success=" + r.Success + " code=" + r.ErrorCode);

        List<ConnectionInfo> after = SmbSessionManager.ListConnections(server);
        Console.WriteLine("  连接数(应仍为 1): " + after.Count);
        foreach (ConnectionInfo c in after)
            Console.WriteLine("    " + c.RemoteName + "  user=[" + c.UserName + "]");

        if (after.Count >= 1)
            Console.WriteLine("  >>> 通过：失败连接没有拆掉已有会话");
        else
            Console.WriteLine("  >>> 失败：已有会话被破坏了！");

        Console.WriteLine();
        Console.WriteLine("### 验证：Disconnect(force=false, thorough=false) 只断看得见的连接");
        SwitchResult d1 = SmbSessionManager.Disconnect(server, false, false);
        Console.WriteLine("  " + d1.Message);
        Console.WriteLine("  剩余连接数: " + SmbSessionManager.ListConnections(server).Count);

        Console.WriteLine();
        Console.WriteLine("### 验证：SwitchTo 的 thorough 模式会清干净 IPC$");
        Mpr.WNetAddConnection2(MakeNr(server + @"\IPC$"), "", null, Mpr.CONNECT_TEMPORARY);
        Console.WriteLine("  重新建立会话后连接数: " + SmbSessionManager.ListConnections(server).Count);

        SwitchResult d2 = SmbSessionManager.Disconnect(server, true, true);
        Console.WriteLine("  thorough 断开: " + d2.Message);
        int left = SmbSessionManager.ListConnections(server).Count;
        Console.WriteLine("  剩余连接数(应为 0): " + left);
        Console.WriteLine(left == 0 ? "  >>> 通过：隐藏会话已清干净" : "  >>> 失败：仍有残留");

        Console.WriteLine();
        Console.WriteLine("SAFETY TESTS DONE");
    }

    static NETRESOURCE MakeNr(string remote)
    {
        NETRESOURCE nr = new NETRESOURCE();
        nr.dwScope = Mpr.RESOURCE_GLOBALNET;
        nr.dwType = Mpr.RESOURCETYPE_DISK;
        nr.dwDisplayType = Mpr.RESOURCEDISPLAYTYPE_SHARE;
        nr.dwUsage = Mpr.RESOURCEUSAGE_CONNECTABLE;
        nr.lpRemoteName = remote;
        return nr;
    }

    static string WinError(int rc)
    {
        return rc == 0 ? "" : "(" + Win32Error.Describe(rc) + ")";
    }
}
