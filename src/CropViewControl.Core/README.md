# CropViewControl.Core

`CropViewControl.AvaloniaUI`（Avalonia UI 実装）と `CropViewControl.MewUI`（MewUI 実装）が共有する、**UI フレームワーク非依存の矩形オーバーレイ定義と描画の計算**を提供するプロジェクトです。

## 目的

クロップビューの矩形オーバーレイのうち、UI フレームワークに依存しない部分（矩形の描画情報・枠線スタイル・破線パターン・描画入力の解決・ルーペ画像へのピクセル描画）を本プロジェクトへ切り出すことで、同じロジックを別 UI からも再利用できます。

**「どう描くか」の判断（負サイズのクランプ・太さの下限・破線かどうか・背景色を使うか）は本プロジェクトが単一所有します**。 UI 層は確定した描画入力（`OverlayRectFrame`）を描画先の型へ写すだけで、式と判断を持ちません。

## 収録する型

| 型 | 公開 | 責務 |
| --- | --- | --- |
| `OverlayRect` | `public` | 外部提供矩形の描画情報（矩形・枠線スタイル・前景色・背景色・太さ） |
| `OverlayLineStyle` | `public` | 枠線スタイル（`Solid` / `Dashed` / `Dotted`） |
| `OverlayRectLayout` | `public` | 描画入力の解決（負サイズのクランプ・太さの下限・破線の判定とダッシュ長の引き継ぎ） |
| `OverlayRectFrame` | `public` | 矩形 1 個分の描画入力（配置・太さ・半幅・破線の判定と長さ・色・描画可否） |
| `OverlayLineDash` | `internal` | 枠線スタイルに対応するダッシュ長の単一所有（ベクター描画とラスタ描画で共有） |
| `OverlayRectRasterizer` | `internal` | 矩形の 4 辺を `EsBitmap`（ルーペ画像）へピクセル単位で描画する |

`internal` の型は UI 実装（`CropViewControl.AvaloniaUI`）とテスト（`CropViewControl.MewUI.Tests` / `CropViewControl.Core.Tests`）からのみ `InternalsVisibleTo` で参照できます（使用中のアセンブリにのみ公開し、使わない IVT は残さない）。

## 層の責務の境界

| 層 | 責務 |
| --- | --- |
| Core（本プロジェクト） | 矩形の契約、描画入力の解決（判断）、ダッシュ長、ルーペ画像へのラスタライズ |
| UI 層 | ドメイン座標 → ビューポート座標の変換（ズームビューの変換を経由する）、描画先の型への写像、子要素の配置 |

## 依存方向

```text
CropViewControl.AvaloniaUI（Avalonia 実装） / CropViewControl.MewUI
  └──> CropViewControl.Core（本プロジェクト）
         └──> Platform.Core（コア幾何型・画像コンテナ）
```

本プロジェクトが UI フレームワークへ依存することはありません。 外部パッケージへの直接依存もありません。

## 座標型・色型

| 用途 | 型 | 変換 |
| --- | --- | --- |
| 座標 | `EsRect`（`Platform.Core`） | UI 側で `.ToEsRect()` / `.ToAvaloniaRect()` |
| 色 | `System.Drawing.Color`（`System.Drawing.Primitives`。 クロスプラットフォーム） | UI 側で `.ToDrawingColor()` / `.ToAvaloniaColor()`（`Platform.AvaloniaUI`） |

`System.Drawing.Color` は `System.Drawing.Common`（Windows 専用の描画 API）ではなく、共有フレームワーク同梱の値型です。 `EsBitmap`（`Platform.Core`）のピクセル API と同じ色型を使います。

## ビルド

```powershell
dotnet build src/CropViewControl.Core/src/CropViewControl.Core.csproj
```

## テスト

`tests/` が本プロジェクトの単体テストを持ちます。 UI 初期化が不要なため、すべて `[Fact]` で並列実行されます。

```powershell
dotnet test src/CropViewControl.Core/tests/CropViewControl.Core.Tests.csproj
```
