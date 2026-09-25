# Feature.LoupeCore

`LoupeControl.AvaloniaUI`（Avalonia UI 実装）と `LoupeControl.MewUI`（MewUI 実装）が共有する、**UI フレームワーク非依存のルーペロジック**を提供するプロジェクトです。

## 目的

ルーペ（拡大鏡）の計算ロジックと寸法のうち、UI フレームワークに依存しない部分（寸法の値・数値の安全域補正・表示サイズ / 位置の計算・切り出し領域の計算・装飾矩形の計算・ラベル配置・ログ定義）を本プロジェクトへ切り出すことで、同じロジックと見た目を MewUI など別 UI からも再利用できます。

**操作の唯一の窓口は公開ファサード `Loupe`** です。UI 層とホストは本型だけを参照し、表示レイアウト・描画入力・通知内容の解決を本型へ委ねます（個々の計算クラスはファサードの内部実装として使われます）。

**描画は契約（`ILoupeRenderer`）で受け取り、差し替えられます**。本コアは描画 API を持たず描画層も参照できない（依存の向きは `Core` → 描画層の一方向）。既定実装は描画層（`LoupeControl.UIBase` の `LoupeRenderer`。色は `LoupePaint` をコンストラクタで受け取る）が提供し、コンストラクタで注入します。

## 収録する型

| 型 | 責務 |
| --- | --- |
| `Loupe` | **公開ファサード（操作の唯一の窓口）**。設定・環境値・拡大中心・位置オフセットの保持、表示レイアウトの解決（`ResolveLayout`）、描画入力の生成（`CreateRequest`）、描画完了通知の内容解決（`ResolveNotification`）、座標変換ポートの設定（`SetCoordinateTransform`）、描画実装の保持（`Renderer`。差し替え可能） |
| `ILoupeRenderer` | 描画内容（何を描くか）を描画面（どう描くか）へ写す実装の契約（**描画の差し替え点**）。既定実装は描画層の `LoupeRenderer`（色は `LoupePaint` をコンストラクタで受け取る）。本コアは描画 API を持たず、実装を注入で受ける |
| `LoupeLayout` | 表示レイアウトの値（表示サイズ（DIP）と表示位置（親領域 DIP）。配置しない場合は位置が null）。ファサードの結果として UI・ホストへ渡す |
| `LoupeRenderNotification` | 描画完了通知の値（切り出し左上・スケール・**描く層の順序**）。切り出し領域とスケール、および層の順序は `Loupe.ResolveNotification` が単一所有する |
| `LoupeOptions` | ルーペの設定（表示サイズ・拡大率・寸法）を 1 つの値として所有（**public**）。既定値・数値項目の安全域への補正（生成時。値の設定経路によらず範囲内の値だけが保持される）を持つ。UI 層は本型を 1 プロパティとして公開するだけ |
| `LoupeMetrics` | ルーペの**寸法**（市松模様のタイルサイズ・外枠の太さ・中心座標ラベルのフォントサイズと角丸半径）5 項目の単一所有（**public**）。既定値・数値項目の安全域への補正（生成時）・ラベル表示の判定（`ShouldShowCoordLabel`）を持つ。**色は持たない**（色は描画にだけ必要な設定であり、描画層 `LoupeControl.UIBase` の `LoupePaint` が単一所有する） |
| `LoupeValueCoercer` | ルーペの数値パラメータ（表示サイズ / 拡大率 / スケーリング / 市松模様 / 外枠 / ラベル文字サイズ / ラベルの角丸）の既定値・安全域（最小 / 最大）の単一所有と、範囲外の値のクランプ |
| `LoupeMetricsScaler` | 寸法値（市松模様のタイルサイズ・外枠の太さ）を描画に使う物理ピクセルへ換算（安全域への補正は `LoupeMetrics` の生成時に済んでいるため再補正しない）。描画層は換算結果を使うだけ。**中心座標ラベルは本型の対象外**（ラベルは DIP で配置するため換算を必要としない） |
| `LoupeRenderRequest` | 1 フレーム分の描画入力（**コア⇔UI の契約**）。UI 層は自 UI の公開プロパティから本値を組み立てて描画層・コアの計算へ渡すだけで、描画に必要な値の単位と組み立て順（DIP → 物理ピクセルの換算とクランプ）を持たない |
| `LoupeCoordLabel` | 中心座標ラベルの描画契約（文言・**DIP** の寸法。**色は持たない**）と配置の計算（`ComputeFrame`）。**表示はコントロールが行う**（`Border` + `TextBlock` の要素として配置する）ため、実寸測定だけを UI が行い本値へ渡す（測定は描画 API 依存）。体裁（背景・文字原点）は `EsLabelLayout`（Platform.Core）が単一所有し、要素への写像は `EsLabelExtensions.ToElement`（拡張メソッド。色は引数で受け取る）が単一所有する |
| `Loupe.Position.cs`（`Loupe` の **partial**・**internal**） | 表示位置（中心 + オフセット）の計算と親領域内へのクランプ（`ComputePosition`。親サイズが未確定の場合の判定も含む）。本体と同じ型であり、外部の入口はファサードの `ResolveLayout` だけとする（内部手順はファサードの実装詳細のため外部に公開しない） |
| `LoupeDecorationGeometry` | 十字線（縦横 2 本）と外枠（4 辺）の描画矩形の計算（描画層 `LoupeControl.UIBase` の `LoupeRenderer` が返された `EsRect` を描画ポートへ渡すだけ） |
| `LoupeDrawLayer` | 描く層とその順序（**描画順はコアが単一所有する**。中心座標ラベルは必ず最後 = 常に最上位）。描画の実行は UI 非依存の描画層（`LoupeControl.UIBase` の `LoupeRenderer`）が担い、`LoupeRenderNotification.Layers` を先頭から処理するだけである（順序を判断しない） |
| `LoupeCheckerboard` | 市松模様のタイルの明暗判定と、暗色タイルの**列挙子**（`ComputeDarkTiles`）。タイル数は描画先の大きさに比例して増えるため、リストを確保せずに列挙する（各 UI が同一のパターンを描画できるようにする） |

