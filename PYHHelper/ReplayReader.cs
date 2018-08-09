using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;

namespace PYHHelper
{
    class ReplayReader
    {
        public static Dictionary<string, int> PropIndex = new Dictionary<string, int>()
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

        public static int Switch12P(int index)
        {
            if (index == 14) return 16;
            if (index == 16) return 14;
            if (index == 9) return 11;
            if (index == 11) return 9;
            if (index == 40) return 42;
            if (index == 42) return 40;
            return index;
        }

        private static byte[] PacketBegin = new byte[] {0x10, 0x00, 0x00, 0x08};
        private static byte[] IntBegin = new byte[] {0x02, 0x00, 0x00, 0x05};
        static byte[] Slice(byte[] arr, int offset, int length)
        {
            return arr.Skip(offset).Take(length).ToArray();
        }
        public static List<string> Open(string fileName)
        {
            byte[] rep = File.ReadAllBytes(fileName);
            Int32 metaLength = BitConverter.ToInt32(Slice(rep, 13, 4), 0);

            byte[] CompressedMetaData = Slice(rep, 21, metaLength);
            byte[] RestData = Slice(rep, 21 + metaLength, rep.Length - 21 + metaLength);
            //File.WriteAllBytes("E:\\restData.txt", RestData);

            byte[] MetaData = Inflate(CompressedMetaData);

            var Names = new List<string>();
            for (int i = 0; i < MetaData.Length; i++)
            {
                if (Slice(MetaData, i, 4).SequenceEqual(PacketBegin))
                {
                    int nameLength = BitConverter.ToInt32(Slice(MetaData, i + 4, 4), 0);
                    byte[] packetNameArray = Slice(MetaData, i + 8, nameLength);
                    string packetName = Encoding.GetEncoding("Shift_JIS").GetString(packetNameArray);
                    Names.Add(packetName);
                    i += 8;
                    i += nameLength;
                    i--;
                }
                else if(Slice(MetaData, i, 4).SequenceEqual(IntBegin))// Int
                {
                    int value = BitConverter.ToInt32(Slice(MetaData, i + 4, 4), 0);
                    Names.Add(value.ToString());
                    i += 8;
                    i--;
                }
            }

            return Names;
        }

        public static void ModifyRep(string fileName)
        {
            byte[] rep = File.ReadAllBytes(fileName);
            Int32 metaLength = BitConverter.ToInt32(Slice(rep, 13, 4), 0);

            byte[] CompressedMetaData = Slice(rep, 21, metaLength);
            byte[] RestData = Slice(rep, 21 + metaLength, rep.Length - 21 + metaLength);
            //File.WriteAllBytes("E:\\restData.txt", RestData);

            byte[] MetaData = Inflate(CompressedMetaData);
            MetaData = SetValue(MetaData,"slave_name",1,"usami");
            MetaData = SetValue(MetaData, "slave_name", 0, "usami");

            var compressedMeta = Ionic.Zlib.ZlibStream.CompressBuffer(MetaData);

            List<byte> ret = new List<byte>();
            ret.AddRange(Slice(rep, 0, 9));
            byte[] placeholder = {0, 0, 0, 0};
            byte[] length_8 = BitConverter.GetBytes(compressedMeta.Length + 8);
            byte[] length = BitConverter.GetBytes(compressedMeta.Length);
            byte[] uncompresslength = BitConverter.GetBytes(MetaData.Length);
            ret.AddRange(length_8);
            ret.AddRange(length);
            ret.AddRange(uncompresslength);
            ret.AddRange(compressedMeta);
            ret.AddRange(RestData);
            File.WriteAllBytes(fileName + ".mod.rep",ret.ToArray());
            //File.Open(fileName + ".mod.rep")
        }

        private static byte[] SetValue(byte[] MetaData, string prop , int index, string value)
        {
            bool found = false;
            bool found_index = false;
            for (int i = 0; i < MetaData.Length; i++)
            {
                if (Slice(MetaData, i, 4).SequenceEqual(PacketBegin))
                {
                    int nameLength = BitConverter.ToInt32(Slice(MetaData, i + 4, 4), 0);
                    byte[] packetNameArray = Slice(MetaData, i + 8, nameLength);
                    string packetName = Encoding.GetEncoding("Shift_JIS").GetString(packetNameArray);
                    if (packetName == prop)
                        found = true;
                    if (found_index)
                    {
                        List<byte> ret = new List<byte>();
                        ret.AddRange(Slice(MetaData,0,i));
                        byte[] str = Encoding.GetEncoding("Shift_JIS").GetBytes(value);
                        byte[] length = BitConverter.GetBytes(str.Length);
                        //byte[] placeholder = {0, 0, 0, 0};
                        ret.AddRange(PacketBegin);
                        ret.AddRange(length);
                        ret.AddRange(str);
                        ret.AddRange(MetaData.Skip(i + 8 + nameLength));
                        return ret.ToArray();
                    }
                    i += 8;
                    i += nameLength;
                    i--;
                }
                else if (Slice(MetaData, i, 4).SequenceEqual(IntBegin))// Int
                {
                    if (found)
                    {
                        int index_value = BitConverter.ToInt32(Slice(MetaData, i + 4, 4), 0);
                        if (index_value == index)
                            found_index = true;
                        i += 8;
                        i--;
                    }
                }
            }

            return MetaData;
        }

        private static byte[] Inflate(byte[] data)
        {
            MemoryStream outStream = new MemoryStream();
            MemoryStream stream = new MemoryStream();
            var ds = new DeflateStream(stream, CompressionMode.Decompress);
            stream.Write(data, 2, data.Length - 6);
            stream.Seek(0, SeekOrigin.Begin);
            ds.CopyTo(outStream);
            return outStream.ToArray();
        }
    }
}
