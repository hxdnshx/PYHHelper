using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace PYHHelper
{
    public partial class Form1 : Form
    {

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("User.dll", EntryPoint = "SendMessage")]

        private static extern int SendMessage(

            IntPtr hWnd,　　　// handle to destination window 

            int Msg,　　　 // message 

            int wParam,　// first message parameter 

            int lParam // second message parameter 

        );

        private FileSystemWatcher fsw;
        private HttpClient _client;
        private HttpClient _client2;
        
        private ReplayRecord _records;
        private List<ReplayTable> _current;
        private string _filterStr;

        void Log(string str)
        {
            File.AppendAllText("run.log", str);
        }

        public Form1()
        {
            rand = new Random();
            InitializeComponent();
            TH155Addr.TH155AddrStartup(1, this.Handle, TH155Addr.TH155CALLBACK);
            fsw = new FileSystemWatcher();
            fsw.Created += OnFileCreated;
            fsw.Filter = "*.rep";
            fsw.IncludeSubdirectories = true;
            fsw.EnableRaisingEvents = false;
            _client2 = new HttpClient();
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

            _records = new ReplayRecord("repRecord.db");
            _records.Database.EnsureCreated();
            _records.Database.Migrate();
        }

        private DateTime _currentDate;

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            FileInfo fi = new FileInfo(e.FullPath);
            DateTime time = DateTime.Now;
            string newPath = textBox2.Text + "\\" + time.Month + "-" + time.Day + "-" + fi.Name;
            Thread.Sleep(2500);
            File.Copy(e.FullPath, newPath, true);
            //listBox1.Items.Insert(currentindex, newPath);
            insertRecord(newPath);
            //currentindex = (currentindex + 1) % listBox1.Items.Count;
        }

        private void insertRecord(string newPath, bool isBatch = false)
        {
            var info = ReplayReader.Open(newPath);
            if (info.Count < 43)
            {
                File.AppendAllText("exception.txt",$"非预期的info大小{info.Count}：{newPath}\n");
                return;
            }
            try
            {
                _records.Replays.Add(new ReplayTable
                {
                    FileName = newPath,
                    P1Name = info[14],
                    P2Name = info[16],
                    P1Master = info[40],
                    P2Master = info[42],
                    P1Slave = info[9],
                    P2Slave = info[11]
                });
                if(!isBatch)
                    _records.SaveChanges();
            }
            catch (Exception e)
            {
                File.AppendAllText("exception.txt", e.ToString());
            }
        }

        void StatusLog(string str)
        {
            File.WriteAllText("status.txt", str);
        }

        private Regex _filterPat = new Regex(@"{([^,]+),([^\}]+)\}");
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x401)
            {
                string str = Clipboard.GetText();
                ToNetwork(str);
                return;
            }
            else if (m.Msg == 0x402)
            {
                string str = Clipboard.GetText().Replace("，",",");
                var results = _filterPat.Matches(str);
                IQueryable<ReplayTable> query = _records.Replays;
                string filterStr = "";
                int filters = 0;
                foreach (Match result in results)
                {
                    string filter = result.Groups[1].Value;
                    string filterValue = result.Groups[2].Value;
                    bool isValid = true;
                    switch (filter)
                    {
                        case "P1":
                        case "P2":
                            query = query.Where(ele => ele.P1Name.Contains(filterValue) || ele.P2Name.Contains(filterValue));
                            break;
                        case "P1主机":
                        case "P2主机":
                            query = query.Where(ele => ele.P2Master == filterValue || ele.P1Master == filterValue);
                            break;
                        case "P1副机":
                        case "P2副机":
                            query = query.Where(ele => ele.P2Slave == filterValue || ele.P1Slave == filterValue);
                            break;
                        default:
                            isValid = false;
                            break;
                    }

                    if (!isValid)
                        continue;
                    filterStr += $"{filter}为{filterValue} ";
                    filters++;
                }

                if (filters == 0)
                {
                    StatusLog("筛选条件为空，清空当前筛选。");
                    _current = new List<ReplayTable>();
                }
                var resultList = new List<ReplayTable>(query.OrderBy(x => Guid.NewGuid()).Take(10));
                if (resultList.Count <= 0)
                {
                    StatusLog($"失败，找不到满足条件的rep:{filterStr}");
                    //_current = new List<ReplayTable>();
                }
                else
                {
                    StatusLog($"成功，随机挑选{resultList.Count}个播放:{filterStr}");
                    _current = resultList;
                    _filterStr = filterStr;
                }
            }
            base.WndProc(ref m);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            TH155Addr.TH155AddrCleanup();
        }

        private Random rand;
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

        protected string HttpGet(string uri, HttpClient client = null)
        {
            var result = (client??_client).GetAsync(uri).Result;
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
            try
            {
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
                        FileStream file = new FileStream(filePath, FileMode.Create);
                        file.Write(finResult, 0, finResult.Length);
                        file.Close();
                        this.Invoke((Action) (() => { insertRecord(filePath); }));

                    }
                }
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }
            finally
            {
                tencoFetch = false;
            }
            _currentDate = DateTime.Now;
        }
        
        private bool executing = false;
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

                var cursorstat_y = TH155Addr.TH155GetRTChildInt("menu/cursor/target_y");
                var cursorstat_x = TH155Addr.TH155GetRTChildInt("menu/cursor/target_x");
                label5.Text = "cursor:" + cursorstat_x + "," + cursorstat_y;

                var replay_state = TH155Addr.TH155GetRTChildInt("replay/state");
                var play_state = TH155Addr.TH155GetRTChildInt("replay/game_mode");
                label2.Text = "replayStat : " + play_state;

                if (play_state == -1 && TH155Addr.TH155AddrGetState() != 0)
                {
                    //无法获取到信息，重启游戏
                    TerminateTH155();
                }

                if (!IsReplaying() && (checkBox1.Checked || _LoadReplay) && !executing)
                {
                    executing = true;
                    Log($"Exec:SwitchReplay");
                    Task.Run(() => {
                        //Thread.Sleep(2500);
                        if (CheckCursor(-1)) //暂停中
                        {
                            SetTH155Foreground();
                            TH155Addr.VirtualPress(88);//x
                            Log($"Exec Finish:SwitchReplay(Pause)");
                            executing = false;
                            return;
                        }

                        string selectedReplay = "";
                        try
                        {
                            if (_current != null && _current.Count > 0)
                            {
                                selectedReplay = _current.Last().FileName;
                                _current.RemoveAt(_current.Count - 1);
                                StatusLog($"正在播放满足条件的Rep，还剩{_current.Count}个:{_filterStr}");
                            }
                            else
                            {
                                selectedReplay = _records.Replays.OrderBy(x => Guid.NewGuid()).Take(1).First().FileName;
                                StatusLog("正在随机播放所有rep");
                            }

                            var info = ReplayReader.Open(selectedReplay);
                            Func<string, string> processStr = input =>
                            {
                                string ret = input.Replace("\\", "").Trim();
                                return ret.Length == 0 ? "无名黑幕" : ret;
                            };
                            var P1Name = processStr(info[14]);
                            var P2Name = processStr(info[16]);
                            File.WriteAllText("P1.txt", P1Name);
                            ApplyAvatar(P1Name, "P1.png");
                            File.WriteAllText("P2.txt", P2Name);
                            ApplyAvatar(P2Name, "P2.png");
                            File.Copy(selectedReplay, textBox1.Text, true);
                        }
                        catch (Exception ex)
                        {
                            File.AppendAllText("exception.txt",ex.ToString());
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
                        Log($"Exec Finish:SwitchReplay");
                        _LoadReplay = false;
                        executing = false;
                    });
                    
                }
            }
            else if(checkBox2.Checked)
            {
                LaunchTH155();
            }
        }

        private static void ApplyAvatar(string playerID, string dstFile)
        {
            string file = $"Avatar/{playerID.Replace("\\", "")}.png";
            if (File.Exists(file))
                File.Copy(file, dstFile, true);
            else
                File.Copy($"Avatar/Default.png", dstFile, true);
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
                    if (TH155Addr.TH155AddrGetState() == 0)
                        return;
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
                    if (TH155Addr.TH155AddrGetState() == 0)
                        return;
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
                    if (predice() || TH155Addr.TH155AddrGetState() == 0)
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
            Log($"Exec: Launch TH155");
            for (;;)
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
            Log($"Exec Finish:LaunchTH155");
            executing = false;
        }

        private void TerminateTH155()
        {
            var hwnd = TH155Addr.FindWindow();
            try
            {
                SendMessage(hwnd, 0x0010, 0, 0); // WM_CLOSE
            }
            catch (Exception e)
            {
                Log(e.ToString());
            }
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
            Log($"Exec: Network Match");
            File.WriteAllText("P1.txt", "***");
            File.WriteAllText("P2.txt", "***");
            File.Copy($"Avatar/Default.png", "P1.png", true);
            File.Copy($"Avatar/Default.png", "P2.png", true);
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
                if (TH155Addr.TH155IsConnect())
                {
                    //network/player_name/0
                    try
                    {
                        var P1 = TH155Addr.TH155GetRTChildStr("network/player_name/0");
                        var P2 = TH155Addr.TH155GetRTChildStr("network/player_name/1");
                        File.WriteAllText("P1.txt", P1);
                        File.WriteAllText("P2.txt", P2);
                        ApplyAvatar(P1, "P1.png");
                        ApplyAvatar(P2, "P2.png");
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText("exception.txt",ex.ToString());
                    }
                }
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
            Log($"Exec Finish:Network Match");
            executing = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            foreach (var file in Directory.EnumerateFiles(textBox2.Text,"*.rep"))
            {
                insertRecord(file, true);
                //listBox1.Items.Insert(0, file);
            }

            _records.SaveChanges();
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

        private void button4_Click(object sender, EventArgs e)
        {
            Clipboard.SetText("{P1,Finn}{P1主机,usami}");
            Message mockMsg = new Message {Msg = 0x402};
            WndProc(ref mockMsg);
            //ReplayReader.Open(listBox1.Items[0].ToString());
            //ReplayReader.ModifyRep(listBox1.Items[0].ToString());
        }

        private bool _LoadReplay = false;

        private void button5_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                //listBox1.Items.Add(openFileDialog1.FileName);
                //currentindex = listBox1.Items.Count - 1;
                _current = new List<ReplayTable>();
                foreach (var file in openFileDialog1.FileNames)
                {
                    _current.Add(new ReplayTable { FileName = file });
                }
                _LoadReplay = true;
                //ReplayReader.Open(openFileDialog1.FileName);
                //ReplayReader.ModifyRep(openFileDialog1.FileName);
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            TH155Addr.TH155EnumRTCHild();
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            var str = HttpGet("http://pyh.saigetsu.moe/api/ImgUpload/List",_client2);
            str = str.Substring(1, str.Length - 2);
            str = str.Replace("\\n", "\n");
            str = str.Replace("\\\"", "\"");
            var obj = JObject.Parse(str);
            foreach (var elements in obj["datas"] as JArray)
            {
                var profileName = elements["name"].Value<string>();
                var imgPath = "http://pyh.saigetsu.moe/" + elements["img"].Value<string>();
                File.AppendAllText("AvaterLog.log", $"{profileName} {imgPath}\n");
                if (!IsValidFilename(profileName))
                    continue;
                try
                {
                    var data = _client2.GetAsync(imgPath).Result.Content.ReadAsByteArrayAsync().Result;
                    File.WriteAllBytes($"Avatar\\{profileName}.png", data);
                }
                catch (Exception ex)
                {
                    File.AppendAllText("AvaterLog.log", ex + "\n");
                }
            }
        }

        //From:https://stackoverflow.com/questions/62771/how-do-i-check-if-a-given-string-is-a-legal-valid-file-name-under-windows
        static Regex containsABadCharacter = new Regex("["
                                                       + Regex.Escape(new string(System.IO.Path.GetInvalidPathChars())) + "]");
        public static bool IsValidFilename(string testName)
        {

            if (containsABadCharacter.IsMatch(testName)) { return false; };

            // other checks for UNC, drive-path format, etc

            return true;
        }

        private void Form1_Load_1(object sender, EventArgs e)
        {
            
        }
    }

    public class ReplayRecord : DbContext
    {
        public DbSet<ReplayTable> Replays { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbpath}");
        }

        public ReplayRecord(string databasePath)
        {
            _dbpath = databasePath;
        }

        public string _dbpath;
    }

    [Table("ReplayRecord")]
    public class ReplayTable
    {
        [Key]
        public string FileName { get; set; }
        public string P1Name { get; set; }
        public string P2Name { get; set; }
        public string P1Master { get; set; }
        public string P2Master { get; set; }
        public string P1Slave { get; set; }
        public string P2Slave { get; set; }
    }
}
