# LoupeControl.Core 外部機能仕様書

`LoupeControl.Core` は UI フレームワーク非依存のルーペロジックと設定（表示サイズ・拡大率・外観）を提供します。**全型が public** であり、外部が独自の UI アダプタを実装できます（他アセンブリへの `InternalsVisibleTo` は持ちません）。

## 公開 API の可視性

| 型 | 可視性 | 用途 |
| --- | --- | --- |
| `LoupeOptions` | `public` | 設定の単一値（ホストが設定する） |
| `LoupeAppearance` | `public` | 外観の単一値（設定の一部として、また単体でも外観を指定する） |
| `LoupeValueCoercer` | `public` | 数値パラメータの既定値・安全域とクランプ |
| `LoupeAppearanceScaler` | `public` | 外観の寸法値の物理ピクセル換算 |
| `LoupeRenderRequest` | `public` | 1 フレーム分の描画入力（コア⇔UI の契約） |
| `LoupePositionCalculator` | `public` | 表示サイズのクランプ・表示位置の計算 |
| `LoupeRenderArgsCalculator` | `public` | 描画完了通知用の表示情報の計算 |
| `LoupeLabelLayout` | `public` | 座標ラベルの余白と配置矩形 |
| `LoupeCheckerboard` | `public` | 市松模様の明暗判定とタイル矩形 |
| `LoupeDecorationGeometry` | `public` | 十字線・外枠の描画矩形 |
| `LoupeLogFormat` / `LoupeLogMessage` | `public` | ログの書式と定義（UI 非依存の分） |
| `Imaging.LoupeImageExtractor` | `public` | 切り出し領域の計算と `EsBitmap` からの切り出し |

**UI 固有の処理に対応するログは各 UI プロジェクトが所有します**（中間ビットマップを生成する描画パイプライン固有のログ等は `LoupeControl.AvaloniaUI` にあります）。

## `LoupeOptions`（`Estable.Loupe`・`public sealed record`）

ルーペの設定（表示サイズ・拡大率・外観）を 1 つの値として所有します。両 UI は本型を 1 プロパティとして公開するだけであり、UI ごとに既定値がずれたり設定プロパティが増えたりしません。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `RequestedSize` | `int` | `160` | ルーペの基本サイズ（DIP・一辺）。安全域 1〜4096。表示サイズは UI 層が親領域・描画先へクランプして導出する |
| `ZoomLevel` | `int` | `4` | 拡大率（偶数推奨）。安全域 1〜64 |
| `Appearance` | `LoupeAppearance` | `new LoupeAppearance()` | 装飾の外観（下地・市松模様・外枠・十字線・中心座標ラベル） |

`record` のため `with` 式による部分変更と値の等価比較ができます。等価比較は UI のプロパティシステムと併せて「同値の再設定では再描画しない」動作を保証します。

| メンバー | 説明 |
| --- | --- |
| `Default` | すべての項目を既定値にした設定（部分変更の基点: `LoupeOptions.Default with { ZoomLevel = 8 }`） |

数値項目（`RequestedSize` / `ZoomLevel`）は **`init` アクセサで補正**します（規則は `LoupeValueCoercer` が単一所有する）。したがってオブジェクト初期化子・`with` 式・逆シリアル化などの **どの設定経路でも**範囲内の値だけが保持され、UI 側に補正の登録（Avalonia の `coerce` / MewUI の `coerce`）を要求しません。

**含めない値**: 拡大元画像・拡大中心（マウス移動ごとに変わるデータ）、DPI スケーリング係数（環境値）、表示位置のオフセット（UI 固有のホスト結線）。これらは UI の個別プロパティとして公開します（1 つの値へまとめると、中心の更新のたびに外観を含む値全体を組み立て直すことになります）。

## `LoupeAppearance`（`Estable.Loupe`・`public sealed record`）

