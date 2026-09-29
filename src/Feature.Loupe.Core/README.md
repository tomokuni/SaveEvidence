# Feature.Loupe.Core

`LoupeControl.AvaloniaUI`（Avalonia UI 実装）と `LoupeControl.MewUI`（MewUI 実装）が共有する、**UI フレームワーク非依存のルーペの計算**を提供するプロジェクトです。

## 目的

ルーペ（拡大鏡）の計算のうち UI フレームワークに依存しない部分（表示サイズ / 位置の計算・切り出し領域の計算・数値の安全域補正・ログ定義）を本プロジェクトへ切り出すことで、同じ結果と見た目を MewUI など別 UI からも再利用できます。

**操作の唯一の窓口は公開ファサード `Loupe`** です。UI 層とホストは本型だけを参照し、表示レイアウトと**描画の幾何**の解決を本型へ委ねます（個々の計算クラスはファサードの内部実装です）。

**描画に必要な幾何はすべてコアが解決します**。描画層（`Feature.Loupe.Presenter` の `LoupePresenter`）は `Loupe.ResolveGeometry` が返す**幾何**（`LoupeGeometry`）と外観（`LoupeAppearance`）から描画計画を組み立てるだけであり、切り出し領域の算出（切り上げ・中心 → 左上）や DIP → 物理ピクセルの換算を持ちません。

**描画は本コアの関心事ではありません**。本コアは描画 API も描画の抽象も持たない（依存先は `Platform.Core` だけである）。返すのは描画の幾何（`LoupeGeometry`）だけであり、装飾（十字線・枠線）の矩形・文言・表示可否・描画順は描画の都合として UI 側の共有層（`Feature.Loupe.Presenter` の `LoupePresenter`）が単一所有します。

## 収録する型

ルート（`src/`）に置くのは**ファサード本体とその partial**（`Loupe.cs` / `Loupe.Geometry.cs` / `Loupe.Limits.cs`）だけです。それ以外は用途別のフォルダへまとめます。
**1 ファイル 1 型**とします（複数の型を 1 ファイルへ含めない。`partial` による分割は可）。
**名前空間はフォルダに追従**させます（ルート直下のみ `EsUtil.Feature.Loupe.Core`。`Drawing` / `Setting` / `Logging` / `Placement` は `EsUtil.Feature.Loupe.Core.<フォルダ名>`）。公開面も複数のフォルダに分かれるため、利用者は用途に応じて `using` を追加します。

| フォルダ | 型 | 可視性 | 責務 |
| --- | --- | --- | --- |
| ルート | `Loupe` | public | **公開ファサード（操作の唯一の窓口）**。設定・環境値・拡大中心・位置オフセットの保持、表示レイアウトの解決（`ResolveLayout`）、**描画の幾何の解決（`ResolveGeometry`）**、座標変換ポートの設定（`SetCoordTransform`） |
| ルート | `Loupe.Limits`（`Loupe` の入れ子） | public | 数値パラメータの既定値・安全域（最小 / 最大）の単一所有と、範囲外の値の補正。**UI がプロパティの既定値と coerce を登録するときに参照する**ためファサードの入れ子として公開する（枠線の太さの安全域は外観（`LoupeAppearance`）が参照する） |
| ルート | `Loupe.Geometry.cs`（`Loupe` の **partial**） | public | 幾何の**入口**（`ResolveLayout` / `ResolveGeometry`）とその**内部手順**（表示位置のオフセット加算と親領域内へのクランプ）。公開の入口は本 partial が持ち、内部手順は `private static` に閉じる |
| （本コアには置かない） | `LoupePresenter` / `LoupeAppearance` | — | 提示（外観と描画計画の解決）は **UI 側の共有層（`Feature.Loupe.Presenter`）が所有します**（本コアは描画 API も描画の抽象も持たない） |
| `Drawing/` | `LoupeGeometry` | public | **1 フレーム分の描画の幾何**（コア⇔UI の契約）。UI 層は自分で組み立てず、ファサードの `Loupe.ResolveGeometry` が返した値を受け取る（DIP → 物理ピクセルの換算とクランプを UI 層が持たない） |
| `Drawing/` | `LoupeMagnifiedImage` | public | 拡大画像の切り出しと描画の矩形（切り出し領域・描画元・描画先）。`LoupeGeometry` の導出値 |
| `Setting/` | `LoupeOptions` | public | ルーペの設定（**計算に必要な値**である表示サイズ・拡大率）を 1 つの値として所有。既定値・生成時の補正を持つ（設定経路によらず範囲内の値だけが保持される）。**色・フォント・枠線の太さ・表示可否は持たない**（描画にだけ必要な設定のため、外観（`Feature.Loupe.Presenter` の `LoupeAppearance`）が単一所有する） |
| `Placement/` | `LoupeLayout` | public | 表示レイアウトの値（表示サイズと表示位置。配置しない場合は位置が null）。`ResolveLayout` の結果であり、UI 層の要素配置へ渡す |
| `Logging/` | `LoupeLogMessage` | internal | コアが出力するログ定義・変化判定（設定値・拡大中心の変更）。**UI が出力する定義（拡大元画像の変更・描画の例外）と書式は `Feature.Loupe.Presenter` が単一所有する** |

