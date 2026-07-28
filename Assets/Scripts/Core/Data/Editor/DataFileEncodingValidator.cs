using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CrystalMagic.Core;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Data
{
    public static class DataFileEncodingValidator
    {
        private const string DataDirectory = "Assets/Res/Data";
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private static readonly Encoding Gbk = Encoding.GetEncoding(
            936,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);

        [MenuItem("Tools/Validate Data Encoding")]
        public static void Validate()
        {
            string[] paths = Directory.GetFiles(DataDirectory, "*.json", SearchOption.AllDirectories);
            List<string> errors = new();

            foreach (string path in paths)
            {
                try
                {
                    string json = DataFileUtility.ReadJsonText(path);
                    ValidateJsonStrings(path, json, errors);
                }
                catch (Exception exception)
                {
                    errors.Add($"{path}: {exception.Message}");
                }
            }

            if (errors.Count == 0)
            {
                Debug.Log($"[DataFileEncodingValidator] Validated {paths.Length} data JSON files.");
                return;
            }

            Debug.LogError($"[DataFileEncodingValidator] Data validation failed:\n{string.Join("\n", errors)}");
        }

        private static void ValidateJsonStrings(string path, string json, List<string> errors)
        {
            JToken root = JToken.Parse(json);
            IEnumerable<JValue> values = root switch
            {
                JValue value => new[] { value },
                JContainer container => container.Descendants().OfType<JValue>(),
                _ => Enumerable.Empty<JValue>()
            };

            foreach (JValue token in values)
            {
                if (token.Type != JTokenType.String || token.Value is not string value || string.IsNullOrEmpty(value))
                    continue;

                if (ContainsPrivateUseCharacter(value))
                {
                    errors.Add($"{path} ({token.Path}) contains a private-use character: {value}");
                    continue;
                }

                if (TryDecodeGbkMojibake(value, out string recovered))
                    errors.Add($"{path} ({token.Path}) may be GBK mojibake: {value} -> {recovered}");
            }
        }

        private static bool TryDecodeGbkMojibake(string value, out string recovered)
        {
            recovered = null;

            try
            {
                recovered = StrictUtf8.GetString(Gbk.GetBytes(value));
                return recovered != value;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool ContainsPrivateUseCharacter(string value)
        {
            foreach (char character in value)
            {
                if (character >= '\uE000' && character <= '\uF8FF')
                    return true;
            }

            return false;
        }
    }
}
