# Feature.Loupe.Core 外部機能仕様書

`LoupeControl`（選択範囲を拡大表示するコントロール）の **UI 非依存のコア**が提供する外部機能と公開 API の仕様です。利用者（UI 実装・ホスト）から見える情報のみを記載し、内部実装の詳細は記載しません。設計の意図は [README](README.md)、構造と依存は [SPEC_System](SPEC_System.md) を参照してください。

## 1. 提供機能

| 機能 | 提供元 | 説明 |
| --- | --- | --- |
| 表示レイアウトの解決 | `Loupe.ResolveLayout` | 親領域のサイズを渡すと、表示サイズ（要求サイズを親領域の最小辺へクランプ）と表示位置（拡大中心 ＋ オフセットの親領域内へのクランプ）を返す（拡大中心が未設定の場合は位置が null）。**状態を変更しない** |
| 描画の幾何の解決 | `Loupe.ResolveGeometry` | 描画 1 フレーム分の幾何（`LoupeGeometry`）を解決する。DIP → 物理ピクセルの換算・描画先へのクランプ・切り出し領域の算出はコアが行う |
| 提示（描画計画の解決） | 描画層 `Feature.Loupe.Presenter` の `LoupePresenter` | 幾何（`LoupeGeometry`）と外観から描画計画（`LoupeDrawPlan`：層の順序・装飾矩形・文言・配置）を組み立てる（本コアは描画の幾何だけを返す） |
| 描画（本コアの対象外） | 各 UI（`LoupeContextPainter` / `LoupeSkiaPainter`） | 描画計画を自 UI の描画 API へ写す。**描画 API の抽象は持たない**（UI フレームワークごとの描画モデルの差を吸収しきれないため） |
| 拡大中心の管理 | `Loupe` | 拡大中心（ドメイン座標）と表示位置のオフセットを保持する（マウス移動ごとの更新は UI 層が通知する） |
| 設定 | `LoupeOptions` / `Loupe.Limits` | 計算に必要な設定の単一値（表示サイズ・拡大率）と、既定値・安全域への補正 |
| ログ | `Loupe.Log` | 設定・環境値・拡大中心・位置オフセットの変更を検出して出力する（変化したときだけ） |
| 幾何計算 | `LoupePlacementGeometry` / `LoupeDivMath` | 表示位置、区画数の切り上げ（internal。ファサードが吸収する） |

### 前提条件

- 本コアは UI フレームワークに依存しません（依存は `Platform.Core` と `Microsoft.Extensions.Logging.Abstractions`（ログ定義のソース生成）のみ）。UI 固有の型は利用者（UI 実装）が境界で変換します。
- 座標は `EsPoint` / `EsSize` / `EsRect`（`EsUtil.Platform.Core`）、拡大元画像は `EsBitmap` で扱います。
- 描画の可否は**外観（`Feature.Loupe.Presenter` の `LoupeAppearance`）の明示フラグ**（`ShowCrosshair` / `ShowBorder` / `ShowCoordLabel`）が単一所有します（描画にだけ必要な設定のため、コアの設定には含めません）。
- 描画の順序（ペーストボード（画像外の色）→ 拡大画像 → 十字線 → 枠線 → テキスト）と色（`LoupeAppearance`）は描画層 `Feature.Loupe.Presenter` が単一所有します。
- コントロール化にだけ必要な処理（再描画要求のバッチ化・世代番号管理）は `LoupeControl.AvaloniaUI` が所有します。
- ラベルの**文言・基準点**は描画層（`LoupeLabelTexts`）が単一所有しますが、**描画と実寸測定**は利用者が行います（実寸は描画 API でしか測れないため）。

## 2. 公開 API 一覧

可視性は public のみを記載します（`internal` は [SPEC_System](SPEC_System.md) の可視性ポリシーを参照）。

### `EsUtil.Feature.Loupe.Core`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `Loupe` | class | 操作の唯一の窓口となるファサード |
| `Loupe.Limits`（`Loupe` の入れ子） | static class | 数値パラメータの既定値・安全域と補正 |

### `EsUtil.Feature.Loupe.Core.Drawing`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `LoupeGeometry` | record struct | 1 フレーム分の描画の幾何（コア⇔UI の契約）。UI 層は値の組を自分で組み立てず、`Loupe.ResolveGeometry` が返した値を受け取る |
| `LoupeMagnifiedImage` | record struct | 拡大画像の切り出しと描画の矩形（切り出し領域・描画元・描画先）。`LoupeGeometry` の導出値 |

