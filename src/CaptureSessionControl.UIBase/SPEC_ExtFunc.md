# CaptureSessionControl.UIBase 外部機能仕様書

`CaptureSessionControl.UIBase` は、撮影・切り出しのセッションを実装する両 UI（Avalonia 版 / MewUI 版）が
共有する**設定写像**を提供します。 名前空間は `EsUtil.SaveEvidence.CaptureSession.UIBase`、アセンブリ名は
`EsUtil.SaveEvidence.CaptureSession.UIBase` です。

## 提供する外部機能

| 機能 | 提供元 | 内容 |
| --- | --- | --- |
| 選択矩形の外観の写像 | `CapturePaintMapper.ToMarqueeAppearance` | `CaptureSettings` から `MarqueeAppearance`（色・枠線の太さ・表示可否）を組み立てる |
| ルーペの色の写像 | `CapturePaintMapper.ToLoupeAppearance` | `CaptureSettings` から `LoupeAppearance`（5 項目）を組み立てる |

**本層は公開 API を持ちません。** 写像は両 UI のホストが使う内部処理であり、公開面を増やさないため
`internal` としています。

## 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `CapturePaintMapper` | `internal` | `CaptureSessionControl.AvaloniaUI` / `CaptureSessionControl.MewUI` / `CaptureSessionControl.UIBase.Tests` |

`internal` は `InternalsVisibleTo` で両 UI のホストとテストへ公開します。 **`internal` → `public` の変更は
破壊的な設計変更として扱います**。

## `CapturePaintMapper`（内部・設定から色への写像）

### `ToMarqueeAppearance(CaptureSettings settings)`

| 写像先（`MarqueeAppearance`） | 写像元（`CaptureSettings`） | 内容 |
| --- | --- | --- |
| `HandleColor` | `HandleColor` | ドラッグハンドルの塗り色 |
| `HandleBorderColor` | `BorderColor` | ハンドルの枠線色（**選択枠線と共通**） |
| `MarkerColor` | `MarkerColor` | 始点マーカー（十字線）の色 |
| `SizeLabelColor` | `SizeLabelColor` | サイズラベルの文字色 |
| `SizeLabelBackgroundColor` | `TextBackgroundColor` | サイズラベルの背景色（ラベル共通の背景色） |
| `CoordLabelColor` | `CoordLabelColor` | カーソル座標ラベルの文字色 |
| `CoordLabelBackgroundColor` | `CoordLabelBackgroundColor` | カーソル座標ラベルの背景色 |
| `OverlayColor` | `OverlayColor` | 暗転マスク（オーバーレイ）の色 |
| `OuterBorderColor` | `BorderColor` | 外枠の色（**ハンドルの枠線と共通**） |

**写像しない項目**: 内側の塗り（`InnerFillColor`）と内側の枠線（`InnerBorderColor`）。 描画の可否は
`MarqueeAppearance` の明示フラグが単一所有するため、色は既定の透明を保ちます。

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

## `FadeMaskDragRectChildValues`（子コントロールへ適用する値）

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `Marquee` | `MarqueeOptions` | 選択矩形の設定（モードから導出するハンドルの表示可否を反映済み） |
| `MarqueeAppearance` | `MarqueeAppearance` | 選択矩形の外観（ハンドルの表示可否と全面暗転の可否を反映済み） |
| `Loupe` | `LoupeOptions` | ルーペの設定 |
| `LoupeOffset` | `double` | ルーペのカーソル中心からの右下オフセット（DIP） |
| `IsHitTestVisible` | `bool` | 選択矩形コントロールが入力を受け取るか（選択矩形を使うモードのみ true） |
| `Resolve(options, mode, marqueeAppearance)` | static | 設定とモードから上記の値を解決する。 モードの判断はコアの `FadeMaskDragRectModes.Resolve` に委譲する |

**「設定とモードから子コントロールの値を決める」手順を本型が単一所有**します（結果を代入するには子コントロール（UI 固有型）が必要なため、UI 層には代入だけを残します）。 **ハイライト矩形は実行時状態**のため本値に含めず、子コントロールが保持します。

## `FadeMaskHostLabelRenderer` / `MarqueeHostLabelRenderer`（付加情報ラベル）

| 型 | メンバー | 内容 |
| --- | --- | --- |
| `FadeMaskHostLabelRenderer` | `Resolve(content, labels, marqueeAppearance, operationText, infoText, measureText)` | 操作ラベル・情報ラベルの縦積みの配置指示を返す（設定スイッチと文言の有無の両方が成立したときだけ含める） |
| `MarqueeHostLabelRenderer` | `Resolve(...)` | ラベル 1 行の体裁（背景 = 文字の実寸 + 内側余白 × 2）の配置指示を返す |

**文言の生成はホストが担う**（付加情報ラベルは選択矩形コントロールの概念ではないため）。 ただし**描画位置（注目矩形の左上から縦に積む）と表示可否は両 UI で同じ**であるため、本層が単一所有します。 体裁の値（内側余白）は提示層の外観（`MarqueeAppearance`）が単一所有します。

**本層は描画しません**（UI フレームワークごとの描画モデルの差は抽象で吸収しきれないため、描画は各 UI が自 UI の API で行います）。

## 引数と例外

| 引数 | 制約 |
| --- | --- |
| `settings` | 全メンバー non-null（`CaptureSettings.ToNonNull` 済みを渡す）。 null は `ArgumentNullException` |
| `marqueeAppearance`（`Resolve`） | null 不可（`ArgumentNullException`） |

## 依存関係

| 依存 | 用途 |
| --- | --- |
| `CaptureSessionControl.Core`（`EsUtil.SaveEvidence.CaptureSession`） | 写像元の設定型（`CaptureSettings`）と、複合コントロールの設定・モード・状態 |
| `Feature.Marquee.Core` / `Feature.Loupe.Core` | 内包コントロールの設定型（`MarqueeOptions` / `LoupeOptions`） |
| `Feature.Marquee.Presenter`（`EsUtil.Feature.Marquee.Presenter.*`） | 写像先の外観（`MarqueeAppearance`）とラベル配置の値 |
| `Feature.Loupe.Presenter`（`EsUtil.Feature.Loupe.Presenter.Setting`） | 写像先の描画の設定（`LoupeAppearance`） |
| `Platform.Core` | コア幾何型（`EsPoint` / `EsRect`） |

UI フレームワーク（Avalonia / MewUI）への依存はありません。 外部パッケージへの直接依存もありません。

## 本層を置く理由（設計上の位置付け）

写像元がセッションの設定型（`CaptureSettings`）であるため、再利用ライブラリに置くと後者が製品の設定型へ依存します。 また写像先が描画層（`Feature.Marquee.Presenter` / `Feature.Loupe.Presenter`）の
型であるため、`CaptureSessionControl.Core` にも置けません（Core → UIBase の逆流になる）。 両 UI のホストが
参照できる中間の層として本層を置きます。 複合コントロールの共有処理（子コントロールへ適用する値の解決・付加情報ラベルの配置指示）も同じ事情（両 UI で同じ規則を使うことを保証する）で本層が持ちます。