切り出し領域の幾何（一辺・左上・拡大画像の矩形）は `LoupeGeometry` の**メンバー**（`CaptureSize` / `CaptureTopLeft` / `MagnifiedImage` / `SurfaceBounds`）が単一所有します（`PixelSize` と `ZoomLevel` から導出するため、独立したクラスを持ちません）。**一辺の切り上げ（余りを 1 区画として数える）も `CaptureSize` が導出します**（導出元を位置パラメータにも持つと 2 つの値が食い違いうるため）。

## 公開面の階層

公開面は「誰が何のために使うか」で 3 段に分かれます。**外部が使わない内部契約は `internal`** とし、単体テストにのみ `InternalsVisibleTo` で公開します。

| 層 | 利用者 | 型 |
| --- | --- | --- |
| **1. ホスト設定** | アプリ・ホスト | `LoupeOptions` / `LoupeLayout` |
| **2. 公開ファサードと描画の幾何** | UI 実装（描画層） | `Loupe`（操作の唯一の窓口。既定値は `Limits.*Default`） / `LoupeGeometry` / `LoupeMagnifiedImage` |
| **3. 内部ヘルパー** | 本コアのみ（テストのみ IVT） | `LoupeLogMessage`（`Logging/`） |

- **操作はファサード経由に一本化**しています（計算クラスと内部手順は `internal` のため、UI 層はファサード以外の入口を持ちません）。
- **層 3 は外部参照 0 件**を実測して絞り込んだ結果です。「描画の幾何の解決（`Loupe.ResolveGeometry`）と描画に必要な値」だけが公開面です。
- **層 2 は UI アダプタを自作する場合の契約**です（独自 UI を追加するには、この面だけで完結します）。
- **層 1 は設定と受け渡しの値**です（ファサードを持たず、設定値と幾何だけを使う軽い用途でも使えます）。
- `[EditorBrowsable]` による「見えにくくする」対処はしません。公開面も契約も変わらず（見つけにくくなるだけ）、手段と目的が食い違うためです。**隠すか公開するかを二択**とし、隠す場合は `internal` で除きます。

**UI 非依存の汎用ライブラリ**として、特定のアプリ・UI 実装に依存しません（文言・モード等のアプリ固有情報を持ちません）。

## 依存方向

