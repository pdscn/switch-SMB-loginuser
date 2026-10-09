using System;
using System.Collections.Generic;
using System.Text;
using SmbUserSwitch.Core;
using SmbUserSwitch.Native;

class CoreTest
{
    static void Main(string[] args)
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }
        string server = @"\\192.168.11.13";

        Console.WriteLine("### 测试 1: 枚举当前连接");
        List<ConnectionInfo> conns = SmbSessionManager.ListConnections(server);
        foreach (ConnectionInfo c in conns)
        {
            Console.WriteLine("  local=[" + c.LocalName + "] remote=[" + c.RemoteName +
                              "] user=[" + c.UserName + "] hasDrive=" + c.HasDriveLetter);
        }
        Console.WriteLine("  共 " + conns.Count + " 条");
        Console.WriteLine("  CurrentUser() = [" + SmbSessionManager.CurrentUser(server) + "]");

        Console.WriteLine();
        Console.WriteLine("### 测试 2: NormalizeDrive");
        string[] inputs = new string[] { "S", "s:", "S:\\", "  z:  ", "", "AB", "1:", "M:" };
        foreach (string s in inputs)
            Console.WriteLine("  [" + s + "] -> [" + SmbSessionManager.NormalizeDrive(s) + "]");

        Console.WriteLine();
        Console.WriteLine("### 测试 3: NormalizeServer");
        string[] addrs = new string[] { "192.168.11.13", @"\\192.168.11.13", @"\\192.168.11.13\", @"\\192.168.11.13\\", "  \\\\host  " };
        foreach (string s in addrs)
            Console.WriteLine("  [" + s + "] -> [" + AppConfig.NormalizeServer(s) + "]");

        Console.WriteLine();
        Console.WriteLine("### 测试 4: DPAPI 往返 + 错误密码检测");
        string enc = CredentialStore.Protect("hello-测试-123");
        Console.WriteLine("  密文前 24 字符: " + (enc.Length > 24 ? enc.Substring(0, 24) : enc));
        Console.WriteLine("  解密 = [" + CredentialStore.Unprotect(enc) + "]");
        Console.WriteLine("  坏密文 -> " + (CredentialStore.Unprotect("!!!not-base64!!!") == null ? "null(已捕获)" : "未捕获!"));

        Console.WriteLine();
        Console.WriteLine("### 测试 5: 用错误密码连接（预期失败，不崩溃）");
        AccountProfile bad = new AccountProfile();
        bad.Name = "badtest";
        bad.UserName = "definitely_wrong_user";
        bad.RemotePath = server + @"\IPC$";
        bad.DriveLetter = "";
        SwitchResult r = SmbSessionManager.Connect(bad, server, "wrong-password-xyz");
        Console.WriteLine("  Success = " + r.Success);
        Console.WriteLine("  ErrorCode = " + r.ErrorCode);
        Console.WriteLine("  Message = " + r.Message);

        Console.WriteLine();
        Console.WriteLine("### 测试 6: 不存在的服务器（预期 53）");
        AccountProfile nosrv = new AccountProfile();
        nosrv.Name = "nosrv";
        nosrv.UserName = "x";
        nosrv.RemotePath = @"\\192.0.2.99\share";
        nosrv.DriveLetter = "";
        SwitchResult r2 = SmbSessionManager.Connect(nosrv, @"\\192.0.2.99", "x");
        Console.WriteLine("  Success = " + r2.Success + "  ErrorCode = " + r2.ErrorCode);
        Console.WriteLine("  Message = " + r2.Message);

        Console.WriteLine();
        Console.WriteLine("### 测试 7: 非法共享路径（预期 67）");
        AccountProfile badpath = new AccountProfile();
        badpath.Name = "badpath";
        badpath.UserName = "x";
        badpath.RemotePath = @"C:\local\path";
        SwitchResult r3 = SmbSessionManager.Connect(badpath, server, "x");
        Console.WriteLine("  Success = " + r3.Success + "  ErrorCode = " + r3.ErrorCode);
        Console.WriteLine("  Message = " + r3.Message);

        Console.WriteLine();
        Console.WriteLine("### 测试 8: 配置读写往返");
        AppConfig cfg = new AppConfig();
        cfg.LastServer = server;
        ServerProfile sp = cfg.GetOrCreateServer(server);
        AccountProfile a1 = new AccountProfile();
        a1.Name = "测试账号1";
        a1.UserName = "tester";
        a1.PasswordEnc = CredentialStore.Protect("pw123");
        a1.DriveLetter = "S:";
        a1.RemotePath = server + @"\share";
        a1.Note = "中文备注";
        sp.Accounts.Add(a1);
        sp.LastAccount = "测试账号1";

        string tmpPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmbUserSwitch");
        Console.WriteLine("  配置目录 = " + tmpPath);
        Console.WriteLine("  配置文件 = " + CredentialStore.ConfigPath);
        bool existed = System.IO.File.Exists(CredentialStore.ConfigPath);
        Console.WriteLine("  (配置文件当前存在: " + existed + " —— 测试会覆盖它)");
        if (existed)
        {
            Console.WriteLine("  检测到已有配置文件，跳过写入测试以免破坏用户数据。");
        }
        else
        {
            CredentialStore.Save(cfg);
            AppConfig back = CredentialStore.Load();
            Console.WriteLine("  重新载入: servers=" + back.Servers.Count +
                              " lastServer=" + back.LastServer);
            ServerProfile bsp = back.FindServer(server);
            Console.WriteLine("  FindServer 命中: " + (bsp != null));
            if (bsp != null && bsp.Accounts.Count > 0)
            {
                AccountProfile ba = bsp.Accounts[0];
                Console.WriteLine("  账号: name=" + ba.Name + " user=" + ba.UserName +
                                  " drive=" + ba.DriveLetter + " note=" + ba.Note);
                Console.WriteLine("  密码解密 = [" + CredentialStore.Unprotect(ba.PasswordEnc) + "]");
            }
        }

        Console.WriteLine();
        Console.WriteLine("ALL CORE TESTS DONE");
    }
}
