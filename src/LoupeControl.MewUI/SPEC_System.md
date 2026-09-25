# LoupeControl.MewUI システム仕様書

`LoupeControl`（MewUI 版ルーペ）のシステム概要・アーキテクチャ・依存関係・設計判断を記載します。

## システム概要

`LoupeControl.MewUI` は、Avalonia 版 `LoupeControl` と**同一の UI 非依存コア（`LoupeControl.Core`）** を使い、
描画層のみを MewUI（SkiaSharp）で実装したルーペコントロールです。

コアが担う範囲（数値の安全域補正・切り出し領域の計算・装飾矩形の計算）は両 UI で完全に共通であり、
本プロジェクトはその計算結果をキャンバスへ描画するだけです。
これにより **UI ごとの見た目の差を作らずに済み**、UI 追加時の再実装は描画 API の呼び出しのみになります。

## 技術スタック

| 区分 | 採用技術 |
| --- | --- |
| 対象フレームワーク | .NET 10（`net10.0`） |
| 言語 | C# 14 |
| UI フレームワーク | MewUI（`Aprillz.MewUI.Skia` **0.21.1 固定**） |
| 描画 | SkiaSharp（`SKCanvas` へ直接描画する） |
| 基底コントロール | `Aprillz.MewUI.Controls.Panel`（図形は子の `SkiaCanvasView` が描画する） |
| プロパティシステム | `MewProperty<T>`（`MewPropertyOptions.AffectsRender` / `AffectsLayout`） |

## アーキテクチャ

### 依存方向（一方向を厳守）

```text
┌──────────────────────┐        ┌────────────────────────┐
│  LoupeControl.MewUI  │        │ LoupeControl.AvaloniaUI │
│ （MewUI の描画層）   │        │ （Avalonia の描画層）  │
└──────────┬───────────┘        └───────────┬────────────┘
           │                                │
           └────────────┬───────────────────┘
                        ▼
              ┌────────────────────┐
              │  LoupeControl.Core │  コアロジック（単一所有）
              └─────────┬──────────┘
                        ▼
              ┌────────────────────┐
              │   Platform.Core    │  コア幾何型・DPI 変換
              └────────────────────┘
```

Avalonia 版と MewUI 版の間に依存はありません（互いを参照しません）。
両者に共通する計算は必ず `LoupeControl.Core` を経由します。

### 責務の分担

| 層 | 責務 |
| --- | --- |
| `LoupeControl.Core` | 数値の安全域補正、切り出し領域の計算、切り出し、十字線・外枠の矩形計算、市松模様の明暗判定、外観（下地含む）の単一所有 |
| `LoupeControl.MewUI` | コアの計算結果を `SKCanvas` へ描画する。`MewProperty` による入力の受付。DPI の自動追従 |
| ホスト（`app.MewUI` 等） | `Source` / `CenterPoint` の設定、表示位置の決定（コアのファサード `ResolveLayout` を利用） |

## コンポーネント構成

```text
src/LoupeControl.MewUI/
├── src/
│   ├── LoupeControl.cs          MewProperty の定義・DPI 追従・描画の起点
│   ├── LoupeSkiaRenderer.cs     コアの計算結果を SKCanvas へ描画する（市松・画像・十字線・外枠）
│   └── LoupeRenderedEventArgs.cs  描画完了の通知引数（中心座標ラベルはコントロールが表示する）
└── tests/
    ├── TestHelper.cs                テスト用の描画先（SKBitmap + SKCanvas）・画像生成・setter を経由しない設定
    ├── RecordingLogger.cs           ログ収集
    ├── LoupeSkiaRenderer.Tests.cs   描画結果の画素検証（下地・市松模様・拡大画像・十字線・外枠・例外）
    ├── LoupeRenderedEventArgs.Tests.cs      イベント引数の保持
    └── LoupeControl.Tests.cs        プロパティ補正・レイアウト・全設定経路の補正・ログ・要求サイズ
```

