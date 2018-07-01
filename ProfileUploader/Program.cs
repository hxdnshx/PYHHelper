using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using ProfileUploader;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;

namespace ProfileUploader
{
    [DataContract]
    class SettingData
    {
        [DataMember]
        public string Username { get; set; }
        [DataMember]
        public string Password { get; set; }
        [DataMember]
        public string TrackFilePath { get; set; }
        [DataMember]
        public string PYHPath { get; set; }
        [DataMember]
        public bool IsUploadReplay { get; set; }

        public SettingData()
        {
            TrackFilePath = "..\\Default.db";
            Username = "";
            Password = "";
            PYHPath = "..\\..\\";
            IsUploadReplay = false;
        }
    }

    public static class StringHelper
    {
        private static Regex replacepattern = new Regex("\r\n *");
        public static string RemoveSpacing(this string str)
        {
            return replacepattern.Replace(str, "");
        }
    }

    class Program
    {
        static Regex pattern_uname = new Regex("\\A[a-zA-Z0-9_]{1,32}\\Z");
        static Regex pattern_pword = new Regex("\\A[\\x01-\\x7F]{8，255}\\z");
        static Regex pattern_mail = new Regex("\\A[\\x01-\\x7F]+@(([-a-z0-9]+\\.)*[a-z]+|\\[\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}\\])\\z");
        private static string SettingPath = "setting.xml";
        private static Encoding shift_jis;

        static string Hash(string input)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(hash.Length * 2);

                foreach (byte b in hash)
                {
                    // can be "x2" if you want lowercase
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }

        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            shift_jis = Encoding.GetEncoding("shift-jis");
            SettingData setting = ConfigHelper.LoadOrCreate<SettingData>(null, SettingPath);
            HttpClient _client = new HttpClient();

            _client.DefaultRequestHeaders.Add("User-Agent", "Finnite / ProfileUploader");
            //_client.DefaultRequestHeaders.Add("Content-Type", "application /x-www-form-urlencoded");
            _client.DefaultRequestHeaders.Add("Host", "tenco.info");
            _client.DefaultRequestHeaders.Add("Accept", "*/*");

            Func<string, HttpContent, string> HttpPost = (uri, content) =>
            {
                if(!(content is MultipartFormDataContent))
                    content.Headers.Add("Content-Type", "application /x-www-form-urlencoded");
                var result = _client.PostAsync(uri, content).Result;
                if (result.Content.Headers.ContentEncoding.Contains("gzip"))
                {
                    var rawResult = result.Content.ReadAsByteArrayAsync().Result;
                    var finResult = GZipHelper.Decompress_GZip(rawResult);
                    return Encoding.UTF8.GetString(finResult);
                }

                return ((int)result.StatusCode).ToString() + " - " + result.Content.ReadAsStringAsync().Result;
            };
            Func<string, string> HttpGet = uri =>
            {
                var result = _client.GetAsync(uri).Result;
                if (result.StatusCode != HttpStatusCode.OK)
                    return "Error";
                if (result.Content.Headers.ContentEncoding.Contains("gzip"))
                {
                    var rawResult = result.Content.ReadAsByteArrayAsync().Result;
                    var finResult = GZipHelper.Decompress_GZip(rawResult);
                    return Encoding.UTF8.GetString(finResult);
                }

                return result.Content.ReadAsStringAsync().Result;
            };