### `EsUtil.Feature.Loupe.Core.Setting`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `LoupeOptions` | record | 計算に必要な設定（表示サイズ・拡大率）と既定値・補正。**色・フォント・枠線の太さ・表示可否は持たない**（外観（`LoupeAppearance`）が単一所有する） |

### `EsUtil.Feature.Loupe.Core.Placement`

| 型 | 種別 | 説明 |
| --- | --- | --- |
| `LoupeLayout` | readonly record struct | 表示レイアウトの値（表示サイズ（DIP）と表示位置。配置しない場合は位置が null） |

### `Loupe`（ファサード）のメンバー

ルーペの**操作の唯一の窓口**（適用パターン: Facade）。UI 層とホストは本型だけを参照し、配置計算・幾何の計算は internal のため外部からは直接呼べません。

| メンバー | 種別 | 説明 |
| --- | --- | --- |
| `Loupe(LoupeOptions? options = null)` | ctor | 生成（設定を省略した場合は `LoupeOptions.Default`）。描画実装は受け取らない（描画は UI の関心事であり、本コアは描画 API を持たない） |
| `Log` | property | ロガー。null は `NullLogger` へ吸収される。設定・環境値・拡大中心・位置オフセットの変更は本コアが検出して出力する |
| `Options` | property | 設定（表示サイズ・拡大率）。**変化したときだけ**変更ログを出力する |
| `DipToDeviceScaling` | property | 表示 DIP → 物理ピクセルのスケーリング係数（環境値。補正は `Loupe.Limits` が単一所有する）。**変化したときだけ**変更ログを出力する |
| `CenterPoint` | property | 拡大中心（ドメイン座標）。未設定（null）は拡大画像を描かず（画像外の色のみ）、配置も行わない。**変化したときだけ**変更ログを出力する |
| `PositionOffset` | property | 拡大中心からの右下オフセット（DIP。既定は 0）。**変化したときだけ**変更ログを出力する |
| `SetCoordTransform(ICoordTransform?)` | method | ドメイン ↔ 親領域 DIP の座標変換ポートの設定（null は等価変換） |
| `ResolveLayout(EsSize)` | method | 親領域サイズから表示レイアウト（`LoupeLayout` = 表示サイズ + 位置）を解決する（拡大中心未設定時は位置が null） |
| `ResolveGeometry(double displaySize, EsSize? availableSize = null)` | method | 描画 1 フレーム分の幾何（`LoupeGeometry`）の解決（DIP → 物理ピクセルの換算・描画先へのクランプ・切り出し領域の算出を含む） |

**本型が単一所有するもの**: 表示レイアウトの解決（表示サイズの補正と表示位置の決定）・描画の幾何の解決・**設定変更の検出とログ出力**。

表示サイズは `Options.RequestedSize` ではなく、親領域でクランプした後の値を渡します（クランプの基準が親パネルか描画先の表面かで異なるため、値の決定は呼び出し側が担います）。

### `LoupeOptions` のメンバー

計算に必要な設定（表示サイズ・拡大率）を 1 つの値として所有します。両 UI は本型を 1 プロパティとして公開するだけであり、UI ごとに既定値がずれたり設定プロパティが増えたりしません。

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `RequestedSize` | `int` | `160` | ルーペの基本サイズ（DIP・一辺）。安全域 1〜4096。表示サイズは UI 層が親領域・描画先へクランプして導出する |
| `ZoomLevel` | `int` | `4` | 拡大率（偶数推奨）。安全域 1〜64 |

`record` のため `with` 式による部分変更と値の等価比較ができます。等価比較は UI のプロパティシステムと併せて「同値の再設定では再描画しない」動作を保証します。

| メンバー | 説明 |
| --- | --- |
| `Default` | すべての項目を既定値にした設定（部分変更の基点: `LoupeOptions.Default with { ZoomLevel = 8 }`） |

数値項目（`RequestedSize` / `ZoomLevel`）は **`init` アクセサで補正**します（規則は `Loupe.Limits` が単一所有する）。したがってオブジェクト初期化子・`with` 式・逆シリアル化などの **どの設定経路でも**範囲内の値だけが保持され、UI 側に補正の登録（Avalonia の `coerce` / MewUI の `coerce`）を要求しません。

