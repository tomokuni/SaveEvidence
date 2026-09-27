# Feature.LoupeCore

`LoupeControl.AvaloniaUI`（Avalonia UI 実装）と `LoupeControl.MewUI`（MewUI 実装）が共有する、**UI フレームワーク非依存のルーペロジック**を提供するプロジェクトです。

## 目的

ルーペ（拡大鏡）の計算ロジックと寸法のうち、UI フレームワークに依存しない部分（寸法の値・数値の安全域補正・表示サイズ / 位置の計算・切り出し領域の計算・装飾矩形の計算・ラベル配置・ログ定義）を本プロジェクトへ切り出すことで、同じロジックと見た目を MewUI など別 UI からも再利用できます。

**操作の唯一の窓口は公開ファサード `Loupe`** です。UI 層とホストは本型だけを参照し、表示レイアウト・**描画内容**・描画入力・通知内容の解決を本型へ委ねます（個々の計算クラスはファサードの内部実装です）。

**描画に必要な値はすべてコアが解決します**。描画層（`LoupeControl.UIBase` の `LoupeRenderer`）は `Loupe.ResolveContent` が返す**描画内容**（`LoupeDrawContent`）を描画ポートへ写すだけで、幾何計算（タイルの位置・矩形の導出・寸法の換算）を持ちません。

**描画は本コアの関心事ではありません**。本コアは描画 API も描画ポートも持たない（依存先は `Platform.Core` だけである）。返すのは「何を描くか」のデータ（`LoupeDrawContent` と `Layers`）だけであり、層の順序に従った描画手順は UI 側の共有層（`LoupeControl.UIBase` の `LoupeRendererBase`）が単一所有します。

## 収録する型

ルート（`src/`）に置くのは**ファサード本体とその partial**（`Loupe.cs` / `Loupe.DrawContent.cs` / `Loupe.Limits.cs`）だけです。それ以外は用途別のフォルダへまとめます。
**1 ファイル 1 型**とします（複数の型を 1 ファイルへ含めない。`partial` による分割は可）。
**名前空間はフォルダに追従**させます（ルート直下のみ `EsUtil.Feature.LoupeCore`。`Drawing` / `Setting` / `Logging` / `Checkerboard` / `Decoration` / `Placement` は `EsUtil.Feature.LoupeCore.<フォルダ名>`）。公開面も複数のフォルダに分かれるため、利用者は用途に応じて `using` を追加します。

