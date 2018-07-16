using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using PYHHelper;

namespace ReplayRename
{
    public partial class Form1 : Form
    {
        private const string SettingFile = "setting.txt";
        public Form1()
        {
            InitializeComponent();
            if (File.Exists(SettingFile))
            {
                var data = File.ReadAllLines(SettingFile);
                textBox1.Text = data[0];
                folderBrowserDialog1.SelectedPath = data[0];
                textBox2.Text = data[1];
            }
        }

        private Dictionary<string, int> propIndex = new Dictionary<string, int>()
        {
            {"分", 1},
            {"P1副机", 9},
            {"P2副机", 11},
            {"P1", 14},
            {"P2", 16},
            {"时", 37},
            {"P1主机", 40},
            {"P2主机", 42},
            {"日", 44},
            {"年", 46},
            {"秒", 52},
            {"月", 57},
        };
        private void button1_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                textBox1.Text = folderBrowserDialog1.SelectedPath;
        }
        Regex pattern = new Regex("\\{([^\\}]+)\\}");

        private void Log(string txt)
        {
            listBox1.Items.Insert(0,txt);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            var settingstr = $"{textBox1.Text}\n{textBox2.Text}";
            File.WriteAllText(SettingFile,settingstr);
            var task = new Task(() =>
            {
                foreach (var file in Directory.EnumerateFiles(textBox1.Text, "*.rep"))
                {
                    var prop = ReplayReader.Open(file);
                    this.Invoke((Action<string>)Log, $"--{file}");
                    var fi = new FileInfo(file);
                    var result = fi.DirectoryName + "\\" + textBox2.Text;
                    var results = pattern.Matches(result);
                    if (results.Count > 0)
                    {
                        foreach (Match mat in results)
                        {
                            if (!propIndex.ContainsKey(mat.Groups[1].Value))
                            {
                                this.Invoke((Action<string>)Log, $"无效的匹配字段{mat.Groups[1].Value}，结束重命名过程");
                                return;
                            }

                            int index = propIndex[mat.Groups[1].Value];
                            string val = prop[index].Replace("\\", "");
                            if (index == 14 || index == 16)
                            {
                                var bytes = Encoding.GetEncoding("Shift_JIS").GetBytes(val);
                                val = Encoding.Default.GetString(bytes);
                            }

                            result = result.Replace(mat.Groups[0].Value, val);
                        }

                        try
                        {
                            File.Move(file, result);
                            this.Invoke((Action<string>)Log, $"重命名为{result}");
                        }
                        catch (Exception ex)
                        {
                            this.Invoke((Action<string>)Log, "重命名失败！");
                            foreach (var str in ex.ToString().Split('\n'))
                            {
                                this.Invoke((Action<string>) Log, str);
                            }
                        }
                    }
                    else
                    {
                        this.Invoke((Action<string>)Log, $"未找到匹配字段，结束重命名过程");
                        return;
                    }
                }

                this.Invoke((Action<string>)Log, "----重命名结束！");
            });
            task.Start();
        }
    }
}
