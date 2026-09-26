# MarqueeControl システム仕様書（SPEC_System）

## 目次

- [MarqueeControl システム仕様書（SPEC\_System）](#marqueecontrol-システム仕様書spec_system)
  - [目次](#目次)
  - [1. システム概要](#1-システム概要)
  - [2. 技術スタック](#2-技術スタック)
  - [3. アーキテクチャ](#3-アーキテクチャ)
    - [3.1 レイヤー構造](#31-レイヤー構造)
    - [3.2 依存の方向](#32-依存の方向)
  - [4. 例外処理ポリシー](#4-例外処理ポリシー)
    - [4.1 基本方針](#41-基本方針)
    - [4.2 本プロジェクト固有の例外処理](#42-本プロジェクト固有の例外処理)
  - [5. コンポーネント図](#5-コンポーネント図)
  - [6. コンポーネント一覧](#6-コンポーネント一覧)
    - [6.1 MarqueeControl](#61-marqueecontrol)
    - [6.2 Marquee（公開ファサード・コア）](#62-marquee公開ファサードコア)
    - [6.3 MarqueeState](#63-marqueestate)
    - [6.4 SelectionGeometry](#64-selectiongeometry)
    - [6.5 MarqueeRenderer（MarqueeControl.UIBase）](#65-marqueerenderermarqueecontroluibase)
    - [6.6 MarqueeOuterRenderer（MarqueeControl.UIBase）](#66-marqueeouterrenderermarqueecontroluibase)
    - [6.6.1 ラベルの要素配置（文字の表示）](#661-ラベルの要素配置文字の表示)
    - [6.7 MarqueeInnerRenderer（MarqueeControl.UIBase）](#67-marqueeinnerrenderermarqueecontroluibase)
    - [6.8 Marquee（コアのフレーム解決）](#68-marqueeコアのフレーム解決)
    - [6.9 HitTarget](#69-hittarget)
    - [6.10 公開イベント引数](#610-公開イベント引数)
  - [7. 依存ルール](#7-依存ルール)
  - [8. ライフサイクル](#8-ライフサイクル)
    - [8.1 状態の初期化と破棄（所有権）](#81-状態の初期化と破棄所有権)
  - [9. ログ](#9-ログ)
    - [9.1 ログ定義一覧](#91-ログ定義一覧)
- [コア（`EsUtil.Feature.MarqueeCore.Logging.MarqueeLogMessage`）](#コアesutilfeaturemarqueecoreloggingmarqueelogmessage)
- [組み込みの層（`EsUtil.SaveEvidence.Marquee.UIBase.MarqueeWheelLogMessage`）](#組み込みの層esutilsaveevidencemarqueeuibasemarqueewheellogmessage)

---

## 1. システム概要

SaveEvidence は、Windows デスクトップ上の画面をキャプチャし、プレビュー・編集・保存するための Avalonia UI アプリケーションです（.NET 10.0 / C# 14 / MVVM）。

本プロジェクト（MarqueeControl）は、このシステムのうち**領域選択ドラッグ矩形**の UI・操作・描画を自己完結する再利用可能なコントロールです。上位 AP（CaptureSessionControl.AvaloniaUI / app）に依存せず、マウス操作・キーボード操作（nudge）・描画をコントロール内で完結します。

## 2. 技術スタック

| 項目 | 内容 |
| --- | --- |
| **プロジェクト** | `MarqueeControl` |
| **ルート名前空間** | `EsUtil.SaveEvidence.Marquee.AvaloniaUI` |
| **言語** | C# 14（.NET 10.0） |
| **UI フレームワーク** | Avalonia UI 12.1（Control / StyledProperty / DrawingContext） |
| **ターゲットフレームワーク** | net10.0 |
| **ビルドツール** | MSBuild / .NET SDK 10.0 |
| **ログ** | Microsoft.Extensions.Logging.Abstractions（LoggerMessage ソース生成） |
| **テスト** | xUnit v3 / Avalonia.Headless.XUnit |
| **依存プロジェクト** | Feature.MarqueeCore（公開ファサード・描画内容）/ MarqueeControl.UIBase（描画の手順・色・ラベル計画）/ Platform.AvaloniaUI（ScreenConverter・`AvaloniaCanvas`）/ Platform.Native（ICursorService）/ Platform.Core（ICoordTransform / CoordTransform） |

## 3. アーキテクチャ

### 3.1 レイヤー構造

```text
公開コントロール（MarqueeControl）… 入力の受付 / 効果の適用 / 通知の転送 / 描画の起動
  → 状態管理クラス（MarqueeSession）… 入力の解釈と状態遷移の判断
    → 状態（MarqueeState）… 状態保持と更新ロジック
      → 共有型（SelectionGeometry）+ Platform.AvaloniaUI
  → 描画内容の解決（Marquee.Content.cs の ResolveFrame）… 選択の状態 + 設定 → 描画内容 1 値（MarqueeDrawContent）
  → 装飾の解決（Core 内の `Decoration` 名前空間）… 暗転・穴あけ枠・ハイライトの判断
  → 描画の手順（MarqueeControl.UIBase）… `MarqueeRenderer` が色（`MarqueePaint`）を使い、描画ポート `IEsCanvas` へ層の順に写す
  → ラベルの配置（UI 層）… `MarqueeLabelPlan`（描画層）が返す配置矩形へ `Border` + `TextBlock` を置く（ラベルは常に最上位）
```

### 3.2 依存の方向

- 依存は**一方向の階層構造**（MarqueeControl → MarqueeSession → MarqueeState → SelectionGeometry / Platform.AvaloniaUI / Platform.Native / Platform.Core。描画は MarqueeControl → MarqueeRenderer（MarqueeControl.UIBase）→ IEsCanvas（Platform.Core））。
- MarqueeControl は上位 AP に依存しない（再利用可能）。
- **判断はコア（`Marquee`）・反映は UI**: nudge セッション・ダブルクリックの確定/キャンセル・ハンドルのスナップ・クリック検出・最小サイズによるキャンセルはコアが決め、コントロールは `IMarqueeEffects` を実装して要求された効果を適用するだけにする。
- 描画の手順はコア（`EsUtil.Feature.MarqueeCore.Contract` の `MarqueeRendererBase`）と描画層（`MarqueeControl.UIBase` の `MarqueeRenderer` / `MarqueeOuterRenderer` / `MarqueeInnerRenderer`）が単一所有する。色（描画にだけ必要な設定）は同層の `MarqueePaint` が単一所有し、本コントロールは `Paint` プロパティで与える。本コントロールは `Marquee.ResolveContent` が返す描画内容を `AvaloniaCanvas`（`IEsCanvas` の Avalonia 実装）経由で渡すだけであり、装飾の**判断**は持たない（状態（選択矩形・nudge）はコア（`MarqueeState`）に集約する）。
- **装飾（暗転・穴あけ枠・ハイライト）は選択矩形コントロール単体の概念ではない**ため、Core 内の別フォルダ（`Decoration` 名前空間）が単一所有する。本コントロールは `DecoOptions` で設定し、描画内容（`OuterDecoFrame` / `InnerDecoFrame`）をコアの `ResolveContent` から受け取る。
- **文字（サイズラベル・座標ラベル）は本コントロールがテキスト要素（`Border` + `TextBlock`）として配置する**。配置するラベルの一覧と順序は描画層（`MarqueeControl.UIBase` の `MarqueeLabelPlan`）が単一所有し（ラベルの色は描画実装が `MarqueePaint` から写す）、本コントロールは自 UI の API（`FormattedText`）で測った実寸を渡して返された配置を要素へ反映する（ラベル要素は子の末尾に置かれ、子は親より後に描かれるため常に最上位）
- 座標変換は `ICoordTransform`（`Platform.Core` 提供）が担い、コントロールは変換関数を生成しない。
- 上位 AP へはイベントのみで通知し、上位 AP はポインター・キーイベントを直接処理しない。

## 4. 例外処理ポリシー

### 4.1 基本方針

- ライブラリは例外を原則伝播し、上位 AP（app）で処理する。
- UI レイヤー（app）ではユーザー操作起因の例外を各ハンドラで catch し、UI の応答性を維持する。
- ログは LoggerMessage ソース生成で出力する。操作イベントは Debug、Down 直前 / Up 直後の位置・ホイールは Trace で記録する。選択の判断に伴うログ定義はコア（名前空間 `EsUtil.Feature.MarqueeCore` の `MarqueeLogMessage`）が、ズーム要求のログ定義は組み込みの層（`MarqueeControl.UIBase` の `MarqueeWheelLogMessage`）が単一所有し、本プロジェクトはログ定義を持たない

### 4.2 本プロジェクト固有の例外処理

- イベントハンドラ内の例外は握りつぶさず、上位 AP へ伝播する（コントロールの内部状態を不正にしない）。
- 状態は MarqueeState に集約し、クリップ（`MarqueeOptions.ClampToDomainBounds` + `Port.Bounds`）により不正な選択矩形を防止する。

## 5. コンポーネント図

```mermaid
flowchart TB
    subgraph Host["上位AP（消費側）"]
        AP["上位AP<br/>イベント購読・座標ポート設定・配置"]
    end
    subgraph Core["Feature.MarqueeCore（UI 非依存）"]
        direction TB
        Fac["Marquee<br/>public sealed<br/>Marquee.cs<br/>（公開ファサード = 操作の唯一の窓口）"]
        Eff["IMarqueeEffects<br/>public interface<br/>（UI へ要求する効果の適用先）"]
        Ses["MarqueeSession<br/>internal sealed<br/>MarqueeSession.cs<br/>（状態管理クラス: 入力の解釈 ＋ 通知 ＋ 効果）"]
        Ntc["MarqueeNotice<br/>public<br/>（コア → UI の通知）"]
        Fbk["MarqueeFeedback<br/>internal<br/>（コア → UI の効果。適用はファサード）"]
        Stt["MarqueeState<br/>internal sealed<br/>MarqueeState.cs<br/>（状態保持 ＋ 更新ロジック）"]
        Geo["SelectionGeometry<br/>public static<br/>Selection/SelectionGeometry.cs"]
        Hdl["HitTarget<br/>public enum"]
        Hdl2["MarqueeInteractionState<br/>public enum"]
        Hdl3["MarqueeSnapshot<br/>public record<br/>（EsRect / EsPoint）"]
        Frm["Marquee（partial）<br/>internal の ResolveFrame<br/>Marquee.Content.cs<br/>（1 フレーム分の描画内容の解決）"]
    end
    subgraph DRC2["MarqueeControl プロジェクト (MarqueeControl)"]
        direction TB
        Ctl["MarqueeControl<br/>public sealed<br/>: Panel<br/>MarqueeControl.cs（公開 API ＋ 入力 ＋ 描画の起動 ＋ ラベル要素の配置）"]
        Cvs["AvaloniaCanvas<br/>public sealed<br/>Platform.AvaloniaUI<br/>（DrawingContext の IEsCanvas アダプタ）"]
        Lbl["ラベル要素の配置<br/>UI 層<br/>ArrangeOverride<br/>（MarqueeLabelPlan の配置矩形へ Border + TextBlock を置く）"]
        Evt["SelectionRectEventArgs<br/>MarqueePointEventArgs<br/>MarqueeRenderedEventArgs"]
    end
    subgraph Rnd2["MarqueeControl.UIBase プロジェクト（UI 非依存の描画層）"]
        direction TB
        Drw["MarqueeRenderer<br/>public sealed<br/>（MarqueeRendererBase の派生。色は MarqueePaint）"]
        Pnt["MarqueePaint<br/>public sealed record<br/>（ハンドル・マーカー・ラベル・装飾の色）"]
        Odr["MarqueeOuterRenderer<br/>public static<br/>（外側の装飾の描画）"]
        Idr["MarqueeInnerRenderer<br/>public static<br/>（内側の装飾の描画）"]
        Pln["MarqueeLabelPlan<br/>public static<br/>（実寸 → ラベル要素の配置）"]
    end

    AP -->|等イベント購読 / SetCoordinateTransform / RequestConfirm / RequestCancel| Ctl
    AP -->|Rendered（付加情報の描画）| Lbl
    Ctl -->|操作（Press / Move / Release / KeyDown / Wheel / SetSelection / ResolveContent）| Fac
    Ctl -.->|効果の適用| Eff
    Ctl -->|描画の起動（AvaloniaCanvas 経由）| Drw
    Ctl -.->|Paint（色の設定）| Pnt
    Pnt -.->|色| Drw
    Drw -.->|委譲| Odr
    Drw -.->|委譲| Idr
    Ctl -.->|配置指示| Pln
    Ctl --- Cvs
    Fac --> Ses
    Fac -->|効果の適用（IMarqueeEffects 経由）| Eff
    Fac -->|描画内容の解決| Frm
    Ctl --> Evt
    Ses --> Ntc
    Ses -->|効果（適用はファサード）| Fbk
    Ses --> Stt
    Stt --> Geo
    Stt --> Hdl
    Geo --> Hdl
```

## 6. コンポーネント一覧

### 6.1 MarqueeControl

- **分類**: 公開コントロール（`public sealed class : Panel`。XAML を持たずコードのみで子要素を構成する）
- **責務**:
  - ポインターイベント（Pressed / Moved / Released）をコア型へ変換してコア（ファサード）へ通知する
  - キーイベント（矢印キー・Escape / Enter）を `EsKey` へ変換してコアへ通知する。購読は **TopLevel の 1 経路のみ**（フォーカス非依存）
  - コアが要求する効果（カーソル操作・フォーカス・カーソル形状・再描画）を `IMarqueeEffects` 実装で受けて適用する（効果の選択と適用順はコアが単一所有）
  - コアからの通知を公開イベントへ転送する（判断はしない）
  - 描画（`Render`）を起動し、`Marquee.ResolveContent` が返した描画内容（`MarqueeDrawContent`）のうち**図形の項目**（外側/内側の装飾・ハンドル・マーカー）を描画層（`MarqueeControl.UIBase`）の各レンダラへ渡し（色は `Paint` の `MarqueePaint`）、**サイズ・座標ラベルはテキスト要素として配置する**（子の末尾に置かれ常に最上位）。描画のたびに `Rendered`（描画内容全体）を発火してホストへ渡す（ホストは付加情報を描ける）
  - ホイール操作を組み込みの層（`MarqueeControl.UIBase` の `MarqueeWheelInterpreter`）へ通知し、要求（ズーム / スクロール）を受け取る（Ctrl の判定・連携先への適用・ログは同層が単一所有する）
  - 祖先 ScrollViewer の ScrollChanged 購読（`ViewportChanged` の発行はコントロール化の層の `MarqueeViewportNotifier` へ委譲）
  - 設定（寸法・表示可否・対話パラメータ・色）・環境値（倍率）・文言プロバイダの保持とファサード（色は描画実装）への伝搬
- **公開API**:
- イベント 11 種: `SelectionStarted` / `SelectionChanged` / `SelectionCompleted` / `SelectionCancelled` / `LoupePositionMoved` / `ViewportChanged` / `ZoomRequested` / `ScrollRequested` / `ConfirmRequested` / `CancelRequested` / `Rendered`（描画完了。描画コンテキストと描画内容を渡し、ホストは付加情報（操作・情報ラベル）を描ける。サイズ・座標ラベルはコントロールがテキスト要素として表示する）。**生のポインター入力（Mouse 系）は公開しない**（ホストは自 UI の入力イベントを使う。押下状態は `CurrentSnapshot.IsMouseDown`）
  - StyledProperty 5 種: `Options`（不変の設定値 `MarqueeOptions`。寸法・表示可否・対話パラメータを内包） / `DecoOptions`（装飾の表示可否と寸法） / `Paint`（色の単一値 `MarqueePaint`） / `Scaling` / `DisplayScaleScaling`（環境値。XAML のバインディング・スタイルで設定できるよう依存関係プロパティとして公開する）。設定はコア（ファサード）または描画実装（`Paint`）へ委譲する（MewUI 版と同じ形）
  - プロパティ: `Log`（保持・`NullLogger` へのフォールバックはコアが単一所有） / `SizeLabelOverrideProvider`（外側の装飾のラベル文言。デリゲートのため設定値に含めない） / `HasSelection` / `SelectionRect`
  - メソッド: `SetCoordinateTransform(ICoordTransform?)` / `SetSelection` / `ResetSelection` / `RequestCancel` / `RequestConfirm` / `AttachScrollViewer`
- **構成（薄い層）**: 公開 API・入力の受付・効果の適用・通知の転送・描画の起動のみを集約し、判断（状態遷移・確定/キャンセル・スナップ・クリック検出・装飾の描画内容）と描画の手順はコアと描画層へ委譲する
- **依存**: Marquee（公開ファサード）/ IMarqueeEffects / MarqueeSession（間接） / MarqueeOptions / MarqueeNotice / MarqueeDrawContent （Feature.MarqueeCore）/ MarqueePaint / MarqueeRenderer / MarqueeOuterRenderer / MarqueeInnerRenderer / MarqueeLabelPlan（MarqueeControl.UIBase）/ ICoordTransform（Platform.Core）/ ICursorService（Platform.Native）/ AvaloniaCanvas・ScreenConverter（Platform.AvaloniaUI）

### 6.2 Marquee（公開ファサード・コア）

- **分類**: ファサードクラス（`public sealed class`・コア提供。適用パターン: Facade）
- **責務**: Marquee の**操作の唯一の窓口**。UI 層とホストは本型だけを参照する
  - 操作の提供: `Press` / `Move` / `Release` / `KeyDown` / `EndNudge` / `SetSelection` / `ResetSelection` / `RequestConfirm` / `RequestCancel` / `ResolveContent`
  - 設定・環境値の保持: `Options` / `DecoOptions` / `Scaling` / `DisplayScaleScaling` / `SizeLabelOverrideProvider` / `Log` / `SetCoordinateTransform`
  - 効果の適用: `IMarqueeEffects`（UI が実装）へフォーカス・再描画・カーソル形状・ポインターキャプチャ・座標変換を要求する（適用順の単一所有）
  - ホイールの解釈（`MarqueeWheelInterpreter.Handle`）: Ctrl = ズーム要求 / それ以外 = スクロール（連携先があれば適用、無ければ `ScrollRequested`）とログを組み込みの層へ委ね、要求を公開イベントへ転送する
  - 通知: `Notified`（状態機械の通知をそのまま転送する）
- **内部実装**: 状態機械 `MarqueeSession`（`internal`）を保持・利用する（フレーム解決は同じ型の `Marquee.Content.cs`（partial）にあり、`internal` の `ResolveFrame` として実装する）
- **可視性**: public（ファサードのみ。内部の状態機械とフレーム解決は `internal` で隠す）
- **備考**: 設定・効果の適用・通知の転送・描画内容の解決を 1 型に集約したため、UI 層は自 UI の公開プロパティ・公開イベント・描画 API へつなぐだけになる（ホイールの解釈は組み込みの層（`MarqueeControl.UIBase`）が単一所有する）

### 6.3 MarqueeState

- **分類**: 内部状態クラス（`internal sealed class`・コンポジション。コア提供）
- **責務**:
  - 選択矩形・ドラッグ状態・アクティブハンドル・nudge 状態（基点＋累積オフセット）の保持
  - 純粋ロジック: `HitTest` / `StartNewSelection` / `StartDrag` / `UpdateDrag` / `EndDrag` / `Nudge` / `GetInteractionState` / `CreateSnapshot`（幾何計算は `SelectionGeometry` へ委譲する）
  - クリップ（`MarqueeOptions.ClampToDomainBounds` + `Port.Bounds` による境界内制限。境界 Rect 生成は `GetBoundsRect` に一元化）
  - カーソル操作（`ICursorService`）
  - nudge 終了の共通化（`EndNudge`。`Reset` / `SetSelection` / `MarqueeSession` から共用し、カーソル再表示を委譲）
- **可視性**: internal（本コントロールはコアの公開ファサード（`Marquee`）と公開型（`MarqueeSnapshot`）のみを使う。`MarqueeState` / `MarqueeSession` はコアの内部実装であり参照しない）
- **依存**: SelectionGeometry / HitTarget / ICoordTransform（Platform.Core）/ ICursorService（Platform.Native）

### 6.4 SelectionGeometry

- **分類**: 静的クラス（`public static class`・コア提供）
- **責務**: 選択矩形の純粋な幾何計算（正規化・ハンドル位置・ヒットテスト・対角計算）
- **可視性**: public（コア提供）
- **依存**: UI 非依存（`Feature.MarqueeCore` の `EsPoint` / `EsRect`）

### 6.5 MarqueeRenderer（MarqueeControl.UIBase）

- **分類**: 公開 sealed クラス（`public sealed class`。`MarqueeRendererBase`（abstract）を継承する。所属は UI 非依存の描画層 `MarqueeControl.UIBase`）
- **責務**: 描画内容（`MarqueeDrawContent`）を描画ポート `IEsCanvas` へ写す（**描画手順は基底の `MarqueeRendererBase` が単一所有**し、本クラスは色を与える）
- **可視性**: public（UI 実装・独自コントロールの作者が参照する）
- **特徴**: 描画の設定（`MarqueePaint`）を必須引数で受け取り、`DrawShapes`（図形のみ。**テキストは描かない**）/ フォントサイズ（`SizeLabelFontSize` / `CoordLabelFontSize`）を提供する
- **依存**: Platform.Core（`IEsCanvas`）/ Feature.MarqueeCore（`MarqueeDrawContent` / `SelectionGeometry`）/ `MarqueePaint`（同層）

### 6.6 MarqueeOuterRenderer（MarqueeControl.UIBase）

- **分類**: 公開静的クラス（`public static class`。所属は UI 非依存の描画層 `MarqueeControl.UIBase`）
- **責務**: 選択矩形の外側に描く装飾のうち**非文字の項目**（暗転マスク・穴あけ枠）の描画
- **可視性**: public（UI 実装・独自コントロールの作者が参照する）
- **特徴**: 装飾（`Feature.MarqueeCore` の `Decoration` 名前空間）の `OuterDecoFrame` の各項目を描画ポート `IEsCanvas` へ写すだけで、判断を持たない（どの矩形を暗転するか・枠線を描くかは装飾層が単一所有する）。色は引数の `MarqueePaint`（`OverlayColor` / `OuterBorderColor`）から引く。**文字（サイズ・座標ラベル）は描かない**（コントロールがテキスト要素として表示する）
- **依存**: Platform.Core（`IEsCanvas`）/ Feature.MarqueeCore（`EsUtil.Feature.MarqueeCore.Decoration` の `OuterDecoFrame`）/ `MarqueePaint`（同層）

### 6.6.1 ラベルの要素配置（文字の表示）

- **分類**: `MarqueeControl` の内部実装（`ArrangeOverride` と `ApplyLabelElement` / `MeasureText`。独立した型ではない）
- **責務**: コアの描画内容のうち**ラベル**（サイズ・カーソル座標）をテキスト要素（`Border` + `TextBlock`）として配置する
- **可視性**: 非公開（ホストは `Rendered` の `Content` を参照できる）
- **特徴**: 文言・寸法・表示可否はコアが、色は描画層の `MarqueePaint` が単一所有し、実寸からの配置矩形（一覧と順序）は描画層（`MarqueeControl.UIBase` の `MarqueeLabelPlan`）が単一所有する。本実装は実寸測定（`FormattedText`）と要素への反映のみを持つ。要素の適用（色・余白・配置）とブラシの容量制限付きキャッシュは `EsLabelElementHost`（`Platform.AvaloniaUI.Labels`）が単一所有する
- **依存**: Avalonia（`FormattedText` / テキスト要素）/ MarqueeControl.UIBase（`MarqueeLabelPlan` / `MarqueePaint`）/ Feature.MarqueeCore（装飾と座標ラベルの描画契約）/ Platform.AvaloniaUI（色変換・ブラシキャッシュ）

### 6.7 MarqueeInnerRenderer（MarqueeControl.UIBase）

- **分類**: 公開静的クラス（`public static class`。所属は UI 非依存の描画層 `MarqueeControl.UIBase`）
- **責務**: 穴あけ矩形（選択矩形 / ハイライト矩形）の内側に描く装飾（塗り・内枠）の描画
- **可視性**: public（UI 実装・独自コントロールの作者が参照する）
- **特徴**: 装飾（`Feature.MarqueeCore` の `Decoration` 名前空間）の `InnerDecoFrame` の各項目を描画ポート `IEsCanvas` へ写すだけで、判断を持たない（塗るか・枠線を描くかは装飾層が単一所有する）。色は引数の `MarqueePaint`（`InnerFillColor` / `InnerBorderColor`）から引く
- **依存**: Platform.Core（`IEsCanvas`）/ Feature.MarqueeCore（`EsUtil.Feature.MarqueeCore.Decoration` の `InnerDecoFrame`）/ `MarqueePaint`（同層）

### 6.8 Marquee（コアのフレーム解決）

- **分類**: 公開ファサード（`public sealed partial class`）の**描画内容解決の部分**（`Marquee.Content.cs`。コア提供）
- **責務**: 1 フレーム分の描画内容（外側の装飾・内側の装飾・穴あけ矩形・ハンドル・始点マーカー・サイズラベル・カーソル座標ラベル・層の順序）を解決する（セッションの状態 + 設定 + 文言プロバイダ → `MarqueeDrawContent`）
- **可視性**: internal（`ResolveFrame` はファサード `Marquee.ResolveContent` からのみ使う。外部には公開しない）
- **特徴**: 選択矩形と選択の有無は**セッションが単一のソース**であり、設定値には選択の状態を持たせない。描くかどうかの判断（非表示の設定・選択なし・nudge 中・表示可否フラグ（`ShowOverlay` / `ShowOuterBorder` / `ShowInnerFill` / `ShowInnerBorder`）・穴あけ矩形なし）はすべて本部分が持つ

### 6.9 HitTarget

- **分類**: 公開 enum
- **責務**: ドラッグハンドルの種別（ヒットテスト・矩形変形・nudge・描画で共通利用）
- **可視性**: public（上位 AP がカーソル形状のマッピングに使用するため）

### 6.10 公開イベント引数

- `MarqueeInteractionState` enum（public）: NoSelection / StartPointNudging / Dragging / DraggingNudging / Selected / SelectedNudging
- `MarqueeSnapshot` readonly record struct（public）: State / SelectionRect / ActiveTarget / MousePosition / IsMouseDown / NudgeActive / HasSelection。座標は UI 非依存の `EsRect` / `EsPoint`（各 UI の境界で自前の型へ変換する）。値型のため通知のたびにヒープを確保しない
- `SelectionRectEventArgs`（public）: Rect（Avalonia）+ Snapshot
- `MarqueePointEventArgs`（public）: Point（Avalonia）+ Snapshot
- `MarqueeRenderedEventArgs`（public）: Context（`DrawingContext`。描画中のみ有効）+ Content（`MarqueeDrawContent`。図形と文字の描画契約）

## 7. 依存ルール

- 依存方向は**一方向の階層構造**です（層の全体像は [`src/README.md`](../README.md)）。コア内部は MarqueeControl → Marquee（ファサード） → MarqueeSession → MarqueeState → SelectionGeometry、描画は MarqueeControl → MarqueeRenderer（MarqueeControl.UIBase）→ IEsCanvas（Platform.Core）、組み込みの規則は MarqueeControl → MarqueeWheelInterpreter / MarqueeViewportNotifier（MarqueeControl.UIBase）。
- MarqueeControl は上位 AP に**依存しない**（再利用可能）。
- 判断（状態遷移・確定/キャンセル・スナップ・クリック検出）はコアの `Marquee`（内部の `MarqueeSession`）に集約し、UI 層は効果（`IMarqueeEffects`）を適用するだけにする。
- 描画の手順は描画層（`MarqueeControl.UIBase` の `MarqueeRenderer` / `MarqueeOuterRenderer` / `MarqueeInnerRenderer`）が単一所有し、UI はアダプタ（`AvaloniaCanvas`）とラベル要素の配置を担う。状態（選択矩形・nudge）は MarqueeState に集約し、装飾の**判断**は持たない（`Marquee.ResolveContent` が返す描画内容を渡すだけ）。
- **サイズ・カーソル座標ラベルはコントロールがテキスト要素として配置する**（配置矩形は描画層 `MarqueeControl.UIBase` の `MarqueeLabelPlan` が実寸から求める）。操作・情報ラベルはホストが `Rendered` のハンドラで描く。`Rendered` の契約は描画内容全体であるため、ホストは付加情報以外の上書きもできる。
- 座標変換は `ICoordTransform`（`Platform.Core` 提供）が担い、コントロールは変換関数を生成しない。
- 上位 AP へはイベントのみで通知し、上位 AP はポインター・キーイベントを直接処理しない。
- **キーイベントは 1 経路で受ける**（TopLevel のトンネル購読）。コントロール自身のキーハンドラを持たないため、同一キーの二重処理を防ぐ仕組み（フラグ等）が不要になる。トンネルはルートから下降するため、祖先のバブリングハンドラより先に処理済みにできる。

## 8. ライフサイクル

```mermaid
sequenceDiagram
    participant AP as 上位AP
    participant Ctl as MarqueeControl
    participant Fac as Marquee（コアのファサード）
    participant Ses as MarqueeSession
    participant Stt as MarqueeState

    AP->>Ctl: SetCoordinateTransform(ICoordTransform)
    Ctl->>Fac: SetCoordinateTransform（ポート保持と再描画要求）
    Fac->>Ses: SetPort
    Ses->>Stt: Port 設定

    AP->>Ctl: マウス押下（PointerPressed）
    Ctl->>Fac: Press(ドメイン座標, clickCount)
    Fac->>Ses: Press
    Ses->>Stt: HitTest / StartNewSelection / StartDrag
    Fac-->>Ctl: 効果（フォーカス・キャンセル・再描画）を IMarqueeEffects 経由で適用
    Fac-->>Ctl: 通知（SelectionStarted）
    Ctl-->>AP: SelectionStarted / SelectionChanged

    AP->>Ctl: マウス移動（PointerMoved）
    Ctl->>Fac: Move(ドメイン座標)
    Fac->>Ses: Move
    Ses->>Stt: UpdateDrag
    Ctl-->>AP: SelectionChanged / LoupePositionMoved

    AP->>Ctl: マウス解放（PointerReleased）
    Ctl->>Fac: Release(ドメイン座標)
    Fac->>Ses: Release
    Ses->>Stt: EndDrag（確定 or キャンセル判定）
    Ctl-->>AP: SelectionCompleted / SelectionCancelled

    AP->>Ctl: マウスホイール（PointerWheelChanged）
    Ctl->>Fac: Wheel(controlKey, shiftKey, 回転量, 位置)
    Fac-->>Ctl: ZoomRequested / ScrollRequested
    Ctl-->>AP: ZoomRequested / ScrollRequested

    AP->>Ctl: 矢印キー（KeyDown）
    Ctl->>Fac: KeyDown(EsKey, カーソル位置)
    Fac->>Ses: KeyDown
    Ses->>Stt: Nudge（整数加算）
    Ctl-->>AP: SelectionChanged / LoupePositionMoved
```

### 8.1 状態の初期化と破棄（所有権）

- コントロール自身が保持する状態（選択矩形・nudge 状態）は `ResetSelection()` で初期化します（上位 AP は状態をポーリングせず、イベントで把握）。
- 上位 AP はコントロールを Visual Tree から除去するだけで破棄できます（所有権は上位 AP にあり、明示的な解放 API は不要）。
- ビジュアルツリー切断時（`OnDetachedFromVisualTree`）は TopLevel キー購読・ScrollViewer 購読を解除し、コア（`Marquee.ResetSelection`）へ対話状態の初期化を要求します（nudge 中ならポインターを再表示して選択状態を初期化。「切断」自体は UI のライフサイクルであるため、コアに専用 API は持ちません）。

## 9. ログ

ログ定義はコア（`EsUtil.Feature.MarqueeCore.Logging.MarqueeLogMessage`、`Feature.MarqueeCore/src/Logging/MarqueeLogMessage.cs`）と、組み込みの層（`EsUtil.SaveEvidence.Marquee.UIBase.MarqueeWheelLogMessage`）が単一所有する
（入力の判断に伴うログは UI から出せず、後から出すと行の順序が変わるため）。UI 層に固有のログ定義は持たない
（通常ホイールのスクロールログは、スクロールの実行を連携 `ScrollViewer` または `ScrollRequested` 通知へ委ねる形になり、
本コントロール内に記録すべき判断が無くなったため削除した）。
各メソッドは `this ILogger` 第一引数で定義し、呼び出し側では logger 引数を省略する。未設定（null）の場合は `NullLogger` で無出力。
出力先・レベル制御はホストアプリケーションが所有する（定義のみ）。マウス移動の高頻度ログは行わない（Down 直前・Up 直後の位置のみ `Trace` で記録）。

### 9.1 ログ定義一覧

#### コア（`EsUtil.Feature.MarqueeCore.Logging.MarqueeLogMessage`）

| レベル | メソッド | 出力内容 |
| --- | --- | --- |
| `Debug` | `MarqueeLogMessage.LogMouseDown` | ポインター押下（位置 / クリック数 / 選択有無） |
| `Debug` | `MarqueeLogMessage.LogMouseUp` | ポインター解放（位置 / ドラッグ有無 / 選択有無） |
| `Debug` | `MarqueeLogMessage.LogDoubleClick` | ダブルクリック（位置 / 選択内側か） |
| `Debug` | `MarqueeLogMessage.LogDragStart` | ドラッグ開始（ハンドル / 位置）。新規選択も `handle=None` として記録する |
| `Debug` | `MarqueeLogMessage.LogKeyDown` | キー入力（キー名 / 処理有無） |
| `Debug` | `MarqueeLogMessage.LogConfirmRequested` | 確定要求（矩形） |
| `Debug` | `MarqueeLogMessage.LogCancelRequested` | キャンセル要求 |
| `Trace` | `MarqueeLogMessage.LogPositionBeforeDown` | Down 直前のマウス位置 |
| `Trace` | `MarqueeLogMessage.LogPositionAfterUp` | Up 直後のマウス位置 |
| `Debug` | `MarqueeLogMessage.LogSettingChanged` | 設定値変更（変更前後） |

#### 組み込みの層（`EsUtil.SaveEvidence.Marquee.UIBase.MarqueeWheelLogMessage`）

| レベル | メソッド | 出力内容 |
| --- | --- | --- |
| `Debug` | `MarqueeWheelLogMessage.LogZoomRequested` | Ctrl+ホイールによるズーム要求（拡大/縮小） |
