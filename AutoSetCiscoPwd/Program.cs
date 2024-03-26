using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoSetCiscoPwd
{
    internal class Program
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #region 钩子
        private const int WH_CALLWNDPROCRET = 12;
        private const int WM_CREATE = 0x0001;
        private const int WM_CLOSE = 0x0010;

        private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private static IntPtr hookID = IntPtr.Zero;

        static IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0 && wParam == (IntPtr)WM_CREATE)
            {
                CWPRETSTRUCT msg = (CWPRETSTRUCT)Marshal.PtrToStructure(lParam, typeof(CWPRETSTRUCT));
                IntPtr windowHandle = msg.hwnd;
                StringBuilder windowTitle = new StringBuilder(256);
                GetWindowText(windowHandle, windowTitle, windowTitle.Capacity);
                var title = windowTitle.ToString();
                if (windowTitle.ToString() == "abc")
                {
                    SendMessage(windowHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                }
            }

            return CallNextHookEx(hookID, code, wParam, lParam);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CWPRETSTRUCT
        {
            public IntPtr lResult;
            public IntPtr lParam;
            public IntPtr wParam;
            public uint message;
            public IntPtr hwnd;
        }


        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        #endregion

        [STAThread]
        static void Main()
        {
            if (Process.GetProcessesByName("AutoSetCiscoPwd").Length > 1)
                return;

            const string appName = "AutoSetCiscoPwd";

            // 添加到注册表启动项
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                key.SetValue(appName, Application.ExecutablePath);
            }

            while (true)
            {
                try
                {
                    EnumWindows(EnumWindowsCallback, IntPtr.Zero);
                }
                catch (Exception ex)
                {

                    System.IO.File.WriteAllText("err.txt", ex.ToString(), Encoding.UTF8);
                }
                finally
                {
                    Thread.Sleep(500);
                }
            }

        }

        private static bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam)
        {
            StringBuilder windowText = new StringBuilder(256);
            GetWindowText(hWnd, windowText, 256);
            var title = windowText.ToString();
            if (title.Contains("Cisco Secure Client") && title.Contains(" | "))
            {
                var isPwdWindow = FindControlWithText(hWnd, "Please enter your password");
                if (isPwdWindow != IntPtr.Zero)
                {
                    var textHwnd = FindChildControlByClass(hWnd, "Edit");
                    if (textHwnd != IntPtr.Zero)
                    {
                        var password = System.IO.File.ReadAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "password.txt"), Encoding.UTF8);

                        SetTextBoxValue(textHwnd, password.Trim());

                        //找到确定按钮
                        var btnOkHwnd = FindControlWithText(hWnd, "确定");
                        if (btnOkHwnd != IntPtr.Zero)
                        {
                            ClickButton(btnOkHwnd);
                            return false;
                        }

                    }
                }
            }

            return true;
        }

        static IntPtr FindControlWithText(IntPtr parentHandle, string text)
        {
            IntPtr childHandle = IntPtr.Zero;

            // 获取第一个子控件句柄
            childHandle = FindWindowEx(parentHandle, IntPtr.Zero, null, null);

            while (childHandle != IntPtr.Zero)
            {
                StringBuilder windowText = new StringBuilder(256);
                GetWindowText(childHandle, windowText, 256);

                if (windowText.ToString().Contains(text))
                {
                    return childHandle;
                }

                // 继续查找下一个子控件
                childHandle = FindWindowEx(parentHandle, childHandle, null, null);
            }

            return IntPtr.Zero;
        }

        static IntPtr FindChildControlByClass(IntPtr parentHandle, string className)
        {
            IntPtr childHandle = IntPtr.Zero;

            // 获取第一个子控件句柄
            childHandle = FindWindowEx(parentHandle, IntPtr.Zero, null, null);

            while (childHandle != IntPtr.Zero)
            {
                StringBuilder classBuffer = new StringBuilder(256);
                GetClassName(childHandle, classBuffer, 256);

                if (classBuffer.ToString() == className)
                {
                    return childHandle;
                }

                // 继续查找下一个子控件
                childHandle = FindWindowEx(parentHandle, childHandle, null, null);
            }

            return IntPtr.Zero;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        static void ClickButton(IntPtr buttonHandle)
        {
            const int BM_CLICK = 0x00F5;

            SendMessage(buttonHandle, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        }
        static void SetTextBoxValue(IntPtr textBoxHandle, string text)
        {
            const int WM_SETTEXT = 0x000C;

            SendMessage(textBoxHandle, WM_SETTEXT, IntPtr.Zero, text);
        }
    }
}
