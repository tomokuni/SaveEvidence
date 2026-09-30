# FadeMaskDragRectControl.Core 外部機能仕様書

`FadeMaskDragRectControl.Core` は、領域選択の複合コントロール `FadeMaskDragRectControl`（Avalonia 版 / MewUI 版）が共有する **UI フレームワーク非依存**の機能を提供します。 名前空間はすべて `EsUtil.SaveEvidence.FadeMaskDragRect`、アセンブリ名は `FadeMaskDragRectControl.Core` です。

## 提供する外部機能

| 機能 | 提供元 | 内容 |
| --- | --- | --- |
| 動作モードの型 | `FadeMaskDragRectMode` | 複合コントロールの動作（切り出し / 範囲選択 / スクリーン選択 / ウィンドウ選択）を表す列挙型。 両 UI が同一の型を使う |
| 設定の単一値 | `FadeMaskDragRectOptions` | 内包コントロール（Marquee / 装飾 / Loupe）の設定と複合コントロール固有の設定（破線の色・ルーペのオフセット）を 1 つにまとめた不変値 |
| 付加情報ラベルの外観 | `FadeMaskDragRectLabelOptions` | 操作ラベル・情報ラベルの表示可否・寸法・色。 **文言の生成と描画はホストが行う** |
| モードの解釈 | `FadeMaskDragRectModes.Resolve` | モードから内包コントロールの状態（表示可否）を導出する |
| 選択矩形の可否判定 | `FadeMaskDragRectSelection.IsUsable` | 選択として成立するサイズかどうか |
| 選択矩形オーバーレイの描画 | `FadeMaskDragRectOverlay` | ルーペ画像上へ選択矩形の 4 辺を 2 色の破線で描画する |

## 公開 API の可視性

| 型 | 可視性 | 参照可能なアセンブリ |
| --- | --- | --- |
| `FadeMaskDragRectMode` | `public` | 任意（ホストがモードを指定する契約） |
| `FadeMaskDragRectOptions` | `public` | 任意（UI 実装が子コントロールへ分配する設定の単一値） |
| `FadeMaskDragRectLabelOptions` | `public` | 任意（ホストが描く付加情報ラベルの外観） |
| `FadeMaskDragRectState` / `FadeMaskDragRectModes` | `internal` | `FadeMaskDragRectControl.AvaloniaUI` / `FadeMaskDragRectControl.MewUI` / `.AvaloniaUI.Tests` / `.MewUI.Tests` / `.Core.Tests` |
| `FadeMaskDragRectSelection` | `internal` | 同上 |
| `FadeMaskDragRectOverlay`（入れ子の `Dash` を含む） | `internal` | 同上 |

`internal` は `InternalsVisibleTo` で UI 実装とテストへ公開します。 **`internal` → `public` の変更は破壊的な設計変更として扱います**。

## `FadeMaskDragRectMode`（列挙型）

| メンバー | 内容 |
| --- | --- |
| `Crop` | 切り出し（プレビューコントロール上のオーバーレイ。 全面暗転 + 選択矩形 + ルーペ） |
| `AreaSelect` | 範囲選択（全画面オーバーレイ。 全面暗転 + 選択矩形 + ルーペ + ドラッグハンドル） |
| `ScreenSelect` | スクリーン選択（ハイライト + スクリーン番号ラベルのみ。 クリック確定はホストが担う） |
| `WindowSelect` | ウィンドウ選択（ハイライト + ウィンドウタイトルラベルのみ。 クリック確定はホストが担う） |

`Crop` / `AreaSelect` は選択矩形を使うモード、`ScreenSelect` / `WindowSelect` は使わないモードです。 この区別は `FadeMaskDragRectModes.Resolve` が単一所有し、UI 実装は `switch` を持ちません。

## `FadeMaskDragRectOptions`（設定の単一値）

| メンバー | 型 | 既定値 | 内容 |
| --- | --- | --- | --- |
| `Marquee` | `MarqueeOptions` | `MarqueeOptions.Default` | 選択矩形の設定（選択の判断に使う項目のみ。 色は含まない） |
| `Labels` | `FadeMaskDragRectLabelOptions` | 既定値 | ホストが描く操作・情報ラベルの外観 |
| `Loupe` | `LoupeOptions` | `LoupeOptions.Default` | ルーペの設定（拡大率・表示サイズ・寸法） |
| `SelectionRectColorA` | `System.Drawing.Color` | 白 | ルーペ画像上の選択矩形破線の色 A（描画開始色） |
| `SelectionRectColorB` | `System.Drawing.Color` | 黒 | 同 色 B（色 A と交互に使用する） |
| `LoupeOffset` | `double` | `20.0` | ルーペのカーソル中心からの右下オフセット（DIP） |
| `Default`（static） | `FadeMaskDragRectOptions` | — | 部分変更の基点（`Default with { ... }`） |

装飾の設定は持ちません（表示可否・太さ・色は外観（提示層の `MarqueeAppearance`）が、ハイライト矩形は `MarqueePresenter.HighlightDomainRect` が単一所有する）。

**色は描画にだけ必要な設定のため本値に含みません**（提示層の `MarqueeAppearance` / `LoupeAppearance` が単一所有し、ホストがコントロールへ設定します）。 例外はルーペ画像へ直接書き込む破線の色（`SelectionRectColorA` / `SelectionRectColorB`）で、これは描画 API ではなくピクセル書き込みに使うため本値が持ちます。

**実行時状態**（選択矩形・ハイライト矩形・ルーペ中心・拡大元画像）と**環境値**（DPI スケーリング）は本値に含みません。

```csharp
// 設定は 1 回の代入で子コントロールへ分配される（項目ごとの写し替えをしない）
FadeMaskDragRectOptions options = FadeMaskDragRectOptions.Default with
{
    Loupe = LoupeOptions.Default with { RequestedSize = 240 },
};
```

