# Feature.LoupeCore 外部機能仕様書

`Feature.LoupeCore` は UI フレームワーク非依存のルーペロジックと設定（表示サイズ・拡大率・寸法）を提供します。**操作の唯一の窓口は公開ファサード `Loupe`** であり、UI 層とホストは本型だけを参照します（外部が独自の UI アダプタを実装できるよう、必要な型は public です）。

**ファサードの実装詳細（配置計算・描画引数の計算・ラベル配置）は internal です**（単体テストにのみ `InternalsVisibleTo` で公開します。利用者はファサードの戻り値を使うため、内部手順を参照する必要がありません）。

**コントロール化にだけ必要な処理（再描画要求のバッチ化・世代番号管理）は `LoupeControl.UIBase` が所有します**（本プロジェクトはルーペの計算と寸法に限定します）。**描画の手順と色は `LoupeControl.UIBase` が所有し**（手順は下地 → 市松模様 → 拡大画像 → 十字線 → 外枠。色は `LoupePaint`。描画先は `IEsCanvas`）、本プロジェクトは描画 API も色も持ちません。

## 公開 API の可視性

| 型 | 可視性 | 用途 |
| --- | --- | --- |
| `Loupe` | `public` | **公開ファサード（操作の唯一の窓口）**。設定・環境値・中心・オフセットの保持、表示レイアウトの解決、描画入力の生成、通知内容の解決、描画実装の保持 |
| `ILoupeRenderer` | `public` | 描画内容を描画面へ写す実装の契約（**描画の差し替え点**。既定実装は描画層の `LoupeRenderer`） |
| `LoupeLayout` | `public` | 表示レイアウトの値（表示サイズ（DIP）と表示位置。配置しない場合は位置が null） |
| `LoupeDrawContent` | `public` | 1 フレーム分の描画内容（描画面の矩形・タイル列挙子・十字線 / 円枠の矩形・**層の順序**）。描画層は本値を写すだけ |
| `LoupeCheckerTiles` | `public` | 市松模様の暗色タイルの矩形を、**リストを確保せずに列挙する構造体**（描画内容の一部） |
| `LoupeRenderNotification` | `public` | 描画完了通知の値（切り出し左上・スケール）。描く層の順序は描画内容（`LoupeDrawContent`）が持ち、**描画順はコアが単一所有する**（テキストの層は末尾 = 常に最上位であり、テキストがある場合のみ含める） |
| `LoupeOptions` | `public` | 設定の単一値（ホストが設定する） |
| `LoupeMetrics` | `public` | 寸法の単一値（`LoupeOptions.Metrics` として、また単体でも寸法を指定する。**色・フォントサイズ・角丸は持たない**） |
| `Loupe.Limits`（`Loupe` の入れ子） | `public` | 数値パラメータの既定値・安全域とクランプ（UI がプロパティ登録に使う） |
| `LoupeRenderRequest` | `public` | 1 フレーム分の描画入力（コア⇔UI の契約） |
| `LoupeTextPlacement` | `public` | コアが UI 層へ渡すテキスト（**文言・基準点・アライン・描画領域**）。**要素として置くか canvas へ描くかは UI 層が決める**（フォントサイズ・角丸・色は描画層の `LoupePaint` が持つ） |
| `LoupeLogFormat` / `LoupeLogMessage` | `public` | ログの書式と定義（両 UI が同じレベル・書式で出力するための契約。UI のコードが拡張メソッドを呼ぶ） |

**internal（本アセンブリと単体テストのみ）**: 描画の計算・換算・列挙（`Checkerboard.LoupeCheckerboard`（明暗の判定）/ `Checkerboard.LoupeDivMath`（区画数の切り上げ）/ `Decoration.LoupeDecorationGeometry`（矩形）/ `Decoration.LoupeMetricsScaler`（寸法の換算）/ `Imaging.LoupeImageExtractor`（切り出し）/ `Loupe.ComputePosition` / `LoupeRenderRequest.ResolvePixelSize`）と、描画内容の解決の内部手順。いずれもファサード（`Loupe.ResolveLayout` / `Loupe.CreateRequest` / `Loupe.ResolveContent` / `Loupe.ResolveNotification`）とその内部計算が吸収して提供します。

