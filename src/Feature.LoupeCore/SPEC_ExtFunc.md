# Feature.LoupeCore 外部機能仕様書

`LoupeControl`（選択範囲を拡大表示するコントロール）の **UI 非依存のコア**が提供する外部機能と公開 API の仕様です。利用者（UI 実装・ホスト）から見える情報のみを記載し、内部実装の詳細は記載しません。設計の意図は [README](README.md)、構造と依存は [SPEC_System](SPEC_System.md) を参照してください。

## 1. 提供機能

| 機能 | 提供元 | 説明 |
| --- | --- | --- |
| 表示レイアウトの解決 | `Loupe.ResolveLayout` | 親領域のサイズを渡すと、表示サイズ（要求サイズを親領域の最小辺へクランプ）と表示位置（拡大中心 ＋ オフセットの親領域内へのクランプ）を返す（拡大中心が未設定の場合は位置が null）。**状態を変更しない** |
| 描画入力の生成 | `Loupe.CreateRequest` | 描画 1 フレーム分の入力（`LoupeRenderRequest`）を生成する。DIP → 物理ピクセルの換算と描画先へのクランプはコアが行う |
| 描画内容の解決 | `Loupe.ResolveContent` | 描画入力を渡すと、1 フレーム分の描画内容（描画面・市松模様のタイル・十字線 / 枠線の矩形・拡大画像の幾何・層の順序）を返す。「描くかどうか」の判断と文言はすべてコアが下す |
| 描画（本コアの対象外） | 描画層 `LoupeControl.UIBase` の `ILoupeRenderer` / `LoupeRendererBase` | 描画内容を描画面へ写す手順は UI 側の共有層が単一所有する（本コアは描画 API も描画ポートも持たない）。色は `LoupePaint`、描画先は `IEsCanvas` が担う |
| 拡大中心の管理 | `Loupe` | 拡大中心（ドメイン座標）と表示位置のオフセットを保持する（マウス移動ごとの更新は UI 層が通知する） |
| 設定 | `LoupeOptions` / `Loupe.Limits` | 設定の単一値（寸法・表示可否）と、既定値・安全域への補正 |
| ログ | `Loupe.Log` | 設定・環境値・拡大中心・位置オフセットの変更を検出して出力する（変化したときだけ） |
| 寸法の換算 | `LoupeMetricsScaler` | 寸法値（DIP）の物理ピクセル換算（internal。描画入力の生成が吸収する） |
| 幾何計算 | `LoupeDecoGeometry` / `LoupePlacementGeometry` / `LoupeDivMath` / `LoupeCheckerboard` | 十字線・枠線の矩形、表示位置、区画数の切り上げ、市松模様の明暗（internal。ファサードが吸収する） |

### 前提条件

- 本コアは UI フレームワークに依存しません（依存は `Platform.Core` のみで、外部パッケージへの直接依存もありません）。UI 固有の型は利用者（UI 実装）が境界で変換します。
- 座標は `EsPoint` / `EsSize` / `EsRect`（`EsUtil.Platform.Core`）、拡大元画像は `EsBitmap` で扱います。
- 描画の可否は**コアの明示フラグ**（`LoupeOptions.ShowCheckerboard` / `ShowCrosshair` / `ShowBorder` / `ShowCoordLabel`）が単一所有し、非表示の項目はコアが空の描画内容として返します（描画層は層ごとの可否を判定しません）。
- 描画の順序（下地 → 市松模様 → 拡大画像 → 十字線 → 枠線）と色（`LoupePaint`）は描画層 `LoupeControl.UIBase` が単一所有します。
- コントロール化にだけ必要な処理（再描画要求のバッチ化・世代番号管理）は `LoupeControl.UIBase` が所有します。
- ラベルの**文言・基準点・アライン**はコアが単一所有しますが、**描画と実寸測定**は利用者が行います（実寸は描画 API でしか測れないため）。

## 2. 公開 API 一覧

可視性は public のみを記載します（`internal` は [SPEC_System](SPEC_System.md) の可視性ポリシーを参照）。

### `EsUtil.Feature.LoupeCore`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `Loupe` | class | 操作の唯一の窓口となるファサード |
| `Loupe.Limits`（`Loupe` の入れ子） | static class | 数値パラメータの既定値・安全域と補正 |

