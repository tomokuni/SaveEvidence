using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;

namespace app.Services;

/// <summary><see cref="PixelRect"/> の JSON コンバーター（読み取り専用プロパティ対応）。</summary>
internal sealed class PixelRectConverter : JsonConverter<PixelRect>
{
    public override PixelRect Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        int x = 0, y = 0, w = 0, h = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var prop = reader.GetString();
                reader.Read();
                switch (prop)
                {
                    case "X": x = reader.GetInt32(); break;
                    case "Y": y = reader.GetInt32(); break;
                    case "Width": w = reader.GetInt32(); break;
                    case "Height": h = reader.GetInt32(); break;
                }
            }
        }
        return new PixelRect(x, y, w, h);
    }

    public override void Write(Utf8JsonWriter writer, PixelRect value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Width", value.Width);
        writer.WriteNumber("Height", value.Height);
        writer.WriteEndObject();
    }
}
