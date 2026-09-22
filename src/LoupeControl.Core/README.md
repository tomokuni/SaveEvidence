# LoupeControl.Core

`LoupeControl.AvaloniaUI`（Avalonia UI 実装）と `LoupeControl.MewUI`（MewUI 実装）が共有する、**UI フレームワーク非依存のルーペロジック**を提供するプロジェクトです。

## 目的

ルーペ（拡大鏡）の計算ロジックと外観のうち、UI フレームワークに依存しない部分（外観の値・数値の安全域補正・表示サイズ / 位置の計算・切り出し領域の計算・装飾矩形の計算・ラベル配置・ログ定義）を本プロジェクトへ切り出すことで、同じロジックと見た目を MewUI など別 UI からも再利用できます。

## 収録する型

| 型 | 責務 |
| --- | --- |
| `LoupeOptions` | ルーペの設定（表示サイズ・拡大率・外観）を 1 つの値として所有（**public**）。既定値・数値項目の安全域への補正（生成時。値の設定経路によらず範囲内の値だけが保持される）を持つ。UI 層は本型を 1 プロパティとして公開するだけ |
| `LoupeAppearance` | ルーペの外観（下地・市松模様・外枠・十字線・中心座標ラベル）12 項目の単一所有（**public**）。既定値・数値項目の安全域への補正（生成時）・描画可否の判定（アルファ 0 は描画しない）・ラベル表示の判定（`ShouldShowCoordLabel`）を持つ |
| `LoupeValueCoercer` | ルーペの数値パラメータ（表示サイズ / 拡大率 / スケーリング / 市松模様 / 外枠 / ラベル文字サイズ / ラベルの角丸）の既定値・安全域（最小 / 最大）の単一所有と、範囲外の値のクランプ |
| `LoupeAppearanceScaler` | 外観の寸法値（市松模様のタイルサイズ・外枠の太さ・座標ラベルのフォントサイズ・角丸半径）を描画に使う物理ピクセルへ換算（安全域への補正は外観の生成時に済んでいるため再補正しない）。描画層は換算結果を使うだけ |
| `LoupeRenderRequest` | 1 フレーム分の描画入力（**コア⇔UI の契約**）。UI 層は自 UI の公開プロパティから本値を組み立てて描画層・コアの計算へ渡すだけで、描画に必要な値の単位と組み立て順（DIP → 物理ピクセルの換算とクランプ）を持たない |
| `LoupePositionCalculator` | 表示サイズの親領域へのクランプ（`ClampSizeToArea`）・表示位置（中心 + オフセット）の計算と親領域内へのクランプ（`ComputePosition`。親サイズが未確定の場合の判断も含む）・両者を合成した計算（`Compute`）・描画に使う物理ピクセルサイズの解決（`ResolvePixelSize`）。表示サイズを要素へ先に反映する必要がある UI は `ClampSizeToArea` と `ComputePosition` を個別に呼び、同じ値を使う |
| `LoupeDecorationGeometry` | 十字線（縦横 2 本）と外枠（4 辺）の描画矩形の計算（各 UI は返された `EsRect` を自身の描画 API へ渡すだけ） |
| `LoupeLabelLayout` | 中心座標ラベルの余白（上端からの距離・内側余白）と、背景矩形・文字原点の計算（両 UI で同じ位置に描画する） |
| `LoupeCheckerboard` | 市松模様のタイルの明暗判定と、暗色タイルの矩形列挙（`ComputeDarkTileRects`。各 UI が同一のパターンを描画できるようにする） |
| `LoupeRenderArgsCalculator` | 描画完了イベント引数用の表示情報（切り出し左上 / スケール）の計算。物理ピクセルサイズは実際に描画した値を受け取る |
| `LoupeLogFormat` | 設定値の変更ログが出力する値の文字列化（`(X, Y)` / `Width×Height` / `null`） |
| `LogMessage` | ログ定義（設定変更・拡大中心の変更・描画例外・再描画の進行）と書式・変化判定（同値の再設定では出力しない）の単一所有 |
| `Imaging.LoupeImageExtractor` | 切り出し領域の一辺・左上（ドメイン座標）の計算と、`EsBitmap` からの領域切り出し、切り出し可否を含む取得（`TryExtractRegionAt`）、拡大描画の矩形（`ComputeMagnifiedRects`。引数はビットマップの実寸 `EsBitmap.Size`） |

`LoupeOptions` / `LoupeAppearance` のみ **public** です（ホスト・他プロジェクトが設定を指定するため）。それ以外は `internal` であり、UI 実装（`LoupeControl.AvaloniaUI` / `LoupeControl.MewUI`）・複合コントロール・テストからのみ `InternalsVisibleTo` で参照できます。

## 依存方向

```text
LoupeControl.AvaloniaUI（Avalonia 実装） / LoupeControl.MewUI
  └──> LoupeControl.Core（本プロジェクト）
         └──> Platform.Core（コア幾何型・静的計算）
```

本プロジェクトが UI フレームワークへ依存することはありません。外部パッケージへの直接依存もありません（`Platform.Core` 経由で SkiaSharp を参照します）。

## 層の責務の境界

| 層 | 責務 |
| --- | --- |
| Core（本プロジェクト） | 設定・外観の値とその既定値・補正、状態の判断、両 UI 共通の計算、コア⇔UI の契約（描画入力・描画完了通知）、ログ定義 |
| UI 層（`LoupeControl.*`） | 状態（拡大元画像・拡大中心）の保持と設定（`Options`）の保持、フレームワーク固有の描画とホスト結線 |