| フォルダ | 型 | 可視性 | 責務 |
| --- | --- | --- | --- |
| ルート | `Loupe` | public | **公開ファサード（操作の唯一の窓口）**。設定・環境値・拡大中心・位置オフセットの保持、表示レイアウトの解決（`ResolveLayout`）、描画入力の生成（`CreateRequest`）、**描画内容の解決（`ResolveContent`）**、座標変換ポートの設定（`SetCoordinateTransform`） |
| ルート | `Loupe.Limits`（`Loupe` の入れ子） | public | 数値パラメータの既定値・安全域（最小 / 最大）の単一所有と、範囲外の値の補正。**UI がプロパティの既定値と coerce を登録するときに参照する**ためファサードの入れ子として公開する |
| ルート | `Loupe.DrawContent.cs`（`Loupe` の **partial**） | internal | 描画内容の**内部手順**（市松模様のタイル数・十字線 / 枠線の矩形・中心座標ラベルの文言の解決）を持つ。公開の入口（`ResolveContent`）はファサード本体（`Loupe.cs`）が持つ |
| （本コアには置かない） | `ILoupeRenderer` / `LoupeRendererBase` | — | 描画の契約と描画手順の基底は **UI 側の共有層（`LoupeControl.UIBase`）が所有します**（本コアは描画 API も描画ポートも持たない） |
| `Drawing/` | `LoupeDrawLayer` | public | 描く層とその順序（**描画順はコアが単一所有する**。層の有無は**解決済みの値**から決まり（層にある ⇔ 描く値がある）、下地は常に含む。中心座標ラベルはテキストがある場合のみ末尾 = 常に最上位） |
| `Drawing/` | `LoupeRenderRequest` | public | 1 フレーム分の描画入力（**コア⇔UI の契約**）。UI 層は自分で組み立てず、ファサードの `Loupe.CreateRequest` が返した値を受け取る（単位と組み立て順（DIP → 物理ピクセルの換算とクランプ）を UI 層が持たない）。**換算済みの寸法**（`CheckerSizePixels` / `BorderThicknessPixels`）も含む |
| `Drawing/` | `LoupeDrawContent` | public | **1 フレーム分の描画内容**（描画面の矩形・市松模様のタイル列挙子・十字線の矩形・枠線の矩形・拡大画像の幾何・**層の順序**）。描画層は本値を写すだけで計算を持たない |
| `Drawing/` | `LoupeMagnifiedImage` | public | 拡大画像の切り出しと描画の矩形（切り出し領域・描画元・描画先）。描画内容の一部 |
| `Drawing/` | `LoupeCheckerTiles` | public | 市松模様の暗色タイルの矩形を、**リストを確保せずに列挙する構造体**（描画内容の一部）。列挙子で渡し、描画のたびのヒープ確保をゼロにする |
| `Setting/` | `LoupeOptions` | public | ルーペの設定（表示サイズ・拡大率・寸法（市松タイル・枠線の太さ）と表示可否（中心座標ラベル・市松模様・十字線・枠線））を 1 つの値として所有。既定値・生成時の補正・ラベル表示の判定（`ShouldShowCoordLabel`）を持つ（設定経路によらず範囲内の値だけが保持される）。**色は持たない**（色は描画層 `LoupeControl.UIBase` の `LoupePaint` が単一所有する） |
| `Drawing/` | `LoupeTextPlacement` | public | コアが UI 層へ渡すテキスト（文言・基準点・アライン・描画領域）。**要素として置くか canvas へ描くかは UI 層が決める**（フォントサイズ・角丸・色は描画層 `LoupeControl.UIBase` の `LoupePaint` が持つ） |
| `Placement/` | `LoupeLayout` | public | 表示レイアウトの値（表示サイズと表示位置。配置しない場合は位置が null）。描画入力の生成と UI 層の要素配置の両方へ渡す |
| `Logging/` | `LoupeLogMessage` | internal | コアが出力するログ定義・変化判定（設定値・拡大中心の変更）。**UI が出力する定義（拡大元画像の変更・描画の例外）と書式は `LoupeControl.UIBase` が単一所有する** |
| `Checkerboard/` | `LoupeCheckerboard` | internal | 市松模様の明暗の判定規則（タイル矩形の列挙は `LoupeCheckerTiles`） |
| `Checkerboard/` | `LoupeDivMath` | internal | 区画数の切り上げ除算（タイル数と切り出し領域の一辺が共用する規則） |
| `Decoration/` | `LoupeDecoGeometry` | internal | 十字線（縦横 2 本）と枠線（4 辺）の矩形の計算（結果は描画内容に含まれる） |
| `Decoration/` | `LoupeMetricsScaler` | internal | 寸法値（市松タイル・枠線の太さ）の物理ピクセル換算（結果は描画入力に含まれる） |
| `Placement/` | `LoupePlacementGeometry` | internal | 表示位置（左上）の計算（中心 + オフセットと親領域内へのクランプ）。親サイズ未確定の判定も本クラスが扱う |

切り出し領域の幾何（一辺・左上・拡大画像の矩形）は `LoupeRenderRequest` の**メンバー**（`CaptureSize` / `MagnifiedImage` / `SurfaceBounds` は internal、`CaptureTopLeft` は描画完了通知のために public）が単一所有します（描画入力の値から導出するため、独立したクラスを持ちません）。

## 公開面の階層

公開面は「誰が何のために使うか」で 3 段に分かれます。**外部が使わない内部契約は `internal`** とし、単体テストにのみ `InternalsVisibleTo` で公開します。

| 層 | 利用者 | 型 |
| --- | --- | --- |
| **1. ホスト設定** | アプリ・ホスト | `LoupeOptions` / `LoupeLayout` |
| **2. 公開ファサードと UI アダプタ契約** | UI 実装（描画層） | `Loupe`（操作の唯一の窓口。既定値は `Limits.*Default`） / `LoupeRenderRequest` / `LoupeDrawContent` / `LoupeDrawLayer` / `LoupeTextPlacement` / `LoupeMagnifiedImage` / `LoupeCheckerTiles` |
| **3. 内部ヘルパー** | 本コアのみ（テストのみ IVT） | `LoupeDecoGeometry` / `LoupeMetricsScaler` / `LoupePlacementGeometry` / `LoupeCheckerboard` / `LoupeDivMath` / `LoupeLogMessage` / `Loupe.DrawContent.cs`（「1 フレーム分の描画内容の解決」の公開の入口は `Loupe.ResolveContent`） |