**UI 固有の処理に対応するログは各 UI プロジェクトが所有します**（中間ビットマップを生成する描画パイプライン固有のログ等は `LoupeControl.AvaloniaUI` にあります）。

## `Loupe`（`EsUtil.Feature.LoupeCore`・`public sealed class`）

ルーペの**操作の唯一の窓口**（適用パターン: Facade）。UI 層とホストは本型だけを参照し、配置計算・描画引数の計算は internal のため外部からは直接呼べません。

| メンバー | 説明 |
| --- | --- |
| `Loupe(ILoupeRenderer renderer, LoupeOptions? options = null)` | 生成（設定を省略した場合は `LoupeOptions.Default`）。`renderer` は null 不可（本コアは描画層を参照できないため、描画実装は必須引数で注入する） |
| `Options` | 設定（表示サイズ・拡大率・寸法）の読み書き |
| `RenderScaling` | 表示 DIP → 物理ピクセルのスケーリング係数（環境値。補正は `Loupe.Limits` が単一所有する） |
| `CenterPoint` | 拡大中心（ドメイン座標）。未設定（null）は市松模様のみを描画し、配置も行わない |
| `PositionOffset` | 拡大中心からの右下オフセット（DIP） |
| `SetCoordinateTransform(port)` | ドメイン ↔ 親領域 DIP の座標変換ポートの設定（null は等価変換） |
| `ResolveLayout(parentSize)` | 親領域サイズから表示レイアウト（`LoupeLayout` = 表示サイズ + 位置）を解決する（拡大中心未設定時は位置が null）。**状態を変更しない**（親領域のサイズを保持しないため、値をいつ解決したかに依存しない） |
| `CreateRequest(displaySize, availableSize = null)` | 描画 1 フレーム分の入力（`LoupeRenderRequest`）の生成（DIP → 物理ピクセルの換算と描画先へのクランプを含む）。表示サイズは呼び出し側が渡す（`ResolveLayout` の結果、または描画先の表面から導出した DIP） |
| `ResolveContent(request)` | 描画 1 フレーム分の**描画内容**（`LoupeDrawContent` = 描画面・タイル・矩形・層の順序）を解決する（描画層は計算を持たない） |
| `ResolveNotification(request)` | 実際に描画した入力から描画完了通知の内容（`LoupeRenderNotification`）を解決する |
| `Renderer` | 描画の実装（`ILoupeRenderer`）の取得・設定（**描画の差し替え**。null 不可。設定後の再描画は UI 層が要求する） |
| `CoordLabelFontSize` / `CoordLabelCornerRadius`（描画実装＝UIBase の `LoupeRenderer` のプロパティ） | 中心座標ラベルのフォントサイズと角丸（描画の設定 `LoupePaint` の値への入口）。**描画の契約（`ILoupeRenderer`）には含めない** |

**本型が単一所有するもの**: 表示レイアウトの解決（表示サイズのクランプと表示位置の決定）・描画入力の組み立て・通知内容の解決・描画実装の保持。

### `ILoupeRenderer` のメンバー（`EsUtil.Feature.LoupeCore.Contract`・`public interface`）

| メンバー | 説明 |
| --- | --- |
| `DrawShapes(canvas, source, in request, in content)` | 図形の層（下地・市松模様・拡大画像・十字線・外枠）を描画順に描く（**テキストは描かない**）。テキストを canvas へ描く実装は基底の `DrawLabel`（既定は何もしない）を上書きする |

描く層とその順序はコアが単一所有し（`Loupe.ResolveContent` が返す `LoupeDrawContent.Layers`）、実装 は順序を判断しません。拡大元画像は UI ごとに型が異なるため引数で受け取ります（描画の契約には含めません）。本コアは描画層（`LoupeControl.UIBase`）を参照できない ため、既定実装（描画層の `LoupeRenderer`。色をコンストラクタで受け取る）は利用者が渡します。