| コンポーネント | 責務 |
| --- | --- |
| `LoupeControl` | 入力プロパティ（`MewProperty`）の定義（補正と変更ログもここへ登録する）、DPI 変更の追従、子（図形キャンバス・ラベル要素）の計測と配置、ラベル要素（`Border` + `TextBlock`）の追加・更新。描画は子の `SkiaCanvasView` が行う（描画の順序と内容は描画層が所有） |
| `LoupeSkiaRenderer` | 下地 → 市松模様 → 拡大画像 → 十字線 → 外枠の順に描画する（コアの描画契約 `LoupeRenderRequest` と、矩形計算・外観の物理ピクセル換算 `LoupeAppearanceScaler` をそのまま使用）。**描く層とその順序はコアが単一所有する**（`LoupeDrawLayer` を先頭から処理し、中心座標ラベルは処理しない） |
| `LoupeRenderTarget`（テスト） | ウィンドウ不要の描画先。`SKBitmap` と `SKCanvas` を保持し、画素を読み取る |

## クラス図

```mermaid
classDiagram
    class SkiaCanvasView {
        <<Aprillz.MewUI.Skia>>
        +PaintSurface
        +ContinuousAnimation
        +MeasureContent(size) Size
    }
    class LoupeControl {
        +EsBitmap? Source
        +EsPoint? CenterPoint
        +LoupeOptions Options
        +double RenderScaling
        +LoupeLayout ResolveLayout(parentSize)
        +SetCoordinateTransform(port) void
        +OnDpiChanged(oldDpi, newDpi) void
        +MeasureContent(size) Size
        +ArrangeContent(bounds) void
        -SyncLabelElements(count) void
        -OnPaintSurface(canvas, info) void
    }
    class ShapeCanvas {
        <<子の図形キャンバス>>
        +PaintSurface
    }
    class LoupeOptions {
        <<LoupeControl.Core>>
        +int RequestedSize
        +int ZoomLevel
        +LoupeAppearance Appearance
    }
    class LoupeAppearance {
        <<LoupeControl.Core>>
        +Color BackgroundColor
        +Color CrossColor
        +Color CheckerColorA
        +Color CheckerColorB
        +Color FrameColor
        +bool ShowCoordLabel
        +Color CoordLabelColor
        +Color CoordLabelBackgroundColor
        +ShouldShowCoordLabel(hasCenter) bool
    }
    class LoupeSkiaRenderer {
        +Render(canvas, source, request, layers)$ void
        -DrawBackground(canvas, request) void
    }
    class LoupeValueCoercer {
        <<LoupeControl.Core>>
        +CoerceRequestedSize(v) int$
        +CoerceZoomLevel(v) int$
        +CoerceRenderScaling(v) double$
    }
    class LoupeDecorationGeometry {
        <<LoupeControl.Core>>
        +ComputeCrosshairRects(pixelSize, zoomLevel)$ IReadOnlyList~EsRect~
        +ComputeFrameRects(pixelSize, width)$ IReadOnlyList~EsRect~
    }
    class LoupeCheckerboard {
        <<LoupeControl.Core>>
        +IsDarkTile(col, row) bool$
        +ComputeDarkTileRects(pixelSize, tileSize)$ EsRect[]
    }
    class LoupeImageExtractor {
        <<LoupeControl.Core>>
        +ComputeCaptureSize(pixelSize, zoomLevel) int$
        +ComputeMagnifiedRects(captureSize, zoomLevel) (EsRect, EsRect)$
        +TryExtractRegionAt(source, request) EsBitmap?
    }
    class LoupeAppearanceScaler {
        <<LoupeControl.Core>>
        +ScaleCheckerSize(appearance, scaling) int$
        +ScaleFrameWidth(appearance, scaling) int$
        +ScaleCoordLabelFontSize(appearance, scaling) double$
    }
    class Loupe {
        <<LoupeControl.Core / 公開ファサード>>
        +Options / RenderScaling / CenterPoint / PositionOffset
        +DisplaySize double
        +SetCoordinateTransform(port) void
        +ResolveLayout(parentSize) LoupeLayout
        +CreateRequest(availableSize) LoupeRenderRequest
        +ResolveNotification(request) LoupeRenderNotification
    }
    class LoupePositionCalculator {
        <<LoupeControl.Core / internal>>
        +Compute(requestedSize, parentSize, centerDip, offset)$ (EsSize, EsPoint)
        +ClampSizeToArea(size, areaSize) double$
        +ComputePosition(displaySize, parentSize, centerDip, offset)$ EsPoint
        +ResolvePixelSize(size, renderScaling, availableSize) int$
    }
    class CoordinateFormatter {
        <<Platform.Core>>
        +Format(point) string$
    }

    SkiaCanvasView <|-- ShapeCanvas
    Panel <|-- LoupeControl
    LoupeControl *-- ShapeCanvas : 子（図形の描画）
    LoupeControl *-- Loupe : 操作の唯一の窓口（公開ファサード）
    LoupeControl ..> LoupeSkiaRenderer : 描画を委譲（子の OnPaintSurface から）
    Loupe ..> LoupeOptions : 設定の単一値
    LoupeSkiaRenderer ..> LoupeCheckerboard : 明暗判定
    LoupeSkiaRenderer ..> LoupeDecorationGeometry : 矩形
    LoupeSkiaRenderer ..> LoupeImageExtractor : 切り出し・拡大矩形
    LoupeSkiaRenderer ..> LoupeAppearanceScaler : 外観の物理ピクセル換算
    LoupeControl ..> LoupeValueCoercer : 値の補正（プロパティ登録の coerce）
    Loupe ..> LoupePositionCalculator : 表示レイアウト・描画サイズ・描画入力の解決
    LoupeSkiaRenderer ..> LoupeAppearanceScaler : ラベル文字サイズの換算
    Loupe ..> CoordinateFormatter : ラベル文字列
```