UI 層に残してよい分岐は 4 種のみです（それ以外の判断はコアへ移します）。

1. 提示の可否（拡大元画像・拡大中心が揃っていない、大きさ 0、親が未接続、非表示）
2. 空のコレクションをスキップする性能ガード（描画結果を変えない）
3. フレームワーク固有値の解決（DPI・UI 色型→描画色型の変換）
4. 中間バッファの有無など UI 固有の描画方式の差（描画結果は一致させる。座標ラベルをビットマップへ焼き込むかレイアウト要素として描くかの差もここに含む。Avalonia 版はヘッドレスでビットマップの画素を検証できないため要素で描き、MewUI 版は Skia のキャンバスへ描画して画素を検証する）

**外観の数値の安全域への補正は値の型（`LoupeOptions` / `LoupeAppearance`）が生成時に適用します**（`init` アクセサ）。UI 側のプロパティへ補正の登録を要求すると、登録を忘れた UI だけバインディング経由の値が安全域外になります（実例あり: 両 UI で 1 回ずつ発生）。型の生成時に補正すれば、値の設定経路（オブジェクト初期化子・`with` 式・逆シリアル化）によらず範囲内の値だけが保持され、UI 側に補正の判断が残りません。

**「透明な色は描画しない」はコアが判定します**（`LoupeAppearance.Has*`）。UI 層は判定結果で描画 API を呼ぶ／呼ばないを決めるだけで、アルファを自前で見ません（見ると両 UI で判定がずれ、片方だけ修正されます）。

**外観の寸法値の物理ピクセル換算は `LoupeAppearanceScaler` を通します**。UI 層が `CheckerSize * scaling` のような式を自前で書くと、片方の UI だけ換算が抜けて DPI ≠ 100% で見た目がずれます（実例あり）。

**「変化したときだけログ出力する」判定はコアのログ定義が単一所有します**（`LogCenterPointChanged` / `LogSourceChanged(EsBitmap?, EsBitmap?)` など）。UI 層は設定の前後を渡すだけで、同値判定を持ちません。設定（`LoupeOptions`）の変更は `LogOptionsChanged` が**変化した項目だけ**を出力します（差分検出を UI 層に置くと、設定を 1 つの値にまとめた意味が失われます）。

**設定（表示サイズ・拡大率・外観）は 1 つの値（`LoupeOptions`）にまとめます**。UI 層の公開プロパティを設定の数だけ増やすと、UI ごとに宣言・アクセサ・補正の登録・変更ハンドラが並び、同じ設定を複数回に分けて代入する呼び出し（設定の適用箇所）が生まれます。1 つの値にすれば UI は `Options` 1 プロパティを公開するだけで済み、**既定値と補正はコアの 1 箇所**が所有します（参照実装は `DragRectControl` の `DragRectOptions`）。

**設定と状態（データ）は区別します**。マウス移動ごとに変わる拡大元画像・拡大中心は個別プロパティのままです（設定に含めると、中心の更新のたびに外観を含む値全体を組み立て直すことになります）。DPI スケーリング係数（環境値）と表示位置のオフセット（UI 固有のホスト結線）も設定に含めません。

**表示サイズ（クランプ済み）の決定は UI 側の 1 箇所だけが行います**。Avalonia 版は親パネルのサイズを必要とするため表示サイズを自コントロールで算出しますが、書き込み元を 1 箇所（`LoupeControl.UpdatePosition`）に限定します。複数箇所で書き込むと、要求サイズの変更直後にクランプ前の値で描画され、要素サイズと描画ビットマップが食い違います（実例あり）。

**下地色（`LoupeAppearance.BackgroundColor`）は外観が単一所有します**。市松模様の色は半透明のため、背後の要素を透かしたくない場合はホストが不透明色を指定します（既定は透明 = 透かす）。**キャンバスへ直接描画する UI（MewUI）とウィンドウの上へ重ねる UI（Avalonia）のどちらも同じ外観値で指定でき、UI 層に固有の下地プロパティを持ちません**。塗る処理と描画順は描画層が単一所有します。

## 座標型

UI フレームワークの幾何型（`Avalonia.Point` / `Size` 等）ではなく、`Platform.Core` のコア幾何型 `EsPoint` / `EsSize` を使用します。

UI 実装側は、境界（サービス呼び出し・イベント引数の生成箇所）で 1 行の相互変換（`.ToEsPoint()` / `.ToEsSize()` / `.ToAvaloniaPoint()`）を行います。UI 内部の状態・描画は UI 側の型のままで構いません。

## ビルド

```powershell
dotnet build src/LoupeControl.Core/src/LoupeControl.Core.csproj
```

ループ系の 3 プロジェクト（Core / AvaloniaUI / MewUI）は `GenerateDocumentationFile` を有効にし、**存在しないメンバーへの `<see cref>` をビルド警告（CS1574）で検出**します（メンバー改名・削除でドキュメントが腐るのを防ぐ）。XML は検証にのみ使い、配布物（`bin` / `publish`）へは出しません（`CopyDocumentationFileToOutputDirectory=false`）。公開メンバーの未記載（CS1591）は対象外です。

## テスト

`tests/` が本プロジェクトの単体テストを持ちます。UI 初期化が不要なため、すべて `[Fact]` で並列実行されます。

```powershell
dotnet test src/LoupeControl.Core/tests/LoupeControl.Core.Tests.csproj
```