- **操作はファサード経由に一本化**しています（計算クラスと内部手順は `internal` のため、UI 層はファサード以外の入口を持ちません）。
- **層 3 は外部参照 0 件**を実測して絞り込んだ結果です。「描画内容の解決（`Loupe.ResolveContent`）と描画に必要な値」だけが公開面です。
- **層 2 は UI アダプタを自作する場合の契約**です（独自 UI を追加するには、この面だけで完結します）。
- **層 1 は設定と受け渡しの値**です（ファサードを持たず、設定値と描画契約だけを使う軽い用途でも使えます）。
- `[EditorBrowsable]` による「見えにくくする」対処はしません。公開面も契約も変わらず（見つけにくくなるだけ）、手段と目的が食い違うためです。**隠すか公開するかの二択**とし、隠す場合は `internal` で除きます。

**UI 非依存の汎用ライブラリ**として、特定のアプリ・UI 実装に依存しません（文言・モード等のアプリ固有情報を持ちません）。

## 依存方向

層の全体像（Core / UIBase / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（LoupeControl.AvaloniaUI / LoupeControl.MewUI）
  入力の受付・効果の適用・通知の転送・描画先の用意・ラベル要素の配置
    ├──> LoupeControl.UIBase   どう描くか（UIBase の ILoupeRenderer を実装し、描画内容を写す）＋ 組み込み先がある場合にだけ必要な規則
    └──> Feature.LoupeCore ← 本プロジェクト   何を描くか + 判断（状態・値・純粋計算・描画内容の契約）
           └──> Platform.Core（Es* 値型・ICoordTransform）
