# LoupeControl.MewUI 外部機能仕様書

`LoupeControl`（MewUI 版ルーペ）が外部利用者へ提供する機能と公開 API の仕様です。
内部実装の詳細（描画手順・キャッシュ等）は記載しません。

## 概要

ルーペ（拡大鏡）は、**拡大元の 100% 画像**と**拡大中心（元画像ピクセル座標）**を受け取り、
市松模様の背景・拡大画像・十字線・外枠を描画するコントロールです（中心座標ラベルはテキスト要素として表示します）。

**Avalonia 版 `LoupeControl` と同じ UI 非依存コア（`LoupeControl.Core`）を使うため、設定値と見た目は両 UI で一致します。**

## 公開 API

名前空間 `Estable.MewUI.Loupe` / クラス `LoupeControl`（`public sealed`、`Panel` 派生。図形は子の `SkiaCanvasView` が描画する）。

### プロパティ

| プロパティ | 型 | 既定値 | 安全域（範囲外は自動補正） | 説明 |
| --- | --- | --- | --- | --- |
| `Source` | `EsBitmap?` | `null` | — | 拡大元の 100% 画像。null の場合は市松模様のみ |
| `CenterPoint` | `EsPoint?` | `null` | — | 拡大中心（元画像ピクセル座標）。null の場合は市松模様のみ |
| `Options` | `LoupeOptions` | `LoupeOptions.Default` | 下表のとおり | 設定（表示サイズ・拡大率・外観）。`with` 式で部分変更する |
| `RenderScaling` | `double` | 1.0 | 0.1〜8.0（非有限は既定値） | DIP → 物理ピクセルの係数（環境値。設定には含めない） |

`Options`（コアの `LoupeOptions`。UI 非依存の値）の項目:

| 項目 | 型 | 既定値 | 安全域（範囲外は自動補正） | 説明 |
| --- | --- | --- | --- | --- |
| `RequestedSize` | `int` | 160 | 1〜4096 | 一辺のサイズ（DIP） |
| `ZoomLevel` | `int` | 4 | 1〜64 | 拡大率（偶数推奨） |
| `Appearance` | `LoupeAppearance` | コアの既定値 | 下表のとおり | 外観（下地・市松模様・外枠・十字線・中心座標ラベル）。数値項目を安全域へ補正する |

`Options.Appearance`（コアの `LoupeAppearance`。色は UI 非依存の `System.Drawing.Color`）の項目:

| 項目 | 型 | 既定値 | 安全域（範囲外は自動補正） | 説明 |
| --- | --- | --- | --- | --- |
| `BackgroundColor` | `DrawingColor` | 透明 | — | 下地（ベース）色。市松模様・拡大画像より先に表面全体へ塗る。背後の要素を透かさない場合は不透明色を指定する（既定は透明 = 背後の要素を透かす） |
| `CrossColor` | `DrawingColor` | 赤 40%（`#66FF0000`） | — | 十字線の色（不透明度はアルファ）。アルファ 0 は描画しない |
| `CheckerSize` | `int` | 6 | 1〜128 | 市松模様の 1 タイルサイズ（DIP） |
| `CheckerColorA` | `DrawingColor` | Gray 30% | — | 市松模様の明色。アルファ 0 は描画しない |
| `CheckerColorB` | `DrawingColor` | DarkGray 30% | — | 市松模様の暗色。アルファ 0 は描画しない |
| `FrameColor` | `DrawingColor` | LightGray | — | 外枠の色。アルファ 0 は描画しない |
| `FrameWidth` | `int` | 2 | 1〜64 | 外枠の太さ（DIP） |
| `ShowCoordLabel` | `bool` | `true` | — | 上端中央の座標ラベルの表示可否（中心未設定時は描画しない） |
| `CoordLabelColor` | `DrawingColor` | 白 | — | 座標ラベルの文字色 |
| `CoordLabelBackgroundColor` | `DrawingColor` | 黒 60%（`#99000000`） | — | 座標ラベルの背景色 |
| `CoordLabelFontSize` | `double` | 12.0 | 6〜48（非有限は既定値） | 座標ラベルのフォントサイズ（DIP） |
| `CoordLabelCornerRadius` | `int` | 2 | 0〜64 | 座標ラベルの背景の角丸半径（DIP。0 は角丸なし） |