**色・フォント・枠線の太さ・表示可否は本型に含みません**（描画にだけ必要な設定のため、外観 `Feature.Loupe.Presenter` の `LoupeAppearance` が単一所有します。枠線の太さの補正は `LoupeAppearance` が `Loupe.Limits.CoerceBorderThickness` へ委譲します）。ホストは UI の `Appearance` プロパティへ `LoupeAppearance` を指定します。

**含めない値**: 拡大元画像・拡大中心（マウス移動ごとに変わるデータ）、DPI スケーリング係数（環境値）、表示位置のオフセット（UI 固有のホスト結線）。これらは UI の個別プロパティとして公開します。

### `Loupe.Limits` のメンバー

ルーペの数値パラメータの既定値と安全域を単一所有し、範囲外の値を補正します（非有限は既定値）。**枠線の太さの安全域は外観（`LoupeAppearance`）が参照します**。

| 定数 | 値 | 説明 |
| --- | --- | --- |
| `RequestedSizeMin` / `RequestedSizeMax` / `RequestedSizeDefault` | `1` / `4096` / `160` | 表示サイズ要求値（DIP） |
| `ZoomLevelMin` / `ZoomLevelMax` / `ZoomLevelDefault` | `1` / `64` / `4` | 拡大率 |
| `BorderThicknessMin` / `BorderThicknessMax` / `BorderThicknessDefault` | `1` / `64` / `2` | 枠線の太さ（ピクセル。外観（`LoupeAppearance`）が参照する） |
| `DipToDeviceScalingMin` / `DipToDeviceScalingMax` / `DipToDeviceScalingDefault` | `0.1` / `8.0` / `1.0` | スケーリング係数（環境値。他の数値より後に置く） |

| メソッド | 説明 |
| --- | --- |
| `CoerceRequestedSize(int)` | 表示サイズ要求値を 1〜4096 へ補正 |
| `CoerceZoomLevel(int)` | 拡大率を 1〜64 へ補正 |
| `CoerceBorderThickness(int)` | 枠線の太さを 1〜64 へ補正（外観（`LoupeAppearance`）が呼ぶ） |
| `CoerceDipToDeviceScaling(double)` | スケーリング係数を 0.1〜8.0 へ補正。非有限値は `DipToDeviceScalingDefault` |

### 描画の契約（本コアには置かない）

描画の契約は本コアにありません（**本コアは描画 API も描画の抽象も持ちません**）。描画計画の解決は `Feature.Loupe.Presenter` の `LoupePresenter.ResolveDrawPlan` が、描画は各 UI の描画実装（`LoupeContextPainter` / `LoupeSkiaPainter`）が担います。層の順序は `LoupeDrawLayer` として描画層が単一所有し、UI はその順に写すだけです。

```csharp
var loupe = new Loupe(options) { CenterPoint = cursorDomain, DipToDeviceScaling = 1.25 };
loupe.SetCoordTransform(port);

var layout = loupe.ResolveLayout(parentSize);          // 表示サイズ + 表示位置（配置しない場合は Position が null）
var geometry = loupe.ResolveGeometry(layout.DisplaySize, canvasSize); // 幾何（物理ピクセル・切り出し領域を解決済み）

// 描画完了通知（ホストが描画済み画像へオーバーレイを描くための情報）は幾何から読む
var captureTopLeft = geometry.CaptureTopLeft;   // 切り出し領域の左上（ドメイン座標。中心未設定は null）
var scale = geometry.ZoomLevel;                 // ドメイン座標 → ルーペ画像ピクセルの変換係数
```

## 3. 契約（利用者が守る・受け取る値）

### 描画の幾何（`LoupeGeometry`）

1 フレーム分の描画の幾何（**コア⇔UI の契約**）です。**UI 層は本値を自分で組み立てません**（設定・環境値から描画に必要な形へ写す組み立て順は `Loupe.ResolveGeometry` が単一所有します）。UI 層は本値を提示層（`LoupePresenter`）へ渡すだけで、描画に必要な値の単位と換算（DIP → 物理ピクセル）を持ちません。