※ Core は UIBase を参照しない（UIBase がコアの描画内容を使う向きだけ）。Core は Common も参照しない。
```

本プロジェクトが UI フレームワークへ依存することはありません。外部パッケージへの直接依存もありません（`Platform.Core` 経由で SkiaSharp を参照します）。

**コントロール化にだけ必要な処理は `LoupeControl.UIBase` が所有します**（再描画要求のバッチ化・世代番号管理）。**描画の手順（層の順序に従う写し方）と色も `LoupeControl.UIBase` が所有します**（手順は下地 → 市松模様 → 拡大画像 → 十字線 → 枠線を層の順に描く。色は `LoupePaint`。描画先は `IEsCanvas`）。本プロジェクトはルーペの計算と寸法（どのアセンブリでも同じ結果になる部分）に限定し、描画 API も色も持ちません。

## 層の責務の境界

| 層 | 責務 |
| --- | --- |
| Core（本プロジェクト） | 設定・寸法の値とその既定値・補正、状態の判断、両 UI 共通の計算、コア⇔UI の契約（描画入力・描画完了通知）、公開ファサード（`Loupe` = 操作の唯一の窓口）、ログ定義 |
| UIBase（`LoupeControl.UIBase`） | 描画の手順（`LoupeRendererBase` と `LoupeRenderer`）と色（`LoupePaint`）、実寸 → ラベル要素の写像（`LoupeLabelPlan`）、組み込み先がある場合にだけ必要な規則（`LoupeRenderCoordinator`） |
| UI 層（`LoupeControl.*`） | 状態（拡大元画像・拡大中心）の保持と設定（`Options`・`Paint`）の保持、**ファサードへの委譲**（表示レイアウト・描画入力・通知内容の解決）、描画層（`LoupeRenderer`）への描画の委譲、描画ポートの適合（`AvaloniaCanvas` / `SkiaEsCanvas`）とラベル要素の配置、ホスト結線 |

UI 層に残してよい分岐は 4 種のみです（それ以外の判断はコアへ移します）。

1. 提示の可否（拡大元画像・拡大中心が揃っていない、大きさ 0、親が未接続、非表示）
2. 空のコレクションをスキップする性能ガード（描画結果を変えない）
3. フレームワーク固有値の解決（DPI・UI 色型→描画色型の変換）
4. 中間バッファの有無など UI 固有の描画方式の差（中間ビットマップを生成するかキャンバスへ直接描くか。描画結果は一致させる）

**数値の安全域への補正は値の型（`LoupeOptions`）が生成時に適用します**（`init` アクセサ）。UI 側のプロパティへ補正の登録を要求すると、登録を忘れた UI だけバインディング経由の値が安全域外になります（実例あり: 両 UI で 1 回ずつ発生）。型の生成時に補正すれば、値の設定経路（オブジェクト初期化子・`with` 式・逆シリアル化）によらず範囲内の値だけが保持され、UI 側に補正の判断が残りません。

**描画の可否はコアの明示フラグが単一所有します**（`LoupeOptions.ShowCheckerboard` / `ShowCrosshair` / `ShowBorder` / `ShowCoordLabel`）。非表示の項目はコアが**空の描画内容**として返し、描画層は層ごとの描画可否を判定しません（判定を持つと両 UI でずれ、片方だけ修正されます）。`LoupePaint` は色とラベルの体裁のみを持ちます（`Platform.Core` の `DrawingColorExtensions.IsVisible` は、ホストの省略可能な色設定をコアのフラグへ写す場面で使います）。

**寸法値の物理ピクセル換算はコアが描画入力（`LoupeRenderRequest.CheckerSizePixels` / `BorderThicknessPixels`）へ済ませます**。UI 層が `CheckerSize * scaling` のような式を自前で書くと、片方の UI だけ換算が抜けて DPI ≠ 100% で見た目がずれます（実例あり）。

**「変化したときだけログ出力する」判定はコアが単一所有します**（`Options` / `DipToDeviceScaling` / `CenterPoint` / `PositionOffset` の setter が変化を検出し、`LogOptionsChanged` などの定義で出力します）。UI 層は変更通知で値を転送するだけで、同値判定も前後の値の保持も持ちません（設定（`LoupeOptions`）の変更は `LogOptionsChanged` が**変化した項目だけ**を出力します）。ログの出力先は `Log` プロパティで受け取ります（UI の `Log` 設定がコアへ伝搬します）。

**設定（表示サイズ・拡大率・寸法）は 1 つの値（`LoupeOptions`）にまとめます**。UI 層の公開プロパティを設定の数だけ増やすと、UI ごとに宣言・アクセサ・補正の登録・変更ハンドラが並び、同じ設定を複数回に分けて代入する呼び出し（設定の適用箇所）が生まれます。1 つの値にすれば UI は `Options` 1 プロパティを公開するだけで済み、**既定値と補正はコアの 1 箇所**が所有します（参照実装は `MarqueeControl` の `MarqueeOptions`）。

**設定と状態（データ）は区別します**。マウス移動ごとに変わる拡大元画像・拡大中心は個別プロパティのままです（設定に含めると、中心の更新のたびに寸法を含む値全体を組み立て直すことになります）。DPI スケーリング係数（環境値）と表示位置のオフセット（UI 固有のホスト結線）も設定に含めません。

**表示サイズ（クランプ済み）の決定はコアのファサードが単一所有します**。要求サイズを親領域の最小辺へクランプする規則は `Loupe.ResolveLayout` の 1 箇所です（親領域のサイズを引数で受けるため、クランプの基準をコアが保持しません）。UI 層は `ResolveLayout` の結果を要素サイズ・描画入力・配置へ写すだけです（複数箇所でクランプすると、要求サイズの変更直後にクランプ前の値で描画され、要素サイズと描画ビットマップが食い違います）。

**操作の入口は公開ファサード（`Loupe`）に一本化します**。UI 層とホストは本型だけを参照し、表示レイアウトの解決（`ResolveLayout`）・描画入力の生成（`CreateRequest`）を本型へ委ねます（MewUI 版のホストも位置計算のために計算クラスを直接呼ばず、コントロールの `ResolveLayout` を経由します）。

**下地色（`LoupePaint.BackgroundColor`）は描画層（`LoupeControl.UIBase`）が単一所有します**。市松模様の色は半透明のため、背後の要素を透かしたくない場合はホストが不透明色を指定します（既定は透明 = 透かす）。**キャンバスへ直接描画する UI（MewUI）とウィンドウの上へ重ねる UI（Avalonia）のどちらも同じ色の値で指定でき、UI 層に固有の下地プロパティを持ちません**。塗る処理と描画順は UI 非依存の描画層（`LoupeControl.UIBase` の `LoupeRenderer`）が単一所有します。

## 機能差に由来する非対称（もう一方のコアとの違い）

本コアと `Feature.MarqueeCore` は同じ規約（`UI → UIBase → Core → Platform.Core` の一方向依存、層の順序をコアが所有、色は描画層が所有）で作られています。揃えない（揃えると書になる）違いは機能差に由来するもので、以下に列挙します。

| 非対称 | Loupe | Marquee | 理由 |
| --- | --- | --- | --- |
| 描画パイプラインの段数 | 3 段（`ResolveLayout` → `CreateRequest` → `ResolveContent`） | 2 段（`CreateRequest` → `ResolveContent`） | Marquee は親セル全面へ重ねるため配置の余地がない（`LoupeLayout` 相当を持たない） |
| 入力の通知 | 持たない（状態はホストが設定するだけ） | 対話モジュール（`Interaction/` + `Contract/` + `Press` / `Move` / `Release` / `KeyDown` + `Notified`）を持つ | ルーペは入力の解釈を持たない |
| 寸法の型 | `int`（物理ピクセルへ換算して描く） | `double`（DIP のまま描く） | 描く座標系が違う（オフスクリーン画像 / 生きたコンテキスト） |
| 設定型の数 | 1 型（`LoupeOptions`） | 2 型（`MarqueeOptions` + `MarqueeDecoOptions`） | Marquee の装飾は撮影オーバーレイ等へ組み込むときだけ必要なサブ概念 |
| 描画データの粒度 | 層ごとに素の矩形（`BorderRects` 等） | 層ごとに専用型（`MarqueeHandles` / `MarqueeMarker` / `MarqueeOuterDecoFrame`） | Marquee は生きたコンテキストへ直接描く（幾何を型で渡す） |
| 下地の層 | `LoupeDrawLayer.Background` を持つ | 持たない（`MarqueeDrawLayer` の先頭は `Decoration`） | ルーペは描画面（ビットマップ）を消す必要がある（Marquee は UI が毎回消す） |
| 描画の通知 | `LoupeRenderRequest` の `CaptureTopLeft` / `ZoomLevel` | セッションの通知（`MarqueeNotice`） | 通知の発生源が違う（コアの描画入力 / 状態機械） |
| 表示可否の述語の引数 | `ShouldShowCoordLabel(bool)` | `ShouldShowSizeLabel(bool)` / `ShouldShowCoordLabel()` | ルーペの座標ラベルは拡大中心が未設定になりうる（Marquee の座標ラベルは入力に常にある） |
| `Limits` の追加役割 | 安全域と補正のみ | 未設定（0 以下）の解決規則も持つ（`ResolveDisplayScale`） | ラベルの表示スケールは Marquee 固有の概念 |
| ログ定義の構成 | `LogOptionsChanged` のみ | `LogOptionsChanged` + `LogDecoOptionsChanged`（入れ子のキーを使う） | 設定型の数の違いに由来する |

## 主な API

```csharp
// 色は描画層（LoupeControl.UIBase）の LoupePaint が単一所有する（コアの LoupeOptions は色を持たない）
var paint = LoupePaint.Default with { CheckerColorA = Color.White };  // 色は System.Drawing.Color（UI 非依存）で指定する
// コアは描画実装を持たない（描画は UI 層の都合であり、層の順序に従う手順は UIBase が単一所有する）
var loupe = new Loupe(options);                                // options は null 可（null は LoupeOptions.Default）
loupe.Log = logger;                                            // null は NullLogger へ吸収される
loupe.SetCoordinateTransform(new CoordTransform(p => p, p => p, () => null)); // ドメイン ⇔ 親領域 DIP

