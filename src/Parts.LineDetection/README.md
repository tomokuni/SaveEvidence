# Parts.LineDetection

## 目次

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. システム要件](#3-システム要件)
- [4. インストール](#4-インストール)
- [5. クイックスタート](#5-クイックスタート)
  - [5.1 ViewModel](#51-viewmodel)
  - [5.2 検出結果](#52-検出結果)
- [6. サンプルアプリケーション](#6-サンプルアプリケーション)
- [7. ドキュメント](#7-ドキュメント)
- [8. プロジェクト構成](#8-プロジェクト構成)
- [9. ライセンス](#9-ライセンス)

---

## 1. 概要

Avalonia UI 用の**ウィンドウ枠となる辺（線分）の検出ライブラリ**です。 キャプチャ画像（スクリーンショット等）から、Windows のウィンドウ枠となる水平・垂直の辺を検出します。

- **ロジック方式（Canny + Hough）**: モデル不要の画像処理方式。 Canny エッジ + HoughLinesP で水平・垂直の辺を検出し、矩形（ウィンドウの 4 辺）を優先出力します。

検出器は共通インターフェース `ILineDetector` で抽象化されるため、方式を差し替えて利用できます。

## 2. 機能一覧

- `ILineDetector` を実装した検出器（ロジック方式）を提供
- 水平・垂直の辺（線分）を `LineDetectionResult` として返却
- `EsBitmap`（BGRA）を入力とし、画像のピクセルを解析
- ロジック方式:
  - Canny エッジ + HoughLinesP による水平・垂直の辺検出
  - 同一直線上の断片をクラスタリングして長い辺へ統合
  - 近接する平行線（透過枠で分裂した枠線等）の統合
  - 文字列（断続エッジ）の誤検出を除外（エッジ支持率）
  - 矩形（ウィンドウの 4 辺）を優先出力（対辺同長・コーナー近接）

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- OpenCvSharp5（ロジック方式・内部利用）
  - OpenCvSharp5.runtime.win.slim（ネイティブ。 `videoio` を除く縮小構成。 動画入出力を使わないため `opencv_videoio_ffmpeg500_64.dll` 約 31 MB を持ち込まない）

## 4. インストール

`src/Parts.LineDetection/src/Parts.LineDetection.csproj` をプロジェクト参照に追加します。

```xml
<ProjectReference Include="..\Parts.LineDetection\src\Parts.LineDetection.csproj" />
```

## 5. クイックスタート

### 5.1 ViewModel

```csharp
using EsUtil.SaveEvidence.Parts.LineDetection;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;

ILineDetector detector = new LogicLineDetector();

// 検出器は EsBitmap を入力とする（BGRA8888）。Bitmap → EsBitmap 変換して渡す
using var unified = bitmap.ToEsBitmap();
var lines = detector.Detect(unified);
```

### 5.2 検出結果

`Detect` は `IReadOnlyList<LineDetectionResult>` を返します。 各結果は始点・終点・向き（水平 / 垂直）・スコア（長さ）を持ちます。

```csharp
foreach (var line in lines)
{
    Console.WriteLine($"{line.Orientation}: ({line.Start})-({line.End}) score={line.Score}");
}
```

## 6. サンプルアプリケーション

外観・機能を再現したサンプルアプリが `src/Parts.LineDetection/sample/` にあります。 クリップボードから画像を貼り付けて検出結果（線分一覧・画像上のオーバーレイ・ルーペ拡大）を確認できます。

`RunSample.bat` または次で起動します。

```powershell
dotnet run --project src/Parts.LineDetection/sample/Parts.LineDetection.Sample.csproj
```

## 7. ドキュメント

- `SPEC_ExtFunc.md`: 外部機能仕様書（公開 API 仕様）
- `SPEC_System.md`: システム仕様書（アーキテクチャ・内部設計）

## 8. プロジェクト構成

```text
src/Parts.LineDetection/
├── src/          # ライブラリ本体（Parts.LineDetection.csproj）
├── sample/       # サンプルアプリケーション（Parts.LineDetection.Sample.csproj）
└── tests/        # テスト（Parts.LineDetection.Tests.csproj）
```

## 9. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
