using System;
using System.Collections.Generic;
using System.Text;
using SmbUserSwitch.Core;
using SmbUserSwitch.Native;
class State {
  static void Main() {
    try { Console.OutputEncoding = Encoding.UTF8; } catch { }
    string s = @"\192.168.11.13";
    Console.WriteLine("全部连接:");
    foreach (ConnectionInfo c in ConnectionEnumerator.Enumerate())
      Console.WriteLine("  " + (c.HasDriveLetter? c.LocalName+" -> ":"(无盘符) ") + c.RemoteName + "  user=[" + c.UserName + "]");
    Console.WriteLine();
    Console.WriteLine("指向 " + s + " 的:");
    foreach (ConnectionInfo c in SmbSessionManager.ListConnections(s))
      Console.WriteLine("  " + c.RemoteName + "  user=[" + c.UserName + "]");
  }
}
