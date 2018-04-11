using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PYHHelper
{
    public partial class Form1 : Form
    {

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public Form1()
        {
            InitializeComponent();
            TH155Addr.TH155AddrStartup(1, this.Handle, TH155Addr.TH155CALLBACK);
        }


        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            TH155Addr.TH155AddrCleanup();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }

        private bool execed = false;
        private void timer1_Tick_1(object sender, EventArgs e)
        {
            int state = TH155Addr.TH155AddrGetState();
            string str;
            label1.Text = "State : " + state;
            if (state >= 1)
            {
                if (execed == false)
                {
                    execed = true;
                    TH155Addr.TH155EnumRTCHild();
                }

                var replay_state = TH155Addr.TH155GetRTChildInt("replay/state");
                label2.Text = "replayStat : " + replay_state;

                if (replay_state != 2 && checkBox1.Checked)
                {
                    Thread.Sleep(1500);
                    
                    File.Copy(listBox1.Items[0].ToString(), textBox1.Text, true);
                    if (listBox1.Items.Count > 1)
                        listBox1.Items.RemoveAt(0);
                    var hwnd = TH155Addr.FindWindow();
                    SetForegroundWindow(hwnd);
                    Thread.Sleep(500);
                    TH155Addr.VirtualPress(90);
                    //SendKeys.SendWait("z");
                    Thread.Sleep(2000);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            foreach (var file in Directory.EnumerateFiles(textBox2.Text,"*.rep"))
            {
                listBox1.Items.Insert(0, file);
            }
        }
    }
}
