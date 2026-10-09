using System;
using System.Text;
using SmbUserSwitch.Core;
using SmbUserSwitch.Native;
class Step {
  static void Main() {
    try { Console.OutputEncoding = Encoding.UTF8; } catch { }
    string s = @"\192.168.11.13";
    AppConfig cfg = CredentialStore.Load();
    ServerProfile sp = cfg.FindServer(s);
    AccountProfile a = null;
    foreach (AccountProfile x in sp.Accounts) if (x.UserName=="liushuai") a = x;
    string pwd = CredentialStore.Unprotect(a.PasswordEnc);

    string[] targets = {
      s + @"\IPC$",
      s,
      s + @"\homes",
      s + @"\Home",
      s + @"\data",
      s + @"\Data",
      s + @"\share",
      s + @"\liushuai",
    };
    foreach (string t in targets) {
      SmbSessionManager.Disconnect(s, true, true);
      System.Threading.Thread.Sleep(200);
      NETRESOURCE nr = new NETRESOURCE();
      nr.dwScope = Mpr.RESOURCE_GLOBALNET; nr.dwType = Mpr.RESOURCETYPE_DISK;
      nr.dwDisplayType = Mpr.RESOURCEDISPLAYTYPE_SHARE;
      nr.dwUsage = Mpr.RESOURCEUSAGE_CONNECTABLE;
      nr.lpRemoteName = t;
      int rc = Mpr.WNetAddConnection2(nr, pwd, a.UserName, Mpr.CONNECT_TEMPORARY);
      Console.WriteLine(string.Format("  {0,-34} rc={1,-6} {2}", t, rc, rc==0?"<<< 成功":Win32Error.Describe(rc)));
      if (rc == 0) {
        foreach (ConnectionInfo c in SmbSessionManager.ListConnections(s))
          Console.WriteLine("       当前连接: " + c.RemoteName + " user=[" + c.UserName + "]");
      }
    }
    SmbSessionManager.Disconnect(s, true, true);
  }
}
