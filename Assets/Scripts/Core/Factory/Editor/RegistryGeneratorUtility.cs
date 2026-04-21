using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CrystalMagic.Editor
{
    public static class RegistryGeneratorUtility
    {
        public static List<Type> CollectTypes(Type baseType, bool subclassOnly)
        {
            var result = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type type in assembly.GetTypes())
                    {
                        if (type.IsAbstract || type.IsInterface)
                        {
                            continue;
                        }

                        bool match = subclassOnly
                            ? type.IsSubclassOf(baseType)
                            : baseType.IsAssignableFrom(type);
                        if (match)
                        {
                            result.Add(type);
                        }
                    }
                }
                catch
                {
                }
            }

            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
            return result;
        }

        public static void WriteFile(string outputPath, string content)
        {
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, content, Encoding.UTF8);
        }

        public static string GetFriendlyTypeName(Type type)
        {
            return type.FullName?.Replace("+", ".") ?? type.Name;
        }
    }
}
