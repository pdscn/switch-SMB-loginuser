using System;
using System.Runtime.InteropServices;
using System.Text;

class Diag2
{
    [StructLayout(LayoutKind.Sequential)]
    struct USE_INFO_2
    {
        public IntPtr ui2_local;
        public IntPtr ui2_remote;
        public IntPtr ui2_password;
        public int ui2_status;
        public int ui2_asg_type;
        public int ui2_refcount;
        public int ui2_usecount;
        public IntPtr ui2_username;
        public IntPtr ui2_domainname;
    }

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    static extern int NetUseEnum(string servername, int level, out IntPtr bufptr,
        int prefmaxlen, out int entriesread, out int totalentries, ref int resume_handle);

    [DllImport("netapi32.dll")]
    static extern int NetApiBufferFree(IntPtr buf);

    static void Main()
    {
        Console.WriteLine("SizeOf(USE_INFO_2) = " + Marshal.SizeOf(typeof(USE_INFO_2)));
        IntPtr buf;
        int read, total = 0, resume = 0;
        int rc = NetUseEnum(null, 2, out buf, -1, out read, out total, ref resume);
        Console.WriteLine("NetUseEnum level2 rc = " + rc + "  read = " + read + "  total = " + total);
        if (rc != 0) return;
        try
        {
            int stride = Marshal.SizeOf(typeof(USE_INFO_2));
            for (int i = 0; i < read; i++)
            {
                IntPtr p = (IntPtr)(buf.ToInt64() + i * stride);
                USE_INFO_2 u = (USE_INFO_2)Marshal.PtrToStructure(p, typeof(USE_INFO_2));
                Console.WriteLine("--- use " + i + " ---");
                Console.WriteLine("  local   = [" + S(u.ui2_local) + "]");
                Console.WriteLine("  remote  = [" + S(u.ui2_remote) + "]");
                Console.WriteLine("  user    = [" + S(u.ui2_domainname) + "\\" + S(u.ui2_username) + "]");
                Console.WriteLine("  status  = " + u.ui2_status + "  asg_type = " + u.ui2_asg_type);
            }
        }
        finally { NetApiBufferFree(buf); }

        Console.WriteLine();
        Console.WriteLine("结论：IPC$ 是否出现在上面列表里 -> " +
            (Contains(buf, read, "IPC$") ? "出现" : "未出现（需盲断）"));
    }

    static bool Contains(IntPtr buf, int read, string needle)
    {
        int stride = Marshal.SizeOf(typeof(USE_INFO_2));
        for (int i = 0; i < read; i++)
        {
            IntPtr p = (IntPtr)(buf.ToInt64() + i * stride);
            USE_INFO_2 u = (USE_INFO_2)Marshal.PtrToStructure(p, typeof(USE_INFO_2));
            string r = S(u.ui2_remote);
            if (r != null && r.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    static string S(IntPtr p) { return p == IntPtr.Zero ? null : Marshal.PtrToStringUni(p); }
}
