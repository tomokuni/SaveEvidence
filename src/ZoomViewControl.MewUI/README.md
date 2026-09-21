# ZoomViewControl.MewUI

`ZoomViewControl.AvaloniaUI`（Avalonia UI 実装）と同じ **UI 非依存コア（`ZoomViewControl.Core`）** を使い、描画層と入力処理のみを **MewUI** で実装したズーム・パン可能な画像表示コントロールです。

## 目的

ズーム表示の計算（Fit 実効倍率・ズーム中心維持・スクロール範囲クランプ・座標変換・段階遷移）は `ZoomViewControl.Core` が単一所有します。
本プロジェクトはその計算結果を使って **Skia のキャンバスへ画像を描画し、入力を処理する** 層です。
そのため Avalonia 版と **同じズーム挙動・同じ設定**（`ZoomConfig`）になります。

## 特徴

| 項目 | 内容 |
| --- | --- |
| 基底クラス | `Aprillz.MewUI.Skia.Controls.SkiaCanvasView`（SkiaSharp で直接描画する） |
| 描画 | 画像を 1 回の `DrawBitmap` で表示矩形へ描画する（Avalonia 版は `Image` + `RenderTransform` で拡大する） |
| ズーム | `ZoomConfig`（範囲・ホイール倍率・レベル集合）に従う。既定は 10〜500% の 23 段階・ホイール ×1.1 |
| 入力 | 左ドラッグでパン / ホイールでパン / Shift+ホイールで横パン / Ctrl+ホイールでポインタ中心のズーム |
| DPI 追従 | `OnDpiChanged` で表示スケーリング係数を `newDpi / 96` へ自動更新する |
| 連続再描画 | **無効**。ズーム・パンの変更時にのみ再描画する |
| スクロールバー | 画像がビューポートより大きい軸にオーバーレイ表示する（サムのドラッグ・トラックのクリックで移動） |

## インストール（プロジェクト参照）

```xml
<ProjectReference Include="..\..\ZoomViewControl.MewUI\src\ZoomViewControl.MewUI.csproj" />
```

パッケージは `Aprillz.MewUI.Skia` を参照します（バージョンは `MewUIVersion` プロパティで 0.21.1 に固定）。

## 使い方

```csharp
using Estable.MewUI.ZoomView;
using Estable.Platform.Imaging;

var zoomView = new ZoomViewControl
{
    Source = esBitmap,          // コアの EsBitmap
    CenterAlign = true,         // 画像がビューポートより小さい軸を中央寄せする
};

// ズーム操作（ホストのボタンなどから呼ぶ）
zoomView.ZoomIn();              // 次の段階レベルへ
zoomView.ZoomOut();             // 前の段階レベルへ
zoomView.ZoomFit();             // ビューポートに収まる倍率へ

// 状態の購読（ボタンの活性制御や倍率表示に使う）
zoomView.ZoomChanged += (_, e) =>
{
    zoomText.Text = $"{e.ZoomPercent:0}%";
    zoomInButton.IsEnabled = e.CanZoomIn;
    zoomOutButton.IsEnabled = e.CanZoomOut;
};
```

> **注意**: `ZoomConfig` は `public` ですが、遷移・幾何計算（`ZoomTransition` / `ZoomMath`）は `internal` です。
> ホストはコントロールの公開 API（`ZoomIn` / `ZoomPercent` / `ImageRectViewport` 等）を経由して利用します。

## プロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Source` | `EsBitmap?` | `null` | ズーム対象の画像。設定すると Fit 表示へリセットする |
| `CenterAlign` | `bool` | `true` | 画像がビューポートより小さい軸を中央寄せする（false で左上寄せ） |
| `RenderScaling` | `double` | 1.0 | DIP → 物理ピクセルの係数。DPI 変更で自動更新 |
| `ShowScrollBars` | `bool` | `true` | オーバーレイのスクロールバーを表示するか |
| `ScrollBarThickness` | `double` | MewUI 標準のスクロールバーと同じ太さ（`ThemeMetrics.ScrollBarThickness`。既定 4.0） | スクロールバーの太さ（DIP） |
| `ScrollBarMargin` | `double` | `8.0` | ビューポート端からの余白（DIP）。ウィンドウのリサイズ判定領域（端から 6 DIP）を避ける値 |
| `ScrollBarColor` | `SKColor` | 白 63% | サムの色（アルファで不透明度を表す） |
| `ScrollBarTrackColor` | `SKColor` | 白 16% | トラックの色（アルファ 0 の場合はトラックを描画しない） |
| `ZoomPercent` | `double` | Fit の実効倍率 | 実効ズーム率（%）。0 より大きい値のみ設定でき、範囲へクランプされる |
| `IsFit` | `bool` | `true` | Fit 表示中かどうか（`ZoomPercent` は 0 にならないため Fit 判定はこちら） |
| `CanZoomIn` / `CanZoomOut` | `bool` | `false` | ズーム可否（画像未設定時は false） |
| `Offset` | `EsVector` | `default` | パン位置（コンテンツ座標。読み取り専用） |
| `Log` | `ILogger` | `NullLogger` | ズーム操作ログの出力先（null は `NullLogger`。Avalonia 版と同一の書式） |

