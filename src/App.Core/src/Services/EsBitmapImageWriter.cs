using System.Runtime.Versioning;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.SaveEvidence.App.Core.Services;

/// <summary><see cref="IImageWriter"/> の実装（コアのファイル保存へ委譲する）。</summary>
/// <remarks>
/// <b>保存形式とエンコードは <see cref="EsBitmapBase.Save(string)"/> が単一所有する</b>（拡張子が形式を決め、
/// 未対応の拡張子は <see cref="NotSupportedException"/>。 実装は OS の GDI+ を直接呼ぶ）。<br/>
/// <b>なぜ本プロジェクト（UI 非依存）に置けるのか</b>: 呼び出し元の UI 実装を必要としないため、両 UI で同じ実装を
/// 使える（実装が 1 つになり、保存結果が UI で食い違わない）。 本クラスは Windows 専用の API を呼ぶため
/// <see cref="SupportedOSPlatformAttribute"/> で限定し、利用側（両アプリは <c>net10.0-windows</c>）へ前提を明示する。<br/>
/// <b>契約を残す理由</b>: <see cref="IImageWriter"/>（インターフェイス）は <c>MainViewModel</c> が受け取る抽象であり、
/// テストがモックへ差し替える入口である（本クラスはその唯一の本番実装）。
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class EsBitmapImageWriter : IImageWriter
{
    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">画像またはファイルパスが null の場合。</exception>
    /// <exception cref="ArgumentException">ファイルパスが空白の場合。</exception>
    /// <exception cref="NotSupportedException">拡張子が対応形式でない場合。</exception>
    /// <exception cref="System.Runtime.InteropServices.ExternalException">エンコード（GDI+）が失敗した場合。</exception>
    public void Save(EsBitmapBase image, string filePath)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(filePath);

        image.Save(filePath);
    }
}
