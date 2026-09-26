# LoupeControl 外部機能仕様書

## 目次

- [LoupeControl 外部機能仕様書](#loupecontrol-外部機能仕様書)
  - [目次](#目次)
  - [1. 文書概要](#1-文書概要)
  - [2. 目的](#2-目的)
  - [3. 外部利用条件](#3-外部利用条件)
    - [3.1 プロジェクト参照](#31-プロジェクト参照)
    - [3.2 必要な名前空間](#32-必要な名前空間)
    - [3.3 Visual Treeへの配置](#33-visual-treeへの配置)
  - [4. 基本的な使用方法](#4-基本的な使用方法)
    - [4.1 AXAMLでの使用例](#41-axamlでの使用例)
    - [4.2 コードからの使用例](#42-コードからの使用例)
    - [4.3 Binding用ViewModel例](#43-binding用viewmodel例)
  - [5. 公開API](#5-公開api)
    - [5.1 プロパティ](#51-プロパティ)
      - [5.1.1 Source](#511-source)
      - [5.1.2 CenterPoint](#512-centerpoint)
      - [5.1.3 Options（設定の単一値）](#513-options設定の単一値)
      - [5.1.4 RenderScaling](#514-renderscaling)
      - [5.1.5 Metrics（寸法）と Paint（色）](#515-metrics寸法と-paint色)
      - [5.1.6 IsVisible](#516-isvisible)
      - [5.1.7 Log](#517-log)
    - [5.2 メソッド](#52-メソッド)
      - [5.2.1 RequestRender](#521-requestrender)
      - [5.2.2 Clear](#522-clear)
      - [5.2.3 SetCoordinateTransform](#523-setcoordinatetransform)
      - [5.2.4 RefreshPosition](#524-refreshposition)
    - [5.3 表示位置・サイズの自己計算（座標変換ポート方式）](#53-表示位置サイズの自己計算座標変換ポート方式)
      - [5.3.1 PositionOffset](#531-positionoffset)
    - [5.4 イベント](#54-イベント)
      - [5.4.1 Rendered](#541-rendered)
    - [5.5 型](#55-型)
      - [5.5.1 LoupeControl](#551-loupecontrol)
  - [6. 非同期描画](#6-非同期描画)
  - [7. ライフサイクルと所有権](#7-ライフサイクルと所有権)
    - [7.1 Source の所有権](#71-source-の所有権)
    - [7.2 生成 Bitmap の所有権](#72-生成-bitmap-の所有権)
  - [8. 表示位置・サイズの責務](#8-表示位置サイズの責務)
  - [9. 推奨使用パターン](#9-推奨使用パターン)
  - [10. 制約事項](#10-制約事項)
  - [11. サンプルアプリケーション](#11-サンプルアプリケーション)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `LoupeControl.AvaloniaUI`（公開クラスは `LoupeControl`） |
| プロジェクト | `LoupeControl.AvaloniaUI/src/LoupeControl.AvaloniaUI.csproj` |
| 名前空間 | `EsUtil.SaveEvidence.Loupe.AvaloniaUI` |
| 対象フレームワーク | `net10.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主要基底クラス | `Avalonia.Controls.Panel`（コードのみ。AXAML を持たない） |
| 表示方式 | 子の `Image` 要素へのBitmap設定と、中心座標ラベルのテキスト要素（`Border` + `TextBlock`） |
| 主な用途 | 画像中心周辺の拡大表示、画像端の透明領域の市松模様表示 |

## 2. 目的

`LoupeControl` は、指定された画像の任意の中心座標周辺を拡大し、ルーペ画像として表示するAvaloniaコントロールです。

次の機能を提供します。

- Avalonia Bindingによる画像入力
- 指定中心座標周辺の画像領域抽出
- 1〜64倍の拡大表示
- 画像範囲外の透明領域への市松模様表示
- 十字線の表示
- 外枠の表示
- DPIスケーリングに対応したBitmap生成
- 非同期のルーペ画像生成（切り出し・合成は UI スレッドで実行）
- 古い非同期描画結果を破棄する世代管理
- 非表示時のBitmap解放
- 描画エラーの注入ロガーへの記録
- 描画完了通知イベント（`Rendered`・表示情報付きオーバーレイ描画用。中心座標ラベルの契約も含む）

## 3. 外部利用条件

### 3.1 プロジェクト参照

利用側プロジェクトから `LoupeControl.AvaloniaUI.csproj` を参照します（`Feature.LoupeCore` は本プロジェクトが参照するため、追加の参照は不要です）。

```xml
<ProjectReference Include="..\LoupeControl.AvaloniaUI\src\LoupeControl.AvaloniaUI.csproj" />
```

### 3.2 必要な名前空間

```csharp
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EsUtil.SaveEvidence.Loupe.AvaloniaUI;
```

### 3.3 Visual Treeへの配置

`LoupeControl` は `Panel` であるため、表示するには親Visualの子要素として配置します。

```csharp
var loupe = new LoupeControl
{
    IsVisible = true,
    IsHitTestVisible = false,
    ZIndex = 100
};

panel.Children.Add(loupe);
```

`DrawCurrent` による外部 `DrawingContext` 描画APIは提供しません。ルーペは常に内部の `Image` 要素で表示します。

## 4. 基本的な使用方法

### 4.1 AXAMLでの使用例

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:loupe="clr-namespace:EsUtil.SaveEvidence.Loupe.AvaloniaUI;assembly=LoupeControl.AvaloniaUI">
    <Canvas>
        <loupe:LoupeControl
            IsVisible="{Binding IsLoupeVisible}"
            Source="{Binding SourceImage}"
            CenterPoint="{Binding CenterPoint}"
            Options="{Binding Options}"
            RenderScaling="1.0" />
    </Canvas>
</Window>
```

### 4.2 コードからの使用例

```csharp
var loupe = new LoupeControl
{
    Source = sourceBitmap,
    CenterPoint = new Point(400, 300),
    Options = LoupeOptions.Default with { RequestedSize = 240, ZoomLevel = 4 },
    RenderScaling = 1.0,
    IsHitTestVisible = false,
    IsVisible = true
};

canvas.Children.Add(loupe);
Canvas.SetLeft(loupe, 300);
Canvas.SetTop(loupe, 220);
```

### 4.3 Binding用ViewModel例

```csharp
public IImage SourceImage { get; } = sourceBitmap;

public Point? CenterPoint { get; set; } = new(400, 300);

public bool IsLoupeVisible { get; set; } = true;
```

`Source` は `Image.Source` と同じAvaloniaの `IImage?` 型です。ピクセル抽出処理は `Bitmap` と `EsBitmapAvaloniaImage`（`EsBitmap` を内包する `IImage` アダプタ）をサポートします。

## 5. 公開API

公開 API は「プロパティ / メソッド / イベント / 型」の 4 分類で構成されます。
座標を扱う API は、ドメイン座標系 / DIP 座標系のどちらであるかを明記します。

### 5.1 プロパティ

#### 5.1.1 Source

| 項目 | 内容 |
| --- | --- |
| 型 | `IImage?` |
| 既定値 | `null` |
| 用途 | 拡大元画像 |
| Binding | 対応 |
| 実装上の対応型 | `Bitmap` / `EsBitmapAvaloniaImage` |

`Source` が `null` の場合、画像なしの市松模様を描画します。

`Source` に `Bitmap` 以外の `IImage`（`EsBitmapAvaloniaImage` を除く）を設定した場合、`NotSupportedException` が発生します。例外は `LoupeControl.Log` に記録されます。

#### 5.1.2 CenterPoint

| 項目 | 内容 |
| --- | --- |
| 型 | `Point?` |
| 単位 | **ドメイン座標系**（元画像の物理ピクセル座標） |
| 既定値 | `null` |
| 用途 | 拡大表示の中心座標 |

`CenterPoint` が `null` の場合、画像が設定されていても市松模様のみを描画します。

画像範囲外の座標も指定できます。抽出領域と画像の重なり部分は画像として表示され、範囲外部分は透明となって市松模様が表示されます。

#### 5.1.3 Options（設定の単一値）

| 項目 | 内容 |
| --- | --- |
| 型 | `LoupeOptions`（コアの `EsUtil.Feature.LoupeCore.Setting`） |
| 既定値 | `LoupeOptions.Default`（表示サイズ 160 / 拡大率 4 / 寸法は `LoupeMetrics` の既定値） |
| 用途 | ルーペの設定（表示サイズ・拡大率・寸法）を 1 つの値で指定する |

```csharp
loupe.Options = LoupeOptions.Default with { RequestedSize = 240, ZoomLevel = 4 };
```

設定の値・既定値・数値項目の安全域への補正は**コアの `LoupeOptions` が単一所有**します（UI 側は既定値も補正の登録も持ちません）。値の設定経路（オブジェクト初期化子・`with` 式・バインディング・逆シリアル化）によらず、範囲内の値だけが保持されます。

| `LoupeOptions` の項目 | 型 | 既定値 | 補正範囲 | 説明 |
| --- | --- | --- | --- | --- |
| `RequestedSize` | `int` | `160` | `1`〜`4096` | ルーペの基本サイズ（DIP・一辺） |
| `ZoomLevel` | `int` | `4` | `1`〜`64` | 拡大率 |
| `Metrics` | `LoupeMetrics` | コアの既定値 | | ルーペの寸法（市松模様のタイルサイズ・外枠の太さ）とラベル表示の可否。**色・フォントサイズ・角丸は含まない**（`Paint` プロパティが単一所有する） |

**表示サイズ（`RequestedSize`）のクランプはコア（ファサード）が内包**します: 表示サイズはコアが本値（基本サイズ）を親領域サイズにクランプして導出します（ホストは基本サイズを設定するだけ）。クランプした「実際の表示サイズ」は公開 API として外部へ公開しません（ホストは関与せず、コアが表示と描画で同じ値を使います）。

**設定と状態（データ）の区別**: マウス移動ごとに変わる `Source` / `CenterPoint` は個別プロパティのままです（設定に含めません）。DPI スケーリング係数（`RenderScaling`）と表示位置のオフセット（`PositionOffset`）もフレームワーク・ホスト固有のため設定に含めません。

**描画の差し替え**: `Renderer`（`ILoupeRenderer`。既定は自 UI の `LoupeRendererAvaloniaUI`（色は `Paint` の値で生成）。null 不可）で描画の実装を差し替えられます（描画先は `AvaloniaCanvas` が担うため、描画手順だけを交換できます）。設定すると再生成とラベル要素の再配置を要求します。

```csharp
loupe.Renderer = MyLoupeRenderer.Instance;   // 描画手順の差し替え
```

#### 5.1.4 RenderScaling

| 項目 | 内容 |
| --- | --- |
| 型 | `double` |
| 既定値 | `1.0` |
| 補正範囲 | `0.1`〜`8.0` |
| 用途 | DIPからBitmap物理ピクセルへの変換 |

Bitmapの一辺は概ね次の式で決定されます。

```text
BitmapSize = ceil(表示サイズ × RenderScaling)
```

環境値（DPI）のためコアの `LoupeOptions` には含めず、補正（`coerce`）はプロパティが担います（安全域の定義はコアの `Loupe.Limits` が単一所有します）。

#### 5.1.5 Metrics（寸法）と Paint（色）

ルーペの寸法（市松模様のタイルサイズ・外枠の太さ）とラベル表示の可否は **コアの `LoupeMetrics`（`EsUtil.Feature.LoupeCore.Setting`）が単一所有**し、`Options` の一部（`Options.Metrics`）として指定します（両 UI で既定値・意味が一致します）。**色と、ラベルのフォントサイズ・角丸は描画にだけ必要な設定**であり、描画層 `LoupeControl.UIBase` の `LoupePaint`（`EsUtil.SaveEvidence.Loupe.UIBase`）が単一所有し、本コントロールの `Paint` プロパティとして指定します（コアはこれらの値を決定せず、描画可否の判定にも使いません）。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `Options.Metrics` | `LoupeMetrics` | コアの既定値 | ルーペの寸法とラベル表示の可否。`with` 式で部分変更する。数値項目はコアの `LoupeMetrics` が生成時に安全域へ補正する |
| `Paint` | `LoupePaint`（`EsUtil.SaveEvidence.Loupe.UIBase`） | `LoupePaint.Default` | 描画に使う色（下地・市松模様・外枠・十字線・中心座標ラベルの 7 色）とラベルのフォントサイズ・角丸。`LoupePaint.Default with { ... }` で部分変更する |

`Options.Metrics`（コアの `LoupeMetrics`）の項目（詳細は `Feature.LoupeCore` の `SPEC_ExtFunc.md` を参照）:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `CheckerSize` | `int` | `6` | 市松模様の 1 タイルサイズ（DIP。1〜128 に補正） |
| `FrameWidth` | `int` | `2` | 外枠の太さ（DIP。1〜64 に補正） |
| `ShowCoordLabel` | `bool` | `true` | 中心座標ラベルの表示有無（中心が未設定のときは描画しない） |

`Paint`（`LoupePaint`。詳細は `LoupeControl.UIBase` の `SPEC_ExtFunc.md` を参照）の項目:

| 項目 | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `BackgroundColor` | `System.Drawing.Color` | 透明 | 下地（ベース）色。市松模様・拡大画像より先に全面へ塗る。背後の要素を透かさない場合は不透明色を指定する（既定は透明 = 背後の要素を透かす） |
| `CheckerColorA` / `CheckerColorB` | `System.Drawing.Color` | Gray 30% / DarkGray 30% | 市松模様の明色 / 暗色。アルファ 0 の場合は描画しない |
| `FrameColor` | `System.Drawing.Color` | LightGray（`#FFD3D3D3`） | 外枠の色。アルファ 0 の場合は描画しない |
| `CrossColor` | `System.Drawing.Color` | 赤 40%（`#66FF0000`） | 十字線の色。アルファ 0 の場合は描画しない |
| `CoordLabelColor` | `System.Drawing.Color` | 白 | 座標ラベルの文字色 |
| `CoordLabelBackgroundColor` | `System.Drawing.Color` | 黒 60%（`#99000000`） | 座標ラベルの背景色 |
| `CoordLabelFontSize` | `double` | `12.0` | 座標ラベルのフォントサイズ（DIP。6〜48 に補正。非数は既定値へ補正） |
| `CoordLabelCornerRadius` | `int` | `2` | 座標ラベルの背景の角丸半径（DIP。0〜64 に補正。0 は角丸なし） |

座標ラベルは中心座標（ドメイン座標）を `(x, y)` 形式でルーペ上端中央に表示します。実寸 → 配置矩形の写像（内側余白 4 / 1 DIP・上端中央の中央寄せ）は描画層（`LoupeControl.UIBase` の `LoupeLabelPlan`）が単一所有し、体裁の値（色・フォントサイズ・角丸）は描画の設定（`Paint`）が単一所有します。**表示はコントロールが行います**（`Border` + `TextBlock` の子要素として配置する。実寸測定は `FormattedText`、描画順はコアの `LoupeDrawLayer` が単一所有するため、UI は実測と要素の配置だけを行います）。

寸法項目（`CheckerSize` / `FrameWidth`）は DIP の値であり、描画時に**コアが物理ピクセルへ換算済み**です（`LoupeRenderRequest.CheckerSizePixels` / `FrameWidthPixels`）（`RenderScaling` に追従するため、DPI ≠ 100% でも DIP 基準の見た目になります）。UI 層は換算式を持ちません。

#### 5.1.6 IsVisible

Avalonia標準の `Visual.IsVisible` を使用します。初期値は `false`（コンストラクタで設定。表示指示は `IsVisible = true` で行う）。

- `true`: ルーペ画像を生成して内部 `Image` に表示
- `false`: 未実行の再描画を無効化し、現在のBitmapを解放

非表示中に画像・座標・設定を変更しても、Bitmapは生成されません。

#### 5.1.7 Log

| 項目 | 内容 |
| --- | --- |
| 型 | `ILogger` |
| 既定値 | `NullLogger.Instance` |
| 用途 | 描画エラー・診断の通知先ロガー（出力先は利用側のロギング構成に依存） |

### 5.2 メソッド

#### 5.2.1 RequestRender

```csharp
void RequestRender()
```

現在の状態からルーペ画像の再生成を要求します（**プロパティ変更を伴わない再生成要求**）。

ホストが `Rendered` でビットマップへオーバーレイを描画する場合、重ねるデータの変化（一覧の選択・外部からの状態変更等）ではプロパティが変わらないため、本メソッドで再生成を要求します。

ルーペのプロパティは値が変わらない設定（同一値の再設定）では変更通知を発行しないため、オーバーレイの元データがルーペのプロパティに現れない場合は本メソッドが唯一の再生成手段です。合成コントロール（`FadeMaskDragRectControl`）は、破線の色（外観設定）・破線の表示可否（モード）・選択矩形の変更時に本メソッドを呼び出します。

生成処理自体は非同期です。完了は `Rendered` イベントで通知されます（戻り値を待つ必要はありません）。プロパティ変更と同じ経路を通るため、複数の要求は 1 フレームに 1 回の生成へ結合されます（呼び出し頻度を気にせず呼べます）。非表示中に呼び出した場合は Bitmap を解放して終了します。

```csharp
// 一覧の選択が変わったとき（ルーペの中心は変わらないが、破線を描き直したい）
loupe.RequestRender();
```

#### 5.2.2 Clear

```csharp
void Clear()
```

ルーペを非表示にし、現在のBitmapを解放します。

```csharp
loupe.Clear();
```

#### 5.2.3 SetCoordinateTransform

```csharp
void SetCoordinateTransform(ICoordTransform? port)
```

論理座標（ドメイン）↔ 親パネル DIP の座標変換ポートを設定します（null = 等価変換。MarqueeControl と同方式）。

変換関数は呼び出し毎に評価されるため、ウィンドウサイズ変更・ズーム・スクロール等のレイアウト変化に追従できます。表示位置・サイズは本ポートから自己計算されます（イベント時の DIP キャッシュは使わない）。

#### 5.2.4 RefreshPosition

```csharp
void RefreshPosition()
```

表示位置と表示サイズを最新のレイアウトから再計算します（外部からの更新要求）。

座標変換ポートは呼び出し毎に最新の表示状態を評価するため、パン・ズーム等のレイアウト非変更なビューポート変化時にホスト（上位 AP）が呼び出すことで、再計算だけで追従できます。

### 5.3 表示位置・サイズの自己計算（座標変換ポート方式）

`CenterPoint`（ドメイン座標）と `SetCoordinateTransform` で設定したポートから、ルーペの表示位置と表示サイズを自己計算します。

| 項目 | 計算方法 |
| --- | --- |
| 表示サイズ | 基本サイズ（`Options.RequestedSize`）を親パネルサイズにクランプ（親より大きい場合は縮小） |
| 表示位置 | 中心 →（ポート）→ 親パネル DIP → カーソル右下 + `PositionOffset` → 親パネル内にクランプ（`RenderTransform` で反映） |

再評価タイミング: `CenterPoint` 変更 / ポート設定（`SetCoordinateTransform`）/ `PositionOffset` 変更 / `Options` 変更 / `IsVisible` 変更 / レイアウト時（`ArrangeCore` オーバーライド）/ 外部要求（`RefreshPosition`）。

#### 5.3.1 PositionOffset

| 項目 | 内容 |
| --- | --- |
| 型 | `double` |
| 既定値 | `0` |
| 単位 | DIP 座標系 |
| 用途 | ルーペ中心（カーソル位置）からの右下方向へのオフセット量。カーソルにルーペが重ならないよう、ホストが設定する（例: Crop の `CaptureSettings.LoupeOffset` / AreaSelect の `20.0`） |

### 5.4 イベント

#### 5.4.1 Rendered

```csharp
event EventHandler<LoupeRenderedEventArgs>? Rendered
```

ルーペの描画完了（表示内容の反映）を通知します。拡大画像が実際にコントロールへ反映された時点で発火します。

| 項目 | 内容 |
| --- | --- |
| 引数型 | `LoupeRenderedEventArgs` |
| 発火タイミング | 描画結果が内部 `Image` へ反映された直後（非同期描画・バッチ化のため、プロパティ変更より 1 フレーム遅れる） |
| 用途 | ホストがルーペに重ねるオーバーレイ（線分・選択枠等）の描画位置を計算する |

**`LoupeRenderedEventArgs` のプロパティ**

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Bitmap` | `EsBitmap` | 描画済みのルーペ画像。イベントハンドラ内でピクセルへオーバーレイを直接描画できる（所有権は `LoupeControl` 側） |
| `CaptureTopLeft` | `Point?` | 切り出し領域の左上（ドメイン座標）。中心未設定時は null |
| `Scale` | `double` | ドメイン座標 → ルーペ画像ピクセル座標の変換係数（拡大率と同値。DPI スケーリングの影響を受けない） |

オーバーレイの描画位置は `(ドメイン座標 − CaptureTopLeft) × Scale` で求められます。

通知するのはホストがオーバーレイを描くために必要な最小限（描画済み画像・切り出し領域の左上・スケール）です。表示位置・表示サイズ・拡大率・拡大中心は、描画に必要な値をホストが既に保持しているため通知しません（必要な場合はコントロールの公開プロパティから取得します）。

```csharp
loupe.Rendered += (_, e) =>
{
    if (e.CaptureTopLeft is not { } topLeft) return;
    var local = (ドメイン座標 - topLeft) * e.Scale;
    // local の位置へ線分等を e.Bitmap へ描画する（表示はコントロールが反映する）
};
```

> プロパティ変更による再描画（`RequestRender`）では、最新状態のスナップショットに対して 1 回だけ発火します（連続変更は 1 フレームに 1 回に結合）。

### 5.5 型

公開型は `LoupeControl` と `LoupeRenderedEventArgs` です。
描画・画像処理の実装は internal（非公開）です。UI 非依存コア（`Feature.LoupeCore`）と描画層（`LoupeControl.UIBase` の `LoupeRenderer` / `LoupePaint` / `LoupeLabelPlan`）の型は public です（外部が UI アダプタを実装できるようにするため）。描画の手順と色は描画層が単一所有し、本プロジェクトは描画ポート（`AvaloniaCanvas`）とラベル要素の配置を担います。
UI 固有のログ（`LoupeRenderLog`）は本プロジェクトの internal です。

#### 5.5.1 LoupeControl

公開 API は「5.1 プロパティ」「5.2 メソッド」に記載の通りです。
StyledProperty は同ファイルに定義されます（Avalonia のバインディング・スタイル設定に必要なため public）。

## 6. 非同期描画

ルーペ画像の生成は非同期で実行され、古い描画結果は自動的に破棄されます。複数のプロパティが連続して変更された場合、再描画は 1 フレームに 1 回に結合されます。

古い描画結果が無効になるタイミングは次のとおりです。

- `CenterPoint` の変更
- `Source` の変更
- `Options`（設定・寸法）または `Paint`（色）の変更
- `IsVisible = false`
- `RequestRender()` の実行

描画エラー（`Source` が未対応の `IImage` 等）は `Log` へ記録され、アプリは落ちません（例外は開始側が観測し、呼び出し元へは伝播しません）。

## 7. ライフサイクルと所有権

### 7.1 Source の所有権

`Source` に設定した元画像（`Bitmap` / `EsBitmapAvaloniaImage`）の所有権は利用側にあります。

`LoupeControl` は元画像を破棄しません（`EsBitmapAvaloniaImage` を設定した場合も、内包する `EsBitmap` の破棄は利用側に委ねる）。

### 7.2 生成 Bitmap の所有権

ルーペ表示用に生成したBitmapの所有権は `LoupeControl` が持ちます。

- 次のBitmapを採用するとき、前のBitmapを破棄
- 非表示時に破棄
- `Clear()` 時に破棄
- 世代が古い生成結果は採用せず破棄

## 8. 表示位置・サイズの責務

`LoupeControl` は画像生成と内部Image表示に加え、**表示位置・表示サイズを自己計算**します
（座標変換ポート方式。Marquee と同様）。

- 表示位置: `CenterPoint`（ドメイン座標）を `SetCoordinateTransform` で設定したポートで親パネル DIP へ変換し、カーソル右下 + `PositionOffset` + クランプした位置を `RenderTransform` で反映します。
- 表示サイズ: `Options.RequestedSize`（基本サイズ）を親パネルサイズにクランプして内部管理します（ホストは基本サイズを設定するだけ）。
- 再評価タイミング: `CenterPoint` 変更 / ポート設定 / `PositionOffset` 変更 / `Options` 変更 / レイアウト時（`ArrangeCore`）/ 外部要求（`RefreshPosition`）。

上位APでは次の処理を行います。

- ポインター座標の取得
- 元画像座標への変換
- `SetCoordinateTransform` / `PositionOffset` の設定
- `LoupeControl` のVisual Treeへの追加・削除

## 9. 推奨使用パターン

```csharp
private readonly LoupeControl _loupe = new()
{
    IsHitTestVisible = false,
    // 設定はコアの値を 1 つ設定する（with 式で部分変更する）
    // 寸法はコアの Options.Metrics、色は Paint（LoupePaint）へ指定する
    Options = LoupeOptions.Default with
    {
        RequestedSize = 240,
        ZoomLevel = 4,
        Metrics = new LoupeMetrics
        {
            CheckerSize = 8,
            FrameWidth = 2,
        },
    },
    Paint = LoupePaint.Default with
    {
        CrossColor = DrawingColor.FromArgb(102, 255, 0, 0),
        CheckerColorA = DrawingColor.White,
        CheckerColorB = DrawingColor.LightGray,
        FrameColor = DrawingColor.LightGray,
    },
};

private void InitializeLoupe(Panel panel, Bitmap source)
{
    _loupe.Source = source;
    panel.Children.Add(_loupe);
    _loupe.IsVisible = true;
}

private void UpdateLoupe(Point imagePixelPoint, Point displayPoint)
{
    _loupe.CenterPoint = imagePixelPoint;
    Canvas.SetLeft(_loupe, displayPoint.X);
    Canvas.SetTop(_loupe, displayPoint.Y);
}

private void RefreshLoupe()
{
    // 中心は変わらないが、ビットマップへ重ねる内容が変わった場合に再生成を要求する
    _loupe.RequestRender();
}
```

## 10. 制約事項

- 領域抽出を行うため、実質的な画像入力対応型はAvalonia `Bitmap` です。
- `RenderTargetBitmap` と `DrawingContext` の合成はUIスレッドで実行します。
- 高頻度更新では、利用側でポインター更新頻度を制限することを推奨します。
- `Source` に設定した画像は利用側が有効期間を管理してください。
- 色（`Paint`）・寸法（`Options.Metrics`）の連続変更は、同一Dispatcher待機中である場合に1回へ集約されます。
- `RequestRender()` は完了待機APIではありません。完了は `Rendered` イベントで通知されます。
- 外部 `DrawingContext` へルーペを描画するAPIは提供しません。必要な場合も `LoupeControl` をVisual Treeへ配置してください。

## 11. サンプルアプリケーション

動作確認用サンプルは次のプロジェクトです。

```powershell
dotnet run --project sample/LoupeControl.AvaloniaUI.Sample.csproj
```

サンプルでは次を確認できます。

- `Source` のBinding
- `CenterPoint` のマウス追従
- 画像端から外れた部分の部分的な市松模様
- `Options`（`ZoomLevel` / `RequestedSize` / `Metrics`）と `Paint`（色）・`RenderScaling`
- 十字線・市松模様・外枠の色変更（`Paint`）
- `IsVisible`、`Clear()`、`RequestRender()`
- `Log`（描画エラーの通知先 ILogger 設定例）
