using System;
using System.Threading;
using System.Windows.Forms;
using SmbUserSwitch.UI;

namespace SmbUserSwitch
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // 单实例：两个实例同时操作 SMB 会话会互相打架
            bool createdNew;
            using (Mutex mutex = new Mutex(true, @"Global\SmbUserSwitch.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("SMB 用户切换已在运行中，请查看系统托盘。",
                        "SMB 用户切换", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
                {
                    Exception ex = e.ExceptionObject as Exception;
                    MessageBox.Show("程序遇到未处理的错误：\n\n" + (ex == null ? "(未知)" : ex.Message),
                        "SMB 用户切换", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
                {
                    MessageBox.Show("操作出错：\n\n" + e.Exception.Message,
                        "SMB 用户切换", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                Application.Run(new MainForm());
            }
        }
    }
}
