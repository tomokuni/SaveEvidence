# Parts.CapturePreview.Core

## 目次 <!-- omit from toc -->

- [1. 概要](#1-概要)
- [2. 機能一覧](#2-機能一覧)
- [3. 依存関係](#3-依存関係)
- [4. クイックスタート](#4-クイックスタート)
- [5. ドキュメント](#5-ドキュメント)
- [6. プロジェクト構成](#6-プロジェクト構成)
- [7. テスト](#7-テスト)
- [8. ライセンス](#8-ライセンス)

---

## 1. 概要

撮影（スクリーン / ウィンドウ / 範囲選択）・切り出し（Crop）・切り出しビューの画像ビューポート（ズーム・パン・Fit）の**共通規則**を提供する UI 非依存ライブラリです。 Avalonia 版（`Parts.CapturePreview.ControlAvaloniaUI`）と MewUI 版（`Parts.CapturePreview.ControlMewUI`）が同じ規則を使うため、規則の二重実装を防ぐ目的で独立しています。

**層の責務の境界**: 本プロジェクトは「状態・判断・両 UI 共通の値と計算・コア⇔UI の契約・ログ定義」を担い、UI 層は「入力の受付・Window の生成と表示・DPI の取得・レイアウト・フレームワーク固有の描画」だけを担います。

## 2. 機能一覧

- **切り出し矩形の計算**（`CropBoundsMath`）: 選択矩形 → 画像内のピクセル矩形。 画像内へのクランプ・端数の丸め（0.5 は切り上げ）・終点のピクセルの加算（始点・終点を含むかは矩形の `EsRectInterval` が持ち、**加算の判断をここが単一所有する**）・最小サイズ判定を単一所有する
- **選択矩形→切り出し矩形の解決**（`CropClipMath.ResolveCaptureRect`）: 確定時の切り出し矩形を決める**唯一の入口**（選択矩形も占有範囲も**画面物理座標**で渡す）。 種別ごとの区間の流儀（`CaptureTypeTraits.ResolveInterval`）とクランプ・丸め（`CropBoundsMath`）を組み合わせ、画像の右端・下端で透明な余白が付かないようにする（両 UI が同じ 1 行を呼ぶ）。 UI の選択矩形が画像ローカル座標の場合は、境界で `ToScreenRect` により画面物理座標へ戻してから渡す（１つの座標空間に揃えることで取り違えの余地をなくす）
- **外観・動作設定**（`CaptureSettings`）: 全メンバー nullable の record。 null は「デフォルト値」または「現在値維持（部分更新）」を表し、`Validate` / `ToNonNull` を提供する
- **キャプチャ結果**（`CaptureResult`）: `EsBitmap?`（画像）と `EsRectInt?`（仮想スクリーン座標の領域）の組
- **複合コントロールの設定変換**（`SelectionOverlayOptionsMapper`）: `CaptureSettings` → `SelectionOverlayOptions`（選択矩形 / 暗転マスク / ルーペ / 選択矩形破線）の**寸法・表示可否**の対応を単一所有する（両 UI のホストが同じ変換を使う。 **色・枠線の太さ・表示可否は描画にだけ必要な設定のため写像せず**、提示層の `MarqueeAppearance` / `LoupeAppearance` が単一所有する）
- **ラベル文言の単一所有**（`CaptureLabelTexts`）: キャプチャ種別→動作モードの写像と、操作ラベル（確定・キャンセル方法）・情報ラベル（ウィンドウタイトル）の文言を単一所有する（汎用コントロールは文言を持たず、ホストがプロバイダで与える）
- **キャプチャ完了の通知**（`CaptureCompletedEventArgs`）: 両 UI の Facade（`CaptureSessionControl`）が公開する `CaptureCompleted` イベントの引数（`CaptureResult` を運ぶ）。 **コア層に置く**ことで、UI ごとに別の引数型を公開しない（ホストが UI を跨いだ共通のハンドラを書ける）
  - **画像の平均色**（`ImageColorMath`）: 標本間引きで平均色を求める（描画の背景色の決定に使う）
  - **キャプチャ種別の性質**（`CaptureTypeTraits`）: ハイライト型（クリック確定）か・切り出しの区間の流儀（終点のピクセルを含むか）・カーソル下の検出対象（画面 / ウィンドウ）の判定を単一所有する
- **モニタの実表示倍率**（`MonitorScalingMath`）: 対象矩形の**中心**を含むモニタの倍率を解決する（該当なしはフォールバック）。 対象座標からは**原点の加算**も本クラスが行う（`ResolveForSourceRect`）。 判定に使う一覧の構築（**倍率未確定（0 以下）の除外**）も `Collect` が単一所有する（両 UI が同じ判定点・同じ対象を使う）
- **ハイライトの状態と判断**（`CaptureHighlight` / `CaptureHighlightSession`）: カーソル下の要素の検出結果から「ハイライトの有無・矩形（対象座標）・表示名」を導出する。 **未検出の正規化・対象変換・表示名の変化の判定・診断ログ**を単一所有し、UI は戻り値を設定するだけにする（直前の状態はセッションのプロパティから読む）
- **オーバーレイのキー操作**（`CaptureKeyActions`）: Enter（Return を含む）= 確定 / Escape = キャンセルを単一所有する。 **確定対象が無い場合の Enter は何もしない**（何も検出していない状態でキャンセル扱いにしない）。 UI はキーを `EsKey` へ変換して渡すだけにする
- **クリック確定の状態**（`CaptureClickSelection`）: スクリーン選択 / ウィンドウ選択の「押下で固定・解放で確定」を単一所有する。 退化した検出（空の矩形）は固定せず、押下のたびに置き換え、解放で 1 回だけ取り出す（固定が無い解放では確定しない）
- **切り出しのサイズラベル**（`CropLabelTexts`）: `LT: (l, t),  RB: (r, b),  WH: (w+1, h+1), Zoom: (pz%)` 形式。 `WH` は含まれるピクセル数であり、終点を含めるかの判断は矩形の `EsRectInterval` が持つ（切り出し範囲と同じ値を使う）。 撮影側のサイズラベルとは用途が違うため別途単一所有する。 **未選択は描かない**という判断も `ResolveSizeLabel` が持つ
- **設定の部分更新マージ**（`CaptureSettingsMerge`）: 全 42 項目の「null は現在値維持・非 null は上書き」と、同値再設定のスキップ（参照を差し替えない）を単一所有する
- **セッションの排他スロット**（`CaptureSessionSlot{TCapture, TCrop}`）: 撮影と切り出しで同時に 1 つだけ実行できる規則と、「未実行なら false」の既定値解決を単一所有する
- **仮想スクリーンからの切り出し**（`CropClipMath`）: 選択矩形→切り出し矩形の解決（浮動小数版と整数版の 2 入口）と、画面物理座標 ⇔ 画像ローカル座標の**双方向変換**（原点の加減算）・切り出し・画像の共有値（`ImageSize` / `ImageBounds` / `ValidateImage`）を単一所有する。 **破棄済みの画像は切り出さず null**を返す（ウィンドウクローズとの競合で例外にしない）。 マルチモニタ（原点が負値）でも同じ式で扱える
- **撮影画像の保持**（`CaptureImageHolder`）: 「差し替え時の旧値の破棄」「破棄後に届いた画像のその場での破棄」「未取得時の占有範囲のフォールバック」を単一所有する（背景撮影の完了がウィンドウの後片付けより遅れた場合の破棄漏れを防ぐ）
- **仮想スクリーンの取得**（`CaptureVirtualScreen`）: 取得をタイムアウト付きで待ち、**間に合わなかった場合は遅れて完了した結果を破棄する**（UI は待ち方と破棄を持たない）。 **撮影対象の範囲（全モニタの包含矩形）と「モニタが無ければ撮影しない」前提条件**も `ResolveBounds` が単一所有する（UI がループを書かない）
- **事前切り出しキャッシュ**（`CaptureClipCache`）: 撮影確定前の切り出し結果を保持し、**同じ矩形なら再切り出しをスキップ**、確定時は**所有権ごと**渡す、置き換え時は**旧値を破棄**するという規則を単一所有する
- **撮影セッションの方針値**（`CaptureTiming`）: 画面キャプチャのタイムアウトと、オーナーウィンドウの画面外退避座標（両 UI で同じ値を使う）
- **切り出しの操作内容**（`CropSessionActions`）: 1 つのボタン（キー）で「開始・確定・キャンセル」を切り替える判断を単一所有する（未実行→開始 / 選択あり→確定 / 選択なし→キャンセル）
- **ログ定義**（`CapturePreviewLogMessage`）: 接頭辞・書式・レベル・イベント ID・**完了時の出力の組**（確定は `CaptureCompleted` → `Result` の 2 件、キャンセルは `CaptureCancelled` の 1 件 = `LogCaptureFinished`）を単一所有する（UI 層は呼ぶだけ。 未検出・未取得の空文字化・未取得のサ イズ 0 化も含む）
- **文字列の正規化**（`TextNormalize`）: ログに限らず使う「未取得（null）は空文字」の規則を単一所有する（ログ定義の整形とは別の型。 ハイライトの表示名にも使う）
- **矩形オーバーレイ**（`OverlayRect` / `OverlayRectLayout` 他）: 画像上へ実線・破線・点線の矩形を描く契約と、**「どう描くか」の判断**（負サイズのクランプ・太さの 1 ピクセル下限・破線の判定とダッシュ長・背景色を使うか）を単一所有する（切り出しビューと外部提供矩形が共有する）
- **領域選択の複合コントロールのコア**（`SelectionOverlayMode` / `SelectionOverlayOptions` 他）: 動作モードの型・設定の単一値・モードから導出する表示可否・選択矩形の可否判定・ルーペ画像への破線描画を単一所有する（両 UI の複合コントロールが同じ型と判断を使う）
- **切り出しビューの画像ビューポートの状態と判断**（`ZoomViewSession`）: 内部ズーム率（0 = Fit）・オフセット・設定・表示条件を保持し、Fit の表現（内部ズーム率 0 = 自動）・実効ズーム率の導出・ズーム可否・遷移の適用（ズーム中心の維持）・パン（grab 方式）・スクロールバーのドラッグ・画像変更時の Fit 復帰を単一所有する（UI は入力の変換・効果の適用・描画だけを担う）
- **ズーム遷移の規則**（`ZoomTransition` / `ZoomOptions`）: ⊖⊕ の 23 段階遷移・Ctrl+ホイールの ×1.1 遷移・可否判定を単一所有する。 設定は `ZoomOptions`（`MinZoom` / `MaxZoom` / `WheelFactor` / `ButtonZoomLevels` の検証付き不変値）が持ち、既定は 10〜500%・ 23 段階・×1.1
- **画像ビューポートの幾何**（`ZoomMath` / `ZoomScrollBarGeometry`）: Fit 実効倍率・ズーム中心を維持するオフセット・スクロール範囲のクランプ・ビューポート ⇔ コンテンツ座標の変換・% ⇔ 倍率の換算と、オーバーレイスクロールバーのトラック・サムの矩形と可動範囲・サム位置からのオフセット逆算を単一所有する
- **ホイール入力の解釈**（`ZoomWheelIntent`）: 「Ctrl 併用 = ズーム（前方向 = 拡大）/ それ以外 = パン（Shift 併用 = 横方向）」を単一所有する（UI ごとに論理式を書かない）。 回転量 → 移動量の換算と適用は `ZoomViewSession.ZoomByWheel` / `PanByWheel` が担う
- **画像ビューポートの外観**（`ZoomViewAppearance`）: 下地・市松模様（タイルサイズと 2 色）・スクロールバーの外観を単一値で保持し、描画可否と、市松模様を描く範囲（画像の表示矩形の内側）・明色タイル矩形・タイルサイズの実効値を単一所有する
- **画像ビューポートの通知と効果**（`ZoomState` / `ZoomFeedback` / `ZoomNotice`）: 通知する 4 値（実効ズーム率・Fit 状態・可否）・UI が適用する効果（再描画・ポインタキャプチャ。 適用順）・通知の種別を単一所有する
- **画像ビューポートのズーム操作ログ**（`CapturePreviewLogMessage` の `LogZoomIn` / `LogZoomOut` / `LogZoomInWheel` / `LogZoomOutWheel`）: ズーム操作の実行前後（例: `[CapturePreview] ZoomIn: 50% -> 62%`）と、変更前の実効ズーム率の丸め（`Percent`）を単一所有する
- **提示の写像**（`CapturePaintMapper` / `SelectionOverlayChildValues` / `SelectionOverlayLabelRenderer` / `MarqueeHostLabelRenderer`）: 製品の設定型（`CaptureSettings`）から提示層の外観（`MarqueeAppearance` / `LoupeAppearance`）への写像、設定とモードから内包コントロールへ適用する値の解決、付加情報ラベル（操作・情報）の配置指示を単一所有する。 製品の設定型と提示層の外観型の両方を必要とするため、両者を参照できる唯一の層（本プロジェクト）に置く（UI ごとに書くと写像漏れが片方だけに生じ、ビルドが通るため無言で色が効かなくなる）
- **UI 非依存**: Avalonia / MewUI のどちらの型も参照しない（色は `System.Drawing.Color`、座標は `Es*` 型）

## 3. 依存関係

```text
Parts.CapturePreview.Core
    ├── EsUtil.Platform.Core（Es* 幾何型 / EsBitmap）
    ├─ EsUtil.Feature.Marquee.Presenter（MarqueeAppearance・MarqueeLabelPlacement、および推移参照の Marquee.Core = MarqueeOptions）
    ├─ EsUtil.Feature.Loupe.Presenter（LoupeAppearance、および推移参照の Loupe.Core = LoupeOptions）
    └── Microsoft.Extensions.Logging.Abstractions（ログ定義のソース生成）
```

依存は一方向です（UI プロジェクトから本プロジェクトを参照し、逆は参照しません）。 提示層（`EsUtil.Feature.*.Presenter`）への参照は、製品の設定型と提示層の外観を橋渡しする写像（`Presentation/`）を持つために限ります。 機能コア（`EsUtil.Feature.*.Core`）の型（`MarqueeOptions` / `LoupeOptions`）は提示層の公開面に現れるため、参照の宣言は提示層だけにします。

## 4. クイックスタート

```csharp
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Crop;
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

- [SPEC_API.md](docs/SPEC_API.md) — 公開 API のリファレンス
- [SPEC_System.md](docs/SPEC_System.md) — 設計・依存・検証方針

## 6. プロジェクト構成

```text
src/
│   ├── Parts.CapturePreview.Core.csproj  # ライブラリ（net10.0）
│   ├── Capture/                            # 撮影の判断と共有値
│   │   ├── CaptureTypeTraits.cs            # キャプチャ種別の性質（ハイライト型・区間の流儀・検出対象）
│   │   ├── CaptureElementTarget.cs         # カーソル下の検出対象（画面 / ウィンドウ）
│   │   ├── CaptureHighlight.cs             # ハイライトの状態・判断・ログ（検出→ハイライト）
│   │   ├── CaptureKeyAction.cs             # オーバーレイのキー操作（確定 / キャンセル）の解釈
│   │   ├── CaptureClickSelection.cs        # クリック確定の固定（押下で固定・解放で取り出し）
│   │   ├── CaptureLabelTexts.cs            # 撮影の操作・情報ラベル文言とモード写像
│   │   ├── CaptureResult.cs                # キャプチャ結果（record）
│   │   ├── TextNormalize.cs                  # 文字列の正規化（未取得の null を空文字へ）
│   │   ├── ImageColorMath.cs               # 画像の平均色（標本間引き）
│   │   └── MonitorScalingMath.cs           # モニタの実表示倍率（矩形中心で判定・倍率未確定の除外）
│   ├── Contract/                           # ホストが実装するポート
│   │   └── ICaptureOwnerWindow.cs          # 撮影時に退避するオーナーウィンドウ
│   ├── Crop/                               # 切り出し
│   │   ├── CropBoundsMath.cs               # 選択矩形 → 切り出し範囲（区間・クランプ・丸め・最小サイズ）
│   │   ├── CropClipMath.cs              # 切り出し範囲と画像ローカル座標の変換・画像からの抽出
│   │   ├── CropLabelTexts.cs               # 切り出しのサイズラベル書式
│   │   └── CropSessionAction.cs            # 切り出しの操作内容（開始 / 確定 / キャンセル）
│   ├── Logging/                            # ログ定義
│   │   └── CapturePreviewLogMessage.cs     # ログ定義（接頭辞・本文書式・イベント ID・値の整形）
│   ├── Overlay/                            # 矩形オーバーレイ
│   │   ├── OverlayRect.cs                  # 外部提供矩形の描画情報（矩形・スタイル・色・太さ）
│   │   ├── OverlayLineStyle.cs             # 枠線スタイル（実線 / 破線 / 点線）
│   │   ├── OverlayRectLayout.cs            # 描画入力の解決（クランプ・太さの下限・破線の判定）
│   │   ├── OverlayRectFrame.cs             # 矩形 1 個分の描画入力
│   │   ├── OverlayLineDash.cs              # スタイルごとのダッシュ長の単一所有
│   │   ├── OverlayDashWalk.cs              # 矩形の 1 辺を破線の区間へ分割する（両ラスタライザが共用）
│   │   └── OverlayRectRasterizer.cs        # 矩形の 4 辺を EsBitmap へラスタライズする
│   ├── Presentation/                       # 提示の写像（設定 → 外観・ラベル配置）
│   │   ├── CapturePaintMapper.cs           # CaptureSettings → MarqueeAppearance / LoupeAppearance
│   │   ├── SelectionOverlayChildValues.cs  # 設定とモード → 内包コントロールへ適用する値の解決
│   │   ├── SelectionOverlayLabelRenderer.cs # 付加情報ラベル（操作・情報）の縦積みの配置指示
│   │   ├── MarqueeHostLabelRenderer.cs     # ラベル 1 行の体裁（背景 = 実寸 + 内側余白 × 2）
│   │   └── HighlightAppearanceDefaults.cs  # 強調（ハイライト）の既定の外観
│   ├── SelectionOverlay/                   # 選択オーバーレイ（暗転マスク + 選択矩形 + ルーペ）のコア
│   │   ├── SelectionOverlayMode.cs         # 動作モード（Crop / AreaSelect / ScreenSelect / WindowSelect）
│   │   ├── SelectionOverlayState.cs        # モードから導出する状態（表示可否）
│   │   ├── SelectionOverlayOptions.cs      # 設定の単一値（寸法・表示可否）
│   │   ├── SelectionOverlayLabelOptions.cs # 付加情報ラベル（操作・情報）の外観
│   │   ├── SelectionUsability.cs           # 選択矩形の可否判定（退化した矩形を選択として扱わない）
│   │   ├── SelectionOutlineRasterizer.cs   # ルーペ画像への選択矩形破線の描画
│   │   └── SelectionOverlayOptionsMapper.cs # 設定 → 選択オーバーレイの設定値への変換
│   ├── Session/                            # 撮影セッションの状態と手順
│   │   ├── CaptureSessionState.cs          # セッションの実行中・完了の排他
│   │   ├── CaptureSessionSlot.cs           # 撮影・切り出しの排他スロット
│   │   ├── CaptureCompletion.cs            # 結果の単一確定（TCS のラップ）
│   │   ├── CaptureOwnerScope.cs            # オーナー退避とキャプチャ除外の手順
│   │   ├── CaptureTiming.cs                # タイムアウト・画面外退避座標
│   │   ├── CaptureImageHolder.cs           # 撮影画像の保持（所有権・差し替え・破棄後）
│   │   ├── CaptureClipCache.cs             # 事前切り出しキャッシュ（同一判定・所有権・破棄）
│   │   └── CaptureVirtualScreen.cs         # 仮想スクリーン取得の待ち方（タイムアウト・撮影対象の範囲）
│   ├── Setting/                            # 設定
│   │   ├── CaptureSettings.cs              # 外観・動作設定（record）
│   │   └── CaptureSettingsMerge.cs         # 設定の部分更新マージ
│   └── ZoomView/                           # 切り出しビューの画像ビューポート
│       ├── ZoomViewSession.cs              # 状態・読み取り・設定の反映（唯一の所有者）
│       ├── ZoomViewSession.Zoom.cs         # ズーム遷移（⊖⊕・ホイール・Fit 復帰）
│       ├── ZoomViewSession.Pan.cs          # パン（grab・ホイール換算）と座標変換
│       ├── ZoomViewSession.ScrollBar.cs    # スクロールバー（配置・同期値・ドラッグ）
│       ├── ZoomOptions.cs                  # ズーム遷移の設定（検証付き不変値）
│       ├── ZoomState.cs                    # 通知する 4 値（実効ズーム率・Fit 状態・可否）
│       ├── ZoomFeedback.cs                 # UI が適用する効果（再描画・ポインタキャプチャ）
│       ├── ZoomNotice.cs                   # 通知の種別と通知時点の値
│       ├── ZoomViewAppearance.cs           # 画像ビューポートの外観（下地・市松模様・スクロールバー）
│       ├── ZoomScrollBarGeometry.cs        # スクロールバーの幾何（トラック・サム・逆算）
│       ├── ZoomScrollBarMetrics.cs         # 標準スクロールバー部品へ写す同期値
│       ├── ZoomTransition.cs               # 段階遷移・ホイール遷移・可否判定（純粋関数）
│       ├── ZoomMath.cs                     # Fit 実効倍率・中心維持・クランプ・座標変換（純粋関数）
│       └── ZoomAvailability.cs             # ズーム可否を単一に保持する値オブジェクト
├── tests/
│   ├── Parts.CapturePreview.Core.Tests.csproj
    ├── RecordingLogger.cs                 # ログを収集するテスト用ロガー（共通）
    ├── Capture/                           # 撮影の判断と共有値の検証
    │   ├── CaptureClickSelection.Tests.cs
    │   ├── CaptureHighlight.Tests.cs
    │   ├── CaptureKeyActions.Tests.cs
    │   ├── CaptureLabelTexts.Tests.cs
    │   ├── CaptureResult.Tests.cs
    │   ├── CaptureTypeTraits.Tests.cs
    │   ├── ImageColorMath.Tests.cs
    │   └── MonitorScalingMath.Tests.cs
    ├── Crop/                              # 切り出しの検証
    │   ├── CropBoundsMath.Tests.cs
    │   ├── CropClipMath.Tests.cs
    │   └── CropLabelTexts.Tests.cs
    ├── Logging/                           # ログ書式の検証
    │   └── CapturePreviewLogMessage.Tests.cs
    ├── Overlay/                           # 矩形オーバーレイの検証
    │   ├── OverlayLineDash.Tests.cs
    │   ├── OverlayDashWalk.Tests.cs
    │   ├── OverlayRectLayout.Tests.cs
    │   └── OverlayRectRasterizer.Tests.cs
    ├── Presentation/                      # 提示の写像の検証
    │   ├── CapturePaintMapper.Tests.cs
    │   ├── MarqueeHostLabelRenderer.Tests.cs
    │   ├── SelectionOverlayChildValues.Tests.cs
    │   └── SelectionOverlayLabelRenderer.Tests.cs
    ├── SelectionOverlay/                  # 選択オーバーレイのコアの検証
    │   ├── SelectionOverlayState.Tests.cs
    │   ├── SelectionOverlayOptions.Tests.cs
    │   ├── SelectionOverlayOptionsMapper.Tests.cs
    │   ├── SelectionUsability.Tests.cs
    │   └── SelectionOutlineRasterizer.Tests.cs
    ├── Session/                           # セッションの状態と手順の検証
    │   ├── CaptureSessionState.Tests.cs
    │   ├── CaptureSessionSlot.Tests.cs
    │   ├── CaptureCompletion.Tests.cs
    │   ├── CaptureOwnerScope.Tests.cs
    │   ├── CaptureTiming.Tests.cs
    │   ├── CaptureImageHolder.Tests.cs
    │   ├── CaptureClipCache.Tests.cs
    │   ├── CaptureVirtualScreen.Tests.cs
    │   └── CaptureVirtualScreenBounds.Tests.cs
    ├── Setting/                           # 設定の検証
    │   ├── CaptureSettings.Tests.cs
    │   └── CaptureSettingsMerge.Tests.cs
    └── ZoomView/                          # 画像ビューポートの検証
        ├── ZoomViewSession.Tests.cs
        ├── ZoomOptions.Tests.cs
        ├── ZoomTransition.Tests.cs
        ├── ZoomMath.Tests.cs
        ├── ZoomScrollBarGeometry.Tests.cs
        └── ZoomViewAppearance.Tests.cs
```

## 7. テスト

`src/Parts.CapturePreview.Core/tests` が本プロジェクトのテストを持ちます（xUnit v3）。 実行手順と合否基準は [検証手順.md](../../.github/検証手順.md) が定めます。

`powershell
dotnet test src/Parts.CapturePreview.Core/tests/Parts.CapturePreview.Core.Tests.csproj
`

## 8. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