### `EsUtil.Feature.LoupeCore.Drawing`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `LoupeDrawContent` | record struct | 1 フレーム分の描画内容（描画面・タイル列挙子・十字線 / 枠線の矩形・拡大画像の幾何・層の順序） |
| `LoupeRenderRequest` | record struct | 1 フレーム分の描画入力（コア⇔UI の契約）。値の組は `LoupeRenderRequest.Create` が 1 箇所に閉じる |
| `LoupeMagnifiedImage` | record struct | 拡大画像の切り出しと描画の矩形（切り出し領域・描画元・描画先） |
| `LoupeCheckerTiles` | struct | 市松模様の暗色タイルの矩形を、**リストを確保せずに列挙する構造体**（描画内容の一部） |
| `LoupeTextPlacement` | record struct | コアが UI 層へ渡すテキスト（文言・基準点・アライン・描画領域）。**要素として置くか canvas へ描くかは UI 層が決める** |
| `LoupeDrawLayer` | enum | 描画層（`Background` / `Checkerboard` / `Image` / `Crosshair` / `Border` / `Label`）。**`Label` はテキストがある場合だけ末尾に置く**（UI 要素として置く UI は無視してよい） |

### `EsUtil.Feature.LoupeCore.Setting`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `LoupeOptions` | record | 設定（寸法・表示可否）と既定値・補正。**色・フォントサイズは持たない**（色は描画層の `LoupePaint` が単一所有する） |

### `EsUtil.Feature.LoupeCore.Placement`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `LoupeLayout` | readonly record struct | 表示レイアウトの値（表示サイズ（DIP）と表示位置。配置しない場合は位置が null） |

### `Loupe`（ファサード）のメンバー

ルーペの**操作の唯一の窓口**（適用パターン: Facade）。UI 層とホストは本型だけを参照し、配置計算・描画引数の計算は internal のため外部からは直接呼べません。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `Loupe(LoupeOptions? options = null)` | ctor | 生成（設定を省略した場合は `LoupeOptions.Default`）。描画実装は受け取らない（描画は UI の関心事であり、本コアは描画 API を持たない） |
| `Log` | property | ロガー。null は `NullLogger` へ吸収される。設定・環境値・拡大中心・位置オフセットの変更は本コアが検出して出力する |
| `Options` | property | 設定（表示サイズ・拡大率・寸法・表示可否）。**変化したときだけ**変更ログを出力する |
| `DipToDeviceScaling` | property | 表示 DIP → 物理ピクセルのスケーリング係数（環境値。補正は `Loupe.Limits` が単一所有する）。**変化したときだけ**変更ログを出力する |
| `CenterPoint` | property | 拡大中心（ドメイン座標）。未設定（null）は市松模様のみを描画し、配置も行わない。**変化したときだけ**変更ログを出力する |
| `PositionOffset` | property | 拡大中心からの右下オフセット（DIP。既定は 0）。**変化したときだけ**変更ログを出力する |
| `SetCoordinateTransform(ICoordTransform?)` | method | ドメイン ↔ 親領域 DIP の座標変換ポートの設定（null は等価変換） |
| `ResolveLayout(EsSize)` | method | 親領域サイズから表示レイアウト（`LoupeLayout` = 表示サイズ + 位置）を解決する（拡大中心未設定時は位置が null） |
| `CreateRequest(double displaySize, EsSize? availableSize = null)` | method | 描画 1 フレーム分の入力（`LoupeRenderRequest`）の生成（DIP → 物理ピクセルの換算と描画先へのクランプを含む） |
| `ResolveContent(LoupeRenderRequest)` | method | 1 フレーム分の描画内容（`LoupeDrawContent`）の解決 |

**本型が単一所有するもの**: 表示レイアウトの解決（表示サイズの補正と表示位置の決定）・描画入力の組み立て・**設定変更の検出とログ出力**。

表示サイズは `Options.RequestedSize` ではなく、親領域でクランプした後の値を渡します（クランプの基準が親パネルか描画先の表面かで異なるため、値の決定は呼び出し側が担います）。

### `LoupeOptions` のメンバー

