using System;
using System.Text;
using SmbUserSwitch.Core;
using SmbUserSwitch.Native;
class Control {
  static void Main() {
    try { Console.OutputEncoding = Encoding.UTF8; } catch { }
    Console.WriteLine("=== 对照: 本机 SMB (客户端栈是否正常) ===");
    string s = @"\localhost";
    Mpr.WNetCancelConnection2("S:", 0, true);
    Mpr.WNetCancelConnection2(s + @"\Videos", 0, true);
    Mpr.WNetCancelConnection2(s + @"\IPC$", 0, true);
    NETRESOURCE nr = new NETRESOURCE();
    nr.dwScope = Mpr.RESOURCE_GLOBALNET; nr.dwType = Mpr.RESOURCETYPE_DISK;
    nr.dwDisplayType = Mpr.RESOURCEDISPLAYTYPE_SHARE;
    nr.dwUsage = Mpr.RESOURCEUSAGE_CONNECTABLE;
    nr.lpLocalName = "S:";
    nr.lpRemoteName = s + @"\Videos";
    int rc = Mpr.WNetAddConnection2(nr, null, null, Mpr.CONNECT_TEMPORARY);
    Console.WriteLine("  本机 localhost\Videos  rc=" + rc + "  " + (rc==0 ? "成功 <<< 客户端栈正常" : Win32Error.Describe(rc)));
    Mpr.WNetCancelConnection2("S:", 0, true);

    Console.WriteLine();
    Console.WriteLine("=== 目标服务器重测 ===");
    string t = @"\192.168.11.13";
    AppConfig cfg = CredentialStore.Load();
    ServerProfile sp = cfg.FindServer(t);
    foreach (AccountProfile a in sp.Accounts) {
      string pwd = CredentialStore.Unprotect(a.PasswordEnc);
      SmbSessionManager.Disconnect(t, true, true);
      System.Threading.Thread.Sleep(300);
      NETRESOURCE n2 = new NETRESOURCE();
      n2.dwScope = Mpr.RESOURCE_GLOBALNET; n2.dwType = Mpr.RESOURCETYPE_DISK;
      n2.dwDisplayType = Mpr.RESOURCEDISPLAYTYPE_SHARE;
      n2.dwUsage = Mpr.RESOURCEUSAGE_CONNECTABLE;
      n2.lpRemoteName = t + @"\IPC$";
      int r = Mpr.WNetAddConnection2(n2, pwd, a.UserName, Mpr.CONNECT_TEMPORARY);
      Console.WriteLine("  [" + a.UserName + "] rc=" + r + "  " + (r==0 ? "成功" : Win32Error.Describe(r)));
      SmbSessionManager.Disconnect(t, true, true);
      System.Threading.Thread.Sleep(300);
    }
  }
}
