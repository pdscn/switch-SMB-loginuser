using System;
using System.Collections.Generic;
using System.Text;
using SmbUserSwitch.Core;
using SmbUserSwitch.Native;

/// <summary>
/// 诊断 WNetCancelConnection2 到底认哪种名字：本地盘符 还是 远程 UNC。
/// </summary>
class CancelDiag
{
    static void Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        string server = @"\\localhost";
        string share = @"\\localhost\Videos";
        string drive = "S:";

        Cleanup(server, share, drive);

        Console.WriteLine("=========== 场景 A: 带盘符的映射，用【本地名 S:】取消 ===========");
        Run(server, share, drive, "本地名", drive);

        Console.WriteLine();
        Console.WriteLine("=========== 场景 B: 带盘符的映射，用【远程名 UNC】取消 ===========");
        Run(server, share, drive, "远程名", share);

        Console.WriteLine();
        Console.WriteLine("=========== 场景 C: 不带盘符的连接，用【远程名】取消 ===========");
        Cleanup(server, share, drive);
        int rc = Add(null, share);
        Console.WriteLine("  建立无盘符连接 rc=" + rc);
        Show(server, "建立后");
        rc = Mpr.WNetCancelConnection2(share, 0, true);
        Console.WriteLine("  取消(远程名) rc=" + rc + " " + Win32Error.Format(rc));
        Show(server, "取消后");

        Console.WriteLine();
        Console.WriteLine("=========== 场景 D: 断开后立即查询是否真没了 ===========");
        Cleanup(server, share, drive);
        Add(drive, share);
        Show(server, "建立后");
        int r1 = Mpr.WNetCancelConnection2(drive, 0, true);
        Console.WriteLine("  取消(本地名) rc=" + r1);
        Show(server, "取消后立刻");
        System.Threading.Thread.Sleep(500);
        Show(server, "等 500ms 后");

        Cleanup(server, share, drive);
        Console.WriteLine();
        Console.WriteLine("DIAG DONE");
    }

    static void Run(string server, string share, string drive, string label, string cancelTarget)
    {
        Cleanup(server, share, drive);
        int rc = Add(drive, share);
        Console.WriteLine("  建立 " + drive + " -> " + share + "  rc=" + rc);
        Show(server, "建立后");

        int rc2 = Mpr.WNetCancelConnection2(cancelTarget, 0, true);
        Console.WriteLine("  用" + label + " [" + cancelTarget + "] 取消 rc=" + rc2 +
                          " " + (rc2 == 0 ? "" : Win32Error.Format(rc2)));
        Show(server, "取消后");

        bool gone = SmbSessionManager.ListConnections(server).Count == 0;
        Console.WriteLine("  >>> " + (gone ? "成功断开" : "断开失败 —— 连接仍在！"));
        Cleanup(server, share, drive);
    }

    static void Show(string server, string label)
    {
        List<ConnectionInfo> c = SmbSessionManager.ListConnections(server);
        Console.Write("  [" + label + "] " + c.Count + " 条:");
        foreach (ConnectionInfo x in c)
            Console.Write("  " + (x.HasDriveLetter ? x.LocalName + "->" : "") + x.RemoteName);
        Console.WriteLine();
    }

    static int Add(string local, string remote)
    {
        NETRESOURCE nr = new NETRESOURCE();
        nr.dwScope = Mpr.RESOURCE_GLOBALNET;
        nr.dwType = Mpr.RESOURCETYPE_DISK;
        nr.dwDisplayType = Mpr.RESOURCEDISPLAYTYPE_SHARE;
        nr.dwUsage = Mpr.RESOURCEUSAGE_CONNECTABLE;
        nr.lpLocalName = local;
        nr.lpRemoteName = remote;
        return Mpr.WNetAddConnection2(nr, null, null, Mpr.CONNECT_TEMPORARY);
    }

    static void Cleanup(string server, string share, string drive)
    {
        Mpr.WNetCancelConnection2(drive, 0, true);
        Mpr.WNetCancelConnection2(share, 0, true);
        Mpr.WNetCancelConnection2(server + @"\IPC$", 0, true);
    }
}