| `LoupeLogFormat` | 設定値の変更ログが出力する値の文字列化（`(X, Y)` / `Width×Height` / `null`） |
| `LoupeLogMessage` | ログ定義（設定変更・拡大中心の変更・描画例外・再描画の進行）と書式・変化判定（同値の再設定では出力しない）の単一所有 |
| `Imaging.LoupeImageExtractor` | 切り出し領域の一辺・左上（ドメイン座標）の計算と、`EsBitmap` からの領域切り出し、切り出し可否を含む取得（`TryExtractRegionAt`）、拡大描画の矩形（`ComputeMagnifiedRects`。引数はビットマップの実寸 `EsBitmap.Size`） |

**公開面の性格**: 本プロジェクトの型はすでに「UI アダプタ契約」（描画層・ホストが使う計算・契約・ログ）に限定されています。操作の入口はファサード（`Loupe`）に一本化し、表示サイズの決定（唯一の書き込み元を持つ規則）・表示位置・描画入力・通知内容の解決は本型が単一所有します（個々の計算クラスはファサードの内部で使われます）。

**大部分の型は public** です（外部が独自の UI アダプタを実装できるようにするため）。内部手順（配置計算・描画引数の計算）は **internal** とし、単体テストにのみ `InternalsVisibleTo`（`Feature.LoupeCore.Tests`）で公開します。

**UI 固有の処理に対応するログは各 UI プロジェクトが所有します**（中間ビットマップを生成する描画パイプライン固有のログは `LoupeControl.AvaloniaUI` にあります）。

## 依存方向

層の全体像（Core / UIBase / UI）は [`src/README.md`](../README.md) を参照してください。

```text
UI 層（LoupeControl.AvaloniaUI / LoupeControl.MewUI）
  入力の受付・効果の適用・通知の転送・描画先の用意・ラベル要素の配置
    ├──> LoupeControl.UIBase   どう描くか（描画 API の呼び出し手順。ILoupeRenderer を実装する）
    ├──> LoupeControl.UIBase   組み込み先がある場合にだけ必要な規則
    └──> Feature.LoupeCore ← 本プロジェクト   何を描くか + 判断（状態・値・純粋計算・コア⇔UI の契約）
           └──> Platform.Core（Es* 値型・ICoordTransform・IEsCanvas・EsLabelLayout）
※ Core は UIBase を参照しない（本層の描画実装がコアの契約を実装する向きだけ）。
```

