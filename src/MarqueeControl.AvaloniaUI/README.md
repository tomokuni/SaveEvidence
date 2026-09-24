# MarqueeControl

> プロジェクト名: `MarqueeControl.AvaloniaUI`（Avalonia UI 版）。UI 非依存のロジックは `MarqueeControl.Core`、MewUI 版は `MarqueeControl.MewUI` にあります。

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
- **選択矩形の外側に描く装飾の描画**: 暗転マスク・穴あけ枠・サイズ/操作/情報ラベル・ハイライト（外観と表示ポリシーは `DecoOptions`（装飾層 `MarqueeControl.Core` の `Decoration` 名前空間の `MarqueeDecoOptions`）が単一所有し、描画内容の判断は Core（`Decoration`）が持つ）。**選択枠線はこの穴あけ枠が兼ねる**（別々に描くと線幅が二重になるため）
- **ハンドル表示の切替**: `Options.ShowHandles = false` でハンドルを描かず枠線と外側の装飾だけを表示する
- カーソル座標ラベルの表示（任意設定・`(x, y)` 形式）
- **文字は UI 層が要素として配置する**: サイズラベル（`MarqueeSizeLabel`）とカーソル座標ラベルの**文言・外観・配置の計算は Core が単一所有**し、実寸から配置矩形を求める写像は `MarqueeControl.CoreForControl` の `MarqueeLabelLayoutPlan` が単一所有し、UI 層が `Border` + `TextBlock` を子要素として置く（子要素は親の描画より後に描かれるため常に最上位）。操作・情報ラベルはアプリ固有の付加情報のためホストが自前で描画完了イベント（`Rendered`）で行います（ホスト向けの共通実装として UI 層のラベル要素 を提供）。`Rendered` は選択矩形の描画内容（`Content`）と装飾の描画内容（`Deco`）を渡します（座標ラベルと装飾ラベルをまとめて描ける）
- ダブルクリックの確定/キャンセル判定（矩形内 = 確定 / 矩形外 = キャンセル）
- Avalonia Binding 対応の StyledProperty
- ドメイン↔DIP 座標変換ポート（`ICoordTransform` 直接注入。型は `Platform.Core` 提供）
- **閉区間描画（始点・終点を含む）**: ハンドルと外側の装飾（穴あけ枠）は終点（Right/Bottom）に +1 して DIP 変換（キャプチャの閉区間 +1 と一致。規則は装飾層の `DecoGeometry` が単一所有）
- **ホイール操作**: Ctrl+ホイールで `ZoomRequested`（方向と発生位置） / 通常ホイールで `ScrollRequested`（生の回転量）を発火します。解釈（Ctrl の判定・Shift の横変換・連携先への適用）とログは組み込みの層（`MarqueeControl.CoreForControl` の `MarqueeWheelInterpreter`）が単一所有します。連携 ScrollViewer（`AttachScrollViewer`）を設定した場合は適用先として登録され、本コントロールがスクロールします場合は上位 AP がスクロール先を決める
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
using Estable.AvaloniaUI.Marquee;
using EsUtil.Platform.Core.Coordinate; // ICoordTransform / CoordTransform（コア提供）
using EsUtil.Platform.Core.Geometry;   // EsPoint / EsSize（コア提供）
```

## 5. クイックスタート

### AXAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:marquee="clr-namespace:Estable.AvaloniaUI.Marquee;assembly=MarqueeControl.AvaloniaUI">
    <Canvas>
        <marquee:MarqueeControl x:Name="SelectionOverlay" />
    </Canvas>
</Window>
```

外観・対話パラメータは 1 つの設定値（`Options`）にまとまっているため、AXAML の属性では指定せずコード（またはバインディング）から設定します。

### Code-behind（設定・イベント購読・座標ポート設定）