## `FadeMaskDragRectLabelOptions`（付加情報ラベルの外観）

| メンバー | 型 | 既定値 | 内容 |
| --- | --- | --- | --- |
| `ShowOperationLabel` | `bool` | `true` | 操作ラベル（確定・キャンセルの操作方法）を表示するか |
| `OperationFontSize` | `double` | `12.0` | 操作ラベルのフォントサイズ（DIP） |
| `OperationColor` | `System.Drawing.Color` | `#FFDCDCDC` | 操作ラベルの文字色 |
| `OperationBackgroundColor` | `System.Drawing.Color` | `#99000000` | 操作ラベルの背景色（黒 60%） |
| `ShowInfoLabel` | `bool` | `true` | 情報ラベル（対象ウィンドウのタイトル等）を表示するか |
| `InfoFontSize` | `double` | `14.0` | 情報ラベルのフォントサイズ（DIP） |
| `InfoColor` | `System.Drawing.Color` | 白 | 情報ラベルの文字色 |
| `InfoBackgroundColor` | `System.Drawing.Color` | `#99000000` | 情報ラベルの背景色（黒 60%） |
| `Default`（static） | `FadeMaskDragRectLabelOptions` | — | 部分変更の基点 |

**表示可否は 2 つの条件がともに成立したときだけ真**です。 本値のスイッチ（`ShowOperationLabel` / `ShowInfoLabel`）に加え、ホストが返す文言が空でないことが必要です（対象外のモードでは文言が空になるため、スイッチだけでは判定できません）。

**操作ラベル・情報ラベルは選択矩形コントロールの概念ではありません**（撮影オーバーレイ等へ組み込むホストが必要とする付加情報）。 したがって文言の生成と描画はホストが行い、本値はその描画に使う外観のみを保持します。

## `FadeMaskDragRectState`（内部・モードから導出する状態）

| メンバー | 型 | 内容 |
| --- | --- | --- |
| `UsesSelection` | `bool` | 選択矩形・ルーペ・選択矩形の破線を使用するモードか（`Crop` / `AreaSelect`） |
| `ShowFullOverlay` | `bool` | 全面暗転（注目なし）を表示するか。 `UsesSelection` と同値 |
| `ShowSelectionOverlay` | `bool` | ルーペ画像上へ選択矩形の破線を描画するか。 `UsesSelection` と同値 |
| `IsLoupeVisible(hasSource, hasCenter)` | `bool` | ルーペを表示するか（選択矩形を使うモード **かつ** 画像あり **かつ** 中心あり） |

`default(FadeMaskDragRectState)` は「選択矩形を使わない」になるため、UI 実装は本値を直接生成せず `FadeMaskDragRectModes.Resolve` から得ます。

## `FadeMaskDragRectModes`（内部・モードの解釈）

| メソッド | 内容 |
| --- | --- |
| `Resolve(FadeMaskDragRectMode mode)` | モードに対応する `FadeMaskDragRectState` を返す。 **未定義の値は `WindowSelect` と同じ扱い**（選択矩形を使わない） |

## `FadeMaskDragRectSelection`（内部・選択矩形の可否）

| メソッド | 内容 |
| --- | --- |
| `IsUsable(double width, double height)` | 幅と高さがともに正の場合は true。 **片方だけの判定を UI に書かせない** |

## `FadeMaskDragRectOverlay`（内部・選択矩形オーバーレイの描画）

| メソッド | 内容 |
| --- | --- |
| `DrawSelectionRect(EsBitmap bitmap, EsRect selectionDomainRect, EsPoint captureTopLeft, double scale, Color colorA, Color colorB)` | 選択矩形の 4 辺を 2 色の破線で `EsBitmap`（ルーペの表示画像）へ書き込む |
| `EnumerateDashes(EsRect selectionDomainRect, EsPoint captureTopLeft, double scale, int pixelSize)` | 選択矩形の 4 辺の破線を 1 ピクセル厚の矩形として列挙する（`IReadOnlyList<Dash>`） |

**破線の幾何は `EnumerateDashes` が単一所有**し、描画先は呼び出し側が選べます（Avalonia 版は `DrawSelectionRect` で `EsBitmap` へ直接書き込み、MewUI 版は `EnumerateDashes` の結果を `SKCanvas` へ描きます）。 このため両 UI の破線が同じ位置・長さ・色順になります。

| 定数（内部） | 値 | 内容 |
| --- | --- | --- |
| ダッシュ長 | 1 ピクセル | 描画する部分の長さ |
| 空白長 | 1 ピクセル | ダッシュ間の空き |
| 破線の最小倍率 | `2.0` | これ未満では破線が潰れるため描画しない |

`Dash`（入れ子の内部型）は 1 ダッシュ分の矩形（`X` / `Y` / `Width` / `Height`）と色の種別（`IsColorA`）を持ちます。 水平な辺は高さ 1、垂直な辺は幅 1 になります。 画像の外側へはみ出すダッシュは描画側が無視します（例外にしません）。

## 依存関係

| 依存 | 用途 |
| --- | --- |
| `Platform.Core`（`EsUtil.Platform.Core.*`） | コア幾何型（`EsRect` / `EsPoint`）・`EsBitmap`（描画先） |
| `Feature.Marquee.Core`（`EsUtil.Feature.Marquee.Core.Setting`） | `MarqueeOptions`（設定の構成要素） |
| `Feature.Loupe.Core`（`EsUtil.Feature.Loupe.Core.Setting`） | `LoupeOptions`（設定の構成要素） |

UI フレームワーク（Avalonia / MewUI）への依存はありません。 外部パッケージへの直接依存もありません。
