using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PYHHelper
{
    public partial class Form1 : Form
    {

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        private FileSystemWatcher fsw;
        private HttpClient _client;

        public Form1()
        {
            InitializeComponent();
            TH155Addr.TH155AddrStartup(1, this.Handle, TH155Addr.TH155CALLBACK);
            fsw = new FileSystemWatcher();
            fsw.Created += OnFileCreated;
            fsw.Filter = "*.rep";
            fsw.IncludeSubdirectories = true;
            fsw.EnableRaisingEvents = false;

            _client = new HttpClient();
            _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 11_0 like Mac OS X) AppleWebKit/604.1.38 (KHTML, like Gecko) Version/11.0 Mobile/15A356 Safari/604.1");
            //client.DefaultRequestHeaders.Add("Content-Type", "application /x-www-form-urlencoded");
            _client.DefaultRequestHeaders.Add("Referer", "https://tenco.info/game/7/account/nanashi/");
            _client.DefaultRequestHeaders.Add("Origin", "https://tenco.info/game/7/account/nanashi/");
            _client.DefaultRequestHeaders.Add("Host", "tenco.info");
            _client.DefaultRequestHeaders.Add("Accept", "*/*");
            _client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            _client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.8");

            _currentDate = DateTime.Now;
        }

        private DateTime _currentDate;

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            FileInfo fi = new FileInfo(e.FullPath);
            DateTime time = DateTime.Now;
            string newPath = textBox2.Text + "\\" + time.Month + "-" + time.Day + "-" + fi.Name;
            Thread.Sleep(2500);
            File.Copy(e.FullPath, newPath, true);
            listBox1.Items.Insert(currentindex, newPath);
            currentindex = (currentindex + 1) % listBox1.Items.Count;
        }


        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x401)
            {
                string str = Clipboard.GetText();
                ToNetwork(str);
                return;
            }
            base.WndProc(ref m);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            TH155Addr.TH155AddrCleanup();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }

        //From : https://stackoverflow.com/questions/13879911/decompress-a-gzip-compressed-http-response-chunked-encoding
        public static byte[] Decompress_GZip(byte[] gzip)
        {
            using (GZipStream stream = new GZipStream(new MemoryStream(gzip),
                CompressionMode.Decompress))
            {
                byte[] buffer = new byte[1024];
                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0, 1024);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    }
                    while (count > 0);
                    return memory.ToArray();
                }
            }
        }

        protected string HttpGet(string uri)
        {
            var result = _client.GetAsync(uri).Result;
            if (result.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                var rawResult = result.Content.ReadAsByteArrayAsync().Result;
                var finResult = Decompress_GZip(rawResult);
                return Encoding.UTF8.GetString(finResult);
            }

            return result.Content.ReadAsStringAsync().Result;
        }

        //https://stackoverflow.com/questions/1405048/how-do-i-decode-a-url-parameter-using-c
        private static string DecodeUrlString(string url)
        {
            string newUrl;
            while ((newUrl = Uri.UnescapeDataString(url)) != url)
                url = newUrl;
            return newUrl;
        }

        private bool tencoFetch = false;
        private void FetchTenco()
        {
            if (tencoFetch)
                return;
            tencoFetch = true;
            var data = HttpGet("https://tenco.info/game/7/replay/");
            var date = _currentDate.ToString("yyyyMMdd");
            var pat = new Regex($"(//tenco.info/replay/7/0/({date}[^\\.]+\\.rep))\"");
            var mat = pat.Matches(data);
            foreach (Match match in mat)
            {
                var result = _client.GetAsync($"http:{match.Groups[1].Value}").Result;
                var filePath = textBox2.Text + "\\" + DecodeUrlString(match.Groups[2].Value);
                if (result.StatusCode == HttpStatusCode.OK && !File.Exists(filePath))
                {
                    var rawResult = result.Content.ReadAsByteArrayAsync().Result;
                    var finResult = Decompress_GZip(rawResult);
                    FileStream file = new FileStream(filePath,FileMode.Create);
                    file.Write(finResult,0,finResult.Length);
                    file.Close();
                    this.Invoke((Action) (() =>
                    {
                        listBox1.Items.Insert(currentindex, filePath);
                    }));

                }
            }

            tencoFetch = false;
            _currentDate = DateTime.Now;
        }

        private bool execed = false;
        private bool executing = false;
        private int currentindex = 0;
        private void timer1_Tick_1(object sender, EventArgs e)
        {
            label6.Text = (executing ? "Executing" : "Idleing") + (tencoFetch ? "Fetching" : "");
            int state = TH155Addr.TH155AddrGetState();
            string str;
            label1.Text = "State : " + state;
            if (_currentDate.Day != DateTime.Now.Day) //日替
            {
                FetchTenco();
            }
            if (state >= 1)
            {
                if (execed == false)
                {
                    execed = true;
                    TH155Addr.TH155EnumRTCHild();
                }

                var cursorstat_y = TH155Addr.TH155GetRTChildInt("menu/cursor/target_y");
                var cursorstat_x = TH155Addr.TH155GetRTChildInt("menu/cursor/target_x");
                label5.Text = "cursor:" + cursorstat_x + "," + cursorstat_y;

                var replay_state = TH155Addr.TH155GetRTChildInt("replay/state");
                var play_state = TH155Addr.TH155GetRTChildInt("replay/game_mode");
                label2.Text = "replayStat : " + play_state;

                if (!IsReplaying() && checkBox1.Checked && !executing)
                {
                    executing = true;
                    Task.Run(() => {
                        //Thread.Sleep(2500);
                        if (CheckCursor(-1)) //暂停中
                        {
                            SetTH155Foreground();
                            TH155Addr.VirtualPress(88);//x
                            executing = false;
                            return;
                        }

                        for (;;)
                        {
                            if (!File.Exists(listBox1.Items[currentindex].ToString()))
                            {
                                listBox1.Items.RemoveAt(currentindex);
                                currentindex = currentindex % listBox1.Items.Count;
                                continue;
                            }
                            File.Copy(listBox1.Items[currentindex].ToString(), textBox1.Text, true);
                            currentindex = (currentindex + 1) % listBox1.Items.Count;
                            break;
                        }

                        SetTH155Foreground();
                        Thread.Sleep(500);
                        if (IsMainMenu())
                        {
                            SwitchTo(512).Wait();
                            TH155Addr.VirtualPress(90);
                            Thread.Sleep(300);
                            TH155Addr.VirtualPress(90);
                            Thread.Sleep(300);
                        }
                        TH155Addr.VirtualPress(90);
                        //SendKeys.SendWait("z");
                        Thread.Sleep(2000);
                        executing = false;
                    });
                    
                }
            }
            else if(checkBox2.Checked)
            {
                LaunchTH155();
            }
        }

        private bool IsMainMenu()
        {
            /*
             * 424:network
               512:replay
               
               396:watch
               
               72-88:main menu
               310:network
               350:replay
               
               
               0,0 开始界面
               
               
               -1,-1 输入ip地址
             */
            var cursorstat_x = TH155Addr.TH155GetRTChildInt("menu/cursor/target_x");
            return cursorstat_x >= 72 && cursorstat_x <= 88;
        }

        private bool IsNetwork()
        {
            var cursorstat_x = TH155Addr.TH155GetRTChildInt("menu/cursor/target_x");
            return cursorstat_x == 310 || cursorstat_x == -1;
        }

        //是否在replay选择界面
        private bool IsReplay()
        {
            var cursorstat_x = TH155Addr.TH155GetRTChildInt("menu/cursor/target_x");
            return cursorstat_x == 310;
        }

        private bool IsReplaying()
        {
            var cursorstat_x = TH155Addr.TH155GetRTChildInt("menu/cursor/target_x");
            return cursorstat_x == -100;
        }

        private bool CheckCursor(int value)
        {
            var cursorstat_x = TH155Addr.TH155GetRTChildInt("menu/cursor/target_x");
            return cursorstat_x == value;
        }


        private Task ReturnToGameMenu()
        {
            return Task.Run(() =>
            {
                if (TH155Addr.TH155AddrGetState() < 1)
                    return;
                for (; ; )
                {
                    SetTH155Foreground();
                    if (IsMainMenu())
                        break;
                    var replay_state = TH155Addr.TH155GetRTChildInt("replay/state");
                    var current_y = TH155Addr.TH155GetRTChildInt("menu/cursor/target_y");
                    if (IsReplaying() &&
                        current_y == 216)
                    {
                        TH155Addr.VirtualPress(88);//x
                        Thread.Sleep(1000);
                        SwitchTo(306,()=>
                        {
                            return !CheckCursor(-1);
                        }).Wait();
                        Thread.Sleep(100);
                        TH155Addr.VirtualPress(90);
                        Thread.Sleep(100);
                    }
                    TH155Addr.VirtualPress(88);//x
                    Thread.Sleep(1000);
                }
            });
        }

        private Task SwitchTo(int loc, Func<bool> predice = null)
        {
            return Task.Run(() =>
            {
                if (TH155Addr.TH155AddrGetState() < 1)
                    return;
                
                for (; ; )
                {
                    var cursorstat_y = TH155Addr.TH155GetRTChildInt("menu/cursor/target_y");
                    SetTH155Foreground();
                    if (predice!=null && predice())
                        return;
                    if (loc == cursorstat_y)
                        break;
                    TH155Addr.VirtualPressEx(0x28);//Down
                    Thread.Sleep(100);
                }
            });
        }

        private Task WaitingUntil(Func<bool> predice)
        {
            return Task.Run(() =>
            {
                for (;;)
                {
                    if (predice())
                        return;
                    Thread.Sleep(1000);
                }
            });
        }

        private async void LaunchTH155()
        {
            if (executing)
                return;
            executing = true;
            for(;;)
            {
                Process proc = new Process();
                proc.StartInfo.FileName = "steam://run/716710";
                proc.StartInfo.UseShellExecute = true;
                proc.StartInfo.RedirectStandardOutput = false;
                proc.StartInfo.RedirectStandardError = false;
                proc.Start();
                Thread.Sleep(13000);
                if (TH155Addr.TH155AddrGetState() > 0)
                    break;
            }
            
            //await WaitingUntil(() => TH155Addr.TH155AddrGetState() > 0);
            SetTH155Foreground();
            //TH155Addr.VirtualPress(90);
            //Thread.Sleep(6000);
            await SwitchTo(512);
            Thread.Sleep(200);
            TH155Addr.VirtualPress(90);
            Thread.Sleep(300);
            executing = false;
        }

        private void SetTH155Foreground()
        {
            var hwnd = TH155Addr.FindWindow();
            SetForegroundWindow(hwnd);
        }

        private async void ToNetwork(string IPAddress)
        {
            if (executing)
                return;
            if (TH155Addr.TH155AddrGetState() < 1)
                return;
            executing = true;
            Clipboard.SetText(IPAddress);
            SetTH155Foreground();
            await ReturnToGameMenu();
            await SwitchTo(424);
            TH155Addr.VirtualPress(90);
            await WaitingUntil(IsNetwork);
            await SwitchTo(396,()=>!IsNetwork());// 观战
            if (IsNetwork())
            {
                TH155Addr.VirtualPress(90);
                Thread.Sleep(200);
                TH155Addr.VirtualPress(0x43);
                Thread.Sleep(200);
                TH155Addr.VirtualPress(90);
                Thread.Sleep(5000);
                await WaitingUntil(() =>
                {
                    //var is_watch = TH155Addr.TH155GetRTChildInt("network/is_watch");
                    //var is_disconnect = TH155Addr.TH155GetRTChildInt("network/is_disconnect");
                    return !TH155Addr.TH155IsConnect();
                });
            }

            //Return to replay
            await ReturnToGameMenu();
            //Thread.Sleep(200);
            //await SwitchTo(512);
            //TH155Addr.VirtualPress(90);
            //Thread.Sleep(300);
            executing = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            foreach (var file in Directory.EnumerateFiles(textBox2.Text,"*.rep"))
            {
                listBox1.Items.Insert(0, file);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            ToNetwork(textBox3.Text);
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox3.Checked)
            {
                fsw.Path = textBox4.Text;
                fsw.EnableRaisingEvents = true;
            }
            else
            {
                fsw.EnableRaisingEvents = false;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Task.Run((Action)FetchTenco);
        }
    }
}
