using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glazecs.App.Desktop.Services
{
    public class CultureInfoJsonConverter : JsonConverter<CultureInfo>
    {
        public override CultureInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? cultureName = reader.GetString();

            if (string.IsNullOrEmpty(cultureName))
            {
                return CultureInfo.InvariantCulture;
            }

            try
            {
                return new CultureInfo(cultureName);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.CurrentCulture;
            }
        }

        public override void Write(Utf8JsonWriter writer, CultureInfo value, JsonSerializerOptions options)
        {
            // Сериализуем только имя культуры (например, "ru-RU")
            writer.WriteStringValue(value.Name);
        }
    }
}
