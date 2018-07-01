using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PYHHelper
{
    /*
     * EXPORTS
       TH155AddrStartup
       TH155AddrCleanup
       TH155AddrGetState
       TH155AddrGetParam
     */
    class TH155Addr
    {
        public const int TH155CALLBACK = 0x8000 + 10;

        [DllImport("TH155Addr.dll", EntryPoint = "TH155AddrStartup")]
        public static extern int TH155AddrStartup(int APIVersion, IntPtr hWnd, int callbackMsg);

        [DllImport("TH155Addr.dll", EntryPoint = "TH155AddrCleanup")]
        public static extern int TH155AddrCleanup();
        [DllImport("TH155Addr.dll", EntryPoint = "TH155AddrGetParam")]
        public static extern UInt32 TH155AddrGetParam(int param);
        [DllImport("TH155Addr.dll", EntryPoint = "TH155AddrGetState")]
        public static extern int TH155AddrGetState();
        [DllImport("TH155Addr.dll", EntryPoint = "TH155GetRTChildStr")]
        public static extern int TH155GetRTChildStr(string param, byte[] buffer);//[Out, MarshalAs(UnmanagedType.LPStr)]

        [DllImport("TH155Addr.dll", EntryPoint = "TH155EnumRTCHild")]
        public static extern int TH155EnumRTCHild();


        [DllImport("TH155Addr.dll", EntryPoint = "TH155GetRTChildInt")]
        public static extern int TH155GetRTChildInt(string param);

        [DllImport("TH155Addr.dll", EntryPoint = "TH155FindWindow")]
        public static extern IntPtr FindWindow();

        [DllImport("TH155Addr.dll", EntryPoint = "TH155IsConnect")]
        public static extern bool TH155IsConnect();

        [DllImport("TH155Addr.dll", EntryPoint = "VirtualPress")]
        public static extern void VirtualPress(int id);

        /// <summary>
        /// 专用于上下左右
        /// </summary>
        /// <param name="id"></param>
        [DllImport("TH155Addr.dll", EntryPoint = "VirtualPress_Extra")]
        public static extern void VirtualPressEx(int id);


        public static string TH155GetRTChildStr(string param)
        {
            byte[] buffer = new byte[256];
            TH155GetRTChildStr(param, buffer);
            int i;
            for (i = 0; i < 256; i++)
            {
                if (buffer[i] == '\0')
                    break;
            }
            return Encoding.GetEncoding("Shift_JIS").GetString(buffer.Take(i).ToArray());
        }
        
    }
}