```csharp
var loupe = new Loupe(new LoupeRenderer(LoupePaint.Default), options) { CenterPoint = cursorDomain, RenderScaling = 1.25 };
loupe.SetCoordinateTransform(port);

var layout = loupe.ResolveLayout(parentSize);          // 表示サイズ + 表示位置（配置はしない場合は Position が null）
var request = loupe.CreateRequest(layout.DisplaySize, canvasSize); // 描画入力（物理ピクセルを解決済み）
var notification = loupe.ResolveNotification(request); // 切り出し左上・スケール・層の順序
var content = loupe.ResolveContent(request);    // テキストは CoordLabelText として受け取る（UI 層が扱いを決める）
```

## `LoupeOptions`（`EsUtil.Feature.LoupeCore.Setting`・`public sealed record`）

ルーペの設定（表示サイズ・拡大率・寸法）を 1 つの値として所有します。両 UI は本型を 1 プロパティとして公開するだけであり、UI ごとに既定値がずれたり設定プロパティが増えたりしません。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `RequestedSize` | `int` | `160` | ルーペの基本サイズ（DIP・一辺）。安全域 1〜4096。表示サイズは UI 層が親領域・描画先へクランプして導出する |
| `ZoomLevel` | `int` | `4` | 拡大率（偶数推奨）。安全域 1〜64 |
| `Metrics` | `LoupeMetrics` | `new LoupeMetrics()` | ルーペの寸法（市松模様のタイルサイズ・外枠の太さ）と中心座標ラベルの表示可否。**色・フォントサイズ・角丸は含まない**（描画層の `LoupePaint` が単一所有する） |

`record` のため `with` 式による部分変更と値の等価比較ができます。等価比較は UI のプロパティシステムと併せて「同値の再設定では再描画しない」動作を保証します。

| メンバー | 説明 |
| --- | --- |
| `Default` | すべての項目を既定値にした設定（部分変更の基点: `LoupeOptions.Default with { ZoomLevel = 8 }`） |

数値項目（`RequestedSize` / `ZoomLevel`）は **`init` アクセサで補正**します（規則は `Loupe.Limits` が単一所有する）。したがってオブジェクト初期化子・`with` 式・逆シリアル化などの **どの設定経路でも**範囲内の値だけが保持され、UI 側に補正の登録（Avalonia の `coerce` / MewUI の `coerce`）を要求しません。

**含めない値**: 拡大元画像・拡大中心（マウス移動ごとに変わるデータ）、DPI スケーリング係数（環境値）、表示位置のオフセット（UI 固有のホスト結線）。これらは UI の個別プロパティとして公開します（1 つの値へまとめると、中心の更新のたびに寸法を含む値全体を組み立て直すことになります）。

## `LoupeMetrics`（`EsUtil.Feature.LoupeCore.Setting`・`public sealed record`）

ルーペの寸法 2 項目（市松模様のタイルサイズ・外枠の太さ）とラベル表示の可否を 1 つの値として所有します。**色・フォントサイズ・角丸は持たず**、これらは描画にだけ必要な設定として描画層 `LoupeControl.UIBase` の `LoupePaint` が単一所有します（コアはこれらの値を決定せず、描画可否の判定にも使いません）。両 UI は本型を 1 つ公開するだけで寸法を設定でき、UI ごとに既定値がずれません。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `CheckerSize` | `int` | `6` | 市松模様の 1 タイルサイズ（DIP）。安全域 1〜128 |
| `FrameWidth` | `int` | `2` | 外枠の太さ（DIP）。安全域 1〜64 |
| `ShowCoordLabel` | `bool` | `true` | 中心座標ラベルの表示可否 |

色（下地・市松模様・外枠・十字線・中心座標ラベル）と中心座標ラベルのフォントサイズ・角丸は本型に含まず、描画にだけ必要な設定として描画層 `LoupeControl.UIBase` の `LoupePaint` が単一所有します（既定は `LoupePaint.Default`。詳細は同プロジェクトの仕様書を参照）。ホストは UI の `Paint` プロパティへ `LoupePaint` を指定します。

`record` のため `with` 式による部分変更と値の等価比較ができます。

| メンバー | 説明 |
| --- | --- |
| `ShouldShowCoordLabel(bool hasCenterPoint)` | ラベルを描画するかを判定（`ShowCoordLabel` かつ拡大中心あり） |

「透明は描画しない」の判断（`HasBackground` / `HasCheckerColorA` / `HasCheckerColorB` / `HasFrame` / `HasCrosshair`）は、色を持つ描画層の `LoupePaint` が単一所有します。

