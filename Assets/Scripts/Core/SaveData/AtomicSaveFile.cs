using System;
using System.IO;
using System.Text;

namespace CrystalMagic.Core
{
    public static class AtomicSaveFile
    {
        public static void Write(string path, string content)
        {
            string fullPath = Path.GetFullPath(path);
            string temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using StreamWriter writer = new(stream, new UTF8Encoding(false), 4096, true);
                    writer.Write(content);
                    writer.Flush();
                    stream.Flush(true);
                }
                if (File.Exists(fullPath))
                    File.Replace(temporary, fullPath, Path.ChangeExtension(fullPath, ".backup.json"));
                else File.Move(temporary, fullPath);
            }
            finally
            {
                // 仅删除本次写入的唯一临时文件；绝不先删除旧存档再覆盖。
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
