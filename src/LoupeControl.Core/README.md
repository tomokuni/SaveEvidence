# LoupeControl.Core

`LoupeControl.AvaloniaUI`（Avalonia UI 実装）と `LoupeControl`（MewUI 実装）が共有する、**UI フレームワーク非依存のルーペロジック**を提供するプロジェクトです。

## 目的

ルーペ（拡大鏡）の計算ロジックのうち、UI フレームワークに依存しない部分（数値の安全域補正・表示サイズ / 位置の計算・切り出し領域の計算・装飾矩形の計算・描画完了時の表示情報の計算）を本プロジェクトへ切り出すことで、同じロジックを MewUI など別 UI からも再利用できます。

## 収録する型

| 型 | 責務 |
| --- | --- |
| `LoupeValueCoercer` | ルーペの数値パラメータ（表示サイズ / 拡大率 / スケーリング / 市松模様 / 外枠 / ラベル文字サイズ）の既定値・安全域（最小 / 最大）の単一所有と、範囲外の値のクランプ |
| `LoupePositionCalculator` | 表示サイズの親領域へのクランプ、表示位置（中心 + オフセット）の計算と親領域内へのクランプ |
| `LoupeDecorationGeometry` | 十字線（縦横 2 本）と外枠（4 辺）の描画矩形の計算（各 UI は返された `EsRect` を自身の描画 API へ渡すだけ） |
| `LoupeCheckerboard` | 市松模様のタイルの明暗判定（各 UI が同一のパターンを描画できるようにする） |
| `LoupeRenderArgsCalculator` | 描画完了イベント引数用の表示情報（ピクセルサイズ / 切り出しサイズ / 切り出し左上 / スケール）の計算 |
| `Imaging.LoupeImageExtractor` | 切り出し領域の一辺・左上（ドメイン座標）の計算と、`EsBitmap` からの領域切り出し |

型はすべて `internal` です。UI 実装（`LoupeControl` / `LoupeControl.MewUI`）とテスト（`LoupeControl.AvaloniaUI.Tests` / `LoupeControl.MewUI.Tests` / `LoupeControl.Core.Tests`）からのみ `InternalsVisibleTo` で参照でき、公開 API は増やしていません。

## 依存方向

```text
LoupeControl.AvaloniaUI（Avalonia 実装） / LoupeControl.MewUI
  └──> LoupeControl.Core（本プロジェクト）
         └──> PlatformFactory.Core（コア幾何型・静的計算）
```

本プロジェクトが UI フレームワークへ依存することはありません。外部パッケージへの直接依存もありません（`PlatformFactory.Core` 経由で SkiaSharp を参照します）。

## 座標型

UI フレームワークの幾何型（`Avalonia.Point` / `Size` 等）ではなく、`PlatformFactory.Core` のコア幾何型 `EsPoint` / `EsSize` を使用します。

UI 実装側は、境界（サービス呼び出し・イベント引数の生成箇所）で 1 行の相互変換（`.ToEsPoint()` / `.ToEsSize()` / `.ToAvaloniaPoint()`）を行います。UI 内部の状態・描画は UI 側の型のままで構いません。

## ビルド

```powershell
dotnet build src/LoupeControl.Core/src/LoupeControl.Core.csproj
```

## テスト

`tests/` が本プロジェクトの単体テストを持ちます。UI 初期化が不要なため、すべて `[Fact]` で並列実行されます。

```powershell
dotnet test src/LoupeControl.Core/tests/LoupeControl.Core.Tests.csproj
```