ルーペの外観 12 項目を 1 つの値として所有します。両 UI は本型を 1 つ公開するだけで外観を設定でき、UI ごとに既定値がずれません。色は UI 非依存の `System.Drawing.Color` です。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `BackgroundColor` | `DrawingColor` | 透明 | 下地（ベース）色。市松模様・拡大画像より先に全面へ塗る。市松模様の色は半透明のため、背後の要素を透かさない場合はホストが不透明色を指定する |
| `CheckerSize` | `int` | `6` | 市松模様の 1 タイルサイズ（DIP）。安全域 1〜128 |
| `CheckerColorA` | `DrawingColor` | Gray 30%（`#4C808080`） | 市松模様の明色 |
| `CheckerColorB` | `DrawingColor` | DarkGray 30%（`#4CA9A9A9`） | 市松模様の暗色 |
| `FrameColor` | `DrawingColor` | LightGray（`#FFD3D3D3`） | 外枠の色 |
| `FrameWidth` | `int` | `2` | 外枠の太さ（DIP）。安全域 1〜64 |
| `CrossColor` | `DrawingColor` | 赤 40%（`#66FF0000`） | 十字線の色 |
| `ShowCoordLabel` | `bool` | `true` | 中心座標ラベルの表示可否 |
| `CoordLabelColor` | `DrawingColor` | 白 | ラベルの文字色 |
| `CoordLabelBackgroundColor` | `DrawingColor` | 黒 60%（`#99000000`） | ラベルの背景色 |
| `CoordLabelFontSize` | `double` | `12.0` | ラベルのフォントサイズ（DIP）。安全域 6〜48 |
| `CoordLabelCornerRadius` | `int` | `2` | ラベルの背景の角丸半径（DIP）。安全域 0〜64（0 は角丸なし） |

`record` のため `with` 式による部分変更と値の等価比較ができます。

| メンバー | 説明 |
| --- | --- |
| `ShouldShowCoordLabel(bool hasCenterPoint)` | ラベルを描画するかを判定（`ShowCoordLabel` かつ拡大中心あり） |
| `HasCrosshair` / `HasFrame` / `HasCheckerColorA` / `HasCheckerColorB` / `HasBackground` | アルファが 0 でない（= 描画する）かを返す。「透明は描画しない」の判断を本型が単一所有する |

数値項目（`CheckerSize` / `FrameWidth` / `CoordLabelFontSize` / `CoordLabelCornerRadius`）は **`init` アクセサで補正**します。したがってオブジェクト初期化子・`with` 式・逆シリアル化などの**どの設定経路でも**範囲内の値だけが保持され、UI 側に補正の登録（Avalonia の `coerce` / MewUI の `coerce`）を要求しません（規則は `LoupeValueCoercer` が単一所有する）。

状態（拡大元画像・拡大中心・拡大率・基本サイズ・スケーリング係数・表示オフセット）は本型に含めません。

## `LoupeValueCoercer`（`Estable.Loupe`）

ルーペの数値パラメータの既定値と安全域を単一所有し、範囲外の値をクランプします。

### 定数

| 定数 | 値 | 説明 |
| --- | --- | --- |
| `RequestedSizeMin` / `RequestedSizeMax` / `RequestedSizeDefault` | `1` / `4096` / `160` | 表示サイズ要求値（DIP） |
| `ZoomLevelMin` / `ZoomLevelMax` / `ZoomLevelDefault` | `1` / `64` / `4` | 拡大率 |
| `RenderScalingMin` / `RenderScalingMax` / `RenderScalingDefault` | `0.1` / `8.0` / `1.0` | スケーリング係数 |
| `CheckerSizeMin` / `CheckerSizeMax` / `CheckerSizeDefault` | `1` / `128` / `6` | 市松模様の 1 タイルサイズ（ピクセル） |
| `FrameWidthMin` / `FrameWidthMax` / `FrameWidthDefault` | `1` / `64` / `2` | 外枠の太さ（ピクセル） |
| `CoordLabelFontSizeMin` / `CoordLabelFontSizeMax` / `CoordLabelFontSizeDefault` | `6` / `48` / `12.0` | 座標ラベルのフォントサイズ（DIP） |
| `CoordLabelCornerRadiusMin` / `CoordLabelCornerRadiusMax` / `CoordLabelCornerRadiusDefault` | `0` / `64` / `2` | 座標ラベルの背景の角丸半径（ピクセル。0 は角丸なし） |