数値項目（`CheckerSize` / `FrameWidth`）は **`init` アクセサで補正**します。したがってオブジェクト初期化子・`with` 式・逆シリアル化などの**どの設定経路でも**範囲内の値だけが保持され、UI 側に補正の登録（Avalonia の `coerce` / MewUI の `coerce`）を要求しません（規則は `Loupe.Limits` が単一所有する）。

状態（拡大元画像・拡大中心・拡大率・基本サイズ・スケーリング係数・表示オフセット）は本型に含めません。

## `Loupe.Limits`（`EsUtil.Feature.LoupeCore`）

ルーペの数値パラメータの既定値と安全域を単一所有し、範囲外の値をクランプします。

### 定数

| 定数 | 値 | 説明 |
| --- | --- | --- |
| `RequestedSizeMin` / `RequestedSizeMax` / `RequestedSizeDefault` | `1` / `4096` / `160` | 表示サイズ要求値（DIP） |
| `ZoomLevelMin` / `ZoomLevelMax` / `ZoomLevelDefault` | `1` / `64` / `4` | 拡大率 |
| `RenderScalingMin` / `RenderScalingMax` / `RenderScalingDefault` | `0.1` / `8.0` / `1.0` | スケーリング係数 |
| `CheckerSizeMin` / `CheckerSizeMax` / `CheckerSizeDefault` | `1` / `128` / `6` | 市松模様の 1 タイルサイズ（ピクセル） |
| `FrameWidthMin` / `FrameWidthMax` / `FrameWidthDefault` | `1` / `64` / `2` | 外枠の太さ（ピクセル） |

### メソッド

| メソッド | 説明 |
| --- | --- |
| `CoerceRequestedSize(int)` | 表示サイズ要求値を 1〜4096 へクランプ |
| `CoerceZoomLevel(int)` | 拡大率を 1〜64 へクランプ |
| `CoerceRenderScaling(double)` | スケーリング係数を 0.1〜8.0 へクランプ。非有限値は `RenderScalingDefault` |
| `CoerceCheckerSize(int)` | 市松模様の 1 タイルサイズを 1〜128 へクランプ |
| `CoerceFrameWidth(int)` | 外枠の太さを 1〜64 へクランプ |

## `LoupeDrawContent` / `LoupeCheckerTiles`（`EsUtil.Feature.LoupeCore.Drawing`・`public`）

`Loupe.ResolveContent` が返す**1 フレーム分の描画内容**です。描画層は本値を写すだけで、幾何計算（タイルの位置・矩形の導出・寸法の換算）を持ちません。

| メンバー | 説明 |
| --- | --- |
| `SurfaceBounds` | 描画面の全体（幅・高さが物理ピクセルサイズの正方形） |
| `CheckerTiles` | 市松模様の暗色タイルの矩形を列挙する構造体（`LoupeCheckerTiles`。リストを確保しない） |
| `CrosshairRects` / `FrameRects` | 十字線（縦横 2 本）/ 円枠（4 辺）の矩形 |
| `Layers` | 描く層とその順序。**描画順はコアが単一所有する**（テキストの層 `Label` はテキストがある場合のみ末尾 = 常に最上位。**UI 要素として置く UI は本層を無視してよい**） |

`LoupeCheckerTiles` は `foreach` でもボックス化せず、`IEsCanvas.FillRects` へ制約付きジェネリックとして渡せます（列挙は 1 回だけ）。

## `Decoration.LoupeMetricsScaler`（`EsUtil.Feature.LoupeCore.Decoration`・**internal**）

寸法値（DIP）を描画に使う物理ピクセルへ換算する規則を単一所有します。安全域 への補正は `LoupeMetrics`（生成時）が済ませているため再補正はしません。**換算結果は `LoupeRenderRequest.Create` が描画入力（`CheckerSizePixels` / `FrameWidthPixels`）へ解決済みとして含めるため、描画層は本型を呼びません。**

