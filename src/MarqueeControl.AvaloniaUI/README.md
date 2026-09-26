# MarqueeControl

> プロジェクト名: `MarqueeControl.AvaloniaUI`（Avalonia UI 版）。UI 非依存のロジックは `Feature.MarqueeCore`、MewUI 版は `MarqueeControl.MewUI` にあります。

## 目次

- [MarqueeControl](#marqueecontrol)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. インストール](#4-インストール)
  - [5. クイックスタート](#5-クイックスタート)
    - [AXAML](#axaml)
    - [Code-behind（設定・イベント購読・座標ポート設定）](#code-behind設定イベント購読座標ポート設定)
  - [6. 座標系](#6-座標系)
  - [7. サンプル](#7-サンプル)
  - [8. ドキュメント](#8-ドキュメント)
  - [9. プロジェクト構成](#9-プロジェクト構成)
  - [10. ライセンス](#10-ライセンス)

---

## 1. 概要

Avalonia UI 用のリサイズ可能な領域選択コントロールです。マウスドラッグ・キーボード微調整（nudge）・描画をコントロール内で完結します。

## 2. 機能一覧

- マウスドラッグによる領域選択
- 選択矩形のドラッグ移動
- 8方向ドラッグハンドルによるリサイズ
- 矢印キーによる1Pixel単位のキーボード微調整（nudge）
- ドメイン座標系で正確な nudge（矢印キー1回 = ドメイン1Pixel）
- ドラッグハンドル・始点マーカーの自己描画
- **選択矩形の外側に描く装飾の描画**: 暗転マスク・穴あけ枠・ハイライト（**表示ポリシーと寸法**は `DecoOptions`（`Feature.MarqueeCore` の `Decoration` 名前空間の `MarqueeDecoOptions`）が、**色**は描画層（`MarqueeControl.UIBase` の `MarqueePaint`）が単一所有し、描画内容の判断は Core（`Decoration`）が持つ）。**選択枠線はこの穴あけ枠が兼ねる**（別々に描くと線幅が二重になるため）
- **ハンドル表示の切替**: `Options.ShowHandles = false` でハンドルを描かず枠線と外側の装飾だけを表示する
- **色の設定（`Paint`）**: ハンドル・十字マーカー・ラベル・装飾の色は**描画にだけ必要な設定**のためコアには含めず、描画層（`MarqueeControl.UIBase` の `MarqueePaint`）が単一所有する。`Paint` プロパティで指定し（既定は `MarqueePaint.Default`）、部分変更は `Paint = MarqueePaint.Default with { ... }` で行う（指定しない色は既定値を保つ）。**描くかどうかは色のアルファではなく `DecoOptions` の明示フラグ（`ShowOverlay` / `ShowOuterBorder` / `ShowInnerFill` / `ShowInnerBorder`）が決める**
- カーソル座標ラベルの表示（任意設定・`(x, y)` 形式）
- **文字は UI 層が要素として配置する**: サイズラベル（`MarqueeTextPlacement`）とカーソル座標ラベル（`MarqueeTextPlacement`）の**文言・寸法は Core が、色は描画層の `MarqueePaint` が単一所有**し、実寸から配置矩形を求める写像は描画層（`MarqueeControl.UIBase` の `MarqueeLabelPlan`）が単一所有し、UI 層が `Border` + `TextBlock` を子要素として置く（子要素は親の描画より後に描かれるため常に最上位）。実寸測定は自 UI の API（`FormattedText`）で行う。操作・情報ラベルはアプリ固有の付加情報のため、ホスト（`FadeMaskDragRectControl`）が `Rendered` のハンドラで自前で描く（外観は `FadeMaskDragRectOptions.Labels` が単一所有する）
- ダブルクリックの確定/キャンセル判定（矩形内 = 確定 / 矩形外 = キャンセル）
- Avalonia Binding 対応の StyledProperty
- ドメイン↔DIP 座標変換ポート（`ICoordTransform` 直接注入。型は `Platform.Core` 提供）
- **閉区間描画（始点・終点を含む）**: ハンドルと外側の装飾（穴あけ枠）は終点（Right/Bottom）に +1 して DIP 変換（キャプチャの閉区間 +1 と一致。規則は装飾層の `DecoGeometry` が単一所有）
- **ホイール操作**: Ctrl+ホイールで `ZoomRequested`（方向と発生位置） / 通常ホイールで `ScrollRequested`（生の回転量）を発火します。解釈（Ctrl の判定・Shift の横変換・連携先への適用）とログは組み込みの層（`MarqueeControl.UIBase` の `MarqueeWheelInterpreter`）が単一所有します。連携 ScrollViewer（`AttachScrollViewer`）を設定した場合は適用先として登録され、本コントロールがスクロールします場合は上位 AP がスクロール先を決める
- **薄い UI 層**: 入力の解釈（nudge セッション・確定/キャンセル・スナップ・クリック検出・最小サイズ判定）はコアの公開ファサード（`Marquee`）が担います。本コントロールは「入力の受付」「効果の適用」「通知の転送」「描画」のみを行います

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- C# 14

## 4. インストール

現在は NuGet パッケージではなく、プロジェクト参照で利用します。

```xml
<ProjectReference Include="..\MarqueeControl.AvaloniaUI\src\MarqueeControl.AvaloniaUI.csproj" />
```

必要な名前空間:

```csharp
using Avalonia;
using Avalonia.Media;                        // Colors
using EsUtil.SaveEvidence.Marquee.AvaloniaUI;
using EsUtil.Feature.MarqueeCore.Setting; // MarqueeOptions
using EsUtil.Feature.MarqueeCore.Decoration; // MarqueeDecoOptions
using EsUtil.SaveEvidence.Marquee.UIBase;    // MarqueePaint
using EsUtil.Platform.Core.Coordinate;       // ICoordTransform / CoordTransform（コア提供）
using EsUtil.Platform.Core.Geometry;         // EsPoint / EsSize（コア提供）
```

## 5. クイックスタート

### AXAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:marquee="clr-namespace:EsUtil.SaveEvidence.Marquee.AvaloniaUI;assembly=MarqueeControl.AvaloniaUI">
    <Canvas>
        <marquee:MarqueeControl x:Name="SelectionOverlay" />
    </Canvas>
</Window>
```

外観・表示可否・対話パラメータは設定値（`Options` / `DecoOptions`）、色は描画の設定値（`Paint`）にまとまっているため、AXAML の属性では指定せずコード（またはバインディング）から設定します。

### Code-behind（設定・イベント購読・座標ポート設定）

```csharp
var marquee = new MarqueeControl
{
    // 選択矩形の設定をまとめて 1 回で渡す（同値の再設定では再描画されない）
    Options = MarqueeOptions.Default with
    {
        HandleSize = 10.0,
    },

    // 色（描画にだけ必要な設定）は描画層の 1 値が単一所有する
    Paint = MarqueePaint.Default with
    {
        // 選択枠線は外側の装飾（暗転マスクの穴あけ枠）が描く（色はここで与える）
        OuterBorderColor = Colors.White.ToDrawingColor(),
    },

    // 装飾（暗転マスク・穴あけ枠）の表示可否と寸法は別の設定値が単一所有する
    DecoOptions = MarqueeDecoOptions.Default with
    {
        ShowOverlay = true,
        ShowOuterBorder = true,
        OuterAppearance = MarqueeDecoOptions.Default.OuterAppearance with { BorderThickness = 2.0 },
    },
};

// 結果は公開イベントで通知される（マウス・キーイベントを直接処理しない）
marquee.SelectionChanged += (_, e) => UpdatePreview(e.Rect);
marquee.SelectionCompleted += (_, e) => ConfirmSelection(e.Rect);
marquee.SelectionCancelled += (_, _) => CancelSelection();
marquee.ConfirmRequested += (_, e) => ConfirmSelection(e.Rect);
marquee.CancelRequested += (_, _) => CancelSelection();

// ラベル（サイズ・カーソル座標）はコントロールがテキスト要素として配置する。
// ホストは描画完了イベントで付加情報（操作・情報ラベル）を描ける（図形はコントロールが描き終えている）。
marquee.Rendered += (_, e) =>
{
    // e.Content: 穴あけ矩形（付加情報の基準）・サイズラベル・座標ラベル・層の順序
    // e.Context: ホスト固有の付加情報を描くための描画コンテキスト
};

// 座標ポートを設定（ドメイン = 画像ピクセル等）。ポート生成は Platform.AvaloniaUI の CoordTransform ファクトリを使用する。
marquee.SetCoordinateTransform(CoordTransform.DomainScale(() => 2.0)); // 例: DPI 200%（Forward = DIP→ドメイン × 2）
// 複雑な変換は Compose / ScrollOffset / ImageLayout 等で組み立てる

panel.Children.Add(marquee);
```

## 6. 座標系

MarqueeControl は 3 つの座標系を使い分けます。

| 座標系 | 用途 |
| --- | --- |
| ドメイン座標 | 選択状態・nudge・イベント通知 |
| コントロールローカル DIP | マウス入力・描画 |
| 画面物理 Pixel | カーソル操作・画面変換 |

ドメイン ↔ DIP の変換は `SetCoordinateTransform(ICoordTransform?)` で注入します（Forward = DIP→ドメイン / Inverse = ドメイン→DIP / Bounds = ドメイン境界）。

## 7. サンプル

`sample/` に動作確認用のサンプルアプリが含まれています。マウスドラッグによる選択・nudge・外観変更・イベントログを確認できます。

```bash
dotnet run --project sample/MarqueeControl.AvaloniaUI.Sample.csproj
```

詳細は [sample/README.md](sample/README.md) を参照してください。

## 8. ドキュメント

- [外部機能仕様書](SPEC_ExtFunc.md)
- [システム仕様書](SPEC_System.md)

## 9. プロジェクト構成

```text
MarqueeControl/
├── src/
│   ├── MarqueeControl.csproj     # 独立プロジェクト（Feature.MarqueeCore + MarqueeControl.UIBase + Platform.AvaloniaUI を参照）
│   ├── MarqueeControl.cs         # 公開 API + 効果の適用 + 通知の転送 + ポインター入力 + 描画の起動
│   ├── MarqueeControl.Input.cs   # キーボード入力（フォーカス非依存。コア型 EsKey への変換と通知）
│   └── MarqueeControl.Visual.cs     内部: `Content.Layers` の順に図形を描くよう描画層（MarqueeRenderer）へ渡し、ラベル要素を配置する
│   ├── SelectionRectEventArgs.cs  # 公開イベント引数（選択矩形通知）
│   ├── MarqueePointEventArgs.cs  # 公開イベント引数（座標通知）
│   ├── MarqueeRenderedEventArgs.cs  # 公開イベント引数（描画完了。描画コンテキストと描画内容を通知する）
│   └── （ラベル要素（`Border` + `TextBlock`）の配置は本コントロールが行う）
├── sample/                        # サンプルアプリ（sample/README.md 参照）
└── tests/                         # 単体テスト（公開 API 経由）
```

### 9.1 共有コア（`Feature.MarqueeCore`）と描画層（`MarqueeControl.UIBase`）

UI 非依存の幾何計算・状態機械・入力の解釈は `src/Feature.MarqueeCore` が単一所有し、描画の手順（層の順序と描画ポートへの写し方）と色（`MarqueePaint`）は `src/MarqueeControl.UIBase` が単一所有します（Avalonia / MewUI で共有）。UI 層はアダプタ（`AvaloniaCanvas` など）とラベル要素の配置を担います。

本コントロールの層構成（Core / UIBase / UI）は [`src/README.md`](../README.md) を参照してください。

```text
Feature.MarqueeCore/
├── src/
│   ├── Marquee.cs / Marquee.DrawContent.cs  # 公開ファサードとその partial（描画内容の解決）
│   ├── Contract/                            # 描画と効果の契約（IMarqueeRenderer / IMarqueeEffects / MarqueeRendererBase / MarqueeEffectsBase）
│   ├── Drawing/                             # 描画内容の契約（MarqueeDrawContent / MarqueeDrawLayer / MarqueeTextPlacement 等）
│   ├── Interaction/                         # 入力と通知（HitTarget / MarqueeInteractionState / MarqueeSnapshot / MarqueeNotice / MarqueeCursorKind）
│   ├── Setting/                             # 設定の値（MarqueeOptions）
│   ├── Logging/                             # ログ定義（MarqueeLogMessage。内部）
│   ├── Content/ / Session/                  # 内部（フレーム解決の入力・状態機械）
│   ├── Selection/                           # 内部（純粋な幾何計算）
│   └── Decoration/                          # 装飾（設定・外観・幾何・描画内容）
└── tests/                                   # 単体テスト（UI 非依存・並列実行）```

### 9.2 装飾（`Feature.MarqueeCore` の `Decoration` 名前空間）

選択矩形の**外側・内側に描く装飾**（暗転マスク・穴あけ枠・ラベル・ハイライト・塗り）は、選択矩形コントロール単体の概念ではないため Core 内のサブ概念として同居します（`MarqueeDecoOptions`（設定）/ `MarqueeDrawContent`（描画内容）。詳細は同プロジェクトの `README.md`）。**色は描画層（`MarqueeControl.UIBase` の `MarqueePaint`）が単一所有します**（装飾の設定は表示可否と寸法だけを持ちます）。

## 10. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