### メソッド

| メソッド | 説明 |
| --- | --- |
| `CoerceRequestedSize(int)` | 表示サイズ要求値を 1〜4096 へクランプ |
| `CoerceZoomLevel(int)` | 拡大率を 1〜64 へクランプ |
| `CoerceRenderScaling(double)` | スケーリング係数を 0.1〜8.0 へクランプ。非有限値は `RenderScalingDefault` |
| `CoerceCheckerSize(int)` | 市松模様の 1 タイルサイズを 1〜128 へクランプ |
| `CoerceFrameWidth(int)` | 外枠の太さを 1〜64 へクランプ |
| `CoerceCoordLabelFontSize(double)` | ラベル文字サイズを 6〜48 へクランプ。非有限値は既定値 |
| `CoerceCoordLabelCornerRadius(int)` | ラベルの角丸半径を 0〜64 へクランプ（0 は角丸なし） |

## `LoupeAppearanceScaler`（`Estable.Loupe`）

外観の寸法値（DIP）を描画に使う物理ピクセルへ換算する規則を単一所有します。安全域への補正は `LoupeAppearance`（生成時）が済ませているため再補正はせず、描画層は返された値だけを使います。

| メソッド | 説明 |
| --- | --- |
| `ScaleCheckerSize(LoupeAppearance, double scaling)` | 市松模様の 1 タイルサイズを物理ピクセルへ換算（切り上げ・最低 1）。`CheckerSize` は 1〜128 へ補正してから換算 |
| `ScaleFrameWidth(LoupeAppearance, double scaling)` | 外枠の太さを物理ピクセルへ換算（切り上げ・最低 1）。`FrameWidth` は 1〜64 へ補正してから換算 |
| `ScaleCoordLabelFontSize(LoupeAppearance, double scaling)` | ラベル文字サイズを物理ピクセルへ換算（実数のまま。描画 API へそのまま渡せるため切り上げない） |
| `ScaleCoordLabelCornerRadius(LoupeAppearance, double scaling)` | ラベルの角丸半径を物理ピクセルへ換算。補正後の値が 0 の場合は 0（角丸なし）のまま返す（他の寸法値と異なり最低 1 へ切り上げない） |

UI 層が `CheckerSize * scaling` のような式を自前で書かないための型です（片方の UI だけ換算が抜けると、DPI ≠ 100% で両 UI の見た目がずれます）。

## `LoupeRenderRequest`（`Estable.Loupe`）

1 フレーム分の描画入力（**コア⇔UI の契約**）です。UI 層は自 UI の公開プロパティから本値を組み立てて描画層・コアの計算へ渡すだけで、描画に必要な値の単位と組み立て順を持ちません。

**役割の分担**: ホストが設定する入力（設定値・環境値）は UI の公開プロパティ（`Options` / `RenderScaling`）が持ち、本値はそれらから**描画 1 フレーム分に必要な形へ解決したスナップショット**です（描画層は公開プロパティを読まない）。

| メンバー | 型 / 値 | 説明 |
| --- | --- | --- |
| `Center` | `EsPoint?` | 拡大中心（ドメイン座標 = 元画像ピクセル座標）。null の場合は市松模様のみを描画する |
| `PixelSize` | `int` | ルーペの一辺のサイズ（物理ピクセル。表示サイズ × `RenderScaling` を描画先でクランプ済み） |
| `ZoomLevel` | `int` | 拡大率（1〜64。`LoupeOptions.ZoomLevel` の補正済みの値） |
| `Appearance` | `LoupeAppearance` | 外観（下地・色・寸法・描画可否。`LoupeOptions.Appearance` の値） |
| `Scaling` | `double` | 表示 DIP → 物理ピクセルのスケーリング係数 |