層の全体像（Core / Presenter / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（LoupeControl.AvaloniaUI / LoupeControl.MewUI）
  入力の受付・効果の適用・通知の転送・描画（自 UI の描画 API）・ラベル要素の配置
    ├──> Feature.Loupe.Presenter   提示（LoupePresenter）＋外観（LoupeAppearance）＋組み込み先がある場合にだけ必要な規則
    └──> Feature.Loupe.Core ← 本プロジェクト   計算（状態・値・純粋計算・描画の幾何）
           └──> Platform.Core（Es* 値型・ICoordTransform）
※ Core は提示層（Presenter）を参照しない（提示層がコアの幾何を使う向きだけ）。
```

本プロジェクトが UI フレームワークへ依存することはありません。**直接参照する外部パッケージは `Microsoft.Extensions.Logging.Abstractions`（`LoggerMessage` ソース生成）だけ**です（SkiaSharp は参照しません）。

**コントロール化にだけ必要な処理は `Feature.Loupe.Presenter` が所有します**（再描画要求のバッチ化・世代番号管理は `LoupeControl.AvaloniaUI`）。**描画計画（層の順序・装飾矩形・文言・配置）と外観も `Feature.Loupe.Presenter` が所有します**（層はペーストボード → 拡大画像 → 十字線 → 枠線 → テキストの順。色は `LoupeAppearance`）。**描画そのものは各 UI が行います**（描画 API の抽象は持ちません）。本プロジェクトはルーペの計算（どのアセンブリでも同じ結果になる部分）に限定し、描画 API も色も持ちません。

## 層の責務の境界

| 層 | 責務 |
| --- | --- |
| Core（本プロジェクト） | 設定とその既定値・補正、状態の判断、表示レイアウトの計算、描画の幾何（切り出し領域・描画面）の解決、公開ファサード（`Loupe` = 操作の唯一の窓口）、ログ定義 |
| 提示層（`Feature.Loupe.Presenter`） | 提示（`LoupePresenter`：描画計画（層の順序・装飾矩形・文言）の解決）、外観（`LoupeAppearance`：色・フォント・角丸・枠線の太さ・表示可否）、実寸 → ラベル要素の写像（`LoupePresenter`（`LabelPlan` partial））、装飾矩形の計算（`LoupePresenter`（`DrawPlan` partial）） |
| UI 層（`LoupeControl.*`） | 状態（拡大元画像・拡大中心）の保持と設定（`Options`・`Appearance`）の保持、**提示層への委譲**（表示レイアウト・描画計画）、描画計画の描画（`LoupeContextPainter` / `LoupeSkiaPainter`）とラベル要素の配置、ホスト結線、拡大元画像のログ |

UI 層に残してよい分岐は 4 種のみです（それ以外の判断はコア・提示層へ移します）。

1. 提示の可否（拡大元画像・拡大中心が揃っていない、大きさ 0、親が未接続、非表示）
2. 空のコレクションをスキップする性能ガード（描画結果を変えない）
3. フレームワーク固有値の解決（DPI・UI 色型→描画色型の変換）
4. UI 固有の描画方式の差（中間ビットマップを生成するかキャンバスへ直接描くか。描画結果は一致させる）

**数値の安全域への補正は値の型が生成時に適用します**（`init` アクセサ）。UI 側のプロパティへ補正の登録を要求すると、登録を忘れた UI だけバインディング経由の値が安全域外になります（実例あり: 両 UI で 1 回ずつ発生）。型の生成時に補正すれば、値の設定経路（オブジェクト初期化子・`with` 式・逆シリアル化）によらず範囲内の値だけが保持され、UI 側に補正の判断が残りません（枠線の太さの補正は外観（`LoupeAppearance`）が `Loupe.Limits.CoerceBorderThickness` へ委譲します）。

**描画の可否は外観（`LoupeAppearance`）の明示フラグが単一所有します**（`ShowCrosshair` / `ShowBorder` / `ShowCoordLabel`）。これらはコアの計算・判断に入らない（描画にだけ必要な）設定であるため、コアの設定（`LoupeOptions`）には含めず、表示可否から描画計画を組み立てる `LoupePresenter` が扱います。透明色を「描かない」の代用にしません。

**寸法値の物理ピクセル換算は提示層が描画計画の解決時に行います**（`Platform.Core` の `DpiScaler.ScaleToPixel`。呼び出すのは `LoupePresenter.ResolveDrawPlan`）。UI 層が `BorderThickness * scaling` のような式を自前で書くと、片方の UI だけ換算が抜けて DPI ≠ 100% で見た目がずれます（実例あり）。

**「変化したときだけログ出力する」判定はコアが単一所有します**（`Options` / `DipToDeviceScaling` / `CenterPoint` / `PositionOffset` の setter が変化を検出し、`LogOptionsChanged` などの定義で出力します）。UI 層は変更通知で値を転送するだけで、同値判定も前後の値の保持も持ちません（設定（`LoupeOptions`）の変更は `LogOptionsChanged` が**変化した項目だけ**を出力します）。ログの出力先は `Log` プロパティで受け取ります（UI の `Log` 設定がコアへ伝搬します）。

**設定（計算に必要な値）は 1 つの値（`LoupeOptions`）にまとめます**。UI 層の公開プロパティを設定の数だけ増やすと、UI ごとに宣言・アクセサ・補正の登録・変更ハンドラが並び、同じ設定を複数回に分けて代入する呼び出し（設定の適用箇所）が生まれます。1 つの値にすれば UI は `Options` 1 プロパティを公開するだけで済み、**既定値と補正はコアの 1 箇所**が所有します（参照実装は `MarqueeControl` の `MarqueeOptions`）。

**設定と状態（データ）は区別します**。マウス移動ごとに変わる拡大元画像・拡大中心は個別プロパティのままです（設定に含めると、中心の更新のたびに値全体を組み立て直すことになります）。DPI スケーリング係数（環境値）と表示位置のオフセット（UI 固有のホスト結線）も設定に含めません。

**表示サイズ（クランプ済み）の決定はコアのファサードが単一所有します**。要求サイズを親領域の最小辺へクランプする規則は `Loupe.ResolveLayout` の 1 箇所です（親領域のサイズを引数で受けるため、クランプの基準をコアが保持しません）。UI 層は `ResolveLayout` の結果を要素サイズ・描画計画・配置へ写すだけです（複数箇所でクランプすると、要求サイズの変更直後にクランプ前の値で描画され、要素サイズと描画ビットマップが食い違います）。

**操作の入口は公開ファサード（`Loupe`）に一本化します**。UI 層とホストは本型だけを参照し、表示レイアウトの解決（`ResolveLayout`）・描画の幾何の解決（`ResolveGeometry`）を本型へ委ねます（MewUI 版のホストも位置計算のために計算クラスを直接呼ばず、コントロールの `ResolveLayout` を経由します）。

**画像外の色（`LoupeAppearance.PasteboardColor`）は外観が単一所有します**。描画層（UI）は本色で描画面全体を塗ってから拡大画像を重ねるため、画像の範囲外（画像端の透明パディングと切り上げの余りを含む）が本色の無地になります（既定は不透明の中性グレー。透明を指定すると背後の要素がそのまま見えます）。**キャンバスへ直接描画する UI（MewUI）とウィンドウの上へ重ねる UI（Avalonia）のどちらも同じ色の値で指定でき、UI 層に固有の画像外プロパティを持ちません**。

## 機能差に由来する非対称（もう一方のコアとの違い）

本コアと `Feature.Marquee.Core` は同じ規約（`UI → Presenter → Core → Platform.Core` の一方向依存、層の順序は提示層が所有、色は外観が所有）で作られています。揃えない（揃えると害になる）違いは機能差に由来するもので、以下に列挙します。

| 非対称 | Loupe | Marquee | 理由 |
| --- | --- | --- | --- |
| 描画パイプラインの段数 | 3 段（`ResolveLayout` → `ResolveGeometry` → 描画計画の解決） | 2 段（`ResolveGeometry` → 描画計画の解決） | Marquee は親セル全面へ重ねるため配置の余地がない（`LoupeLayout` 相当を持たない）。配置先はホストの親セル全面であり、位置を決める入力がコアに無い |
| 描画の幾何の生成の形 | `ResolveGeometry(double displaySize, EsSize? availableSize = null)`（一辺の表示サイズ＝親パネルのクランプ結果と、描画面＝ビットマップの領域が別） | `ResolveGeometry(in MarqueeRenderRequest)`（描画領域＝座標空間。表示サイズと同じ） | 本コアは正方形のルーペを自前の描画面へ描く（任意矩形の描画領域へ直接描く Marquee とは座標系の対応が違う） |
| 入力の通知と効果ポート | 持たない（状態はホストが設定するだけ。設定は表示のみ。変換の設定は再描画を要求せず、表示位置の再計算は UI が行う。ロガーはファサード自身が保持） | 対話モジュール（`Interaction/` + `Contract/` + `Press` / `Move` / `Release` / `KeyDown` + `Notified`）／設定に対話パラメータ（`MinSelectionSize` / `HandleHitRadius` / `ClampToDomainBounds`）／ラベル文言プロバイダ（`SizeLabelOverrideProvider` 等）／座標変換の設定で**コアが再描画を要求**（`RequestRedraw`）／ロガーはセッションが保持 | ルーペは入力の解釈も効果ポートも持たない（ホストが状態を設定するだけであり、再描画の要求先が無い） |
| 寸法の型 | `int`（物理ピクセルへ換算。矩形の塗りで描く） | `double`（DIP のまま。線で描く） | 描画プリミティブが違う（塗り / 線） |
| 設定型の数とログ定義 | 1 型（`LoupeOptions`）。ログは `LogOptionsChanged` のみ | 1 型（`MarqueeOptions`）。ログは `LogOptionsChanged` のみ | 装飾の設定はコアから提示層（外観）へ移したため、両コアとも 1 型 |
| 描画データの粒度 | 層ごとに素の矩形（`BorderRects` / `CrosshairRects`） | 層ごとに専用型（`MarqueeHandles` / `MarqueeMarker`） | 描画プリミティブが違う（塗る矩形の列 / 線の座標） |
| 描画の幾何の座標変換ポート | 幾何に持たない（ポートは `ResolveLayout` の表示位置解決だけに使う） | 幾何が保持する（`Port` ＋ 未指定時の `DomainToDipScaling`。`ResolvedPort` / `ToDipRect` が自分で解決する） | 本コアは自身の物理ピクセル面で描画が完結する（Marquee は選択矩形をドメイン → DIP へ変換して描き、その DIP はホストの座標系で決まる） |
| 下地の層 | `LoupeDrawLayer.Pasteboard`（画像外の色）で描画面全体を塗る | 持たない（`MarqueeDrawLayer` の先頭は `Decoration`） | ルーペは描画先のビットマップを自分で置き換える（Marquee は自身の表面へ直接描き、消去はフレームワークが行う） |
| 描画完了の通知の内容 | `LoupeRenderedEventArgs`（`CaptureTopLeft` / `ZoomLevel`） | `MarqueeRenderedEventArgs`（`MarqueeDrawPlan`: 層・文言の有無） | ホストが重ね描きするために必要な情報が違う（切り出し領域と倍率 / 選択矩形の所在） |
| 表示可否の述語の引数 | `LoupeAppearance.ShouldShowCoordLabel(bool)` | `MarqueeAppearance.ShouldShowSizeLabel(bool)` / `ShouldShowCoordLabel()` | ルーペの座標ラベルは拡大中心が未設定になりうる（Marquee の座標ラベルは入力に常にある） |
| `Limits` の追加役割 | 安全域と補正のみ | 未設定（0 以下）の解決規則も持つ（`ResolveDisplayScale`） | ラベルの表示スケールは Marquee 固有の概念 |
| 区画数の計算 | `LoupeGeometry.CaptureSize`（導出プロパティ）が持つ | 持たない | 切り出し領域の一辺が「余りを 1 区画として数える」規則を使う |
| 選択の幾何 | 持たない | `Selection/`（`MarqueeSelection`）を持つ | ルーペは選択操作を持たない |
| テスト補助（スパイ） | `RecordingLogger` のみ | `EffectsSpy` / `CursorServiceSpy` / `RecordingLogger` | ルーペは効果ポートもカーソルサービスも持たないため差し替える対象が無い（ログの検証だけが共通） |
| ドキュメントの追加の節 | 追加の節なし | 3 節（README「名前の由来と一般的な呼称」「選択矩形の外側に描く装飾」、SPEC_System「3.3 状態遷移」） | 改名の経緯・装飾・状態遷移はルーペに対応する概念が無い（共通の節は両コアで同一順） |

## 主な API

```csharp
// 色・フォント・枠線の太さ・表示可否は外観（Feature.Loupe.Presenter の LoupeAppearance）が単一所有する
// （コアの LoupeOptions は計算に必要な値（表示サイズ・拡大率）だけを持つ）
var appearance = LoupeAppearance.Default with { PasteboardColor = Color.White };  // 色は System.Drawing.Color（UI 非依存）
var loupe = new Loupe(options);                                // options は null 可（null は LoupeOptions.Default）
loupe.Log = logger;                                            // null は NullLogger へ吸収される
loupe.SetCoordTransform(new CoordTransform(p => p, p => p, () => null)); // ドメイン ⇔ 親領域 DIP