設定（表示サイズ・拡大率・寸法・表示可否）を 1 つの値として所有します。両 UI は本型を 1 プロパティとして公開するだけであり、UI ごとに既定値がずれたり設定プロパティが増えたりしません。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `RequestedSize` | `int` | `160` | ルーペの基本サイズ（DIP・一辺）。安全域 1〜4096。表示サイズは UI 層が親領域・描画先へクランプして導出する |
| `ZoomLevel` | `int` | `4` | 拡大率（偶数推奨）。安全域 1〜64 |
| `CheckerSize` | `int` | `6` | 市松模様の 1 タイルサイズ（DIP）。安全域 1〜128 |
| `BorderThickness` | `int` | `2` | 枠線の太さ（DIP）。安全域 1〜64 |
| `ShowCoordLabel` | `bool` | `true` | 中心座標ラベルの表示可否 |
| `ShowCheckerboard` | `bool` | `true` | 市松模様の表示可否（透明色を「描かない」の代用にしない） |
| `ShowCrosshair` | `bool` | `true` | 十字線の表示可否 |
| `ShowBorder` | `bool` | `true` | 枠線の表示可否 |

`record` のため `with` 式による部分変更と値の等価比較ができます。等価比較は UI のプロパティシステムと併せて「同値の再設定では再描画しない」動作を保証します。

| メンバー | 説明 |
| --- | --- |
| `Default` | すべての項目を既定値にした設定（部分変更の基点: `LoupeOptions.Default with { ZoomLevel = 8 }`） |
| `ShouldShowCoordLabel(bool hasCenterPoint)` | 中心座標ラベルを描画するかを判定（`ShowCoordLabel` かつ拡大中心あり） |

数値項目（`RequestedSize` / `ZoomLevel` / `CheckerSize` / `BorderThickness`）は **`init` アクセサで補正**します（規則は `Loupe.Limits` が単一所有する）。したがってオブジェクト初期化子・`with` 式・逆シリアル化などの **どの設定経路でも**範囲内の値だけが保持され、UI 側に補正の登録（Avalonia の `coerce` / MewUI の `coerce`）を要求しません。

**色・フォントサイズ・角丸は本型に含みません**（描画にだけ必要な設定のため、描画層 `LoupeControl.UIBase` の `LoupePaint` が単一所有します）。ホストは UI の `Paint` プロパティへ `LoupePaint` を指定します。

**含めない値**: 拡大元画像・拡大中心（マウス移動ごとに変わるデータ）、DPI スケーリング係数（環境値）、表示位置のオフセット（UI 固有のホスト結線）。これらは UI の個別プロパティとして公開します。

### `Loupe.Limits` のメンバー

ルーペの数値パラメータの既定値と安全域を単一所有し、範囲外の値を補正します（非有限は既定値）。

| 定数 | 値 | 説明 |
| --- | --- | --- |
| `RequestedSizeMin` / `RequestedSizeMax` / `RequestedSizeDefault` | `1` / `4096` / `160` | 表示サイズ要求値（DIP） |
| `ZoomLevelMin` / `ZoomLevelMax` / `ZoomLevelDefault` | `1` / `64` / `4` | 拡大率 |
| `DipToDeviceScalingMin` / `DipToDeviceScalingMax` / `DipToDeviceScalingDefault` | `0.1` / `8.0` / `1.0` | スケーリング係数 |
| `CheckerSizeMin` / `CheckerSizeMax` / `CheckerSizeDefault` | `1` / `128` / `6` | 市松模様の 1 タイルサイズ（ピクセル） |
| `BorderThicknessMin` / `BorderThicknessMax` / `BorderThicknessDefault` | `1` / `64` / `2` | 枠線の太さ（ピクセル） |

| メソッド | 説明 |
| --- | --- |
| `CoerceRequestedSize(int)` | 表示サイズ要求値を 1〜4096 へ補正 |
| `CoerceZoomLevel(int)` | 拡大率を 1〜64 へ補正 |
| `CoerceDipToDeviceScaling(double)` | スケーリング係数を 0.1〜8.0 へ補正。非有限値は `DipToDeviceScalingDefault` |
| `CoerceCheckerSize(int)` | 市松模様の 1 タイルサイズを 1〜128 へ補正 |
| `CoerceBorderThickness(int)` | 枠線の太さを 1〜64 へ補正 |

### 描画の契約（`ILoupeRenderer` / `LoupeRendererBase`）

描画の契約は `LoupeControl.UIBase`（UI 側の共有層）が所有します（本コアは描画 API も描画ポートも持ちません）。引数は描画ポート（`IEsCanvas`）・拡大元画像・描画内容（`LoupeDrawContent`）であり、描く値はすべて描画内容に解決済みです。層の順序はコアが `LoupeDrawContent.Layers` として単一所有し、実装はその順に写すだけです。