**役割の分担**: ホストが設定する入力（設定値・環境値）は UI の公開プロパティ（`Options` / `DipToDeviceScaling`）が持ち、本値はそれらから**描画 1 フレーム分に必要な形へ解決したスナップショット**です（提示層は公開プロパティを読まない）。

| メンバー | 型 / 値 | 説明 |
| --- | --- | --- |
| `Center` | `EsPoint?` | 拡大中心（ドメイン座標 = 元画像ピクセル座標）。null の場合は拡大画像を描画しない |
| `DisplaySize` | `double` | ルーペの一辺の表示サイズ（DIP。ラベルの位置の基準であり、物理ピクセルサイズの換算元） |
| `PixelSize` | `int` | ルーペの一辺のサイズ（物理ピクセル。表示サイズ × `DipToDeviceScaling` を描画先でクランプ済み） |
| `DipToDeviceScaling` | `double` | 表示 DIP → 物理ピクセルのスケーリング係数（描画計画の解決（枠線の太さの換算）にも使う） |
| `ZoomLevel` | `int` | 拡大率（1〜64。設定 `Options.ZoomLevel` の値。描画完了通知のスケールにも使う） |
| `CaptureSize` | `int` | 切り出し領域の一辺（ドメインピクセル）。物理ピクセルサイズを拡大率で割った切り上げ（切り上げの規則は `LoupeDivMath.CeilDiv` が単一所有する） |
| `SurfaceBounds` | `EsRect` | 描画先の全領域（左上を原点とする一辺 `PixelSize` の正方形・物理ピクセル）。ペーストボード（画像外の色）を全面へ塗るために本値が導出する（UI 層は矩形を組み立てない） |
| `CaptureTopLeft` | `EsPoint?` | 拡大中心から切り出し領域の左上（ドメイン座標）。中心未設定の場合は null（描画しない） |
| `MagnifiedImage` | `LoupeMagnifiedImage?` | 拡大画像の切り出し・描画元・描画先の矩形。中心未設定の場合は null |

| メソッド | 説明 |
| --- | --- |
| （なし） | ファサードの `Loupe.ResolveGeometry` が唯一の生成元である（UI 層は本値を組み立てない） |

拡大元画像は UI ごとに型が異なる（Avalonia は `IImage`、MewUI は `EsBitmap`）ため本値に含めず、描画実装が解決した `EsBitmap` を別引数として渡します（両 UI が同じ手順で本値を受け取ります）。

### 切り出し領域の導出（`LoupeGeometry` のメンバー）

描画の幾何から切り出し領域を導出します（`LoupeGeometry` の値だけから決まるため、独立したクラスを持ちません）。

| メンバー | 説明 |
| --- | --- |
| `CaptureSize` | 切り出し領域の一辺（ドメインピクセル）。物理ピクセルサイズを拡大率で割った切り上げ。十字線の交点も本値を使う |
| `CaptureTopLeft` | 拡大中心から切り出し領域の左上（ドメイン座標）。中心未設定の場合は null（描画しない）。**ホストが描画済み画像へオーバーレイを描くために public** |
| `MagnifiedImage` | 拡大画像の切り出し・描画元・描画先の矩形（`LoupeMagnifiedImage`）。**拡大倍率と描画先の大きさの関係を単一所有**し、両 UI が同じ拡大結果になるようにする。中心未設定の場合は null |

**切り出しの実行はありません**: 切り出しは描画実装（各 UI の painter）が `EsBitmap.Crop` を呼んで行います。本メンバーは「どこを切り出すか」を解決する値だけを持ちます。

### 表示可否・装飾・文言（描画層が単一所有）

以下はコアには含みません（描画にだけ必要な設定のため、描画層 `Feature.Loupe.Presenter` が単一所有します）。詳細は `Feature.Loupe.Presenter` の SPEC を参照してください。

| 項目 | 所有者 |
| --- | --- |
| 表示可否（`ShowCrosshair` / `ShowBorder` / `ShowCoordLabel`） | `LoupeAppearance`（外観） |
| 色・フォントサイズ・角丸・枠線の太さ | `LoupeAppearance`（外観） |
| 描画層と順序（`LoupeDrawLayer`）・十字線 / 枠線の矩形 | `LoupePresenter` / `LoupeDecorationGeometry` |
| 中心座標ラベルの文言・基準点 | `LoupeLabelTexts` |
| 実寸 → ラベル要素の配置 | `LoupeLabelPlan` |
| 枠線の太さの物理ピクセル換算 | `Platform.Core` の `DpiScaler`（呼び出すのは `LoupePresenter.ResolveDrawPlan`） |