本プロジェクトが UI フレームワークへ依存することはありません。外部パッケージへの直接依存もありません（`Platform.Core` 経由で SkiaSharp を参照します）。

**コントロール化にだけ必要な処理は `LoupeControl.UIBase` が所有します**（再描画要求のバッチ化・世代番号管理）。**描画の手順と色は `LoupeControl.UIBase` が所有します**（手順は下地 → 市松模様 → 拡大画像 → 十字線 → 外枠を層の順に描く。色は `LoupePaint`。描画先は `IEsCanvas`）。本プロジェクトはルーペの計算と寸法（どのアセンブリでも同じ結果になる部分）に限定し、描画 API も色も持ちません。

## 層の責務の境界

| 層 | 責務 |
| --- | --- |
| Core（本プロジェクト） | 設定・寸法の値とその既定値・補正、状態の判断、両 UI 共通の計算、コア⇔UI の契約（描画入力・描画完了通知）、公開ファサード（`Loupe` = 操作の唯一の窓口）、ログ定義 |
| UIBase（`LoupeControl.UIBase`） | 描画の手順（`LoupeRendererBase` が単一所有）と色（`LoupePaint`）、実寸 → ラベル要素の写像（`LoupeLabelPlan`）、組み込み先がある場合にだけ必要な規則（`LoupeRenderCoordinator`） |
| UI 層（`LoupeControl.*`） | 状態（拡大元画像・拡大中心）の保持と設定（`Options`・`Paint`）の保持、**ファサードへの委譲**（表示レイアウト・描画入力・通知内容の解決）、描画層（`LoupeRenderer`）への描画の委譲、描画ポートの適合（`AvaloniaCanvas` / `SkiaEsCanvas`）とラベル要素の配置、ホスト結線 |

UI 層に残してよい分岐は 4 種のみです（それ以外の判断はコアへ移します）。

1. 提示の可否（拡大元画像・拡大中心が揃っていない、大きさ 0、親が未接続、非表示）
2. 空のコレクションをスキップする性能ガード（描画結果を変えない）
3. フレームワーク固有値の解決（DPI・UI 色型→描画色型の変換）
4. 中間バッファの有無など UI 固有の描画方式の差（中間ビットマップを生成するかキャンバスへ直接描くか。描画結果は一致させる）

**寸法の数値の安全域への補正は値の型（`LoupeOptions` / `LoupeMetrics`）が生成時に適用します**（`init` アクセサ）。UI 側のプロパティへ補正の登録を要求すると、登録を忘れた UI だけバインディング経由の値が安全域外になります（実例あり: 両 UI で 1 回ずつ発生）。型の生成時に補正すれば、値の設定経路（オブジェクト初期化子・`with` 式・逆シリアル化）によらず範囲内の値だけが保持され、UI 側に補正の判断が残りません。

**「透明な色は描画しない」の判定は描画層（`LoupeControl.UIBase`）が単一所有します**（`LoupePaint.Has*`。判定の規則は `DrawingColorExtensions.IsVisible`（`Platform.Core`）が単一所有する）。UI 層は判定結果で描画 API を呼ぶ／呼ばないを決めるだけで、アルファを自前で見ません（見ると両 UI で判定がずれ、片方だけ修正されます）。

**寸法値の物理ピクセル換算は `LoupeMetricsScaler` を通します**。UI 層が `CheckerSize * scaling` のような式を自前で書くと、片方の UI だけ換算が抜けて DPI ≠ 100% で見た目がずれます（実例あり）。

**「変化したときだけログ出力する」判定はコアのログ定義が単一所有します**（`LogCenterPointChanged` / `LogSourceChanged(EsBitmap?, EsBitmap?)` など）。UI 層は設定の前後を渡すだけで、同値判定を持ちません。設定（`LoupeOptions`）の変更は `LogOptionsChanged` が**変化した項目だけ**を出力します（差分検出を UI 層に置くと、設定を 1 つの値にまとめた意味が失われます）。

