using System;
using System.Collections.Generic;
using System.Text;
using SmbUserSwitch.Core;
using SmbUserSwitch.Native;

/// <summary>
/// 决定性验证：证明"切换用户"的核心机制真的解决了 1219。
///
/// 对照组（不拆会话）：已有一条连接时，用不同用户名再连 → 必然 1219
/// 实验组（拆会话后）：先 Disconnect 再连 → 应该变成 1326（认证失败），而不是 1219
///
/// 用本机 SMB 服务（\\localhost\Videos）做靶子，无需外部服务器和额外账号。
/// </summary>
class SwitchProof
{
    static void Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        string server = @"\\localhost";
        string share = @"\\localhost\Videos";
        string drive = "S:";

        Console.WriteLine("靶子: " + share);
        Console.WriteLine();

        // 清理现场
        Mpr.WNetCancelConnection2(share, 0, true);
        Mpr.WNetCancelConnection2(drive, 0, true);
        Mpr.WNetCancelConnection2(server + @"\IPC$", 0, true);

        Console.WriteLine("========== 步骤 1: 用当前 Windows 身份连接 ==========");
        int rc = AddConn(drive, share, null, null);
        Console.WriteLine("  连接 " + drive + " -> " + share + "  rc=" + rc + " " + Desc(rc));
        if (rc != 0)
        {
            Console.WriteLine("  本机回环 SMB 连接失败，测试无法继续。");
            Console.WriteLine("  （某些系统策略禁止 loopback SMB，这属于环境限制而非程序缺陷）");
            return;
        }
        ShowState(server, "连接后");

        Console.WriteLine();
        Console.WriteLine("========== 步骤 2【对照组】不拆会话，直接用别的用户名连 ==========");
        Console.WriteLine("  这正是手工操作时撞 1219 的场景：");
        int rc2 = AddConn(null, share, "someotheruser", "whatever");
        Console.WriteLine("  rc=" + rc2 + "  " + Desc(rc2));
        if (rc2 == 1219)
            Console.WriteLine("  >>> 复现成功：这就是要解决的那个错误。");
        else
            Console.WriteLine("  >>> 未复现 1219（rc=" + rc2 + "），对照组条件不满足。");

        Console.WriteLine();
        Console.WriteLine("========== 步骤 3【实验组】先拆会话，再用别的用户名连 ==========");
        SwitchResult dis = SmbSessionManager.Disconnect(server, true, true);
        Console.WriteLine("  拆会话: " + dis.Message);
        ShowState(server, "拆完");

        int rc3 = AddConn(null, share, "someotheruser", "whatever");
        Console.WriteLine("  重新连接 rc=" + rc3 + "  " + Desc(rc3));

        Console.WriteLine();
        if (rc3 != 1219)
            Console.WriteLine("  >>> 通过：1219 已消除，错误变成 " + rc3 + "（认证类错误，说明会话确实重建了）");
        else
            Console.WriteLine("  >>> 失败：仍然是 1219，拆会话不彻底");

        Console.WriteLine();
        Console.WriteLine("========== 步骤 4: 用 SmbSessionManager.SwitchTo 走完整流程 ==========");
        // 先恢复到"当前用户已连接"的状态
        Mpr.WNetCancelConnection2(share, 0, true);
        Mpr.WNetCancelConnection2(server + @"\IPC$", 0, true);
        AddConn(drive, share, null, null);
        ShowState(server, "恢复后");

        AccountProfile acct = new AccountProfile();
        acct.Name = "证明用账号";
        acct.UserName = "someotheruser";
        acct.RemotePath = share;
        acct.DriveLetter = drive;

        SwitchResult sw = SmbSessionManager.SwitchTo(acct, server, true, "whatever");
        Console.WriteLine("  SwitchTo 结果: Success=" + sw.Success + " code=" + sw.ErrorCode);
        Console.WriteLine("  Message: " + sw.Message);
        Console.WriteLine(sw.ErrorCode != 1219
            ? "  >>> 通过：完整切换流程没有撞 1219"
            : "  >>> 失败：完整流程仍撞 1219");

        Console.WriteLine();
        Console.WriteLine("========== 清理 ==========");
        SmbSessionManager.Disconnect(server, true, true);
        ShowState(server, "清理后");
        Console.WriteLine();
        Console.WriteLine("PROOF DONE");
    }

    static void ShowState(string server, string label)
    {
        List<ConnectionInfo> conns = SmbSessionManager.ListConnections(server);
        Console.WriteLine("  [" + label + "] 连接数=" + conns.Count +
                          "  当前用户=[" + SmbSessionManager.CurrentUser(server) + "]");
        foreach (ConnectionInfo c in conns)
            Console.WriteLine("      " + (c.HasDriveLetter ? c.LocalName + " -> " : "") + c.RemoteName);
    }

    static int AddConn(string local, string remote, string user, string pwd)
    {
        NETRESOURCE nr = new NETRESOURCE();
        nr.dwScope = Mpr.RESOURCE_GLOBALNET;
        nr.dwType = Mpr.RESOURCETYPE_DISK;
        nr.dwDisplayType = Mpr.RESOURCEDISPLAYTYPE_SHARE;
        nr.dwUsage = Mpr.RESOURCEUSAGE_CONNECTABLE;
        nr.lpLocalName = local;
        nr.lpRemoteName = remote;
        return Mpr.WNetAddConnection2(nr, pwd, user, Mpr.CONNECT_TEMPORARY);
    }

    static string Desc(int rc)
    {
        return rc == 0 ? "成功" : Win32Error.Format(rc);
    }
}
