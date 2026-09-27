# CaptureSessionControl.UIBase 外部機能仕様書

`CaptureSessionControl.UIBase` は、撮影・切り出しのセッションを実装する両 UI（Avalonia 版 / MewUI 版）が
共有する**設定写像**を提供します。名前空間は `EsUtil.SaveEvidence.CaptureSession.UIBase`、アセンブリ名は
`CaptureSessionControl.UIBase` です。

## 提供する外部機能

| 機能 | 提供元 | 内容 |
| --- | --- | --- |
| 選択矩形の色の写像 | `CapturePaintMapper.ToMarqueePaint` | `CaptureSettings` から `MarqueePaint`（9 項目）を組み立てる |
| ルーペの色の写像 | `CapturePaintMapper.ToLoupeAppearance` | `CaptureSettings` から `LoupeAppearance`（5 項目）を組み立てる |

**本層は公開 API を持ちません。** 写像は両 UI のホストが使う内部処理であり、公開面を増やさないため
`internal` としています。

## 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `CapturePaintMapper` | `internal` | `CaptureSessionControl.AvaloniaUI` / `CaptureSessionControl.MewUI` / `CaptureSessionControl.UIBase.Tests` |

`internal` は `InternalsVisibleTo` で両 UI のホストとテストへ公開します。**`internal` → `public` の変更は
破壊的な設計変更として扱います**。

## `CapturePaintMapper`（内部・設定から色への写像）

### `ToMarqueePaint(CaptureSettings settings)`

| 写像先（`MarqueePaint`） | 写像元（`CaptureSettings`） | 内容 |
| --- | --- | --- |
| `HandleColor` | `HandleColor` | ドラッグハンドルの塗り色 |
| `HandleBorderColor` | `BorderColor` | ハンドルの枠線色（**選択枠線と共通**） |
| `MarkerColor` | `MarkerColor` | 始点マーカー（十字線）の色 |
| `SizeLabelColor` | `SizeLabelColor` | サイズラベルの文字色 |
| `SizeLabelBackgroundColor` | `TextBackgroundColor` | サイズラベルの背景色（ラベル共通の背景色） |
| `CoordLabelColor` | `CoordLabelColor` | カーソル座標ラベルの文字色 |
| `CoordLabelBackgroundColor` | `CoordLabelBackgroundColor` | カーソル座標ラベルの背景色 |
| `OverlayColor` | `OverlayColor` | 暗転マスク（オーバーレイ）の色 |
| `OuterBorderColor` | `BorderColor` | 穴あけ枠の色（**ハンドルの枠線と共通**） |

**写像しない項目**: 内側の塗り（`InnerFillColor`）と内側の枠線（`InnerBorderColor`）。描画の可否は
`MarqueeDecoOptions` の明示フラグが単一所有するため、色は既定の透明を保ちます。

### `ToLoupeAppearance(CaptureSettings settings)`

| 写像先（`LoupeAppearance`） | 写像元 | 内容 |
| --- | --- | --- |
| `PasteboardColor` | `LoupePasteboardColor` | 画像外（ペーストボード）の色 |
| `BorderColor` | `LoupeFrameColor` | 枠線の色 |
| `CrossColor` | `LoupeCrossColor` | 十字線の色 |
| `CoordLabelColor` | `LoupeCoordLabelColor` | 中心座標ラベルの文字色 |
| `CoordLabelBackgroundColor` | `LoupeCoordLabelBackgroundColor` | 中心座標ラベルの背景色 |

画像外の色を設定にするのは、**描画層が描画面全体を塗ってから拡大画像を重ねる**ためです（画像の無い部分が本色の無地になる）。
透明を設定すると背後の要素がそのまま見えます（既定は不透明の中性グレー）。

## 引数と例外

| 引数 | 制約 |
| --- | --- |
| `settings` | 全メンバー non-null（`CaptureSettings.ToNonNull` 済みを渡す）。null は `ArgumentNullException` |

## 依存関係

| 依存 | 用途 |
| --- | --- |
| `CaptureSessionControl.Core`（`EsUtil.SaveEvidence.CaptureSession`） | 写像元の設定型（`CaptureSettings`） |
| `MarqueeControl.UIBase`（`EsUtil.SaveEvidence.Marquee.UIBase`） | 写像先の描画の設定（`MarqueePaint`） |
| `LoupeControl.UIBase`（`EsUtil.SaveEvidence.Loupe.UIBase`） | 写像先の描画の設定（`LoupeAppearance`） |

UI フレームワーク（Avalonia / MewUI）への依存はありません。外部パッケージへの直接依存もありません。

## 本層を置く理由（設計上の位置付け）

写像元がセッションの設定型（`CaptureSettings`）であるため、再利用ライブラリ（`FadeMaskDragRectControl.UIBase`）
に置くと後者が製品の設定型へ依存します。また写像先が描画層（`MarqueeControl.UIBase` / `LoupeControl.UIBase`）の
型であるため、`CaptureSessionControl.Core` にも置けません（Core → UIBase の逆流になる）。両 UI のホストが
参照できる中間の層として本層を置きます。