**設定（表示サイズ・拡大率・寸法）は 1 つの値（`LoupeOptions`）にまとめます**。UI 層の公開プロパティを設定の数だけ増やすと、UI ごとに宣言・アクセサ・補正の登録・変更ハンドラが並び、同じ設定を複数回に分けて代入する呼び出し（設定の適用箇所）が生まれます。1 つの値にすれば UI は `Options` 1 プロパティを公開するだけで済み、**既定値と補正はコアの 1 箇所**が所有します（参照実装は `MarqueeControl` の `MarqueeOptions`）。

**設定と状態（データ）は区別します**。マウス移動ごとに変わる拡大元画像・拡大中心は個別プロパティのままです（設定に含めると、中心の更新のたびに寸法を含む値全体を組み立て直すことになります）。DPI スケーリング係数（環境値）と表示位置のオフセット（UI 固有のホスト結線）も設定に含めません。

**表示サイズ（クランプ済み）の決定はコアのファサードが単一所有します**。要求サイズを親領域の最小辺へクランプする規則は `Loupe.ResolveLayout` の 1 箇所です（親領域のサイズを引数で受けるため、クランプの基準をコアが保持しません）。UI 層は `ResolveLayout` の結果を要素サイズ・描画入力・配置へ写すだけです（複数箇所でクランプすると、要求サイズの変更直後にクランプ前の値で描画され、要素サイズと描画ビットマップが食い違います）。

**操作の入口は公開ファサード（`Loupe`）に一本化します**。UI 層とホストは本型だけを参照し、表示レイアウトの解決（`ResolveLayout`）・描画入力の生成（`CreateRequest`）・通知内容の解決（`ResolveNotification`）を本型へ委ねます（MewUI 版のホストも位置計算のために計算クラスを直接呼ばず、コントロールの `ResolveLayout` を経由します）。

**下地色（`LoupePaint.BackgroundColor`）は描画層（`LoupeControl.UIBase`）が単一所有します**。市松模様の色は半透明のため、背後の要素を透かしたくない場合はホストが不透明色を指定します（既定は透明 = 透かす）。**キャンバスへ直接描画する UI（MewUI）とウィンドウの上へ重ねる UI（Avalonia）のどちらも同じ色の値で指定でき、UI 層に固有の下地プロパティを持ちません**。塗る処理と描画順は UI 非依存の描画層（`LoupeControl.UIBase` の `LoupeRenderer`）が単一所有します。

## 座標型

UI フレームワークの幾何型（`Avalonia.Point` / `Size` 等）ではなく、`Platform.Core` のコア幾何型 `EsPoint` / `EsSize` を使用します。

UI 実装側は、境界（サービス呼び出し・イベント引数の生成箇所）で 1 行の相互変換（`.ToEsPoint()` / `.ToEsSize()` / `.ToAvaloniaPoint()`）を行います。UI 内部の状態・描画は UI 側の型のままで構いません。

## ビルド

```powershell
dotnet build src/Feature.LoupeCore/src/Feature.LoupeCore.csproj
```

ループ系の 3 プロジェクト（Core / AvaloniaUI / MewUI）は `GenerateDocumentationFile` を有効にし、**存在しないメンバーへの `<see cref>` をビルド警告（CS1574）で検出**します（メンバー改名・削除でドキュメントが腐るのを防ぐ）。XML は検証にのみ使い、配布物（`bin` / `publish`）へは出しません（`CopyDocumentationFileToOutputDirectory=false`）。公開メンバーの未記載（CS1591）は対象外です。

## テスト

`tests/` が本プロジェクトの単体テストを持ちます。UI 初期化が不要なため、`[Fact]` / `[Theory]` で並列実行されます（**合計 170 件**。`[Theory]` は展開後のケース数）。

```powershell
dotnet test src/Feature.LoupeCore/tests/Feature.LoupeCore.Tests.csproj
```