| メソッド | 説明 |
| --- | --- |
| `Create(center, displaySize, renderScaling, options, availableSize = null)` | UI 層が保持する DIP 値（表示サイズ）と設定から描画入力を組み立てる。表示サイズを物理ピクセルへ換算し、描画先の領域が指定された場合はそれへクランプする（規則は `LoupePositionCalculator.ResolvePixelSize` が単一所有する） |

表示サイズは `Options.RequestedSize` ではなく、UI 層が親領域・描画先へクランプした後の値を渡します（クランプの基準（親パネル / 描画先の表面）は UI ごとに異なるため、値の決定は UI 層が担います）。

拡大元画像は UI ごとに型が異なる（Avalonia は `IImage`、MewUI は `EsBitmap`）ため本値に含めず、描画層が解決した `EsBitmap` を別引数として渡します（両 UI が同じ手順で本値を組み立てられます）。

## `LoupePositionCalculator`（`Estable.Loupe`）

```csharp
public static (EsSize Size, EsPoint Position) Compute(
    double requestedSize, EsSize parentSize, EsPoint centerDip, double offset);
public static EsPoint ComputePosition(
    double displaySize, EsSize parentSize, EsPoint centerDip, double offset);
public static double ClampSizeToArea(double size, EsSize areaSize);
```

- `Size`: 基本サイズを親領域の最小辺へクランプした正方形サイズ（幅 = 高さ）。
- `Position`: 中心（変換後の親領域 DIP 座標）+ `offset` を親領域内（`0` 〜 `親サイズ - Size`）へクランプした左上座標。
- 親領域の幅または高さが 0 以下（レイアウト前・親未接続）のときはクランプを行いません（判断は本メソッドが単一所有する）。
- 座標変換（`ICoordTransform.Inverse`）は呼び出し側が行い、本メソッドは変換後の DIP 座標を受け取ります。

| メンバー | 説明 |
| --- | --- |
| `Compute(requestedSize, parentSize, centerDip, offset)` | 表示サイズと表示位置を計算する（上記。`ClampSizeToArea` と `ComputePosition` を合成する） |
| `ComputePosition(displaySize, parentSize, centerDip, offset)` | 表示位置（左上）を計算する。`displaySize` はクランプ済みの表示サイズを渡す（要素のサイズと位置のクランプで同じ値を使う） |
| `ClampSizeToArea(size, areaSize)` | サイズを提示領域の最小辺へクランプする。領域の幅または高さが 0 以下の場合はクランプしない（未確定として扱う）。単位は呼び出し側が揃える（DIP / 物理ピクセル） |
| `ResolvePixelSize(size, renderScaling, availableSize = null)` | 表示サイズ（DIP）から描画に使う物理ピクセルサイズを求める（切り上げ・最低 1、領域指定時はクランプ）。描画とイベント通知で同じ値を使うために単一所有する |

`ClampSizeToArea` は「領域より大きい場合は領域に合わせる」規則を単一所有し、`Compute`（親パネル DIP）と `ResolvePixelSize`（描画の物理ピクセル）が共用します（描画層は `ResolvePixelSize` を呼び、直接は使わない）。`ResolvePixelSize` は DIP → 物理ピクセルの換算とクランプをまとめて行い、両 UI の描画とイベント通知が同じピクセルサイズを使います。

表示サイズを要素（`Width` / `Height`）へ先に反映する必要がある UI は `Compute` ではなく `ClampSizeToArea` と `ComputePosition` を個別に呼びます（クランプを 2 回計算すると丸めの違いで要素が親領域からはみ出しうるため、同じ値を共有します）。

## `LoupeLabelLayout`（`Estable.Loupe`）