```csharp
var loupe = new Loupe(options) { CenterPoint = cursorDomain, DipToDeviceScaling = 1.25 };
loupe.SetCoordinateTransform(port);

var layout = loupe.ResolveLayout(parentSize);          // 表示サイズ + 表示位置（配置はしない場合は Position が null）
var request = loupe.CreateRequest(layout.DisplaySize, canvasSize); // 描画入力（物理ピクセルを解決済み）
var content = loupe.ResolveContent(request);    // テキストは CoordLabelText として受け取る（UI 層が扱いを決める）

// 描画完了通知（ホストが描画済み画像へオーバーレイを描くための情報）は描画入力から読む
var captureTopLeft = request.CaptureTopLeft;   // 切り出し領域の左上（ドメイン座標。中心未設定は null）
var scale = request.ZoomLevel;                 // ドメイン座標 → ルーペ画像ピクセルの変換係数
```

### `LoupeDrawLayer` の値

| 値 | 内容 |
| --- | --- |
| `Background` | 下地（ベース色）。常に含む（描画面を置き換えて前フレームの残像を残さない） |
| `Checkerboard` | 市松模様（表示の設定が有効な場合のみ） |
| `Image` | 拡大画像（切り出し領域を解決できた場合のみ。拡大中心未設定の場合は含まない） |
| `Crosshair` | 十字線（表示の設定が有効な場合のみ） |
| `Border` | 枠線（表示の設定が有効な場合のみ） |
| `Label` | 中心座標ラベル（テキストがある場合のみ。常に最上位） |

## 3. 契約（利用者が守る・受け取る値）

### 描画内容（`LoupeDrawContent`）

`Loupe.ResolveContent` が返す**1 フレーム分の描画内容**です。描画層は本値を写すだけで、幾何計算（タイルの位置・矩形の導出・寸法の換算）を持ちません。

| メンバー | 説明 |
| --- | --- |
| `SurfaceBounds` | 描画面の全体（幅・高さが物理ピクセルサイズの正方形） |
| `CheckerTiles` | 市松模様の暗色タイルの矩形を列挙する構造体（`LoupeCheckerTiles`。リストを確保しない） |
| `MagnifiedImage` | 拡大画像の切り出しと描画の矩形（`LoupeMagnifiedImage`。中心未設定の場合は null） |
| `CrosshairRects` / `BorderRects` | 十字線（縦横 2 本）/ 枠線（4 辺）の矩形 |
| `CoordLabelText` | テキスト（`LoupeTextPlacement`。null は表示しない）。文言・基準点・アライン・描画領域を持つ |
| `Layers` | 描く層とその順序。先頭から順に処理する。**層の有無は描く値の有無から決まる**（層にある ⇔ 描く値がある。下地は常に含む）。テキストの層（`Label`）はテキストがある場合のみ末尾（常に最上位）。**UI 要素として置く UI は本層を無視してよい** |

`LoupeCheckerTiles` は `foreach` でもボックス化せず、`IEsCanvas.FillRects` へ制約付きジェネリックとして渡せます（列挙は 1 回だけ）。

### テキストの位置（`LoupeTextPlacement`）

- コアが決めるのは**文言・基準点・アライン（水平/垂直）・描画領域**です（`EsHorizontalAlign` / `EsVerticalAlign` で基準点がテキストのどこを指すかを指定するため、コアは幅・高さを知りません）。
- 中心座標ラベルの基準点は**ルーペ上端の中央**（`DisplaySize / 2`、上端から 2.0 DIP）であり、アラインは**水平中央・上端**です（水平位置は基準点の中央寄せで決まるため、コアはラベルの幅を知らずに指定できます）。描画領域は既定（クランプなし）です。
- **フォントサイズ・色・角丸は描画の設定（`LoupePaint`）**、実寸から背景矩形を求める計算とアラインの適用は描画層の `LoupeLabelPlan` が単一所有します。**要素として置くか canvas へ描くかは UI 層が決めます**。
- 単位は DIP です（描画領域・ルーペの表示サイズと同じ座標系。物理ピクセルへの換算は描画時に行います）。

### 描画入力（`LoupeRenderRequest`）

1 フレーム分の描画入力（**コア⇔UI の契約**）です。**UI 層は本値を自分で組み立てません**（設定・環境値から描画に必要な形へ写す組み立て順は `Loupe.CreateRequest` が単一所有します）。UI 層は本値を `Loupe.ResolveContent` と描画層へ渡すだけで、描画に必要な値の単位と換算（DIP → 物理ピクセル）を持ちません。

