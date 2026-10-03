# App.Parts.CapturePreviewControl.Core

## 目次

- [App.Parts.CapturePreviewControl.Core](#apppartscapturepreviewcontrolcore)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. 依存関係](#3-依存関係)
  - [4. クイックスタート](#4-クイックスタート)
  - [5. ドキュメント](#5-ドキュメント)
  - [6. プロジェクト構成](#6-プロジェクト構成)
  - [7. ライセンス](#7-ライセンス)

---

## 1. 概要

撮影（スクリーン / ウィンドウ / 範囲選択）・切り出し（Crop）・切り出しビューの画像ビューポート（ズーム・パン・Fit）の**共通規則**を提供する UI 非依存ライブラリです。 Avalonia 版（`App.Parts.CapturePreviewControl.AvaloniaUI`）と MewUI 版（`App.Parts.CapturePreviewControl.MewUI`）が同じ規則を使うため、規則の二重実装を防ぐ目的で独立しています。

**層の責務の境界**: 本プロジェクトは「状態・判断・両 UI 共通の値と計算・コア⇔UI の契約・ログ定義」を担い、UI 層は「入力の受付・Window の生成と表示・DPI の取得・レイアウト・フレームワーク固有の描画」だけを担います。

## 2. 機能一覧

- **切り出し矩形の計算**（`CropBoundsMath`）: 選択矩形 → 画像内のピクセル矩形。 画像内へのクランプ・端数の丸め（0.5 は切り上げ）・終点のピクセルの加算（始点・終点を含むかは矩形の `EsRectInterval` が持ち、**加算の判断をここが単一所有する**）・最小サイズ判定を単一所有する
- **選択矩形→切り出し矩形の解決**（`CaptureClipMath.ResolveCaptureRect`）: 確定時の切り出し矩形を決める**唯一の入口**（選択矩形も占有範囲も**画面物理座標**で渡す）。 種別ごとの区間の流儀（`CaptureTypeTraits.ResolveInterval`）とクランプ・丸め（`CropBoundsMath`）を組み合わせ、画像の右端・下端で透明な余白が付かないようにする（両 UI が同じ 1 行を呼ぶ）。 UI の選択矩形が画像ローカル座標の場合は、境界で `ToScreenRect` により画面物理座標へ戻してから渡す（１つの座標空間に揃えることで取り違えの余地をなくす）
- **外観・動作設定**（`CaptureSettings`）: 全メンバー nullable の record。 null は「デフォルト値」または「現在値維持（部分更新）」を表し、`Validate` / `ToNonNull` を提供する
- **キャプチャ結果**（`CaptureResult`）: `EsBitmap?`（画像）と `EsRectInt?`（仮想スクリーン座標の領域）の組
- **複合コントロールの設定変換**（`FadeMaskDragRectOptionsMapper`）: `CaptureSettings` → `FadeMaskDragRectOptions`（選択矩形 / 暗転マスク / ルーペ / 選択矩形破線）の**寸法・表示可否**の対応を単一所有する（両 UI のホストが同じ変換を使う。 **色・枠線の太さ・表示可否は描画にだけ必要な設定のため写像せず**、提示層の `MarqueeAppearance` / `LoupeAppearance` が単一所有する）
- **ラベル文言の単一所有**（`CaptureLabelTexts`）: キャプチャ種別→動作モードの写像と、操作ラベル（確定・キャンセル方法）・情報ラベル（ウィンドウタイトル）の文言を単一所有する（汎用コントロールは文言を持たず、ホストがプロバイダで与える）
- **切り出し結果の適用先**（`ICaptureTarget`）: 撮影・切り出しの結果をアプリ側へ渡すポート（現在の画像・画像差し替えの通知・切り出しの適用）。 アプリの対象操作（Undo 退避・未保存化・ステータス通知）は実装側に残す
  - **画像の平均色**（`ImageColorMath`）: 標本間引きで平均色を求める（描画の背景色の決定に使う）
  - **キャプチャ種別の性質**（`CaptureTypeTraits`）: ハイライト型（クリック確定）か・切り出しの区間の流儀（終点のピクセルを含むか）・カーソル下の検出対象（画面 / ウィンドウ）の判定を単一所有する
- **モニタの実表示倍率**（`MonitorScalingMath`）: 対象矩形の**中心**を含むモニタの倍率を解決する（該当なしはフォールバック）。 対象座標からは**原点の加算**も本クラスが行う（`ResolveForSourceRect`）。 判定に使う一覧の構築（**倍率未確定（0 以下）の除外**）も `Collect` が単一所有する（両 UI が同じ判定点・同じ対象を使う）
- **ハイライトの状態と判断**（`CaptureHighlight` / `CaptureHighlightSession`）: カーソル下の要素の検出結果から「ハイライトの有無・矩形（対象座標）・表示名」を導出する。 **未検出の正規化・対象変換・表示名の変化の判定・診断ログ**を単一所有し、UI は戻り値を設定するだけにする（直前の状態はセッションのプロパティから読む）
- **オーバーレイのキー操作**（`CaptureKeyActions`）: Enter（Return を含む）= 確定 / Escape = キャンセルを単一所有する。 **確定対象が無い場合の Enter は何もしない**（何も検出していない状態でキャンセル扱いにしない）。 UI はキーを `EsKey` へ変換して渡すだけにする
- **クリック確定の状態**（`CaptureClickSelection`）: スクリーン選択 / ウィンドウ選択の「押下で固定・解放で確定」を単一所有する。 退化した検出（空の矩形）は固定せず、押下のたびに置き換え、解放で 1 回だけ取り出す（固定が無い解放では確定しない）
- **切り出しのサイズラベル**（`CropLabelTexts`）: `LT: (l, t),  RB: (r, b),  WH: (w+1, h+1), Zoom: (pz%)` 形式。 `WH` は含まれるピクセル数であり、終点を含めるかの判断は矩形の `EsRectInterval` が持つ（切り出し範囲と同じ値を使う）。 撮影の選択（`CaptureLabelTexts.ResolveSizeLabel`）とは用途が違うため別途単一所有する。 **未選択は描かない**という判断も `ResolveSizeLabel` が持つ
- **サイズラベルの文言解決**（`CaptureLabelTexts.ResolveSizeLabel`）: 「確定対象があれば矩形から生成・無ければホスト指定の文言」という規則を単一所有する（UI が同じ三項演算を書かない。 表示倍率のデリゲートは表示時のみ評価する）
- **設定の部分更新マージ**（`CaptureSettingsMerge`）: 全 45 項目の「null は現在値維持・非 null は上書き」と、同値再設定のスキップ（参照を差し替えない）を単一所有する
- **セッションの排他スロット**（`CaptureSessionSlot{TCapture, TCrop}`）: 撮影と切り出しで同時に 1 つだけ実行できる規則と、「未実行なら false」の既定値解決を単一所有する
- **画像の平均色**（`ImageColorMath`）: 標本間引きの間隔と空画像のフォールバックを単一所有する
- **仮想スクリーンからの切り出し**（`CaptureClipMath`）: 選択矩形→切り出し矩形の解決（浮動小数版と整数版の 2 入口）と、画面物理座標 ⇔ 画像ローカル座標の**双方向変換**（原点の加減算）・切り出し・画像の共有値（`ImageSize` / `ImageBounds` / `ValidateImage`）を単一所有する。 **破棄済みの画像は切り出さず null**を返す（ウィンドウクローズとの競合で例外にしない）。 マルチモニタ（原点が負値）でも同じ式で扱える
- **撮影画像の保持**（`CaptureImageHolder`）: 「差し替え時の旧値の破棄」「破棄後に届いた画像のその場での破棄」「未取得時の占有範囲のフォールバック」を単一所有する（背景撮影の完了がウィンドウの後片付けより遅れた場合の破棄漏れを防ぐ）
- **仮想スクリーンの取得**（`CaptureVirtualScreen`）: 取得をタイムアウト付きで待ち、**間に合わなかった場合は遅れて完了した結果を破棄する**（UI は待ち方と破棄を持たない）。 **撮影対象の範囲（全モニタの包含矩形）と「モニタが無ければ撮影しない」前提条件**も `ResolveBounds` が単一所有する（UI がループを書かない）
- **事前切り出しキャッシュ**（`CaptureClipCache`）: 撮影確定前の切り出し結果を保持し、**同じ矩形なら再切り出しをスキップ**、確定時は**所有権ごと**渡す、置き換え時は**旧値を破棄**するという規則を単一所有する
- **撮影セッションの方針値**（`CaptureTiming`）: 画面キャプチャのタイムアウトと、オーナーウィンドウの画面外退避座標（両 UI で同じ値を使う）
- **切り出しの操作内容**（`CropSessionActions`）: 1 つのボタン（キー）で「開始・確定・キャンセル」を切り替える判断を単一所有する（未実行→開始 / 選択あり→確定 / 選択なし→キャンセル）
- **ログ定義**（`CaptureLogMessage` / `CaptureLogFormat` / `CaptureSettingsLogMessage`）: 書式・レベル・イベント ID・**完了時の出力の組**（確定は `CaptureCompleted` → `Result` の 2 件、キャンセルは `CaptureCancelled` の 1 件 = `LogCaptureFinished`）を単一所有する（UI 層は呼ぶだけ。 未検出・未取得の空文字化・未取得のサイズ 0 化も含む）
- **矩形オーバーレイ**（`OverlayRect` / `OverlayRectLayout` 他）: 画像上へ実線・破線・点線の矩形を描く契約と、**「どう描くか」の判断**（負サイズのクランプ・太さの 1 ピクセル下限・破線の判定とダッシュ長・背景色を使うか）を単一所有する（切り出しビューと外部提供矩形が共有する）
- **領域選択の複合コントロールのコア**（`FadeMaskDragRectMode` / `FadeMaskDragRectOptions` 他）: 動作モードの型・設定の単一値・モードから導出する表示可否・選択矩形の可否判定・ルーペ画像への破線描画を単一所有する（両 UI の複合コントロールが同じ型と判断を使う）
- **切り出しビューの画像ビューポートの状態と判断**（`ZoomViewSession`）: 内部ズーム率（0 = Fit）・オフセット・設定・表示条件を保持し、Fit の表現（内部ズーム率 0 = 自動）・実効ズーム率の導出・ズーム可否・遷移の適用（ズーム中心の維持）・パン（grab 方式）・スクロールバーのドラッグ・画像変更時の Fit 復帰を単一所有する（UI は入力の変換・効果の適用・描画だけを担う）
- **ズーム遷移の規則**（`ZoomTransition` / `ZoomOptions`）: ⊖⊕ の 23 段階遷移・Ctrl+ホイールの ×1.1 遷移・可否判定を単一所有する。 設定は `ZoomOptions`（`MinZoom` / `MaxZoom` / `WheelFactor` / `ButtonZoomLevels` の検証付き不変値）が持ち、既定は 10〜500%・ 23 段階・×1.1
- **画像ビューポートの幾何**（`ZoomMath` / `ZoomScrollBarGeometry`）: Fit 実効倍率・ズーム中心を維持するオフセット・スクロール範囲のクランプ・ビューポート ⇔ コンテンツ座標の変換・% ⇔ 倍率の換算と、オーバーレイスクロールバーのトラック・サムの矩形と可動範囲・サム位置からのオフセット逆算を単一所有する
- **画像ビューポートの外観**（`ZoomViewAppearance`）: 下地・市松模様（タイルサイズと 2 色）・スクロールバーの外観を単一値で保持し、描画可否と、市松模様を描く範囲（画像の表示矩形の内側）・明色タイル矩形・タイルサイズの実効値を単一所有する
- **画像ビューポートの通知と効果**（`ZoomState` / `ZoomFeedback` / `ZoomNotice`）: 通知する 4 値（実効ズーム率・Fit 状態・可否）・UI が適用する効果（再描画・ポインタキャプチャ。 適用順）・通知の種別を単一所有する
- **画像ビューポートのログ定義**（`ZoomLogMessage` / `ZoomLogFormat`）: ズーム操作の実行前後（例: `[ZoomIn] 50% -> 62%`）と、変更前の実効ズーム率の丸めを単一所有する
- **UI 非依存**: Avalonia / MewUI のどちらの型も参照しない（色は `System.Drawing.Color`、座標は `Es*` 型）

## 3. 依存関係

```text
App.Parts.CapturePreviewControl.Core
    ├── Platform.Core（Es* 幾何型 / EsBitmap）
    ├─ Feature.Marquee.Core（MarqueeOptions・選択矩形の幾何）
    ├─ Feature.Loupe.Core（LoupeOptions）
    └── Microsoft.Extensions.Logging.Abstractions（ログ定義のソース生成）
```

依存は一方向です（UI プロジェクトから本プロジェクトを参照し、逆は参照しない）。

## 4. クイックスタート

```csharp
using EsUtil.SaveEvidence.Parts.CapturePreview.Core;
using EsUtil.Platform.Core.Geometry;

// 選択矩形（画像ピクセル）から切り出し範囲を求める
// 終点のピクセルを含めるかは矩形の Interval が決める（ドラッグ選択は Closed）
var bounds = CropBoundsMath.ComputeCropBounds(
    new EsRect(10, 10, 40, 40, EsRectInterval.Closed),   // 選択矩形（ドラッグ選択）
    new EsSize(1920, 1080),                              // 画像サイズ
    minSelectionSize: 6);                                // これ未満は空（切り出し不可）

if (!bounds.IsEmpty)
{
    // bounds で画像を切り出す
}

// 切り出しビューの画像ビューポート（ズーム・パン・Fit）は ZoomViewSession が単一所有する
var zoom = new ZoomViewSession();
zoom.SetSource(hasSource: true, new EsSize(1920, 1080));   // 画像の設定（Fit 表示へリセット）
zoom.SetViewport(new EsSize(800, 600));                     // ビューポートのサイズ
var feedback = zoom.ZoomIn();                               // ⊖⊕ の段階遷移
feedback.ApplyVisual(redraw: () => { /* 再描画 */ });        // 効果の適用（適用順は ZoomFeedback が持つ）

if (zoom.CurrentState is { IsFit: false, CanZoomOut: true })
{
    // 実効ズーム率は zoom.ZoomPercent、パンは zoom.PanBy(...) を使う
}
```

## 5. ドキュメント

- `SPEC_ExtFunc.md` — 公開 API の外部仕様
- `SPEC_System.md` — 設計・依存・検証方針

## 6. プロジェクト構成

```text
src/
│   ├── App.Parts.CapturePreviewControl.Core.csproj  # ライブラリ（net10.0）
│   ├── CropBoundsMath.cs                  # 切り出し矩形の計算
│   ├── CropLabelTexts.cs                  # 切り出しのサイズラベル書式
│   ├── ImageColorMath.cs                  # 画像の平均色（標本間引き）
│   ├── CaptureTypeTraits.cs               # キャプチャ種別の性質（ハイライト型・区間の流儀・検出対象）
│   ├── CaptureElementTarget.cs            # カーソル下の検出対象（画面 / ウィンドウ）
│   ├── MonitorScalingMath.cs              # モニタの実表示倍率（矩形中心で判定・倍率未確定の除外）
│   ├── CaptureHighlight.cs                # ハイライトの状態・判断・ログ（検出→ハイライト）
│   ├── CaptureKeyAction.cs                # オーバーレイのキー操作（確定 / キャンセル）の解釈
│   ├── CaptureClickSelection.cs           # クリック確定の固定（押下で固定・解放で取り出し）
│   ├── CaptureClipMath.cs                 # 切り出し矩形の解決と画像ローカル座標との相互変換
│   ├── CaptureImageHolder.cs              # 撮影画像の保持（所有権・差し替え・破棄後）
│   ├── CaptureVirtualScreen.cs            # 仮想スクリーン取得の待ち方（タイムアウト・未使用結果の破棄・撮影対象の範囲）
│   ├── CaptureClipCache.cs                # 事前切り出しキャッシュ（同一判定・所有権・破棄）
│   ├── CaptureTiming.cs                   # タイムアウト・画面外退避座標
│   ├── CropSessionAction.cs               # 切り出しの操作内容（開始 / 確定 / キャンセル）
│   ├── CaptureSessionState.cs             # セッションの実行中・完了の排他
│   ├── CaptureSessionSlot.cs              # 撮影・切り出しの排他スロット
│   ├── CaptureCompletion.cs               # 結果の単一確定（TCS のラップ）
│   ├── CaptureOwnerScope.cs               # オーナー退避とキャプチャ除外の手順
│   ├── CaptureSettingsMerge.cs            # 設定の部分更新マージ
│   ├── CaptureSettingsLogMessage.cs       # 設定適用のログ定義
│   ├── CaptureLogMessage.cs               # 共通ログ定義（開始・確定・キャンセル）
│   ├── CaptureLogFormat.cs                # ログに載せる値の文字列化
│   ├── ICaptureTarget.cs                  # 切り出し結果の適用先（ポート）
│   ├── ICaptureOwnerWindow.cs             # 退避対象のポート
│   ├── CaptureSettings.cs                 # 外観・動作設定（record）
│   ├── FadeMaskDragRectOptionsMapper.cs   # 設定 → 複合コントロールの設定値への変換
│   ├── CaptureLabelTexts.cs               # 撮影の操作・情報ラベル文言とモード写像
│   ├── CaptureResult.cs                   # キャプチャ結果（record）
│   ├── OverlayRect.cs                     # 外部提供矩形の描画情報（矩形・スタイル・色・太さ）
│   ├── OverlayLineStyle.cs                # 枠線スタイル（実線 / 破線 / 点線）
│   ├── OverlayRectLayout.cs               # 描画入力の解決（クランプ・太さの下限・破線の判定）
│   ├── OverlayRectFrame.cs                # 矩形 1 個分の描画入力
│   ├── OverlayLineDash.cs                 # スタイルごとのダッシュ長の単一所有
│   ├── OverlayRectRasterizer.cs           # 矩形の 4 辺を EsBitmap へラスタライズする
│   ├── FadeMaskDragRectMode.cs            # 領域選択の動作モード
│   ├── FadeMaskDragRectOptions.cs         # 複合コントロールの設定の単一値
│   ├── FadeMaskDragRectLabelOptions.cs    # 付加情報ラベル（操作・情報）の外観
│   ├── FadeMaskDragRectModes.cs           # モード → 状態の解決
│   ├── FadeMaskDragRectState.cs           # モードから導出する表示可否
│   ├── FadeMaskDragRectSelection.cs       # 選択矩形の可否判定
│   └── FadeMaskDragRectOverlay.cs         # ルーペ画像への選択矩形破線の描画│   ├── ZoomViewSession.cs                 # 画像ビューポートの状態と判断（唯一の所有者）
│   ├── ZoomOptions.cs                      # ズーム遷移の設定（検証付き不変値）
│   ├── ZoomState.cs                       # 通知する 4 値（実効ズーム率・Fit 状態・可否）
│   ├── ZoomFeedback.cs                    # UI が適用する効果（再描画・ポインタキャプチャ）
│   ├── ZoomNotice.cs                      # 通知の種別と通知時点の値
│   ├── ZoomViewAppearance.cs              # 画像ビューポートの外観（下地・市松模様・スクロールバー）
│   ├── ZoomScrollBarGeometry.cs           # スクロールバーの幾何（トラック・サム・逆算。ZoomScrollBar も定義）
│   ├── ZoomScrollBarMetrics.cs            # 標準スクロールバー部品へ写す同期値
│   ├── ZoomTransition.cs                  # 段階遷移・ホイール遷移・可否判定（純粋関数）
│   ├── ZoomMath.cs                        # Fit 実効倍率・ズーム中心維持・クランプ・座標変換（純粋関数）
│   ├── ZoomAvailability.cs                # ズーム可否を単一に保持する値オブジェクト
│   ├── ZoomLogMessage.cs                  # 画像ビューポートの操作ログの定義
│   └── ZoomLogFormat.cs                   # ログに載せる値の文字列化・丸め└── tests/
    ├── App.Parts.CapturePreviewControl.Core.Tests.csproj
    ├── CropBoundsMath.Tests.cs            # クランプ・丸め・区間の流儀・丸め規則（41 件）
    ├── CropLabelTexts.Tests.cs            # 含まれるピクセル数・ズーム率の丸め・未選択時の表示可否（10 件）

    ├── ImageColorMath.Tests.cs            # 平均色（標本間引き・空画像。5 件）
    ├── CaptureTypeTraits.Tests.cs         # ハイライト型・区間の流儀・検出対象・検出呼び分け・確定対象（16 件）
    ├── MonitorScalingMath.Tests.cs        # 中心判定・境界・フォールバック・対象座標・一覧の構築・代表値（19 件）
    ├── CaptureClipMath.Tests.cs           # 切り出し矩形の解決（座標空間の契約・整数/浮動小数）・双方向変換・画像の共有値（34 件）
    ├── CaptureImageHolder.Tests.cs        # 保持の規則（差し替え・破棄後・フォールバック）（8 件）
    ├── CaptureVirtualScreen.Tests.cs      # タイムアウト待ちと未使用結果の破棄（6 件）
├── CaptureVirtualScreenBoundsTests.cs  # 撮影対象の範囲（包含矩形・負の原点・モニタ無し）（5 件）
    ├── CaptureSettings.Tests.cs           # 設定の検証と既定値・非数値の許容（16 件）
    ├── CaptureClipCache.Tests.cs          # 同一判定・所有権の移動・旧値の破棄（12 件）
    ├── CaptureTiming.Tests.cs             # 方針値と切り出しの操作内容の決定（6 件）
    ├── CaptureSessionState.Tests.cs       # 実行中・完了の排他（6 件）
    ├── CaptureSessionSlot.Tests.cs        # 撮影・切り出しの排他と既定値（13 件）
    ├── CaptureCompletion.Tests.cs         # 結果の単一確定（3 件）
    ├── CaptureOwnerScope.Tests.cs         # 退避・除外の順序（5 件）
    ├── CaptureSettingsMerge.Tests.cs      # 部分更新マージと同値スキップ・入力なし（10 件）
    ├── CaptureLabelTexts.Tests.cs         # モード写像・文言の選定・サイズラベル解決（10 件）
    ├── CaptureLogMessage.Tests.cs         # ログ書式（全定義）と終了ログの組・無効ロガーの安全性（19 件）
    ├── CaptureHighlight.Tests.cs          # 検出→ハイライト状態の導出・変化の判定・ログ（9 件）
    ├── CaptureKeyActions.Tests.cs         # Enter / Return / Escape の解釈と確定対象の有無（10 件）
    ├── CaptureClickSelection.Tests.cs     # 固定と取り出しの規則（8 件）
    ├── CaptureResult.Tests.cs             # キャンセル結果の正規化（4 件）
    ├── FadeMaskDragRectOptionsMapper.Tests.cs  # 設定項目と寸法・表示可否の対応（6 件）
    ├── FadeMaskDragRectModes.Tests.cs       # モードの解決・ルーペ表示可否（12 件）
    ├── FadeMaskDragRectOptions.Tests.cs     # 既定値・部分変更・外側の装飾の単一所有（6 件）
    ├── FadeMaskDragRectOverlay.Tests.cs     # 破線の列挙と描画の画素検証（15 件）
    ├── FadeMaskDragRectSelection.Tests.cs   # 選択矩形の可否判定（9 件）
    ├── OverlayLineDashTests.cs            # スタイルごとのダッシュ長（8 件）
    ├── OverlayRectLayoutTests.cs          # クランプ・太さの下限・破線の判定（21 件）
    ├── OverlayRectRasterizerTests.cs      # ルーペ画像へのラスタライズ（画素検証。11 件）
    ├── CaptureTargetContractTests.cs      # ICaptureTarget の契約（3 件）
    ├── ZoomViewSession.Tests.cs           # 状態と判断・Fit・遷移・パン・スクロールバー（49 件）
    ├── ZoomOptions.Tests.cs                # 不変条件の検証と With による部分更新（18 件）
    ├── ZoomTransition.Tests.cs            # 段階遷移・ホイール遷移・可否判定（46 件）
    ├── ZoomMath.Tests.cs                  # Fit 実効倍率・中心維持・クランプ・座標変換（37 件）
    ├── ZoomScrollBarGeometry.Tests.cs     # トラック・サムの矩形とオフセット逆算（23 件）
    ├── ZoomViewAppearance.Tests.cs        # 描画可否・市松模様の範囲・タイル矩形（30 件）
    └── ZoomLogMessage.Tests.cs            # ログ書式と丸め（8 件）
```

テストは **577 件**（撮影・切り出し 366 件 ＋ 切り出しビューの画像ビューポート 211 件）。

## 7. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
