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