**役割の分担**: ホストが設定する入力（設定値・環境値）は UI の公開プロパティ（`Options` / `DipToDeviceScaling`）が持ち、本値はそれらから**描画 1 フレーム分に必要な形へ解決したスナップショット**です（描画層は公開プロパティを読まない）。

| メンバー | 型 / 値 | 説明 |
| --- | --- | --- |
| `Center` | `EsPoint?` | 拡大中心（ドメイン座標 = 元画像ピクセル座標）。null の場合は市松模様のみを描画する |
| `DisplaySize` | `double` | ルーペの一辺の表示サイズ（DIP。ラベルの位置の基準であり、物理ピクセルサイズの換算元） |
| `PixelSize` | `int` | ルーペの一辺のサイズ（物理ピクセル。表示サイズ × `DipToDeviceScaling` を描画先でクランプ済み） |
| `ZoomLevel` | `int` | 拡大率（1〜64。設定 `Options.ZoomLevel` の値。描画完了通知のスケールにも使う） |
| `Options` | `LoupeOptions` | ルーペの設定（拡大率・寸法。**色は `LoupePaint`（描画層）が単一所有する**） |
| `DipToDeviceScaling` | `double` | 表示 DIP → 物理ピクセルのスケーリング係数 |
| `CheckerSizePixels` | `int` | 市松模様の 1 タイルの一辺（物理ピクセル。換算済みで最低 1。**値 0 を「描かない」の代用にしない**） |
| `BorderThicknessPixels` | `int` | 枠線の太さ（物理ピクセル。換算済みで最低 1。**値 0 を「描かない」の代用にしない**） |
| `SurfaceBounds` | `EsRect` | 描画先の全領域（左上を原点とする一辺 `PixelSize` の正方形・物理ピクセル）。下地・市松模様を全面へ描くために本値が導出する（UI 層は矩形を組み立てない）。**描画層へは描画内容として渡るため本値は internal** |

| メソッド | 説明 |
| --- | --- |
| `Create(center, displaySize, dipToDeviceScaling, options, availableSize = null)` | 描画入力の唯一の生成元。設定・環境値と DIP 値（表示サイズ）から組み立て、表示サイズを物理ピクセルへ換算し、描画先の領域が指定された場合はそれへクランプする（規則はコア内部が単一所有する）。**public である理由**: ファサード（`Loupe.CreateRequest`）と独自 UI が描画入力を組み立てる唯一の入口である（描画先の領域を知るのは呼び出し側だけである） |

拡大元画像は UI ごとに型が異なる（Avalonia は `IImage`、MewUI は `EsBitmap`）ため本値に含めず、描画層が解決した `EsBitmap` を別引数として渡します（両 UI が同じ手順で本値を受け取ります）。

### 切り出し領域の導出（`LoupeRenderRequest` の internal メンバー）

描画入力から切り出し領域の幾何を導出します（描画入力の値だけから決まるため、独立したクラスを持ちません）。

| メンバー | 説明 |
| --- | --- |
| `CaptureSize` | 切り出し領域の一辺（ドメインピクセル）。物理ピクセルサイズを拡大率で割った切り上げ（切り上げの規則は `LoupeDivMath.CeilDiv` が単一所有する）。十字線の交点も本値を使う |
| `CaptureTopLeft` | 拡大中心から切り出し領域の左上（ドメイン座標）。中心未設定の場合は null（描画しない）。**ホストが描画済み画像へオーバーレイを描くために public** |
| `MagnifiedImage` | 拡大画像の切り出し・描画元・描画先の矩形（`LoupeMagnifiedImage`）。**拡大倍率と描画先の大きさの関係を単一所有**し、両 UI が同じ拡大結果になるようにする。中心未設定の場合は null |

**切り出しの実行はありません**: 切り出しは描画層（UIBase の `LoupeRendererBase`）が `EsBitmap.Crop` を呼んで行います。本メンバーは「どこを切り出すか」を解決する値だけを持ちます。

### 寸法の換算（`Decoration.LoupeMetricsScaler`・internal）

寸法値（DIP）を描画に使う物理ピクセルへ換算する規則を単一所有します。安全域への補正は `LoupeOptions`（生成時）が済ませているため再補正はしません。**換算結果は `LoupeRenderRequest.Create` が描画入力（`CheckerSizePixels` / `BorderThicknessPixels`）へ解決済みとして含めるため、描画層は本型を呼びません。**