// 状態（ホストが更新する。設定はコア、拡大元画像と拡大中心は UI 層が保持する）
loupe.DipToDeviceScaling = 1.25;                               // 環境値（DPI 追従。範囲外は補正される）
loupe.CenterPoint = cursorDomain;                              // 拡大中心（元画像ピクセル座標。null は拡大画像なし = 画像外の色のみ）
loupe.PositionOffset = 16;                                     // カーソルから離して配置するためのホスト指定値

// 描画（1 フレーム分。レイアウト → 幾何の順に解決する）
var layout = loupe.ResolveLayout(parentSize);                  // 表示サイズ（DIP）+ 表示位置（配置しない場合は null）
var geometry = loupe.ResolveGeometry(layout.DisplaySize, canvasSize); // 幾何（DIP → 物理ピクセルの換算とクランプ済み）
// 装飾矩形・文言・層の順序は提示層（Feature.Loupe.Presenter の LoupePresenter.ResolveDrawPlan）が解決する
// 描画は UI 層が行う（LoupeContextPainter / LoupeSkiaPainter が描画計画を自 UI の描画 API へ写す）
// 描画完了通知（ホストが描画済み画像へオーバーレイを描くため）は geometry.CaptureTopLeft / geometry.ZoomLevel から読む
```

| メンバー | 用途 |
| --- | --- |
| `Loupe(LoupeOptions?)` | 生成（設定を省略した場合は `LoupeOptions.Default`。描画実装は受け取らない） |
| `ResolveLayout` | 親領域サイズから表示レイアウト（表示サイズ + 表示位置）を解決する。**状態を変更しない**（親領域のサイズを保持しない） |
| `ResolveGeometry` | 描画 1 フレーム分の幾何（`LoupeGeometry`）の解決（DIP → 物理ピクセルの換算と描画先へのクランプを含む） |
| `SetCoordTransform` | 座標変換ポートの設定（null は等価変換。変換関数は呼び出し毎に評価されるためレイアウト変化に追従する） |
| `Options` / `DipToDeviceScaling` / `CenterPoint` / `PositionOffset` / `Log` | 設定・環境値・拡大中心・オフセットの読み書き（変更は**変化したときだけ**ログ出力。`Log` は null を `NullLogger` へ吸収する） |
| `Limits.*` | 数値パラメータの既定値・安全域と補正。UI が依存関係プロパティの既定値と coerce に使う |
| `LoupeOptions.Default` | 設定の既定値（部分変更の基点。`LoupeOptions.Default with { ZoomLevel = 8 }`） |

UI 側（`LoupeControl`）が公開する入口:

| メンバー | 用途 |
| --- | --- |
| `Source` / `CenterPoint` | 拡大元画像と拡大中心（マウス移動ごとに更新する状態。UI 層が保持する） |
| `Options` / `Appearance` | 設定（`LoupeOptions`）と外観（`LoupeAppearance`）。同値の再設定では再描画されない |
| `DipToDeviceScaling` | 環境値（DPI 追従）。モニター間の移動でも UI が自動更新する |
| `Rendered` | 描画完了の通知（ホストが描画済み画像へ選択矩形の破線等を追加描画するための入口） |
| `SetCoordTransform` / `RefreshPosition` | 座標変換ポートの設定と、表示位置の再計算要求（パン・ズームなどレイアウト非変更の変化に追従する） |

## 設計上の約束

1. **判断はコア・反映は UI**: 「どうするか」（表示サイズ・位置・切り出し領域）はコアが決め、UIBase が描画計画（層の有無・矩形・文言・配置）を解決し、UI は描画計画を写すだけにする。
2. **単一所有**: 表示位置は `Loupe.ResolveLayout`（内部手順は `Loupe.Geometry.cs` の private static）、切り出し領域（一辺の切り上げを含む）は `LoupeGeometry` のみが持つ（装飾矩形の計算は提示層の `LoupePresenter`（`DrawPlan` partial）、寸法の換算は `Platform.Core` の `DpiScaler`）。
3. **座標系の 3 区分**: ドメイン座標（元画像 px）→ 親領域 DIP（`ICoordTransform`）→ 物理ピクセル（`DipToDeviceScaling`）のうち、① ドメイン → DIP は `Loupe`、② DIP → 物理ピクセルは `LoupeGeometry` が担う（UIBase は換算しない）。
4. **設定は 1 値・補正は生成時**: 計算に必要な設定は `LoupeOptions` の 1 値にまとめ、数値項目の安全域への補正は型の生成時（`init` アクセサ）に適用する（UI 側に補正の登録を要求しない）。
5. **表示サイズの決定はファサードが単一所有**: 要求サイズを親領域の最小辺へクランプする規則は `Loupe.ResolveLayout` の 1 箇所である（親領域のサイズを引数で受けるため、クランプの基準をコアが保持しない）。
6. **描画順は UIBase が単一所有する**: `LoupeDrawLayer`（`Pasteboard` → `Image` → `Crosshair` → `Border` → `Label`）で層と順序を指示し、UI 層はその順に処理するだけにする。**層は「描く値がある場合だけ」含める**（層の有無 = 描く値の有無。`LoupeDrawPlan.HasText` が層の有無と一致する）。**テキストの層を必ず最後に置く**ため、テキストは常に最上位に表示される。
7. **描画は本コアの関心事ではない**: 本コアは描画 API も描画の抽象も持たず、描画の幾何（`LoupeGeometry`）というデータを返すだけである。装飾・文言・層の順序は UIBase の `LoupePresenter` が解決し、描画は各 UI が行う（描画 API の抽象を持つと UI フレームワークごとの描画モデルの差を吸収しきれない）。
8. **文字は UIBase が文言と基準点を決め、UI 層が要素を配置する**: 中心座標ラベルの文言は `EsPoint.Format`、基準点はルーペの左上の内側（`LoupePresenter`（`LabelTexts` partial） が解決済み）であり、実寸の測定と要素への配置は UI 層が担う（実寸は描画 API でしか測れないため）。体裁は外観（`LoupeAppearance`）と `LoupePresenter`（`LabelPlan` partial） が単一所有する。
9. **色は本コアが持たない**: ペーストボード（画像外）・枠線・十字線・ラベルの色は描画にだけ必要な設定であるため、外観（`Feature.Loupe.Presenter` の `LoupeAppearance`）が単一所有する。
10. **描画の可否は外観の明示フラグが単一所有**: 描くかどうかは `LoupeAppearance` の `ShowCrosshair` / `ShowBorder` / `ShowCoordLabel` が決める（透明色を「描かない」の代用にしない）。非表示の項目は空の矩形として解決される。
11. **変化したときだけログ出力する判断もコア**: `Options` / `DipToDeviceScaling` / `CenterPoint` / `PositionOffset` の setter が変化を検出し、変化した項目だけを出力する（UI 層は変更通知で値を転送するだけで、同値判定も前後の値の保持も持たない）。
12. **操作の入口はファサードのみ**: UI 層とホストは `Loupe` だけを参照し、計算クラス（`internal`）には触れない。表示レイアウトの解決・描画の幾何の解決も本型が単一所有する。

> **Note**: 本 README は**利用仕様書**（使い方と設計の意図）です。公開 API の仕様は [SPEC_ExtFunc.md](SPEC_ExtFunc.md)、構造・依存・例外処理・単一所有の一覧は [SPEC_System.md](SPEC_System.md) を参照してください。

## 座標型

UI フレームワークの幾何型（`Avalonia.Point` / `Size` 等）ではなく、`Platform.Core` のコア幾何型 `EsPoint` / `EsSize` を使用します。

UI 実装側は、境界（サービス呼び出し・イベント引数の生成箇所）で 1 行の相互変換（`.ToEsPoint()` / `.ToEsSize()` / `.ToAvaloniaPoint()`）を行います。UI 内部の状態・描画は UI 側の型のままで構いません。

## ビルド

```powershell
dotnet build src/Feature.Loupe.Core/src/Feature.Loupe.Core.csproj
```

ルーペ系の 4 プロジェクト（Core / Presenter / AvaloniaUI / MewUI）は `GenerateDocumentationFile` を有効にし、**存在しないメンバーへの `<see cref>` をビルド警告（CS1574）で検出**します（メンバー改名・削除でドキュメントが腐るのを防ぐ）。XML は検証にのみ使い、配布物（`bin` / `publish`）へは出しません（`CopyDocumentationFileToOutputDirectory=false`）。公開メンバーの未記載（CS1591）は対象外です。

## テスト

`tests/` が本プロジェクトの単体テストを持ちます。UI 初期化が不要なため、`[Fact]` / `[Theory]` で並列実行されます。

```text
tests/
├── Loupe.Tests.cs                   # 公開ファサード（表示レイアウト・設定変更ログ・確保量の固定）（31 件）
├── Loupe.Limits.Tests.cs            # 数値パラメータの既定値・安全域と補正（11 件）
├── Drawing/                         # 描画の幾何の解決・切り出し領域の導出・区画数の切り上げ（39 件）
├── Logging/                         # ログ定義の生成コード（IsEnabled 両パス・書式・差分検出）（10 件）
├── Placement/                       # 表示位置の計算（5 件）
└── Setting/                         # 設定の既定値・部分変更・安全域への補正・等価性（10 件）
```

**テストはソースのフォルダを鏡映します**（`src/Drawing/` の型は `tests/Drawing/` で検証する）。ファサード型の partial（`Loupe.cs` / `Loupe.Limits.cs`）のテストはルートに置きます。

件数は `[Theory]` を展開したケース数（合計 106 件）です。

```powershell
dotnet test src/Feature.Loupe.Core/tests/Feature.Loupe.Core.Tests.csproj
```

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