`Color` は `Aprillz.MewUI.Color`、`DrawingColor` は `System.Drawing.Color`（外観）です。

## 描画仕様

**描く層とその順序はコアが単一所有します**（`LoupeDrawLayer`）。コアが返す層の順序は次のとおりです（後から描いたものが上に重なります）。

| 順序 | 要素 | 備考 |
| --- | --- | --- |
| 1 | 下地 | `Options.Appearance.BackgroundColor` で表面全体を塗る（透明の場合は前フレームの画素を残さないためキャンバスをクリアする。市松模様の色は半透明のため、背後の要素を透かさない場合はホストが不透明色を指定する） |
| 2 | 市松模様 | 明色で全面を塗った後、暗色タイルを市松位置へ描画する |
| 3 | 拡大画像 | `Source` と `CenterPoint` がある場合のみ。切り出し領域の外は透明（下地の市松模様が見える） |
| 4 | 十字線 | 中央の縦横 2 本（幅は拡大率と同じ物理ピクセル） |
| 5 | 外枠 | 四辺（`Options.Appearance.FrameWidth` の太さ） |

中心座標ラベルはコントロールが **`Border` + `TextBlock` の子要素として配置**します（上記の後ろに重なります。文言・表示可否・配置はコア、実寸測定は `SkiaText.Measure`、描画順はコアの `LoupeDrawLayer` が単一所有します）。子要素は親より後に描かれるため、ラベルは常に最上位になります。

### サイズの決定

| 項目 | 決定方法 |
| --- | --- |
| 表示サイズ（物理ピクセル） | `Options.RequestedSize`（DIP） × `RenderScaling`。キャンバスより大きい場合はキャンバスに合わせる |
| 拡大率 | `Options.ZoomLevel` 倍（最近傍で拡大する） |
| 切り出し領域の一辺 | `ceil(表示サイズ / Options.ZoomLevel)` ドメインピクセル |
| 切り出し領域の左上 | 拡大中心 − 切り出し一辺 / 2（整数化）。画像の外へはみ出した部分（左・上・右・下）は透明パディングとなる |

### 座標ラベルの表示条件

`ShowCoordLabel` が `true` かつ `CenterPoint` が設定済みの場合のみ表示します（判断はコアの `LoupeAppearance.ShouldShowCoordLabel` が単一所有する）。

## レイアウト

| 項目 | 仕様 |
| --- | --- |
| 要求サイズ | 一辺 `Options.RequestedSize`（DIP）の正方形（`Options` の変更はレイアウトを再評価する） |
| 表示位置 | **本コントロールは位置を決定しない**。ホストが親のレイアウト（`HorizontalAlignment` / `VerticalAlignment` / `Margin` / `Canvas` 等）で配置する |

## DPI

モニター間の移動で DPI が変わると、`RenderScaling` は DPI から換算した値（DIP 基準 96 DPI ≠ 1.0）へ自動更新されます（換算規則は `DpiScaler.ToScaling` が単一所有します）。
ホストがスケーリング係数を設定する必要はありません（明示設定した値も DPI 変更時は上書きされます）。

## エラー時の挙動

| 状況 | 挙動 |
| --- | --- |
| `Source` が `null` | 市松模様のみを描画する（例外にしない） |
| `CenterPoint` が `null` | 市松模様のみを描画する（例外にしない） |
| `Source` が破棄済み | 市松模様のみを描画する（例外にしない） |
| 色のアルファが 0 | その層を描画しない（例外にしない。判定はコアの `LoupeAppearance.Has*` が単一所有する） |
| 安全域外の数値 | 安全域へ補正する（例外にしない） |