| メソッド | 説明 |
| --- | --- |
| `ScaleCheckerSize(LoupeMetrics, double scaling)` | 市松模様の 1 タイルサイズを物理ピクセルへ換算（切り上げ・最低 1）。`CheckerSize` は 1〜128 へ補正してから換算 |
| `ScaleFrameWidth(LoupeMetrics, double scaling)` | 外枠の太さを物理ピクセルへ換算（切り上げ・最低 1）。`FrameWidth` は 1〜64 へ補正してから換算 |

**中心座標ラベルは本型の対象外です**（ラベルは canvas へ描く場合も DIP で配置するため、換算を必要としません）。ラベルは DIP で配置するため、物理ピクセルへの換算を必要としません（テキスト要素として配置され、描画 API 側がスケーリングを扱います）。

UI 層が `CheckerSize * scaling` のような式を自前で書かないための型です（片方の UI だけ換算が抜けると、DPI ≠ 100% で両 UI の見た目がずれます）。

## `LoupeRenderRequest`（`EsUtil.Feature.LoupeCore.Drawing`）

1 フレーム分の描画入力（**コア⇔UI の契約**）です。UI 層は自 UI の公開プロパティから本値を組み立てて描画層・コアの計算へ渡すだけで、描画に必要な値の単位と組み立て順を持ちません。

**役割の分担**: ホストが設定する入力（設定値・環境値）は UI の公開プロパティ（`Options` / `RenderScaling`）が持ち、本値はそれらから**描画 1 フレーム分に必要な形へ解決したスナップショット**です（描画層は公開プロパティを読まない）。

| メンバー | 型 / 値 | 説明 |
| --- | --- | --- |
| `Center` | `EsPoint?` | 拡大中心（ドメイン座標 = 元画像ピクセル座標）。null の場合は市松模様のみを描画する |
| `PixelSize` | `int` | ルーペの一辺のサイズ（物理ピクセル。表示サイズ × `RenderScaling` を描画先でクランプ済み） |
| `ZoomLevel` | `int` | 拡大率（1〜64。`LoupeOptions.ZoomLevel` の補正済みの値） |
| `Metrics` | `LoupeMetrics` | ルーペの寸法（市松模様のタイルサイズ・外枠の太さ）。**色は `LoupePaint`（描画層）が単一所有する**。`LoupeOptions.Metrics` の値 |
| `Scaling` | `double` | 表示 DIP → 物理ピクセルのスケーリング係数 |
| `SurfaceBounds` | `EsRect` | 描画先の全領域（左上を原点とする一辺 `PixelSize` の正方形・物理ピクセル）。下地・市松模様を全面へ描くために契約が単一所有する（UI 層は矩形を組み立てない） |

| メソッド | 説明 |
| --- | --- |
| `Create(center, displaySize, renderScaling, options, availableSize = null)` | UI 層が保持する DIP 値（表示サイズ）と設定から描画入力を組み立てる。表示サイズを物理ピクセルへ換算し、描画先の領域が指定された場合はそれへクランプする（規則はコア内部が単一所有する） |

表示サイズは `Options.RequestedSize` ではなく、親領域でクランプした後の値を渡します（クランプの基準が親パネルか描画先の表面かで異なるため、値の決定は呼び出し側が担います）。

拡大元画像は UI ごとに型が異なる（Avalonia は `IImage`、MewUI は `EsBitmap`）ため本値に含めず、描画層が解決した `EsBitmap` を別引数として渡します（両 UI が同じ手順で本値を組み立てられます）。

## `LoupeLogFormat`（`EsUtil.Feature.LoupeCore.Logging`）

| メンバー | 説明 |
| --- | --- |
| `Null` | `"null"`（未設定の表記。UI も使う） |
| `DescribeImage(int? width, int? height)` | `Width×Height` 形式。片方でも未設定なら `null`（UI も使う） |

`FormatNumber` / `FormatPoint` はログ定義（`LoupeLogMessage`）の内部手順のため **internal** です（UI は数値をそのまま渡せる `LogSettingChanged` 系を使います）。

## `LoupeLogMessage`（`EsUtil.Feature.LoupeCore.Logging`）

ルーペのログ定義（`LoggerMessage` ソース生成）を単一所有します。`[LoupeCtrl]` の接頭辞とレベル・書式は両 UI で共通です。

