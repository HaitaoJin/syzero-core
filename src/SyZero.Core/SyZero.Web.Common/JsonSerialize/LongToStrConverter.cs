using Newtonsoft.Json;
using System;
using System.Globalization;

namespace SyZero.Web.Common
{
    /// <summary>
    /// Long转Str
    /// </summary>
    public class LongToStrConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            if (value is ulong unsignedValue)
            {
                writer.WriteValue(unsignedValue.ToString(CultureInfo.InvariantCulture));
                return;
            }

            writer.WriteValue(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return GetDefaultValue(objectType);
            }

            var rawValue = Convert.ToString(reader.Value, CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return GetDefaultValue(objectType);
            }

            var targetType = Nullable.GetUnderlyingType(objectType) ?? objectType;
            if (targetType == typeof(ulong))
            {
                if (ulong.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ulongValue))
                {
                    return ulongValue;
                }

                throw new JsonSerializationException($"Cannot convert value '{rawValue}' to UInt64.");
            }

            if (long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
            {
                return longValue;
            }

            throw new JsonSerializationException($"Cannot convert value '{rawValue}' to Int64.");
        }

        public override bool CanConvert(Type objectType)
        {
            if (objectType == typeof(Int64?) || objectType == typeof(Int64) || objectType == typeof(UInt64?) || objectType == typeof(UInt64))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static object GetDefaultValue(Type objectType)
        {
            var targetType = Nullable.GetUnderlyingType(objectType);
            if (targetType != null)
            {
                return null;
            }

            return objectType == typeof(ulong) ? 0UL : 0L;
        }
    }
}
