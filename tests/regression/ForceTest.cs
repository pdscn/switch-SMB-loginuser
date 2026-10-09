using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SmbUserSwitch.Core;
using SmbUserSwitch.Native;

/// <summary>
/// 验证"文件被占用时断开"的行为：force=false 应该被挡住并报告 2401，
/// force=true 应该能强制断开。
/// </summary>
class ForceTest
{
    static void Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        string server = @"\\localhost";
        string share = @"\\localhost\Videos";
        string drive = "S:";

        Cleanup(server, share, drive);

        Console.WriteLine("### 建立映射 " + drive + " -> " + share);
        int rc = Add(drive, share);
        Console.WriteLine("  rc=" + rc);
        if (rc != 0) { Console.WriteLine("  跳过：本机回环 SMB 不可用"); return; }

        Console.WriteLine();
        Console.WriteLine("### 通过 S: 打开一个文件并保持句柄");
        string probe = null;
        FileStream fs = null;
        try
        {
            string[] files = Directory.GetFiles(drive + "\\");
            if (files.Length == 0)
            {
                Console.WriteLine("  该目录为空，改用新建文件");
                string p = drive + @"\__smbtest_probe.txt";
                File.WriteAllText(p, "probe");
                probe = p;
            }
            else
            {
                probe = files[0];
            }
            fs = new FileStream(probe, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Console.WriteLine("  已打开: " + probe);
        }
        catch (Exception ex)
        {
            Console.WriteLine("  无法打开文件: " + ex.Message);
        }

        Console.WriteLine();
        Console.WriteLine("### 测试 A: force=false 断开（预期被 2401 挡住）");
        SwitchResult a = SmbSessionManager.Disconnect(server, false, true);
        Console.WriteLine("  Success=" + a.Success + " code=" + a.ErrorCode);
        Console.WriteLine("  NeedsForceConfirm=" + a.NeedsForceConfirm);
        Console.WriteLine("  Message=" + a.Message.Replace("\n", "\n    "));
        Console.WriteLine("  当前连接数: " + SmbSessionManager.ListConnections(server).Count);

        Console.WriteLine();
        Console.WriteLine("### 测试 B: force=true 强制断开");
        if (fs != null) { fs.Close(); fs.Dispose(); Console.WriteLine("  (先关闭文件句柄)"); }
        SwitchResult b = SmbSessionManager.Disconnect(server, true, true);
        Console.WriteLine("  Success=" + b.Success + " code=" + b.ErrorCode);
        Console.WriteLine("  Message=" + b.Message);
        Console.WriteLine("  当前连接数: " + SmbSessionManager.ListConnections(server).Count);

        Console.WriteLine();
        Console.WriteLine("### 测试 C: 断开后 S: 应可再次使用");
        int rc2 = Add(drive, share);
        Console.WriteLine("  重新映射 rc=" + rc2 + (rc2 == 0 ? " (盘符已释放)" : " (盘符仍被占用!)"));

        Cleanup(server, share, drive);
        if (probe != null && probe.StartsWith(drive)) { try { File.Delete(probe); } catch { } }
        Console.WriteLine();
        Console.WriteLine("FORCE TEST DONE");
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