// 状態（ホストが更新する。設定はコア、拡大元画像と拡大中心は UI 層が保持する）
loupe.DipToDeviceScaling = 1.25;                               // 環境値（DPI 追従。範囲外は補正される）
loupe.CenterPoint = cursorDomain;                              // 拡大中心（元画像ピクセル座標。null は市松模様のみ）
loupe.PositionOffset = 16;                                     // カーソルから離して配置するためのホスト指定値

// 描画（1 フレーム分。レイアウト → 描画入力 → 描画内容の順に解決する）
var layout = loupe.ResolveLayout(parentSize);                  // 表示サイズ（DIP）+ 表示位置（配置しない場合は null）
var request = loupe.CreateRequest(layout.DisplaySize, canvasSize); // 描画入力（DIP → 物理ピクセルの換算とクランプ済み）
var content = loupe.ResolveContent(request);                   // 値を持つ項目を自 UI の描画 API へ写すだけ
// 描画は UI 層が行う（層の順序に従う手順は LoupeControl.UIBase の LoupeRendererBase が単一所有する）
// テキストを UI 要素として置く場合は content.CoordLabelText を
// LoupeControl.UIBase の LoupeLabelPlan へ渡して配置を求める
// 描画完了通知（ホストが描画済み画像へオーバーレイを描くため）は request.CaptureTopLeft / request.ZoomLevel から読む
```

| メンバー | 用途 |
| --- | --- |
| `Loupe(LoupeOptions?)` | 生成（設定を省略した場合は `LoupeOptions.Default`。描画実装は受け取らない） |
| `ResolveLayout` | 親領域サイズから表示レイアウト（表示サイズ + 表示位置）を解決する。**状態を変更しない**（親領域のサイズを保持しない） |
| `CreateRequest` | 描画 1 フレーム分の入力（`LoupeRenderRequest`）の生成（DIP → 物理ピクセルの換算と描画先へのクランプを含む） |
| `ResolveContent` | 1 フレーム分の描画内容（`LoupeDrawContent`）の解決（描くかどうかの判断はコア） |
| `SetCoordinateTransform` | 座標変換ポートの設定（null は等価変換。変換関数は呼び出し毎に評価されるためレイアウト変化に追従する） |
| `Options` / `DipToDeviceScaling` / `CenterPoint` / `PositionOffset` / `Log` | 設定・環境値・拡大中心・オフセットの読み書き（変更は**変化したときだけ**ログ出力。`Log` は null を `NullLogger` へ吸収する） |
| `Limits.*` | 数値パラメータの既定値・安全域と補正。UI が依存関係プロパティの既定値と coerce に使う |
| `LoupeOptions.Default` | 設定の既定値（部分変更の基点。`LoupeOptions.Default with { ZoomLevel = 8 }`） |

UI 側（`LoupeControl`）が公開する入口:

| メンバー | 用途 |
| --- | --- |
| `Source` / `CenterPoint` | 拡大元画像と拡大中心（マウス移動ごとに更新する状態。UI 層が保持する） |
| `Options` / `Paint` | 設定（`LoupeOptions`）と色（`LoupePaint`）。同値の再設定では再描画されない |
| `DipToDeviceScaling` | 環境値（DPI 追従）。モニター間の移動でも UI が自動更新する |
| `Rendered` | 描画完了の通知（ホストが描画済み画像へ選択矩形の破線等を追加描画するための入口） |
| `SetCoordinateTransform` / `RefreshPosition` | 座標変換ポートの設定と、表示位置の再計算要求（パン・ズームなどレイアウト非変更の変化に追従する） |

## 設計上の約束

1. **判断はコア・反映は UI**: 「どうするか」（表示の可否・層の有無・描画の矩形・ラベルの文言・配置の可否）はコアが決め、UI は描画内容を写すだけにする。
2. **単一所有**: 十字線・枠線の矩形は `LoupeDecoGeometry`、表示位置は `LoupePlacementGeometry`、区画数の切り上げは `LoupeDivMath`、寸法の換算は `LoupeMetricsScaler` のみが持つ。
3. **座標系の 3 区分**: ドメイン座標（元画像 px）→ 親領域 DIP（`ICoordTransform`）→ 物理ピクセル（`DipToDeviceScaling`）のうち、① ドメイン → DIP は `Loupe`、② DIP → 物理ピクセルは `LoupeRenderRequest` が担う（描画層は換算しない）。
4. **設定は 1 値・補正は生成時**: 設定は `LoupeOptions` の 1 値にまとめ、数値項目の安全域への補正は型の生成時（`init` アクセサ）に適用する（UI 側に補正の登録を要求しない）。
5. **表示サイズの決定はファサードが単一所有**: 要求サイズを親領域の最小辺へクランプする規則は `Loupe.ResolveLayout` の 1 箇所である（親領域のサイズを引数で受けるため、クランプの基準をコアが保持しない）。
6. **描画は本コアの関心事ではない**: 本コアは描画 API も描画ポートも持たず、`LoupeDrawContent`（と `Layers`）というデータを返すだけである。描画の手順（層の順序に従う写し方）は UI 側の共有層（`LoupeControl.UIBase` の `LoupeRendererBase`）が単一所有する。
7. **描画順は本コアが単一所有する**: `LoupeDrawLayer`（`Background` → `Checkerboard` → `Image` → `Crosshair` → `Border` → `Label`）で層と順序を指示し、UI 層はその順に処理するだけにする。**層は「描く値がある場合だけ」含める**（層の有無 = 描く値の有無。`LoupeDrawContent.HasText` が層の有無と一致する）。**テキストの層を必ず最後に置く**ため、テキストは常に最上位に表示される。
8. **描画の可否は明示フラグが単一所有**: 描くかどうかは `LoupeOptions` の `ShowCheckerboard` / `ShowCrosshair` / `ShowBorder` / `ShowCoordLabel` が決める（透明色を「描かない」の代用にしない）。非表示の項目は空の描画内容として返す。
9. **文字は Core が文言と基準点を決め、UI 層が要素を配置する**: 中心座標ラベルの文言は `EsPoint.Format`、基準点はルーペ上端の中央（コアが解決済み）であり、実寸の測定と要素への配置は UI 層が担う（実寸は描画 API でしか測れないため）。体裁は描画層（`LoupeControl.UIBase` の `LoupePaint` / `LoupeLabelPlan`）が単一所有する。
10. **色は本コアが持たない**: 下地・市松模様・枠線・十字線・ラベルの色は描画にだけ必要な設定であるため、描画層（`LoupeControl.UIBase` の `LoupePaint`）が単一所有する。
11. **変化したときだけログ出力する判断もコア**: `Options` / `DipToDeviceScaling` / `CenterPoint` / `PositionOffset` の setter が変化を検出し、変化した項目だけを出力する（UI 層は変更通知で値を転送するだけで、同値判定も前後の値の保持も持たない）。
12. **操作の入口はファサードのみ**: UI 層とホストは `Loupe` だけを参照し、計算クラス（`internal`）には触れない。表示レイアウトの解決・描画入力の生成・描画内容の解決も本型が単一所有する。

## 座標型

UI フレームワークの幾何型（`Avalonia.Point` / `Size` 等）ではなく、`Platform.Core` のコア幾何型 `EsPoint` / `EsSize` を使用します。

UI 実装側は、境界（サービス呼び出し・イベント引数の生成箇所）で 1 行の相互変換（`.ToEsPoint()` / `.ToEsSize()` / `.ToAvaloniaPoint()`）を行います。UI 内部の状態・描画は UI 側の型のままで構いません。

## ビルド

```powershell
dotnet build src/Feature.LoupeCore/src/Feature.LoupeCore.csproj
```

ルーペ系の 4 プロジェクト（Core / UIBase / AvaloniaUI / MewUI）は `GenerateDocumentationFile` を有効にし、**存在しないメンバーへの `<see cref>` をビルド警告（CS1574）で検出**します（メンバー改名・削除でドキュメントが腐るのを防ぐ）。XML は検証にのみ使い、配布物（`bin` / `publish`）へは出しません（`CopyDocumentationFileToOutputDirectory=false`）。公開メンバーの未記載（CS1591）は対象外です。

## テスト

`tests/` が本プロジェクトの単体テストを持ちます。UI 初期化が不要なため、`[Fact]` / `[Theory]` で並列実行されます。

```text
tests/
├── Loupe.Tests.cs                   # 公開ファサード（表示レイアウトの解決・描画入力の生成・設定変更ログ・確保量の固定）（31 件）
├── Loupe.DrawContent.Tests.cs       # 描画内容の解決（層の順序・描画可否・層の一覧の共有）（12 件）
├── Loupe.Limits.Tests.cs            # 数値パラメータの既定値・安全域と補正（13 件）
├── Checkerboard/                    # 区画数の切り上げ除算・市松模様の明暗の判定（17 件）
├── Decoration/                      # 十字線と枠線の矩形・寸法の物理ピクセル換算（14 件）
├── Drawing/                         # 描画入力の組み立て・切り出し領域の導出・市松模様のタイル列挙（43 件）
├── Logging/                         # ログ定義の生成コード（IsEnabled 両パス・書式・差分検出）（10 件）
├── Placement/                       # 表示位置の計算（5 件）
└── Setting/                         # 設定の既定値・部分変更・安全域への補正・表示可否・等価性（14 件）
```

**テストはソースのフォルダを鏡映します**（`src/Drawing/` の型は `tests/Drawing/` で検証する）。同じファサード型の partial（`Loupe.cs` / `Loupe.DrawContent.cs` / `Loupe.Limits.cs`）のテストはルートに置きます。

件数は `[Theory]` を展開したケース数（合計 159 件）です。

```powershell
dotnet test src/Feature.LoupeCore/tests/Feature.LoupeCore.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