| メソッド | レベル | 出力書式 |
| --- | --- | --- |
| `LogSettingChanged(name, oldValue, newValue)`（文字列） | Debug | `[LoupeCtrl] Setting: {Name} {OldValue} → {NewValue}`（整形済みの値と設定名を受け取る） |
| `LogSettingChanged(name, oldValue, newValue)`（`int` / `double`） | Debug | 上記と同じ書式。数値をそのまま渡せ、**同値の再設定では出力しない**（変化判定と数値の文字列化を本メソッドが単一所有） |
| `LogCenterPointChanged(EsPoint?, EsPoint?)` | Debug | `LogSettingChanged` の `CenterPoint` 版（値は `LoupeLogFormat.FormatPoint` で整形。**同値の再設定では出力しない**） |
| `LogSourceChanged(EsBitmap? oldValue, EsBitmap? newValue)` | Debug | `Source` 版。**同一インスタンスの再設定では出力しない**（参照の同一性の判定をコアが単一所有） |
| `LogSourceChanged(string oldValue, string newValue)` | Debug | 上記と同じ書式。**設定名はコアが付与**し、寸法の文字列化は `LoupeLogFormat.DescribeImage` を使う（画像型が UI ごとに異なるため、寸法の取り出しだけ各 UI が担う） |
| `LogRequestedSizeChanged(int, int)` | Debug | `RequestedSize` 版（同値なら出力しない） |
| `LogOptionsChanged(LoupeOptions, LoupeOptions)` | Debug | 設定（`LoupeOptions`）の変更版。**変化した項目だけ**を上記の書式で出力する（差分検出を本メソッドが単一所有し、UI 層は変更前後の値を渡すだけ。色は `LoupeOptions` に含めないため対象外） |
| `LogRenderScalingChanged(double, double)` | Debug | `RenderScaling` 版（同値なら出力しない） |
| `LogZoomLevelChanged(int, int)` | Debug | `ZoomLevel` 版（同値なら出力しない） |
| `LogUpdateError(Exception)` | Error | `[LoupeCtrl] Update exception` |

**UI 固有のログは各 UI プロジェクトが所有します**（中間ビットマップを生成する描画パイプライン固有の `LogRenderCore` / `LogGenerateAsyncDone` は `LoupeControl.AvaloniaUI` の `LoupeRenderLog` にあります）。

設定項目ごとの名前付きメソッド（`LogCenterPointChanged` / `LogSourceChanged` / `LogRequestedSizeChanged` / `LogRenderScalingChanged` / `LogZoomLevelChanged`）は、**設定名・書式・変化判定をコアが単一所有**するために提供します。UI 層は値（と Source の寸法）を渡すだけで、設定名の文字列を持ちません。

## `Imaging.LoupeImageExtractor`（`EsUtil.Feature.LoupeCore.Imaging`）

| メソッド | 説明 |
| --- | --- |
| `TryExtractRegionAt(EsBitmap? source, LoupeRenderRequest request)` | 拡大表示用の領域を切り出す。**切り出しの可否を単一所有**し、ソース・中心の未設定や破棄済み（`ObjectDisposedException`）は `null` を返す（例外を描画層へ伝播させない） |
| `ComputeMagnifiedRects(SizeInt captureSize, int zoomLevel)` | 切り出した領域をルーペ領域へ拡大描画するときの矩形（切り出し元＝左上原点の切り出しサイズ、描画先＝左上原点の `zoomLevel` 倍）を計算する。引数はビットマップの実寸（`EsBitmap.Size`）をそのまま渡す（UI 層で型変換しない）。**拡大倍率と描画先の大きさの関係を単一所有**し、両 UI が同じ拡大結果になるようにする |

`ComputeCaptureSize` / `ComputeCaptureTopLeft` / `ExtractRegionAt` は切り出しの内部手順のため **internal** です（利用者はファサードと `TryExtractRegionAt` / `ComputeMagnifiedRects` を使います）。

## 依存

| 依存 | 用途 |
| --- | --- |
| `Platform.Core`（`EsUtil.Platform.Core.*`） | コア幾何型（`EsPoint` / `EsSize`）・`DpiScaler` などの静的計算・`EsBitmap` |

UI フレームワーク（Avalonia / MewUI）への依存はありません。