## メソッド

| メソッド | 説明 |
| --- | --- |
| `ZoomIn()` / `ZoomOut()` | 段階レベルを 1 つ移動する（移動先がなければ何もしない） |
| `ZoomFit()` | Fit 表示へ戻す（Fit へ移行する唯一の経路） |
| `SetZoom(zoomPercent, center)` | ズーム率を設定する（中心を維持。0 以下は Fit） |
| `SetOffset(offset)` / `PanBy(delta)` | パン位置を設定・移動する（スクロール範囲へクランプ） |
| `ImageRectViewport()` | 画像の表示矩形（ビューポート座標・DIP） |
| `DisplaySize()` | 画像の表示サイズ（ビューポート座標・DIP） |
| `ViewportToContent(pos)` / `ContentToViewport(pos)` | オフセット基準の座標変換 |
| `ViewportToContentImage(pos)` / `ContentToViewportImage(pos)` | 画像表示矩形の原点基準の座標変換（中央寄せ補正を含む） |

## イベント

| イベント | 説明 |
| --- | --- |
| `ZoomChanged` | ズーム率・Fit 状態・ズーム可否を通知する（`ZoomChangedEventArgs`） |
| `ViewportChanged` | パン・ズーム・表示サイズの変化を通知する（重ねたオーバーレイの追従に使う） |

## ズーム操作ログ

`Log` プロパティへ `ILogger` を設定すると、ズーム操作の前後のズーム率を Information で出力します（既定は出力なし）。書式は Avalonia 版と同一です。

| 出力元 | 出力例 |
| --- | --- |
| `ZoomIn()` | `[ZoomIn] 50% -> 62%` |
| `ZoomOut()` | `[ZoomOut] 62% -> 50%` |
| Ctrl+ホイール | `[ZoomInWheel] 100% -> 110%` / `[ZoomOutWheel] 110% -> 100%` |

移動先がない場合（上限・下限）は遷移しないため、ログも出力しません。

## 構成

```text
src/ZoomViewControl.MewUI/
├── README.md                # 本ファイル
├── SPEC_ExtFunc.md          # 外部機能仕様書
├── SPEC_System.md           # システム仕様書
├── src/
│   ├── ZoomViewControl.MewUI.csproj  # net10.0 / MewUI 0.21.1 固定
│   ├── ZoomViewControl.cs            # SkiaCanvasView 派生のコントロール（状態・入力）
│   ├── ZoomSkiaRenderer.cs           # 表示矩形の計算と SKCanvas への描画
│   └── ZoomChangedEventArgs.cs       # ズーム率変更イベントの引数
└── tests/
    ├── ZoomViewControl.MewUI.Tests.csproj
    ├── TestHelper.cs                  # テスト共通（描画先・画像生成）
    ├── ZoomSkiaRenderer.Tests.cs      # 表示矩形の計算と描画結果の画素検証
    └── ZoomViewControl.Tests.cs       # ズーム・パン・座標変換
```

## ビルドとテスト

```powershell
dotnet build src/ZoomViewControl.MewUI/src/ZoomViewControl.MewUI.csproj
dotnet test src/ZoomViewControl.MewUI/tests/ZoomViewControl.MewUI.Tests.csproj
```

## 関連ドキュメント

- `../ZoomViewControl.Core/README.md` — 共有する UI 非依存ロジック
- `../ZoomViewControl/README.md` — Avalonia 版ズームビュー
- `../LoupeControl.MewUI/README.md` — 同じ方式で移植したルーペ
- `../app.MewUI/README.md` — 組み込み先のアプリ
- `../../複数UI対応作業.md` — 複数 UI 対応の全体計画と進捗