            if (setting.Username == "")
            {
                Console.WriteLine("账号设定（初次启动时）");
                Console.WriteLine("1.注册账户");
                Console.WriteLine("2.登录");
                Console.Write("输入数字进行选择（默认1）>");
                var sel_1 = Console.ReadKey(true);
                Console.WriteLine("");
                bool isLogin = sel_1.KeyChar == '2';
                if (isLogin)
                {
                    Console.WriteLine("==登录==");
                    string username, password;
                    InputAccountInfo(out username,out password);
                    setting.Username = username;
                    setting.Password = Hash(password);
                    ConfigHelper.SaveData(null, SettingPath, setting);
                    Console.WriteLine("您的账户信息已保存，将在上传记录时进行验证。");
                }
                else
                {
                    Console.WriteLine("==注册==");
                    string username, password,mail="";
                    InputAccountInfo(out username, out password);
                    for (; ; )
                    {
                        Console.WriteLine("请输入有效的邮箱");
                        Console.WriteLine("在当您忘记密码时，可以通过这个邮箱进行找回。");
                        Console.Write("邮箱（直接回车跳过）>");
                        string responseStr = Console.ReadLine() ?? "";
                        if (responseStr == "")
                        {
                            Console.WriteLine("跳过邮箱输入。");
                            break;
                        }
                        if (!pattern_mail.IsMatch(responseStr))
                        {
                            Console.WriteLine("无效的邮箱地址，请重新输入。");
                        }
                        else
                        {
                            mail = responseStr;
                            break;
                        }
                    }
                    setting.Username = username;
                    setting.Password = Hash(password);
                    /*
                     * <account>
                       <name>Finnite</name>
                       <password>明文密码</password>
                       <mail_address>Finnite@outlook.com</mail_address>
                       </account>
                     */
                    XDocument doc = new XDocument(
                        new XElement("account",
                            new XElement("name",username),
                            new XElement("password",password),
                            new XElement("mail_address",mail)));
                    var upData = doc.ToString(SaveOptions.OmitDuplicateNamespaces).RemoveSpacing();
                    {
                        var response = HttpPost("http://tenco.info/api/account.cgi",
                            new ByteArrayContent(Encoding.UTF8.GetBytes(upData)));
                        if (!response.StartsWith("200"))
                        {
                            Console.WriteLine("注册出现错误，请重新运行该程序进行注册");
                            Console.WriteLine("将在5s后自动关闭");
                            Console.WriteLine(response);
                            Thread.Sleep(5000);
                            return;
                        }
                        else
                        {
                            Console.WriteLine("注册成功，服务器返回了查看您成绩的相关地址，请查看。");
                            Console.WriteLine(response);
                            ConfigHelper.SaveData(null, SettingPath, setting);
                        }
                    }
                }

                {
                    Console.WriteLine("请选择是否上传录像文件");
                    Console.WriteLine("1.上传");
                    Console.WriteLine("2.不上传");
                    Console.Write("输入数字进行选择（默认1）>");
                    var sel_2 = Console.ReadKey(true);
                    Console.WriteLine("");
                    if (sel_2.KeyChar != '2')
                    {
                        Console.WriteLine("上传Replay。");
                        setting.IsUploadReplay = true;
                    }
                    else
                    {
                        Console.WriteLine("不上传Replay。");
                        setting.IsUploadReplay = false;
                    }

                    ConfigHelper.SaveData(null, SettingPath, setting);
                }
            }

            ProfileData data = null;
            for (;;)
            {
                if (File.Exists(setting.TrackFilePath))
                {
                    data = new ProfileData(setting.TrackFilePath);
                    break;
                }
                else
                {
                    Console.WriteLine("==对战记录文件配置==");
                    Console.WriteLine($"无效的对战记录文件路径({setting.TrackFilePath})");
                    Console.WriteLine("请将您的对战记录文件(如Default.db)拖到到这个窗口中，然后按下回车。");
                    Console.Write("路径>");
                    var path = Console.ReadLine() ?? "";
                    path = path.Replace("\"", "");
                    setting.TrackFilePath = path;
                    ConfigHelper.SaveData(null, SettingPath, setting);
                }
            }

            for (;;)
            {
                if (setting.IsUploadReplay == false)
                    break;
                if (File.Exists(setting.PYHPath.MixPath("th155.exe")))
                    break;
                Console.WriteLine("==凭依华路径配置==");
                Console.WriteLine($"无效的凭依华游戏路径({setting.PYHPath})");
                Console.WriteLine("请将您的凭依华游戏(th155.exe)拖到到这个窗口中，然后按下回车。");
                Console.Write("路径>");
                var path = Console.ReadLine() ?? "";
                path = path.Replace("\"", "");
                if (File.Exists(path))
                {
                    setting.PYHPath = new FileInfo(path).DirectoryName;
                    ConfigHelper.SaveData(null, SettingPath, setting);
                }
            }

