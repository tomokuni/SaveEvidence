using System.Text.Json;
using System.Text.Json.Serialization;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary><see cref="EsRectInt"/> の JSON コンバーター（読み取り専用プロパティ対応）。</summary>
/// <remarks>
/// <see cref="EsRectInt"/> は読み取り専用プロパティ（<see cref="EsRectInt.Right"/> 等）を持つため、<br/>
/// 既定のシリアライザーでは書き込み時に不要なプロパティが混入し、読み込み時にコンストラクター引数へ束縛できない。<br/>
/// 本コンバーターは <c>X</c> / <c>Y</c> / <c>Width</c> / <c>Height</c> のみを読み書きする<br/>
/// （<c>X</c> / <c>Y</c> / <c>Width</c> / <c>Height</c> のみを読み書きするため、既存の設定ファイルをそのまま読み込める）。<br/>
/// </remarks>
internal sealed class EsRectIntJsonConverter : JsonConverter<EsRectInt>
{
    /// <summary><see cref="EsRectInt"/> を JSON から読み取る。</summary>
    /// <param name="reader">JSON リーダー。</param>
    /// <param name="typeToConvert">変換対象の型。</param>
    /// <param name="options">シリアライズ オプション。</param>
    /// <returns>読み取った矩形。 欠落したプロパティは 0。</returns>
    public override EsRectInt Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
        return new EsRectInt(x, y, w, h);
    }

    /// <summary><see cref="EsRectInt"/> を JSON へ書き込む。</summary>
    /// <param name="writer">JSON ライター。</param>
    /// <param name="value">書き込む矩形。</param>
    /// <param name="options">シリアライズ オプション。</param>
    public override void Write(Utf8JsonWriter writer, EsRectInt value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Width", value.Width);
        writer.WriteNumber("Height", value.Height);
        writer.WriteEndObject();
    }
}
