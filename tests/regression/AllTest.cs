using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SmbUserSwitch.Core;
using SmbUserSwitch.Native;

/// <summary>验证「全部断开」的三种情形。</summary>
class AllTest
{
    static void Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        Console.WriteLine("### 情形 1: 无任何连接时调用");
        Cleanup();
        SwitchResult a = SmbSessionManager.DisconnectAll(false);
        Console.WriteLine("  Success=" + a.Success + "  " + a.Message);

        Console.WriteLine();
        Console.WriteLine("### 情形 2: 多个连接（两个盘符 + IPC$）时全部断开");
        Cleanup();
        int r1 = Add("S:", @"\\localhost\Videos");
        int r2 = Add("T:", @"\\localhost\Videos");
        int r3 = Add(null, @"\\localhost\IPC$");
        Console.WriteLine("  建立: S:=" + r1 + "  T:=" + r2 + "  IPC$=" + r3);
        Show("建立后");

        SwitchResult b = SmbSessionManager.DisconnectAll(false);
        Console.WriteLine("  Success=" + b.Success + "  " + b.Message);
        Show("断开后");

        Console.WriteLine();
        Console.WriteLine("### 情形 3: 有文件被占用时（应返回 2401 并列出占用项）");
        Cleanup();
        Add("S:", @"\\localhost\Videos");
        FileStream fs = null;
        try
        {
            string[] files = Directory.GetFiles("S:\\");
            if (files.Length > 0)
                fs = new FileStream(files[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Console.WriteLine("  已打开句柄: " + (files.Length > 0 ? files[0] : "(无文件)"));
        }
        catch (Exception ex) { Console.WriteLine("  打开文件失败: " + ex.Message); }

        SwitchResult c = SmbSessionManager.DisconnectAll(false);
        Console.WriteLine("  Success=" + c.Success + "  code=" + c.ErrorCode +
                          "  NeedsForceConfirm=" + c.NeedsForceConfirm);
        Console.WriteLine("  Message=" + c.Message.Replace("\n", "\n    "));
        Show("被占用时");

        Console.WriteLine();
        Console.WriteLine("### 情形 4: 强制全部断开");
        if (fs != null) { fs.Close(); fs.Dispose(); }
        SwitchResult d = SmbSessionManager.DisconnectAll(true);
        Console.WriteLine("  Success=" + d.Success + "  " + d.Message);
        Show("强制断开后");

        Cleanup();
        Console.WriteLine();
        Console.WriteLine("ALL-DISCONNECT TESTS DONE");
    }

    static void Show(string label)
    {
        List<ConnectionInfo> c = ConnectionEnumerator.Enumerate();
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

    static void Cleanup()
    {
        foreach (ConnectionInfo c in ConnectionEnumerator.Enumerate())
        {
            string t = c.HasDriveLetter ? c.LocalName : c.RemoteName;
            Mpr.WNetCancelConnection2(t, 0, true);
        }
    }
}