            DateTime lastUpload = DateHelper.FromUnixTime(0);
            {
                Console.WriteLine("==获取最近上传记录==");
                var resp = HttpGet(
                    $"http://tenco.info/api/last_track_record.cgi?game_id=7&account_name={setting.Username}");
                if (resp == "Error")
                {
                    Console.WriteLine("获取发生错误，请重新启动程序进行操作。");
                    Console.WriteLine("将在5s后自动关闭");
                    Thread.Sleep(5000);
                    return;
                }

                try
                {
                    lastUpload = DateTime.Parse(resp);
                }
                catch (Exception)
                {
                    lastUpload = DateHelper.FromUnixTime(0);
                }
            }
            Console.WriteLine($"将从 {lastUpload} 的记录开始上传。");
            {
                lastUpload += TimeZoneInfo.Local.BaseUtcOffset;
                lastUpload += TimeSpan.FromSeconds(10);
                var startTime = lastUpload.ToFileTime();
                var result = data.Profile.Where(e => e.Timestamp > startTime).ToList();
                if(result.Count > 0)
                {
                    Console.WriteLine("==上传记录==");

                    Console.WriteLine($"共 {result.Count} 条");
                    int batchCount = 0;
                    XElement game = new XElement("game");
                    game.Add(new XElement("id", 7));
                    XElement force = new XElement("is_force_insert", true);
                    XDocument doc =
                        new XDocument(
                            new XElement("trackrecordPosting",
                                new XElement("account",
                                    new XElement("name", setting.Username),
                                    new XElement("password", setting.Password)),
                                game));
                    bool isForce = false;
                    int max_count = 250;
                    for (int i = 0; i <= result.Count; i++)
                    {
                        if (batchCount >= max_count || i >= result.Count)
                        {
                            string sendStr = "<?xml version='1.0' encoding='UTF-8'?>" + doc.ToString(SaveOptions.OmitDuplicateNamespaces).RemoveSpacing();
                            var response = HttpPost("http://tenco.info/api/track_record.cgi",
                                new ByteArrayContent(Encoding.UTF8.GetBytes(sendStr)));
                            if (response.StartsWith("401"))
                            {
                                Console.WriteLine("无效的账户信息，请重新启动程序输入新的账户信息。");
                                setting.Username = "";
                                ConfigHelper.SaveData(null, SettingPath, setting);
                                Console.WriteLine("将在5s后自动关闭");
                                Thread.Sleep(5000);
                                return;
                            }
                            else if (response.StartsWith("400"))
                            {
                                Console.WriteLine("有重复的数据，启动强制上传模式。");
                                doc.Element("trackrecordPosting").Add(force);
                                if (isForce)
                                {
                                    i -= batchCount;
                                    max_count = 1;
                                    Console.WriteLine("强制上传失败，重新一条一条传> <");
                                    isForce = false;
                                    i--;
                                    game.RemoveAll();
                                    game.Add(new XElement("id", 7));
                                    batchCount = 0;
                                    continue;
                                }
                                isForce = true;
                                i--;
                                continue;
                            }
                            else if (response.StartsWith("200"))
                            {
                                isForce = false;
                                Console.WriteLine($"第{Math.Max(batchCount - 250, 0)} - {i} 条记录上传完毕");
                                batchCount = 0;
                                game.RemoveAll();
                                game.Add(new XElement("id", 7));
                                if(force.Parent != null)
                                    force.Remove();
                                if (i == result.Count)
                                    break;
                            }
                            else
                            {
                                Console.WriteLine("发生未指定的错误，请重新运行该程序进行上传");
                                Console.WriteLine("将在5s后自动关闭");
                                Console.WriteLine(response);
                                Thread.Sleep(5000);
                                return;
                            }
                        }

                        var ctx = result[i];
                        //2018-05-01T14:47:43+08:00
                        var currTime = DateTime.FromFileTime(ctx.Timestamp);
                        currTime -= TimeZoneInfo.Local.BaseUtcOffset;
                        Func<byte[], string> Convert = s =>
                        {
                            //var refStr = shift_jis.GetBytes("想起_Inf");
                            //var outStr = Encoding.Default.GetString(refStr);
                            //var srcBytes = Encoding.Default.GetBytes(outStr);
                            //var ret = shift_jis.GetString(srcBytes);
                            var ret = shift_jis.GetString(s);
                            return ret;
                        };

                        var p1Name = Convert(ctx.P1Name).Replace("\u0016","");
                        var p2Name = Convert(ctx.P2Name).Replace("\u0016", "");
                        game.Add(
                            new XElement("trackrecord",
                                new XElement("timestamp",
                                    currTime.ToString("yyyy-MM-dd") + "T" +
                                    currTime.ToString("HH:mm:sszzz")),
                                new XElement("p1name", p1Name),
                                new XElement("p1type", ctx.P1ID),
                                new XElement("p1point", ctx.P1Win),
                                new XElement("p2name", p2Name),
                                new XElement("p2type", ctx.P2ID),
                                new XElement("p2point", ctx.P2Win)));
                        batchCount++;
                    }
                }
                else
                {
                    Console.WriteLine("没有需要上传的记录");
                }
                Console.WriteLine("上传过程成功结束。");

                if (setting.IsUploadReplay && result.Count > 0)
                {
                    Console.WriteLine("==上传replay==");
                    XElement game = new XElement("game");
                    game.Add(new XElement("id", 7));
                    XDocument doc =
                        new XDocument(
                            new XElement("replayPosting",
                                new XElement("account",
                                    new XElement("name", setting.Username),
                                    new XElement("password", setting.Password)),
                                game));
                    var replayFilePath = setting.PYHPath.MixPath("replay");
                    var rnd = new Random();
                    foreach (var ctx in result)
                    {
                        var currTime = DateTime.FromFileTime(ctx.Timestamp);
                        currTime -= TimeZoneInfo.Local.BaseUtcOffset;
                        string replayFile = null;
                        currTime = currTime - TimeSpan.FromSeconds(10);
                        for (int j = 0; j < 20; j++)
                        {
                            currTime += TimeSpan.FromSeconds(1);
                            string testFile = replayFilePath.MixPath($"{currTime:yyMMdd}/replay_{currTime:HHmmss}.rep");
                            if (File.Exists(testFile))
                                replayFile = testFile;
                        }
                        if (replayFile != null)
                        {
                            currTime = DateTime.FromFileTime(ctx.Timestamp);
                            currTime -= TimeZoneInfo.Local.BaseUtcOffset;
                            game.Add(
                                new XElement("trackrecord",
                                    new XElement("timestamp",
                                        currTime.ToString("yyyy-MM-dd") + "T" +
                                        currTime.ToString("HH:mm:sszzz")),
                                    new XElement("p1name", ctx.P1Name),
                                    new XElement("p1type", ctx.P1ID),
                                    new XElement("p1point", ctx.P1Win),
                                    new XElement("p2name", ctx.P2Name),
                                    new XElement("p2type", ctx.P2ID),
                                    new XElement("p2point", ctx.P2Win)));
                            string sendStr = "<?xml version='1.0' encoding='UTF-8'?>" + doc.ToString(SaveOptions.OmitDuplicateNamespaces).RemoveSpacing();
                            var form = new MultipartFormDataContent(rnd.Next(10000,99999).ToString() + rnd.Next(10000,99999).ToString());
                            form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(sendStr)),"meta_info");
                            form.Add(new ByteArrayContent(File.ReadAllBytes(replayFile)), "replay_file");
                            game.RemoveAll();
                            game.Add(new XElement("id", 7));
                            var response = HttpPost("http://tenco.info/api/replay_upload.cgi", form);
                            Console.WriteLine($"上传 {replayFile}，返回：");
                            Console.WriteLine(response);
                        }
                        else
                        {
                            Console.WriteLine($"{replayFile}不存在，跳过");
                        }
                    }

                    Console.WriteLine("Replay上传结束");
                }

                Console.WriteLine("所有战绩上传过程结束，将在3秒后自动关闭。");
                Thread.Sleep(3000);
            }
        }

        private static void InputAccountInfo(out string username,out string password)
        {
            for (;;)
            {
                Console.WriteLine("请输入用户名");
                Console.WriteLine("要求：1-32长度，半角大小写字母以及数字下划线_可用");
                Console.Write("用户名>");
                string str = Console.ReadLine() ?? "";
                if (!pattern_uname.IsMatch(str))
                {
                    Console.WriteLine("无效的用户名，请重新输入");
                }
                else
                {
                    username = str;
                    break;
                }
            }

            for (;;)
            {
                Console.WriteLine("请输入密码");
                Console.WriteLine("要求：8-255长度，半角大小写字母以及数字符号可用");
                Console.Write("密码>");
                string str = Console.ReadLine() ?? "";
                if (!pattern_uname.IsMatch(str))
                {
                    Console.WriteLine("无效的密码，请重新输入");
                }
                else
                {
                    password = str;
                    break;
                }
            }
        }
    }
}