## 4. 座標系

| 座標系 | 用途 |
| --- | --- |
| ドメイン座標 | 拡大中心・切り出し領域。元画像ピクセル座標 |
| 親領域 DIP | 表示レイアウト（表示サイズ・表示位置）。`ICoordTransform.Inverse` で得る |
| 物理ピクセル | 描画の幾何のサイズ・寸法。表示 DIP × `DipToDeviceScaling` |

- 拡大中心は `ICoordTransform` を通して親領域 DIP へ変換し、`PositionOffset` を加えてから親領域内へクランプします。
- 切り出し領域の一辺は物理ピクセルサイズを拡大率で割った切り上げです。

## 5. 制限事項

- 描画 API は提供しません（描画計画の解決は描画層 `Feature.Loupe.Presenter` が単一所有し、描画は各 UI が自 UI の描画 API で行います。描画 API の抽象は持ちません）。
- 色（ペーストボード・十字線・枠線・ラベル）は本コアに含みません（描画にだけ必要な設定であるため、外観 `LoupeAppearance` が単一所有します）。
- フォントサイズ・角丸・内側余白は本コアに含みません（外観 `LoupeAppearance` / `LoupeLabelPlan` が単一所有します）。
- 表示可否（十字線・枠線・中心座標ラベル）は本コアに含みません（描画にだけ必要な設定であるため、外観 `LoupeAppearance` が単一所有します）。
- 拡大元画像の取得・切り出しの実行は本コアに含みません（UI ごとに型が異なるため、UI 層が担います）。
- 再描画のバッチ化・世代番号管理は本コアに含みません（コントロール化にだけ必要な処理のため、`LoupeControl.AvaloniaUI` が所有します）。

## 6. ログ

ロガーは `Loupe.Log` で受け取り、**設定の変更**（`[Feature.Loupe.Core] Setting: {名前} {前} → {後}`。Debug）を出力します。ロガー未設定（null）は出力なしです。

**メッセージの接頭辞は、そのログを定義しているプロジェクト名（コアは `[Feature.Loupe.Core]`、UI 側の共有層は `[Feature.Loupe.Presenter]`、UI 実装は `[LoupeControl.AvaloniaUI]` 等）とします**（接頭辞で出力元の層を読み分けられるようにする。全層が同じ接頭辞だと、ログを見てもどのアセンブリの定義が出力したかを特定できない）。

| 契機 | 出力 |
| --- | --- |
| `Options` の set | 変化した項目だけ（`RequestedSize` / `ZoomLevel`） |
| `DipToDeviceScaling` / `CenterPoint` / `PositionOffset` の set | 変化したときだけ |

**差分検出（変化した項目だけを出力する判断）と「同値なら出力しない」規則は本コアが単一所有します**（UI 層は前後の値を渡さず、変更を通知するだけです）。枠線の太さ・表示可否（外観）の変更ログは `Feature.Loupe.Presenter` が出力します。ログ基盤（ファイル出力・レベル制御）はホストが所有し、本コアは定義だけを持ちます。

設定値の変更は `LogSettingChanged(名前, 前, 後)`（整数 / 実数 / 真偽）で出力し、**設定名は `nameof` をコアが付与します**（UI 層に設定名の文字列を持たせません）。拡大中心は座標の整形（`(X, Y)`）を伴うため専用の定義（`LogCenterPointChanged`）を持ちます。設定の変更ログの所要項目は `LoupeOptions` の項目（表示サイズ・拡大率）に一致します。

**UI が出力するログ（拡大元画像の変更・描画の例外）とその書式は `Feature.Loupe.Presenter` が単一所有します**（コアは UI が呼ぶ定義を持ちません）。**UI 固有の処理に対応するログは各 UI プロジェクトが所有します**（中間ビットマップを生成する描画パイプライン固有のログ等は `LoupeControl.AvaloniaUI` にあります）。

## 7. ライセンス

もう一方のコア（`Feature.Marquee.Core`）との機能差に由来する非対称は、README の「機能差に由来する非対称（もう一方のコアとの違い）」を参照してください。

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