## 描画完了イベント

`Rendered` イベントは、ルーペを描画した直後（同一フレーム内）に引数 `LoupeRenderedEventArgs` で通知します。ホストはハンドラ内で描画先キャンバスへ追加描画（選択矩形の破線等のオーバーレイ）を行えます。

中心座標ラベルは**コントロールが子要素として表示します**（`Border` + `TextBlock`）。文言・寸法・表示可否はコア（`LoupeCoordLabel`）が、実寸からの配置は `LoupeLabelLayoutPlan`（`LoupeControl.CoreForControl`）が単一所有するため、コントロールは自 UI の API（`SkiaText.Measure`）で測った実寸を渡し、返された配置を要素へ反映するだけです。

| メンバー | 型 | 説明 |
| --- | --- | --- |
| `Canvas` | `SKCanvas` | ルーペを描画したキャンバス（座標系はルーペ画像のピクセル） |
| `PixelSize` | `int` | 実際に描画したルーペの一辺のサイズ（物理ピクセル。`Options.RequestedSize` × `RenderScaling` をキャンバスでクランプした値） |
| `CaptureTopLeft` | `EsPoint?` | 切り出し領域の左上（ドメイン座標）。中心未設定時は null（オーバーレイを描画できない） |
| `Scale` | `double` | ドメイン座標 → ルーペ画像ピクセル座標の変換係数（拡大率と同値。DPI スケーリングの影響を受けない） |

描画入力（`LoupeRenderRequest`）の組み立てはコアのファサード（`Loupe.CreateRequest`）が単一所有し、コントロールは現在の状態を渡すだけです。

オーバーレイの描画位置は `(ドメイン座標 − CaptureTopLeft) × Scale` で求めます（切り出し領域と倍率の計算は Avalonia 版と同じコアの計算を共有します）。

## ログ

`Log` プロパティへ `ILogger` を設定すると、設定変更と描画の例外を出力します（未設定・null の場合は `NullLogger` になり出力しません）。

| レベル | 出力内容 |
| --- | --- |
| Debug | `[LoupeCtrl] Setting: {名前} {前} → {後}`（Source / CenterPoint / RequestedSize / RenderScaling / ZoomLevel。補正後の値を渡し、変化判定（同一値・同一インスタンスなら出力しない）はコアのログ定義が単一所有する。設定の変更は変化した項目だけを出力する） |
| Error | `[LoupeCtrl] Update exception`（描画の例外。スタックトレース付き。アプリは継続する） |

数値の補正（`coerce`）と変更ログ（`changed`）は**プロパティの登録（`MewProperty.Register`）**へ渡します。したがって setter を経由しない設定（バインディング・`MewObject.SetValue` 相当）でも安全域が保たれ、変更が記録されます（Avalonia 版の `coerce` コールバック + `Changed` クラスハンドラと同じ契約）。

設定（`Options`）と外観（`Options.Appearance`）の数値項目は**コアの型が生成時に補正**するため、本コントロールは補正を登録しません（値の設定経路（オブジェクト初期化子・`with` 式・逆シリアル化・バインディング）によらず範囲内の値だけが保持されます）。

Avalonia 版の再描画パイプライン（RequestRender / RenderCore / GenerateAsyncDone）に相当する Trace ログは持ちません（MewUI 版は同期描画のため）。

## 非対応（今後の対応）

| 項目 | 状況 |
| --- | --- |
| 座標変換ポート（`SetCoordinateTransform`） | 実装済み（`Inverse` = ドメイン → 親領域 DIP。表示位置の計算はコアのファサードが行い、コントロールは本ポートをファサードへ渡す） |
| 再生成の要求メソッド（`Clear` / `RequestRender`） | 未実装（プロパティ変更で自動再描画される） |