```csharp
var marquee = new MarqueeControl
{
    // 選択矩形の設定をまとめて 1 回で渡す（同値の再設定では再描画されない）
    Options = MarqueeOptions.Default with
    {
        HandleSize = 10.0,
    },

    // 装飾（暗転マスク・穴あけ枠・ラベル）は別の設定値が単一所有する
    DecoOptions = MarqueeDecoOptions.Default with
    {
        // 選択枠線は外側の装飾（暗転マスクの穴あけ枠）が描く
        OuterAppearance = MarqueeDecoOptions.Default.OuterAppearance with { BorderColor = Colors.White.ToDrawingColor() },
    },
};

// 結果は公開イベントで通知される（マウス・キーイベントを直接処理しない）
marquee.SelectionChanged += (_, e) => UpdatePreview(e.Rect);
marquee.SelectionCompleted += (_, e) => ConfirmSelection(e.Rect);
marquee.SelectionCancelled += (_, _) => CancelSelection();
marquee.ConfirmRequested += (_, e) => ConfirmSelection(e.Rect);
marquee.CancelRequested += (_, _) => CancelSelection();

// 文字（ラベル・座標ラベル）はコントロールが描かないため、描画完了イベントで描く
marquee.Rendered += (_, e) =>
{
    // 図形（装飾・ハンドル・マーカー）はコントロールが描く。
    // 文字は Content.Layers の順に従い、UI 層が配置したテキスト要素が表示する。
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
│   ├── MarqueeControl.csproj     # 独立プロジェクト（Platform.AvaloniaUI + MarqueeControl.Core + MarqueeControl.Core を参照）
│   ├── MarqueeControl.cs         # 公開 API + 効果の適用 + 通知の転送 + ポインター入力 + 描画
│   ├── MarqueeControl.Input.cs   # キーボード入力（フォーカス非依存。コア型 EsKey への変換と通知）
│   └── MarqueeControl.Visual.cs     内部: `Content.Layers` の順に図形を描き、ラベル要素を配置する
│   ├── SelectionRectEventArgs.cs  # 公開イベント引数（選択矩形通知）
│   ├── MarqueePointEventArgs.cs  # 公開イベント引数（座標通知）
│   ├── MarqueeRenderedEventArgs.cs  # 公開イベント引数（描画完了。選択矩形の描画内容と装飾の描画内容を通知する）
│   ├── ラベルの要素配置（ArrangeOverride）    # 公開（ホスト向け）: ラベル・座標ラベルの文字描画の共通実装
│   └── Drawing/                   # 内部型（MarqueeDrawer / MarqueeOuterDrawer / MarqueeInnerDrawer / MarqueeBrushCache）
├── sample/                        # サンプルアプリ（sample/README.md 参照）
└── tests/                         # 単体テスト（公開 API 経由）
```

### 9.1 共有コア（`MarqueeControl.Core`）

UI 非依存の幾何計算・状態機械・入力の解釈は `src/MarqueeControl.Core` が単一所有します（Avalonia / MewUI で共有）。

```text
MarqueeControl.Core/
├── src/
│   ├── HitTarget.cs                 # 公開型（ドラッグハンドル種別 enum）
│   ├── MarqueeInteractionState.cs   # 公開型（対話状態 enum）
│   ├── MarqueeSnapshot.cs           # 公開型（状態 Snapshot record。座標は EsRect / EsPoint）
│   ├── MarqueeOptions.cs            # 公開型（外観・対話パラメータの設定値 record）
│   ├── Marquee.cs                   # 公開ファサード（操作の唯一の窓口。設定・効果の適用・描画内容の解決）
│   ├── IMarqueeEffects.cs           # コアが要求する効果の適用先（UI 層が実装する）
│   ├── MarqueeFrame.cs              # 内部型（選択矩形の描画内容のフレーム解決。ファサードからのみ使う）
│   ├── MarqueeSession.cs            # 内部型（状態管理クラス。入力の解釈・通知・効果。ファサードからのみ使う）
│   ├── MarqueeNotice.cs             # 内部型（コア → UI の通知の種別と値）
│   ├── MarqueeFeedback.cs           # 内部型（コア → UI の効果。カーソル操作を含む）
│   ├── MarqueeLogMessage.cs                 # 内部型（入力の判断・設定変更の LoggerMessage 定義）
│   ├── MarqueeState.cs              # 内部型（状態保持 + 更新ロジック）
│   └── Selection/                    # 内部型（SelectionGeometry）
└── tests/                            # 単体テスト（UI 非依存・並列実行）
```

### 9.2 装飾（`MarqueeControl.Core` の `Decoration` 名前空間）

選択矩形の**外側・内側に描く装飾**（暗転マスク・穴あけ枠・ラベル・ハイライト・塗り）は、選択矩形コントロール単体の概念ではないため Core 内のサブ概念として同居します（`MarqueeDecoOptions`（設定）/ `MarqueeDrawContent`（描画内容）。詳細は同プロジェクトの `README.md`）。

## 10. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