中心座標ラベルの余白と位置を単一所有します。Avalonia 版はレイアウトシステムへ同じ余白（`Margin` / `Padding`）を渡し、MewUI 版は計算結果（背景矩形・文字原点）を使います。

| メンバー | 値 / 説明 |
| --- | --- |
| `OuterMargin` | `2.0`（ルーペ上端からの余白・DIP） |
| `PaddingX` | `4.0`（背景の左右内側余白・DIP） |
| `PaddingY` | `1.0`（背景の上下内側余白・DIP） |
| `ComputeBackground(double loupePixelSize, double textWidth, double fontSize, double scaling)` | ラベル背景の矩形（物理ピクセル）。幅 = 文字幅 + `PaddingX`×2×`scaling`、高さ = フォントサイズ + `PaddingY`×2×`scaling` |
| `ComputeTextOrigin(double loupePixelSize, double textWidth, double scaling)` | 文字列の左上（物理ピクセル）。水平はルーペ中央へセンタリング、垂直は `OuterMargin`×`scaling` |

## `LoupeLogFormat`（`Estable.Loupe`）

| メンバー | 説明 |
| --- | --- |
| `Null` | `"null"`（未設定の表記） |
| `FormatNumber(int)` / `FormatNumber(double)` | 数値を現在のカルチャに依存しない表記へ変換する（ログを環境非依存にする） |
| `FormatPoint(EsPoint? point)` | `(X, Y)` 形式。未設定は `null` |
| `DescribeImage(int? width, int? height)` | `Width×Height` 形式。片方でも未設定なら `null` |

## `LoupeLogMessage`（`Estable.Loupe`）

ルーペのログ定義（`LoggerMessage` ソース生成）を単一所有します。`[LoupeCtrl]` の接頭辞とレベル・書式は両 UI で共通です。

| メソッド | レベル | 出力書式 |
| --- | --- | --- |
| `LogSettingChanged(name, oldValue, newValue)`（文字列） | Debug | `[LoupeCtrl] Setting: {Name} {OldValue} → {NewValue}`（整形済みの値と設定名を受け取る） |
| `LogSettingChanged(name, oldValue, newValue)`（`int` / `double`） | Debug | 上記と同じ書式。数値をそのまま渡せ、**同値の再設定では出力しない**（変化判定と数値の文字列化を本メソッドが単一所有） |
| `LogCenterPointChanged(EsPoint?, EsPoint?)` | Debug | `LogSettingChanged` の `CenterPoint` 版（値は `LoupeLogFormat.FormatPoint` で整形。**同値の再設定では出力しない**） |
| `LogSourceChanged(EsBitmap? oldValue, EsBitmap? newValue)` | Debug | `Source` 版。**同一インスタンスの再設定では出力しない**（参照の同一性の判定をコアが単一所有） |
| `LogSourceChanged(string oldValue, string newValue)` | Debug | 上記と同じ書式。**設定名はコアが付与**し、寸法の文字列化は `LoupeLogFormat.DescribeImage` を使う（画像型が UI ごとに異なるため、寸法の取り出しだけ各 UI が担う） |
| `LogRequestedSizeChanged(int, int)` | Debug | `RequestedSize` 版（同値なら出力しない） |
| `LogOptionsChanged(LoupeOptions, LoupeOptions)` | Debug | 設定（`LoupeOptions`）の変更版。**変化した項目だけ**を上記の書式で出力する（差分検出を本メソッドが単一所有し、UI 層は変更前後の値を渡すだけ。外観の項目は対象外） |
| `LogRenderScalingChanged(double, double)` | Debug | `RenderScaling` 版（同値なら出力しない） |
| `LogZoomLevelChanged(int, int)` | Debug | `ZoomLevel` 版（同値なら出力しない） |
| `LogUpdateError(Exception)` | Error | `[LoupeCtrl] Update exception` |

