# CropViewControl.Core

`CropViewControl.AvaloniaUI`（Avalonia UI 実装）と `CropViewControl`（MewUI 実装）が共有する、**UI フレームワーク非依存の矩形オーバーレイ定義とラスタライズ**を提供するプロジェクトです。

## 目的

クロップビューの矩形オーバーレイのうち、UI フレームワークに依存しない部分（矩形の描画情報・枠線スタイル・破線パターン・ルーペ画像へのピクセル描画）を本プロジェクトへ切り出すことで、同じロジックを MewUI など別 UI からも再利用できます。

## 収録する型

| 型 | 公開 | 責務 |
| --- | --- | --- |
| `OverlayRect` | `public` | 外部提供矩形の描画情報（矩形・枠線スタイル・前景色・背景色・太さ） |
| `OverlayLineStyle` | `public` | 枠線スタイル（`Solid` / `Dashed` / `Dotted`） |
| `OverlayLineDash` | `internal` | 枠線スタイルに対応するダッシュ長の単一所有（ベクター描画とラスタ描画で共有） |
| `OverlayRectRasterizer` | `internal` | 矩形の 4 辺を `EsBitmap`（ルーペ画像）へピクセル単位で描画する |

`internal` の型は UI 実装（`CropViewControl`）とテスト（`CropViewControl.AvaloniaUI.Tests` / `CropViewControl.Core.Tests`）からのみ `InternalsVisibleTo` で参照できます。

## 依存方向

```text
CropViewControl.AvaloniaUI（Avalonia 実装） / CropViewControl.MewUI
  └──> CropViewControl.Core（本プロジェクト）
         └──> Platform.Core（コア幾何型・画像コンテナ）
```

本プロジェクトが UI フレームワークへ依存することはありません。外部パッケージへの直接依存もありません。

## 座標型・色型

| 用途 | 型 | 変換 |
| --- | --- | --- |
| 座標 | `EsRect`（`Platform.Core`） | UI 側で `.ToEsRect()` / `.ToAvaloniaRect()` |
| 色 | `System.Drawing.Color`（`System.Drawing.Primitives`。クロスプラットフォーム） | UI 側で `.ToDrawingColor()` / `.ToAvaloniaColor()`（`Platform.AvaloniaUI`） |

`System.Drawing.Color` は `System.Drawing.Common`（Windows 専用の描画 API）ではなく、共有フレームワーク同梱の値型です。`EsBitmap`（`Platform.Core`）のピクセル API と同じ色型を使います。

## ビルド

```powershell
dotnet build src/CropViewControl.Core/src/CropViewControl.Core.csproj
```

## テスト

`tests/` が本プロジェクトの単体テストを持ちます。UI 初期化が不要なため、すべて `[Fact]` で並列実行されます。

```powershell
dotnet test src/CropViewControl.Core/tests/CropViewControl.Core.Tests.csproj
```
