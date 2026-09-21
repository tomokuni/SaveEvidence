# FadeMaskControl.Core

`FadeMaskControl.AvaloniaUI`（Avalonia UI 実装）と `FadeMaskControl`（MewUI 実装）が共有する、**UI フレームワーク非依存の暗転オーバーレイ幾何**を提供するプロジェクトです。

## 目的

暗転オーバーレイの計算のうち、UI フレームワークに依存しない部分（穴あけ矩形の決定・暗転領域の帯分割・枠線矩形・論理座標から DIP への変換・ラベル文言・ラベルの行配置）を本プロジェクトへ切り出すことで、同じロジックを MewUI など別 UI からも再利用できます。

## 収録する型

| 型 | 公開 | 責務 |
| --- | --- | --- |
| `FadeMaskRenderMode` | `public` | オーバーレイ描画のモード（AreaSelect / ScreenSelect / WindowSelect / Crop） |
| `FadeMaskOverlayGeometry` | `internal` | 論理座標 → DIP 変換（閉区間の扱いを含む）・穴あけ矩形の決定・暗転領域（4 帯）の計算・枠線矩形の計算 |
| `FadeMaskLabels` | `internal` | 情報ラベルの表示判定・操作ラベルの文言・サイズラベルの文言（`FormatSizeLabel`）の単一所有 |
| `FadeMaskLabelLayout` | `internal` | ラベルの行配置（内側余白 `Padding`・背景矩形・文字の描画原点・次の行の Y 座標）の単一所有 |

`internal` の型は UI 実装（`FadeMaskControl` / `FadeMaskControl.MewUI`）とアプリ（`SaveEvidence.MewUI`）、テストからのみ `InternalsVisibleTo` で参照できます。

## 依存方向

```text
FadeMaskControl.AvaloniaUI（Avalonia 実装） / FadeMaskControl.MewUI
  └──> FadeMaskControl.Core（本プロジェクト）
         └──> Platform.Core（コア幾何型・座標変換）
```

本プロジェクトが UI フレームワークへ依存することはありません。

## 座標型

UI フレームワークの幾何型ではなく、`Platform.Core` のコア幾何型 `EsRect` / `EsPoint` / `EsSize` を使用します。

UI 実装側は、境界で 1 行の相互変換（`.ToEsRect()` / `.ToAvaloniaRect()`）を行います。

## ビルド

```powershell
dotnet build src/FadeMaskControl.Core/src/FadeMaskControl.Core.csproj
```

## テスト

`tests/` が本プロジェクトの単体テストを持ちます。UI 初期化が不要なため、すべて `[Fact]` で並列実行されます。

```powershell
dotnet test src/FadeMaskControl.Core/tests/FadeMaskControl.Core.Tests.csproj
```
