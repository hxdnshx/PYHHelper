using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace LiveHelper
{
    public class LiveHelper : BilibiliDM_PluginFramework.DMPlugin
    {
        [DllImport("User32.dll", EntryPoint = "FindWindow")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [System.Runtime.InteropServices.DllImport("user32",
            EntryPoint = "SendMessage",
            ExactSpelling = false,
            CharSet = CharSet.Auto,
            SetLastError = true)]
        public static extern int SendMessage(IntPtr hWnd,
            int m,
            IntPtr wParam,
            IntPtr lParam);
        public LiveHelper()
        {
            this.Connected += Class1_Connected;
            this.Disconnected += Class1_Disconnected;
            this.ReceivedDanmaku += Class1_ReceivedDanmaku;
            this.ReceivedRoomCount += Class1_ReceivedRoomCount;
            this.PluginAuth = "LiveHelper";
            this.PluginName = "LiveHelper";
            this.PluginCont = "example@example.com";
            this.PluginVer = "v0.0.1";
            targethwnd = FindWindow(null, "PYHHelper");
            setting = new SettingForm();
            setting.Hide();
        }

        private SettingForm setting;
        private IntPtr targethwnd;


        private void Class1_ReceivedRoomCount(object sender, BilibiliDM_PluginFramework.ReceivedRoomCountArgs e)
        {
        }
        static Regex pattern = new Regex("([0-9]+.[0-9]+.[0-9]+.[0-9]+:[0-9]+)");
        private static Regex pattern2 = new Regex(@"点rep-((\{[^,]+,[^\}]+\})+)");
        private void Class1_ReceivedDanmaku(object sender, BilibiliDM_PluginFramework.ReceivedDanmakuArgs e)
        {
            if (!Status)
                return;
            if (e.Danmaku.CommentText == null)
                return;
            var mat2 = pattern2.Match(e.Danmaku.CommentText.Replace("，",","));
            if (mat2.Success)
            {
                targethwnd = FindWindow(null, "PYHHelper");
                if (targethwnd == IntPtr.Zero)
                    return;
                setting.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate
                {
                    Clipboard.SetText(mat2.Groups[1].Value);
                    SendMessage(targethwnd, 0x402, IntPtr.Zero, IntPtr.Zero);
                });
                Log("发送点Rep请求");
            }

            if (e.Danmaku.isAdmin == false)
                return;
            var mat = pattern.Match(e.Danmaku.CommentText);
            if (!mat.Success)
                return;
            targethwnd = FindWindow(null, "PYHHelper");
            if(targethwnd == IntPtr.Zero)
                return;
            setting.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (ThreadStart)delegate
            {
                Clipboard.SetText(mat.Groups[1].Value);
                SendMessage(targethwnd, 0x401, IntPtr.Zero, IntPtr.Zero);
            });
            //throw new NotImplementedException();
            this.Log("成功观战：" + mat.Groups[1].Value);
        }

        private void Class1_Disconnected(object sender, BilibiliDM_PluginFramework.DisconnectEvtArgs e)
        {
        }

        private void Class1_Connected(object sender, BilibiliDM_PluginFramework.ConnectedEvtArgs e)
        {
        }

        public override void Admin()
        {
            base.Admin();
            Console.WriteLine("Hello World");
            this.Log("Hello World");
            this.AddDM("Hello World", true);
        }

        public override void Stop()
        {
            base.Stop();
            //請勿使用任何阻塞方法
            Console.WriteLine("Plugin Stoped!");
            this.Log("Plugin Stoped!");
            this.AddDM("Plugin Stoped!", true);
        }

        public override void Start()
        {
            base.Start();
            //請勿使用任何阻塞方法
            Console.WriteLine("Plugin Started!");
            this.Log("Plugin Started!");
            this.AddDM("Plugin Started!", true);
        }
    }

}