## シーケンス図（描画）

```mermaid
sequenceDiagram
    participant MW as MewUI
    participant LC as LoupeControl
    participant R as LoupeSkiaRenderer
    participant C as LoupeControl.Core
    participant SK as SkiaSharp

    MW->>LC: PaintSurface(canvas, info)
    LC->>LC: Loupe.CreateRequest（コアのファサードが描画契約を組み立てる。物理ピクセルサイズは DisplaySize × RenderScaling をキャンバスへクランプして解決する）
    LC->>R: Render(canvas, Source, request)
    R->>SK: 下地（Options.Appearance.BackgroundColor）を表面全体へ塗る
    R->>C: ComputeDarkTileRects(pixelSize, tileSize)
    R->>SK: 暗色タイルを描画
    alt Source と CenterPoint が設定済み
        R->>C: TryExtractRegionAt(source, request)
        C-->>R: 切り出し画像 / null（切り出せない）
        R->>SK: DrawBitmap(srcRect, dstRect)（最近傍で拡大）
    else 未設定または破棄済み
        R-->>R: 市松模様のみを残す
    end
    R->>C: ComputeCrosshairRects(pixelSize, zoomLevel)
    R->>SK: 十字線を描画
    R->>C: ComputeFrameRects(pixelSize, frameWidth)
    R->>SK: 外枠を描画
    alt ShowCoordLabel かつ CenterPoint が設定済み
        R-->>R: 座標ラベル層は描画しない（コントロールがテキスト要素として表示する）
    else 未設定
        R-->>R: 座標ラベルを表示しない
    end
```

## 設計判断

1. **中間ビットマップを持たない**。Avalonia 版は `RenderTargetBitmap` に描いて `EsBitmap` へコピーするが、
   MewUI 版は `SkiaCanvasView` のキャンバスへ直接描画する（コピーとメモリ確保を排除）。
2. **拡大は最近傍で行う**。ルーペは画素をそのまま拡大する用途であり、補間すると画素が判別できなくなる。
3. **装飾矩形と市松模様の明暗判定をコアへ移した**。両 UI で同じ見た目を保証するため、
   Avalonia 版の `LoupeDecorationRenderer` / `LoupeBackgroundRenderer` はコアへ委譲する形に変更した（二重所有の解消）。
4. **設定（表示サイズ・拡大率・外観）はコアの `LoupeOptions` が単一所有する**。UI 層は `Options` を 1 つ公開して描画時に読むだけであり、既定値・数値項目の補正もコアが持つ。
   描画の可否（アルファ 0）とラベルの余白もコアが判断・所有するため、両 UI の見た目が一致する
   （従来は Avalonia が既定で十字線を描き MewUI は描かないという既定値のドリフトがあった）。
5. **位置を持たない**。表示位置は「親領域サイズ」「中心座標」「オフセット」に依存するホスト側の関心事であり、
   計算はコアの公開ファサード（`Loupe.ResolveLayout`）が単一所有する。コントロールは入力と描画のみを担う。
