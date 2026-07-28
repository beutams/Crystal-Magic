using System;
using System.IO;
using System.Text;

namespace CrystalMagic.Core
{
    public static class DataFileUtility
    {
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private static readonly UTF8Encoding Utf8WithBom = new(true, true);

        public static string ReadJsonText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (!HasUtf8Bom(bytes))
                throw new InvalidDataException($"Data JSON must use UTF-8 BOM: {path}");

            return StrictUtf8.GetString(bytes, 3, bytes.Length - 3);
        }

        public static void WriteJsonText(string path, string json)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, json, Utf8WithBom);
        }

        public static bool HasUtf8Bom(byte[] bytes)
        {
            return bytes != null
                && bytes.Length >= 3
                && bytes[0] == 0xEF
                && bytes[1] == 0xBB
                && bytes[2] == 0xBF;
        }
    }
}
