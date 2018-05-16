using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace ProfileUploader
{
    public static class ConfigHelper
    {
        /// <summary>
        /// 加载,或者新建一个对应的数据结构
        /// </summary>
        /// <typeparam name="T">要从Xml中加载的数据结构</typeparam>
        /// <param name="dt"></param>
        /// <param name="Path"></param>
        /// <returns></returns>
        public static T LoadOrCreate<T>(this object dt, string Path) where T : new()
        {
            T ret;
            DataContractSerializer serializer = new DataContractSerializer(typeof(T));
            if (File.Exists(Path))
            {
                FileStream stream = new FileStream(Path, FileMode.Open, FileAccess.Read);
                ret = (T)serializer.ReadObject(stream);

                stream.Close();
            }
            else
            {
                ret = new T();
            }
            return ret;
        }
        //https://stackoverflow.com/questions/1879395/how-to-generate-a-stream-from-a-string
        public static Stream GenerateStreamFromString(string s)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
        /// <summary>
        /// 保存数据结构到文件(同时输出xml跟json格式)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dt"></param>
        /// <param name="Path"></param>
        /// <param name="list"></param>
        public static void SaveData<T>(this object dt, string Path, T list)
        {
            DataContractSerializer serializer = new DataContractSerializer(typeof(T));
            try
            {
                var settings = new XmlWriterSettings { Indent = true };

                using (var w = XmlWriter.Create(Path, settings))
                {
                    serializer.WriteObject(w, list);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public static string MixPath(this string realPath, string relativePath)
        {
            bool endWithDiv = (realPath.Last() == '/' || realPath.Last() == '\\');
            bool startWithDiv = (relativePath.First() == '/' || relativePath.First() == '\\');
            if (endWithDiv && startWithDiv)
            {
                realPath += "." + relativePath;
            }
            else if (!startWithDiv && !endWithDiv)
                realPath += "/" + relativePath;
            else
                realPath += relativePath;
            return realPath;
        }
    }
}