6. **DPI を自動追従させる**。`OnDpiChanged` で `RenderScaling` を更新し、ホストが DPI を扱わずに済むようにした。
7. **`Options` はレイアウトにも影響する**。`MeasureContent` が要求サイズを返すため、
プロパティへ `MewPropertyOptions.AffectsLayout` を付与して再測定を促す（未付与では変更が反映されない）。
描画サイズは `Options.RequestedSize` × `RenderScaling` を物理ピクセルへ換算し、キャンバスより大きい場合はキャンバスに合わせる
   （「領域へクランプする」規則と DIP → 物理ピクセルの換算はコアのファサード（`Loupe.ResolveLayout` / `Loupe.CreateRequest`）が単一所有し、Avalonia 版の親パネルへのクランプと同じ規則を使う）。
8. **静的画像では連続再描画を無効化する**。`ContinuousAnimation` を `false` にし、プロパティ変更時のみ再描画する。
9. **座標ラベルはコントロールがテキスト要素として表示する**（両 UI 共通）。文言・表示可否・配置はコア（`LoupeCoordLabel` / `LoupeAppearanceScaler`。配置の内部手順は internal の `LoupeLabelLayout`）が単一所有し、コントロールは `LoupeLabelLayoutPlan`（`LoupeControl.CoreForControl`）へ自 UI の実寸（`SkiaText.Measure`）を渡して要素を配置する（ラベル要素は子の末尾に置かれ、子は親より後に描かれるため常に最上位）。Avalonia 版も同じ契約を使い、測定 API（`FormattedText`）だけが異なる。
10. **値の補正と変更ログはプロパティの登録が所有する**。`MewProperty.Register` の `coerce` / `changed` へ渡すため、
   setter を経由しない設定（バインディング等）でも安全域が保たれ、変更が記録される
   （Avalonia 版の `coerce` コールバック + `Changed` クラスハンドラと同じ契約。規則と書式はコアが単一所有する）。
設定（`Options`）と外観（`Options.Appearance`）の数値項目はコアの型が生成時に補正するため、コントロールは `coerce` を登録しない（環境値の `RenderScaling` のみ登録する）。

## テスト方針

| 対象 | 方針 |
| --- | --- |
| `LoupeSkiaRenderer` | `SKBitmap` + `SKCanvas` へ実際に描画し、**画素を実測**して検証する（ウィンドウ不要で並列実行できる） |
| `LoupeControl` | `MewProperty` の補正・既定値・要求サイズを検証する（MewUI の要素生成はホスト登録を要しない） |
| コアロジック | `LoupeControl.Core.Tests` が担保する（本プロジェクトでは再検証しない） |
| アプリ組み込み | `app.MewUI/verify-ui.ps1` が実機の画素を実測して検証する |

## 未実装・今後の対応

| 項目 | 内容 |
| --- | --- |
| 座標変換ポート | 実装済み（`SetCoordinateTransform`。ホストが設定し、コントロールはファサードへ転送する。Avalonia 版と同じ API） |
| 再生成の要求メソッド | `Clear` / `RequestRender` 相当（現在はプロパティ変更で自動再描画） |
| 座標ラベル以外の文字描画 | 日本語のフォントフォールバック（ラベルは `MarqueeControl.MewUI`（選択矩形の外側の装飾）側で対応済み。本コントロールの座標ラベルは数値のみのため未適用） |

## ログ

設定変更を Debug、描画の例外を Error で記録します（Avalonia 版と同一の書式）。

| レベル | 出力元 |
| --- | --- |
| Debug | `Source` / `CenterPoint` / `RequestedSize` / `RenderScaling` / `ZoomLevel` の変更（補正後の値を渡す。変化判定（同一値・同一インスタンスなら出力しない）はコアのログ定義が単一所有し、UI 層は設定の前後を渡すだけ。設定の変更は変化した項目だけを出力する） |
| Error | 描画処理の例外（アプリを落とさずログへ記録して描画を中止する） |

Avalonia 版の Trace ログ（再描画パイプライン）は持ちません（MewUI 版は中間ビットマップを作らず同期描画するため）。  
出力先は `Log` プロパティへ注入します（既定は `NullLogger`）。
