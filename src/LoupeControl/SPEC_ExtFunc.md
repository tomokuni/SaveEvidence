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
      - [5.1.3 RequestedSize](#513-requestedsize)
      - [5.1.4 RenderScaling](#514-renderscaling)
      - [5.1.5 座標ラベル](#515-座標ラベル)
      - [5.1.6 ZoomLevel](#516-zoomlevel)
      - [5.1.7 CrossColor](#517-crosscolor)
      - [5.1.8 CheckerSize](#518-checkersize)
      - [5.1.9 CheckerColorA / CheckerColorB](#519-checkercolora--checkercolorb)
      - [5.1.10 FrameColor](#5110-framecolor)
      - [5.1.11 FrameWidth](#5111-framewidth)
      - [5.1.12 IsVisible](#5112-isvisible)
      - [5.1.13 Log](#5113-log)
    - [5.2 メソッド](#52-メソッド)
      - [5.2.1 RenderAsync](#521-renderasync)
      - [5.2.2 ForceUpdate](#522-forceupdate)
      - [5.2.3 Clear](#523-clear)
      - [5.2.4 SetCoordinateTransform](#524-setcoordinatetransform)
      - [5.2.5 RefreshPosition](#525-refreshposition)
    - [5.3 表示位置・サイズの自己計算（座標変換ポート方式）](#53-表示位置サイズの自己計算座標変換ポート方式)
      - [5.3.1 PositionOffset](#531-positionoffset)
    - [5.4 イベント](#54-イベント)
      - [5.4.1 Rendered](#541-rendered)
    - [5.5 型](#55-型)
      - [5.5.1 LoupeControl](#551-loupecontrol)
  - [6. 非同期描画と世代管理](#6-非同期描画と世代管理)
    - [6.1 非同期描画の範囲](#61-非同期描画の範囲)
    - [6.2 世代番号](#62-世代番号)
    - [6.3 再描画要求の集約](#63-再描画要求の集約)
  - [7. 例外とログ](#7-例外とログ)
    - [7.1 ロガーの責務](#71-ロガーの責務)
    - [7.2 ログレベルポリシー](#72-ログレベルポリシー)
      - [ログ定義一覧](#ログ定義一覧)
    - [7.3 Bitmap / PbBitmapImage 以外の IImage](#73-bitmap--unifiedbitmapimage-以外の-iimage)
    - [7.4 出力先の例](#74-出力先の例)
  - [8. ライフサイクルと所有権](#8-ライフサイクルと所有権)
    - [8.1 Sourceの所有権](#81-sourceの所有権)
    - [8.2 生成Bitmapの所有権](#82-生成bitmapの所有権)
    - [8.3 一時Bitmap](#83-一時bitmap)
  - [9. 表示位置・サイズの責務](#9-表示位置サイズの責務)
  - [10. 推奨使用パターン](#10-推奨使用パターン)
  - [11. 制約事項](#11-制約事項)
  - [12. サンプルアプリケーション](#12-サンプルアプリケーション)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `LoupeControl` |
| プロジェクト | `LoupeControl/LoupeControl.csproj` |
| 名前空間 | `Estable.AvaloniaUI.Loupe` |
| 対象フレームワーク | `net10.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主要基底クラス | `Avalonia.Controls.UserControl` |
| 表示方式 | 内部 `Image` 要素へのBitmap設定 |
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
- 非同期の画像抽出
- 古い非同期描画結果を破棄する世代管理
- 非表示時のBitmap解放
- 描画エラーの注入ロガーへの記録
- 描画完了通知イベント（`Rendered`・表示情報付きオーバーレイ描画用）

## 3. 外部利用条件

### 3.1 プロジェクト参照

利用側プロジェクトから `LoupeControl.csproj` を参照します。

```xml
<ProjectReference Include="..\LoupeControl\LoupeControl.csproj" />
```

### 3.2 必要な名前空間

```csharp
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Estable.AvaloniaUI.Loupe;
```

### 3.3 Visual Treeへの配置

`LoupeControl` は `UserControl` であるため、表示するには親Visualの子要素として配置します。

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
        xmlns:loupe="clr-namespace:Estable.AvaloniaUI.Loupe;assembly=LoupeControl">
    <Canvas>
        <loupe:LoupeControl
            IsVisible="{Binding IsLoupeVisible}"
            Source="{Binding SourceImage}"
            CenterPoint="{Binding CenterPoint}"
            RequestedSize="240"
            ZoomLevel="4"
            RenderScaling="1.0"
            CrossColor="#66FF0000"
            CheckerSize="8"
            CheckerColorA="Gray"
            CheckerColorB="#C0C0C0"
            FrameColor="LightGray"
            FrameWidth="2" />
    </Canvas>
</Window>
```

### 4.2 コードからの使用例

```csharp
var loupe = new LoupeControl
{
    Source = sourceBitmap,
    CenterPoint = new Point(400, 300),
    RequestedSize = 240,
    ZoomLevel = 4,
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

`Source` は `Image.Source` と同じAvaloniaの `IImage?` 型です。ピクセル抽出処理は `Bitmap` と `PbBitmapImage`（`PbBitmap` を内包する `IImage` アダプタ）をサポートします。

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
| 実装上の対応型 | `Bitmap` / `PbBitmapImage` |

`Source` が `null` の場合、画像なしの市松模様を描画します。

`Source` に `Bitmap` 以外の `IImage`（`PbBitmapImage` を除く）を設定した場合、`NotSupportedException` が発生します。例外は `LoupeControl.Log` に記録されます。

#### 5.1.2 CenterPoint

| 項目 | 内容 |
| --- | --- |
| 型 | `Point?` |
| 単位 | **ドメイン座標系**（元画像の物理ピクセル座標） |
| 既定値 | `null` |
| 用途 | 拡大表示の中心座標 |

`CenterPoint` が `null` の場合、画像が設定されていても市松模様のみを描画します。

画像範囲外の座標も指定できます。抽出領域と画像の重なり部分は画像として表示され、範囲外部分は透明となって市松模様が表示されます。

#### 5.1.3 RequestedSize

| 項目 | 内容 |
| --- | --- |
| 型 | `int` |
| 単位 | DIP 座標系 |
| 既定値 | `160` |
| 補正範囲 | `1`〜`4096` |

ルーペの基本サイズ（ユーザー設定値）を設定します。表示するルーペの幅・高さ（正方形）の元になります。

- **クランプはコントロールが内包**します: 表示サイズは本値（基本サイズ）を `UpdatePosition` が親パネルサイズにクランプして導出します（ホストは基本サイズを設定するだけ）。
- 親パネル未接続時（配置前・テスト等）は基本サイズがそのまま表示サイズとして使用されます。
- クランプした「実際の表示サイズ」は公開 API として外部へ公開しません（内部管理）。

#### 5.1.4 RenderScaling

| 項目 | 内容 |
| --- | --- |
| 型 | `double` |
| 既定値 | `1.0` |
| 補正範囲 | `0.1`〜`8.0` |
| 用途 | DIPからBitmap物理ピクセルへの変換 |

Bitmapの一辺は概ね次の式で決定されます。

```text
BitmapSize = ceil(RequestedSize × RenderScaling)
```

#### 5.1.5 座標ラベル

ルーペ上部に中心座標ラベル（`(x, y)` 形式・ドメイン座標）を表示します。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `ShowCoordLabel` | `bool` | `true` | 座標ラベルの表示有無 |
| `CoordLabelColor` | `IBrush?` | `White` | 座標ラベルの文字色 |
| `CoordLabelBackgroundColor` | `IBrush?` | `黒 60%（#99000000）` | 座標ラベルの背景色（不透明度はアルファに統合） |
| `CoordLabelFontSize` | `double` | `12.0` | 座標ラベルのフォントサイズ（6〜48 に補正） |

#### 5.1.6 ZoomLevel

| 項目 | 内容 |
| --- | --- |
| 型 | `int` |
| 既定値 | `4` |
| 補正範囲 | `1`〜`64` |

拡大倍率です。画像の抽出サイズは次の式で算出されます。

```text
CaptureSize = ceil(BitmapSize / ZoomLevel)
```

#### 5.1.7 CrossColor

| 項目 | 内容 |
| --- | --- |
| 型 | `IBrush?` |
| 既定値 | `赤 40%（#66FF0000）` |
| `null` | 十字線を描画しない |

十字線の色です。不透明度は色のアルファに統合します（例: 赤 40% = `#66FF0000`）。

#### 5.1.8 CheckerSize

| 項目 | 内容 |
| --- | --- |
| 型 | `int` |
| 単位 | DIP 座標系として指定し、描画時にRenderScalingで物理ピクセルへ変換 |
| 既定値 | `6` |
| 補正範囲 | `1`〜`128` |

市松模様の1タイルサイズです。

#### 5.1.9 CheckerColorA / CheckerColorB

| 項目 | 内容 |
| --- | --- |
| 型 | `IBrush?` |
| 既定値A | `Gray 30%（#4C808080）` |
| 既定値B | `Silver 30%（#4CA9A9A9）` |
| `null` | 該当色を透明として描画 |

#### 5.1.10 FrameColor

| 項目 | 内容 |
| --- | --- |
| 型 | `IBrush?` |
| 既定値 | `Brushes.LightGray` |
| `null` | 外枠を描画しない |

#### 5.1.11 FrameWidth

| 項目 | 内容 |
| --- | --- |
| 型 | `int` |
| 単位 | DIP 座標系として指定し、描画時にRenderScalingで物理ピクセルへ変換 |
| 既定値 | `2` |
| 補正範囲 | `1`〜`64` |

#### 5.1.12 IsVisible

Avalonia標準の `Visual.IsVisible` を使用します。初期値は `false`（コンストラクタで設定。表示指示は `IsVisible = true` で行う）。

- `true`: ルーペ画像を生成して内部 `Image` に表示
- `false`: 未実行の再描画を無効化し、現在のBitmapを解放

非表示中に画像・座標・外観を変更しても、Bitmapは生成されません。

#### 5.1.13 Log

| 項目 | 内容 |
| --- | --- |
| 型 | `ILogger` |
| 既定値 | `NullLogger.Instance` |
| 用途 | 描画エラー・診断の通知先ロガー（出力先は利用側のロギング構成に依存） |

### 5.2 メソッド

#### 5.2.1 RenderAsync

```csharp
Task RenderAsync()
```

現在のプロパティ状態からルーペ画像を非同期に生成します。

処理の分担は次のとおりです。

1. `IImage` が `Bitmap` または `PbBitmapImage` か検証
2. 元画像（`Bitmap` は複製、`PbBitmapImage` は内包 `PbBitmap`）から領域抽出をバックグラウンド処理
3. 抽出BitmapをUIスレッドへ渡す
4. `RenderTargetBitmap` と `DrawingContext` で合成
5. 世代番号を確認
6. 最新世代であれば内部 `Image.Source` へ設定

`Source` が `Bitmap` / `PbBitmapImage` 以外の `IImage` の場合は `NotSupportedException` がスローされます。

```csharp
await loupe.RenderAsync();
```

#### 5.2.2 ForceUpdate

```csharp
void ForceUpdate()
```

表示中のルーペ画像生成を開始します。同値設定で変更通知が発生しない場合などに使用します。

生成処理自体は非同期です。完了を待つ必要がある場合は、`RenderAsync()` を使用してください。

非表示中に呼び出した場合、Bitmapを解放して終了します。

#### 5.2.3 Clear

```csharp
void Clear()
```

ルーペを非表示にし、現在のBitmapを解放します。

```csharp
loupe.Clear();
```

#### 5.2.4 SetCoordinateTransform

```csharp
void SetCoordinateTransform(ICoordinatePort? port)
```

論理座標（ドメイン）↔ 親パネル DIP の座標変換ポートを設定します（null = 等価変換。DragRectControl と同方式）。

変換関数は呼び出し毎に評価されるため、ウィンドウサイズ変更・ズーム・スクロール等のレイアウト変化に追従できます。表示位置・サイズは本ポートから自己計算されます（イベント時の DIP キャッシュは使わない）。

#### 5.2.5 RefreshPosition

```csharp
void RefreshPosition()
```

表示位置と表示サイズを最新のレイアウトから再計算します（外部からの更新要求）。

座標変換ポートは呼び出し毎に最新の表示状態を評価するため、パン・ズーム等のレイアウト非変更なビューポート変化時にホスト（上位 AP）が呼び出すことで、再計算だけで追従できます。

### 5.3 表示位置・サイズの自己計算（座標変換ポート方式）

`CenterPoint`（ドメイン座標）と `SetCoordinateTransform` で設定したポートから、ルーペの表示位置と表示サイズを自己計算します。

| 項目 | 計算方法 |
| --- | --- |
| 表示サイズ | 基本サイズ（`RequestedSize`）を親パネルサイズにクランプ（親より大きい場合は縮小） |
| 表示位置 | 中心 →（ポート）→ 親パネル DIP → カーソル右下 + `PositionOffset` → 親パネル内にクランプ（`RenderTransform` で反映） |

再評価タイミング: `CenterPoint` 変更 / ポート設定（`SetCoordinateTransform`）/ `PositionOffset` 変更 / `IsVisible` 変更 / レイアウト時（`ArrangeCore` オーバーライド）/ 外部要求（`RefreshPosition`）。

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
| `Position` | `Point` | ルーペの表示左上（親パネル内 DIP） |
| `Size` | `double` | ルーペの表示サイズ（一辺・DIP） |
| `ZoomLevel` | `int` | 拡大率 |
| `CenterPoint` | `Point?` | 拡大中心（ドメイン座標 = 元画像ピクセル座標）。未設定時は null |
| `CaptureTopLeft` | `Point?` | 切り出し領域の左上（ドメイン座標）。中心未設定時は null |
| `Scale` | `double` | ドメイン座標 → ルーペ内容座標（ルーペローカル DIP）の変換係数 |

オーバーレイの描画位置は `(ドメイン座標 − CaptureTopLeft) × Scale` で求められます。

```csharp
loupe.Rendered += (_, e) =>
{
    if (e.CaptureTopLeft is not { } topLeft) return;
    var local = (ドメイン座標 - topLeft) * e.Scale;
    // e.Position / e.Size にオーバーレイを配置し、local に線分等を描画する
};
```

> プロパティ変更による再描画（`RequestRender`）では、最新状態のスナップショットに対して 1 回だけ発火します（連続変更は 1 フレームに 1 回に結合）。

### 5.5 型

公開型は `LoupeControl` と `LoupeRenderedEventArgs` です。
描画・画像処理・ログの実装はすべて internal（非公開）です。

#### 5.5.1 LoupeControl

公開 API は「5.1 プロパティ」「5.2 メソッド」に記載の通りです。
StyledProperty は同ファイルに定義されます（Avalonia のバインディング・スタイル設定に必要なため public）。

## 6. 非同期描画と世代管理

### 6.1 非同期描画の範囲

Avaloniaの描画オブジェクトはUIスレッドで扱う必要があるため、すべての処理をバックグラウンドへ移すのではありません。

| 処理 | 実行場所 |
| --- | --- |
| 画像領域のピクセル抽出 | バックグラウンド |
| ピクセル配列の領域抽出 | バックグラウンド |
| `WriteableBitmap` の生成・ロック書き込み | UIスレッド |
| `RenderTargetBitmap` の合成 | UIスレッド |
| `DrawingContext` の操作 | UIスレッド |
| 内部 `Image.Source` の更新 | UIスレッド |
| 世代判定 | UIスレッド |

### 6.2 世代番号

描画開始ごとに世代番号を発行します。

```text
世代1の描画開始
世代2の描画開始
世代2の抽出・合成が先に完了 → 世代2を採用
世代1が後から完了 → 古い結果として破棄
```

現在の世代と描画開始時の世代が一致しない場合、生成Bitmapを破棄して表示しません。

次の場合も古い描画結果は無効になります。

- `CenterPoint` の変更
- `Source` の変更
- 外観プロパティの変更
- `IsVisible = false`
- `ForceUpdate()` の実行

### 6.3 再描画要求の集約

各対象プロパティの変更は `RequestRender()` を経由します。

```text
プロパティ変更 → Dispatcherへ再描画要求を登録
次のプロパティ変更 → 既に登録済みなら追加登録しない
Dispatcher実行 → 最新状態をスナップショットして1回生成
```

外観プロパティの連続設定もこの仕組みにより集約されます。

## 7. 例外とログ

### 7.1 ロガーの責務

`LoupeControl` はファイルへ直接ログを書き込みません。

```csharp
public ILogger Log { get; set; } = NullLogger.Instance;
```

`Log` に設定された `ILogger` へエラーを記録します。実際の出力先は利用側のロギング構成に依存します。

### 7.2 ログレベルポリシー

ログ定義は `Logging/LogMessage.cs`（LoggerMessage ソース生成）に集約し、`ILogger` の拡張メソッドとして定義する。
呼び出し側では logger 引数を省略し、`Log` プロパティ経由で出力する（例: `Log.LogUpdateError(ex)`）。
トレースログは LogLevel.Trace に限定せず、「重要度 × 発生頻度」でレベルを使い分ける。

| LogLevel | 対象 | 本番での意図 |
| --- | --- | --- |
| `Error` | 例外・失敗（処理継続不可） | 常時出力し、障害を確実に残す |
| `Warning` | 異常だがフォールバックで継続 | 常時出力し、異常発生の痕跡を残す |
| `Information` | ユーザー操作の主要イベント（低頻度） | 常時出力し、操作手順の再現を可能にする |
| `Debug` | 操作イベントの詳細（設定変更） | 障害時のみ有効化して解析 |
| `Trace` | 高頻度・極詳細（再描画内部・位置更新） | 特別な性能・座標解析時のみ有効化 |

判断基準:

1. 例外・警告は常時残す（`Error` / `Warning`）
2. 操作の起点イベント（低頻度）は `Information`
3. マウス・キー等の操作イベント（中頻度）は `Debug`
4. 高頻度・極詳細（再描画・位置更新の連続呼び出し）は `Trace`
5. 設定変更（変更前後）は `Debug`

#### ログ定義一覧

| レベル | メソッド | 出力内容 |
| --- | --- | --- |
| `Error` | `LogMessage.LogUpdateError` | ルーペ更新の例外（スタックトレース付き） |
| `Debug` | `LogMessage.LogSettingChanged` | 設定値変更（変更前後） |
| `Trace` | `LogMessage.LogRequestRender` | 再描画要求（表示状態 / キュー済み） |
| `Trace` | `LogMessage.LogRenderCore` | RenderCore 実行（世代番号） |
| `Trace` | `LogMessage.LogGenerateAsyncDone` | 非同期生成の完了・破棄（設定有無 / 世代） |

### 7.3 Bitmap / PbBitmapImage 以外の IImage

`Source` に `Bitmap` / `PbBitmapImage` 以外の `IImage` が設定された場合:

1. `NotSupportedException` を生成
2. `Log.LogUpdateError(exception)` で記録
3. `RenderAsync()` を直接呼び出した場合は例外を呼び出し元へ返す
4. 自動再描画の場合は例外を観測済みとして処理し、UIスレッドを未処理例外で終了させない

### 7.4 出力先の例

利用側がNLogを設定している場合、`ILogger` のNLogプロバイダーとNLog設定に従ってファイル等へ出力されます。

`LoupeControl` 単体では出力先を決定しません。

## 8. ライフサイクルと所有権

### 8.1 Sourceの所有権

`Source` に設定した元画像（`Bitmap` / `PbBitmapImage`）の所有権は利用側にあります。

`LoupeControl` は元画像を破棄しません（`PbBitmapImage` を設定した場合も、内包する `PbBitmap` の破棄は利用側に委ねる）。

### 8.2 生成Bitmapの所有権

ルーペ表示用に生成したBitmapの所有権は `LoupeControl` が持ちます。

- 次のBitmapを採用するとき、前のBitmapを破棄
- 非表示時に破棄
- `Clear()` 時に破棄
- 世代が古い生成結果は採用せず破棄

### 8.3 一時Bitmap

バックグラウンドではピクセル配列だけを抽出し、合成（`RenderTargetBitmap` / `DrawingContext`）はUIスレッドで行います。

表示への反映は `PbBitmapImage`（`PbBitmap` を内包する `IImage`）を内部 `Image.Source` へ設定し、`WriteableBitmap` の生成は行いません（`ISkiaSharpApiLeaseFeature` による Skia 直描画。Skia が提供されない環境では no-op）。

`LoupeRenderer` が合成する一時 `RenderTargetBitmap` は合成完了後に破棄します。

## 9. 表示位置・サイズの責務

`LoupeControl` は画像生成と内部Image表示に加え、**表示位置・表示サイズを自己計算**します
（座標変換ポート方式。DragRect / FadeMask と同様）。

- 表示位置: `CenterPoint`（ドメイン座標）を `SetCoordinateTransform` で設定したポートで親パネル DIP へ変換し、カーソル右下 + `PositionOffset` + クランプした位置を `RenderTransform` で反映します。
- 表示サイズ: `RequestedSize`（基本サイズ）を親パネルサイズにクランプして内部管理します（ホストは基本サイズを設定するだけ）。
- 再評価タイミング: `CenterPoint` 変更 / ポート設定 / `PositionOffset` 変更 / レイアウト時（`ArrangeCore`）/ 外部要求（`RefreshPosition`）。

上位APでは次の処理を行います。

- ポインター座標の取得
- 元画像座標への変換
- `SetCoordinateTransform` / `PositionOffset` の設定
- `LoupeControl` のVisual Treeへの追加・削除

## 10. 推奨使用パターン

```csharp
private readonly LoupeControl _loupe = new()
{
    IsHitTestVisible = false,
    RequestedSize = 240,
    ZoomLevel = 4,
    CrossColor = Brushes.Red,
    CheckerSize = 8,
    CheckerColorA = Brushes.White,
    CheckerColorB = Brushes.LightGray,
    FrameColor = Brushes.LightGray,
    FrameWidth = 2
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

private async Task RefreshLoupeAsync()
{
    await _loupe.RenderAsync();
}
```

## 11. 制約事項

- 領域抽出を行うため、実質的な画像入力対応型はAvalonia `Bitmap` です。
- `RenderTargetBitmap` と `DrawingContext` の合成はUIスレッドで実行します。
- 高頻度更新では、利用側でポインター更新頻度を制限することを推奨します。
- `Source` に設定した画像は利用側が有効期間を管理してください。
- 外観プロパティの連続変更は、同一Dispatcher待機中である場合に1回へ集約されます。
- `ForceUpdate()` は完了待機APIではありません。完了待機が必要な場合は `RenderAsync()` を使用してください。
- 外部 `DrawingContext` へルーペを描画するAPIは提供しません。必要な場合も `LoupeControl` をVisual Treeへ配置してください。

## 12. サンプルアプリケーション

動作確認用サンプルは次のプロジェクトです。

```powershell
dotnet run --project sample/LoupeControl.Sample.csproj
```

サンプルでは次を確認できます。

- `Source` のBinding
- `CenterPoint` のマウス追従
- 画像端から外れた部分の部分的な市松模様
- `ZoomLevel`、`RequestedSize`、`RenderScaling`
- 十字線・市松模様・外枠の外観変更
- `IsVisible`、`Clear()`、`ForceUpdate()`、`RenderAsync()`
- `Log`（描画エラーの通知先 ILogger 設定例）
