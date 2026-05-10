using System;
using Newtonsoft.Json;
using UnityEngine;

namespace CrystalMagic.Core
{
    public sealed class UnityObjectPathJsonConverter : JsonConverter
    {
        private readonly Func<string, UnityEngine.Object> _loadAsset;

        public UnityObjectPathJsonConverter(Func<string, UnityEngine.Object> loadAsset)
        {
            _loadAsset = loadAsset;
        }

        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType)
        {
            return typeof(UnityEngine.Object).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            string path = reader.Value as string;
            if (string.IsNullOrWhiteSpace(path))
                return null;

            UnityEngine.Object asset = _loadAsset?.Invoke(path);
            if (asset == null)
                return null;

            if (objectType.IsInstanceOfType(asset))
                return asset;

            Debug.LogWarning($"[UnityObjectPathJsonConverter] Asset type mismatch. Path={path}, Expected={objectType.Name}, Actual={asset.GetType().Name}");
            return null;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException($"{nameof(UnityObjectPathJsonConverter)} is read-only.");
        }
    }
}