**UI 固有のログは各 UI プロジェクトが所有します**（中間ビットマップを生成する描画パイプライン固有の `LogRenderCore` / `LogGenerateAsyncDone` は `LoupeControl.AvaloniaUI` の `LoupeRenderLog` にあります）。

設定項目ごとの名前付きメソッド（`LogCenterPointChanged` / `LogSourceChanged` / `LogRequestedSizeChanged` / `LogRenderScalingChanged` / `LogZoomLevelChanged`）は、**設定名・書式・変化判定をコアが単一所有**するために提供します。UI 層は値（と Source の寸法）を渡すだけで、設定名の文字列を持ちません。

## `LoupeRenderArgsCalculator`（`Estable.Loupe`）

```csharp
public static RenderArgs Compute(LoupeRenderRequest request);

public readonly record struct RenderArgs(EsPoint? CaptureTopLeft, double Scale);
```

| メンバー | 説明 |
| --- | --- |
| `CaptureTopLeft` | 切り出し領域の左上（ドメイン座標）。中心が `null` のときは `null` |
| `Scale` | ドメイン座標 → ルーペ画像ピクセル座標の変換係数（**拡大率と同値**）。破線オーバーレイの位置を DPI に依存させない |

引数は**実際に描画した描画入力**（`LoupeRenderRequest`）です（本メソッドが DIP から再換算すると、描画先の領域によるクランプで実際の描画と値がずれ、ホストへ通知する切り出し領域が食い違うため）。

通知内容はホストが実際に消費する値（切り出し領域の左上とスケール）だけに絞ります。物理ピクセルサイズ・切り出し領域の一辺はホストが使わないため結果に含めず、必要な場合は `LoupePositionCalculator.ResolvePixelSize` / `LoupeImageExtractor.ComputeCaptureSize` から直接求めます。

`Scale` は拡大率と同値です。描画は切り出し領域（`ComputeCaptureSize` のドメインピクセル）を `zoomLevel` 倍で描くため、変換係数は DIP 表示サイズや物理ピクセル数、切り出しサイズの切り上げに依存しません。

## `Imaging.LoupeImageExtractor`（`Estable.Loupe.Imaging`）

| メソッド | 説明 |
| --- | --- |
| `ComputeCaptureSize(int pixelSize, int zoomLevel)` | ルーペの一辺（物理ピクセル）から切り出し領域の一辺（ドメインピクセル）を計算（切り上げ）。切り出し・十字線位置・選択矩形変換で共用 |
| `ComputeCaptureTopLeft(int centerX, int centerY, int size)` | 拡大中心から切り出し領域の左上（ドメイン座標）を計算 |
| `ExtractRegionAt(EsBitmap source, int centerX, int centerY, int size)` | 指定位置を中心とする正方形領域を切り出す。ソース範囲外は透明パディング。戻り値の破棄は呼び出し側 |
| `TryExtractRegionAt(EsBitmap? source, LoupeRenderRequest request)` | 拡大表示用の領域を切り出す。**切り出しの可否を単一所有**し、ソース・中心の未設定や破棄済み（`ObjectDisposedException`）は `null` を返す（例外を描画層へ伝播させない） |
| `ComputeMagnifiedRects(SizeInt captureSize, int zoomLevel)` | 切り出した領域をルーペ領域へ拡大描画するときの矩形（切り出し元＝左上原点の切り出しサイズ、描画先＝左上原点の `zoomLevel` 倍）を計算する。引数はビットマップの実寸（`EsBitmap.Size`）をそのまま渡す（UI 層で型変換しない）。**拡大倍率と描画先の大きさの関係を単一所有**し、両 UI が同じ拡大結果になるようにする |

## 依存

| 依存 | 用途 |
| --- | --- |
| `Platform.Core`（`EsUtil.Platform.Core.*`） | コア幾何型（`EsPoint` / `EsSize`）・`DpiScaler` などの静的計算・`EsBitmap` |

UI フレームワーク（Avalonia / MewUI）への依存はありません。
