using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using SmbUserSwitch.Native;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
struct NR {
    public int dwScope;
    public int dwType;
    public int dwDisplayType;
    public int dwUsage;
    public IntPtr lpLocalName;
    public IntPtr lpRemoteName;
    public IntPtr lpComment;
    public IntPtr lpProvider;
}

class Diag
{
    static void Main()
    {
        Console.WriteLine("IntPtr.Size = " + IntPtr.Size);
        Console.WriteLine("Marshal.SizeOf(NR) = " + Marshal.SizeOf(typeof(NR)));

        IntPtr hEnum = IntPtr.Zero;
        int rc = Mpr.WNetOpenEnum(Mpr.RESOURCE_CONNECTED, Mpr.RESOURCETYPE_DISK, 0, null, out hEnum);
        Console.WriteLine("WNetOpenEnum rc = " + rc);
        if (rc != 0) return;

        int bufSize = 16384;
        IntPtr buf = Marshal.AllocHGlobal(bufSize);
        try
        {
            int count = -1;
            int size = bufSize;
            rc = Mpr.WNetEnumResource(hEnum, ref count, buf, ref size);
            Console.WriteLine("WNetEnumResource rc = " + rc + "  count = " + count + "  returnedSize = " + size);

            int stride = Marshal.SizeOf(typeof(NR));
            for (int i = 0; i < count; i++)
            {
                IntPtr p = (IntPtr)(buf.ToInt64() + i * stride);
                NR nr = (NR)Marshal.PtrToStructure(p, typeof(NR));
                string local = nr.lpLocalName == IntPtr.Zero ? null : Marshal.PtrToStringUni(nr.lpLocalName);
                string remote = nr.lpRemoteName == IntPtr.Zero ? null : Marshal.PtrToStringUni(nr.lpRemoteName);
                Console.WriteLine("--- entry " + i + " ---");
                Console.WriteLine("  ptrLocal=0x" + nr.lpLocalName.ToInt64().ToString("X") +
                                  "  ptrRemote=0x" + nr.lpRemoteName.ToInt64().ToString("X"));
                Console.WriteLine("  local  = [" + local + "]  codepoints: " + CodePoints(local));
                Console.WriteLine("  remote = [" + remote + "]  codepoints: " + CodePoints(remote));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
            Mpr.WNetCloseEnum(hEnum);
        }
    }

    static string CodePoints(string s)
    {
        if (s == null) return "(null)";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < s.Length && i < 40; i++)
        {
            sb.Append(((int)s[i]).ToString("X4")).Append(' ');
        }
        return sb.ToString();
    }
}