| メソッド | 説明 |
| --- | --- |
| `ScaleCheckerSize(int checkerSize, double scaling)` | 市松模様の 1 タイルサイズを物理ピクセルへ換算（切り上げ・最低 1。値は補正済みのものを渡す） |
| `ScaleBorderThickness(int borderThickness, double scaling)` | 枠線の太さを物理ピクセルへ換算（切り上げ・最低 1。値は補正済みのものを渡す） |

**中心座標ラベルは本型の対象外です**（ラベルは canvas へ描く場合も DIP で配置するため、換算を必要としません）。UI 層が `CheckerSize * scaling` のような式を自前で書かないための型です（片方の UI だけ換算が抜けると、DPI ≠ 100% で両 UI の見た目がずれます）。

## 4. 座標系

| 座標系 | 用途 |
| --- | --- |
| ドメイン座標 | 拡大中心・切り出し領域。元画像ピクセル座標 |
| 親領域 DIP | 表示レイアウト（表示サイズ・表示位置）。`ICoordTransform.Inverse` で得る |
| 物理ピクセル | 描画入力のサイズ・寸法。表示 DIP × `DipToDeviceScaling` |

- 拡大中心は `ICoordTransform` を通して親領域 DIP へ変換し、`PositionOffset` を加えてから親領域内へクランプします。
- 切り出し領域の一辺は物理ピクセルサイズを拡大率で割った切り上げであり、十字線の交点も同じ値から導出します。

## 5. 制限事項

- 描画 API は提供しません（描画の手順は UI 非依存の描画層 `LoupeControl.UIBase` が単一所有し、描画先は `Platform.Core` の `IEsCanvas` です）。
- 色（下地・市松模様・十字線・枠線・ラベル）は本コアに含みません（描画にだけ必要な設定であるため、描画層 `LoupeControl.UIBase` の `LoupePaint` が単一所有します）。
- フォントサイズ・角丸・内側余白は本コアに含みません（描画層の `LoupePaint` / `LoupeLabelPlan` が単一所有します）。
- 拡大元画像の取得・切り出しの実行は本コアに含みません（UI ごとに型が異なるため、UI 層と描画層が担います）。
- 再描画のバッチ化・世代番号管理は本コアに含みません（コントロール化にだけ必要な処理のため、`LoupeControl.UIBase` が単一所有します）。

## 6. ログ

ロガーは `Loupe.Log` で受け取り、**設定の変更**（`[Feature.LoupeCore] Setting: {名前} {前} → {後}`。Debug）を出力します。ロガー未設定（null）は出力なしです。

**メッセージの接頭辞は、そのログを定義しているプロジェクト名（コアは `[Feature.LoupeCore]`、UI 側の共有層は `[LoupeControl.UIBase]`、UI 実装は `[LoupeControl.AvaloniaUI]` 等）とします**（接頭辞で出力元の層を読み分けられるようにする。全層が同じ接頭辞だと、ログを見てもどのアセンブリの定義が出力したかを特定できない）。

| 契機 | 出力 |
| --- | --- |
| `Options` の set | 変化した項目だけ（`RequestedSize` / `ZoomLevel` / `CheckerSize` / `BorderThickness` / `ShowCoordLabel` / `ShowCheckerboard` / `ShowCrosshair` / `ShowBorder`） |
| `DipToDeviceScaling` / `CenterPoint` / `PositionOffset` の set | 変化したときだけ |

**差分検出（変化した項目だけを出力する判断）と「同値なら出力しない」規則は本コアが単一所有します**（UI 層は前後の値を渡さず、変更を通知するだけです）。ログ基盤（ファイル出力・レベル制御）はホストが所有し、本コアは定義だけを持ちます。

設定値の変更は `LogSettingChanged(名前, 前, 後)`（整数 / 実数 / 真偽）で出力し、**設定名は `nameof` をコアが付与します**（UI 層に設定名の文字列を持たせません）。拡大中心は座標の整形（`(X, Y)`）を伴うため専用の定義（`LogCenterPointChanged`）を持ちます。

**UI が出力するログ（拡大元画像の変更・描画の例外）とその書式は `LoupeControl.UIBase` が単一所有します**（コアは UI が呼ぶ定義を持ちません）。**UI 固有の処理に対応するログは各 UI プロジェクトが所有します**（中間ビットマップを生成する描画パイプライン固有のログ等は `LoupeControl.AvaloniaUI` にあります）。

## 7. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
