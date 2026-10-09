using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SmbUserSwitch.Native;

class SmokeTest
{
    static void Main()
    {
        try { Console.OutputEncoding = Encoding.UTF8; } catch { }

        Console.WriteLine("=== 1. WNetOpenEnum / WNetEnumResource 枚举已连接资源 ===");
        List<string[]> conns = EnumConnections();
        if (conns.Count == 0)
        {
            Console.WriteLine("  (无已连接资源)");
        }
        foreach (string[] c in conns)
        {
            Console.WriteLine("  local=[" + c[0] + "]  remote=[" + c[1] + "]");
        }
        Console.WriteLine("  共 " + conns.Count + " 条");

        Console.WriteLine();
        Console.WriteLine("=== 2. 过滤出 \\\\192.168.11.13 上的连接 ===");
        int n = 0;
        foreach (string[] c in conns)
        {
            if (c[1] != null && c[1].StartsWith(@"\\192.168.11.13\", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("  HIT local=[" + c[0] + "] remote=[" + c[1] + "]");
                n++;
            }
        }
        Console.WriteLine("  匹配 " + n + " 条");

        Console.WriteLine();
        Console.WriteLine("=== 3. DPAPI Protect/Unprotect 往返 ===");
        byte[] entropy = Encoding.UTF8.GetBytes("SmbUserSwitch.v1");
        string secret = "P@ssw0rd-测试-123";
        byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), entropy, DataProtectionScope.CurrentUser);
        byte[] dec = ProtectedData.Unprotect(enc, entropy, DataProtectionScope.CurrentUser);
        string round = Encoding.UTF8.GetString(dec);
        Console.WriteLine("  原文长度=" + secret.Length + " 密文长度=" + enc.Length + " Base64=" + Convert.ToBase64String(enc).Length);
        Console.WriteLine("  往返一致: " + (round == secret ? "YES" : "NO  got=[" + round + "]"));

        Console.WriteLine();
        Console.WriteLine("=== 4. 错误码翻译抽查 ===");
        int[] codes = new int[] { 53, 85, 86, 1219, 1326, 2401, 2250 };
        foreach (int code in codes)
        {
            Console.WriteLine("  " + Win32Error.Format(code));
        }

        Console.WriteLine();
        Console.WriteLine("=== 5. 探测 IPC$ 是否存在（不删除，仅 WNetGetConnection 查询） ===");
        string[] probes = new string[] { @"\\192.168.11.13\IPC$", @"\\192.168.11.13\NOPE$" };
        foreach (string p in probes)
        {
            Console.WriteLine("  " + p + " -> " + Describe(ProbeRemote(p)));
        }

        Console.WriteLine();
        Console.WriteLine("ALL DONE");
    }

    /// <summary>只读探测：对远程名调用 WNetCancelConnection2(name, 0, false) 会误删。
    /// 改用 WNetGetConnection 探测本地盘符；对 UNC 则用一个无害的 AddConnection 试探。
    /// 这里我们只用 WNetGetConnection 检查盘符，UNC 存在性由枚举结果判断。</summary>
    static int ProbeRemote(string unc)
    {
        return -1; // 见上：不做任何可能改变状态的调用
    }

    static string Describe(int code)
    {
        if (code == -1) return "SKIPPED(只读测试，未调用)";
        return Win32Error.Format(code);
    }

    static List<string[]> EnumConnections()
    {
        List<string[]> list = new List<string[]>();
        IntPtr hEnum = IntPtr.Zero;
        int rc = Mpr.WNetOpenEnum(Mpr.RESOURCE_CONNECTED, Mpr.RESOURCETYPE_DISK, 0, null, out hEnum);
        if (rc != 0)
        {
            Console.WriteLine("  WNetOpenEnum 失败: " + Win32Error.Format(rc));
            return list;
        }
        try
        {
            int bufSize = 16384;
            IntPtr buf = Marshal.AllocHGlobal(bufSize);
            try
            {
                while (true)
                {
                    int count = -1;
                    int size = bufSize;
                    rc = Mpr.WNetEnumResource(hEnum, ref count, buf, ref size);
                    if (rc == Mpr.ERROR_MORE_DATA || rc == 0)
                    {
                        int stride = IntPtr.Size == 8 ? 48 : 32;
                        for (int i = 0; i < count; i++)
                        {
                            IntPtr p = (IntPtr)(buf.ToInt64() + i * stride);
                            string local = PtrAt(p, 16);
                            string remote = PtrAt(p, 24);
                            list.Add(new string[] { local, remote });
                        }
                        if (rc == 0) break;
                        continue;
                    }
                    Console.WriteLine("  WNetEnumResource 返回: " + Win32Error.Format(rc));
                    break;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
        finally
        {
            Mpr.WNetCloseEnum(hEnum);
        }
        return list;
    }

    static string PtrAt(IntPtr basePtr, int offset)
    {
        IntPtr sp = Marshal.ReadIntPtr(basePtr, offset);
        if (sp == IntPtr.Zero) return null;
        return Marshal.PtrToStringUni(sp);
    }
}
