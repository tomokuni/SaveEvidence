# FadeMaskDragRectControl システム仕様書（SPEC_System）

## 目次

- [FadeMaskDragRectControl システム仕様書（SPEC\_System）](#fademaskdragrectcontrol-システム仕様書spec_system)
  - [目次](#目次)
  - [1. システム概要](#1-システム概要)
  - [2. 技術スタック](#2-技術スタック)
  - [3. アーキテクチャ](#3-アーキテクチャ)
    - [3.1 レイヤー構造](#31-レイヤー構造)
    - [3.2 依存方向](#32-依存方向)
  - [4. 例外処理ポリシー](#4-例外処理ポリシー)
    - [4.1 基本方針](#41-基本方針)
    - [4.2 本プロジェクト固有の例外処理](#42-本プロジェクト固有の例外処理)
  - [5. コンポーネント図](#5-コンポーネント図)
  - [6. コンポーネント一覧](#6-コンポーネント一覧)
    - [6.1 FadeMaskDragRectControl](#61-fademaskdragrectcontrol)
    - [6.2 FadeMaskDragRectMode](#62-fademaskdragrectmode)
    - [6.3 FadeMaskDragRectOptions（コア）](#63-fademaskdragrectoptionsコア)
    - [6.4 内包コントロール（外部プロジェクト）](#64-内包コントロール外部プロジェクト)
    - [6.5 公開イベント引数](#65-公開イベント引数)
  - [7. 依存ルール](#7-依存ルール)
  - [8. ライフサイクル](#8-ライフサイクル)
  - [9. 配線（状態同期の流れ）](#9-配線状態同期の流れ)

---

## 1. システム概要

SaveEvidence は、Windows デスクトップ上の画面をキャプチャし、プレビュー・編集・保存するための Avalonia UI アプリケーションです（.NET 10.0 / C# 14 / MVVM）。

本プロジェクト（FadeMaskDragRect）は、このシステムのうち**領域選択の複合コントロール**（Marquee（外側の装飾を含む）+ Loupe の組み立て・配線・状態同期）を担います。 Facade / Composite パターンでホストへ統一インターフェースを提供し、CaptureSessionControl.AvaloniaUI のセッション（OverlayWindow / CropSession）から使用されます。

## 2. 技術スタック

| 項目 | 内容 |
| --- | --- |
| **プロジェクト** | `FadeMaskDragRectControl` |
| **ルート名前空間** | `FadeMaskDragRect` |
| **言語** | C# 14（.NET 10.0） |
| **UI フレームワーク** | Avalonia UI 12.1（Panel / Control / DrawingContext） |
| **ターゲットフレームワーク** | net10.0 |
| **ビルドツール** | MSBuild / .NET SDK 10.0 |
| **ログ** | Microsoft.Extensions.Logging.Abstractions（LoggerMessage ソース生成） |
| **テスト** | xUnit v3 / Avalonia.Headless.XUnit |
| **依存プロジェクト** | MarqueeControl / LoupeControl / Platform.AvaloniaUI |

外観（描画にだけ必要な設定）の型は内包コントロールの提示層（`Feature.Marquee.Presenter` の `MarqueeAppearance` / `Feature.Loupe.Presenter` の `LoupeAppearance`）が単一所有します。

## 3. アーキテクチャ

### 3.1 レイヤー構造

```text
公開複合コントロール（FadeMaskDragRectControl）
  → 内包コントロール（MarqueeControl / LoupeControl）+ Platform.AvaloniaUI
```

### 3.2 依存方向

- 依存は**一方向の階層構造**（FadeMaskDragRectControl → MarqueeControl / LoupeControl / Platform.AvaloniaUI）。 逆依存なし。
- 内包コントロールは上位 AP の設定型（CaptureSettings 等）に依存しない（コアの `FadeMaskDragRectOptions` が変換先の境界）。
- ホスト（上位 AP）は FadeMaskDragRectControl の公開 API のみを使用する（内包コントロールへの直接操作はテスト用アクセサ経由のみ）。
- 配線・状態同期は本コントロールに集約し、セッションは配線コードを持たない（DRY）。

## 4. 例外処理ポリシー

### 4.1 基本方針

- ライブラリは例外を原則伝播し、上位 AP（app）で処理する。
- UI レイヤー（app）ではユーザー操作起因の例外を各ハンドラで catch し、UI の応答性を維持する。
- ログは内包コントロール（Marquee / Loupe）の LoggerMessage 定義に委譲する（本プロジェクトは配線のみ）。

### 4.2 本プロジェクト固有の例外処理

- 描画・配線専用の複合コントロールのため例外処理は原則不要（状態は公開プロパティで完全に制御可能）。
- 内包コントロールのイベント（SelectionChanged / ConfirmRequested 等）は中継のみで、例外は握りつぶさない。

## 5. コンポーネント図

```mermaid
flowchart TB
    subgraph Host["上位AP（消費側）"]
        AP["CropSession / OverlayWindow<br/>ライフサイクル・モード固有処理"]
    end
    subgraph FDR["FadeMaskDragRect プロジェクト (FadeMaskDragRect)"]
        direction TB
        Ctl["FadeMaskDragRectControl<br/>public sealed<br/>: Panel<br/>FadeMaskDragRectControl.cs（公開 API ＋ 配線 ＋ レイアウト）"]
        Args["FadeMaskDragRectSelectionEventArgs<br/>FadeMaskDragRectPointEventArgs<br/>public sealed record"]
    end
    subgraph Core["CaptureSessionControl.Core（UI 非依存）"]
        direction TB
        Mode["FadeMaskDragRectMode<br/>public enum"]
        Modes["FadeMaskDragRectModes<br/>internal static<br/>（モード → 状態の解決）"]
        State["FadeMaskDragRectState<br/>internal readonly record struct<br/>（表示可否）"]
        Opt["FadeMaskDragRectOptions<br/>public sealed record<br/>（設定の単一値）"]
        Sel["FadeMaskDragRectSelection<br/>internal static<br/>（選択矩形の可否判定）"]
        Ov["FadeMaskDragRectOverlay<br/>internal static<br/>（選択矩形の破線描画）"]
    end
    subgraph Dep["内包コントロール（外部プロジェクト）"]
        direction TB
        DR["MarqueeControl<br/>EsUtil.SaveEvidence.Marquee.AvaloniaUI<br/>（選択矩形の外側の装飾（暗転マスク・外枠・ハイライト）も描画）"]

        LC["LoupeControl<br/>EsUtil.SaveEvidence.Loupe.AvaloniaUI"]
    end

    AP -->|"Mode / Options / SetCoordTransform / イベント購読"| Ctl
    Ctl -->|"ConfirmRequested / CancelRequested"| AP
    Ctl --> DR

    Ctl --> LC
    Ctl ..> Modes : モードの解釈
    Ctl ..> Opt : 設定の分配（枠線の共用）
    Ctl ..> Sel : 選択矩形の可否
    Ctl ..> Ov : 破線描画
```

## 6. コンポーネント一覧

### 6.1 FadeMaskDragRectControl

| 項目 | 内容 |
| --- | --- |
| ファイル | `FadeMaskDragRectControl.cs` |
| 基底クラス | `Avalonia.Controls.Panel` |
| 可視性 | public sealed |
| 責務 | 内包コントロール（Marquee / Loupe）の生成・配置・配線・状態同期と、ホストへの統一インターフェース提供 |
| 内部アクセサ（internal） | `Loupe` / `Marquee`（自身の Tests のみアクセス可。 公開型のカプセル化のため internal 化） |
| 公開プロパティ（外観） | `MarqueeAppearance` / `LoupeAppearance`（描画にだけ必要な外観の単一値。 内包コントロールへそのまま委譲する） |
| 内部フィールド | `_marquee`（MarqueeControl）/ `_loupeControl`（LoupeControl） |

### 6.2 FadeMaskDragRectMode

| 項目 | 内容 |
| --- | --- |
| ファイル | `CaptureSessionControl.Core/src/FadeMaskDragRectMode.cs`（UI 非依存コア。 `CaptureSessionControl.Core`） |
| 種類 | public enum |
| メンバー | `Crop` / `AreaSelect` / `ScreenSelect` / `WindowSelect` |
| 責務 | 子コントロールの可視性（Marquee / Loupe）・全面暗転・破線の切替指示 |
| 備考 | モードから描画設定への写像はコアの `FadeMaskDragRectModes` が単一所有し、Avalonia / MewUI で共有する |

### 6.3 FadeMaskDragRectOptions（コア）

| 項目 | 内容 |
| --- | --- |
| ファイル | `CaptureSessionControl.Core/src/FadeMaskDragRectOptions.cs`（UI 非依存コア） |
| 種類 | public sealed record |
| 備考 | 既定値は各内包コントロールの既定値を参照する（UI 側に既定値の写しを置かない） |

### 6.4 内包コントロール（外部プロジェクト）

| コンポーネント | プロジェクト | 責務 |
| --- | --- | --- |
| `MarqueeControl` | MarqueeControl | ドラッグ選択・リサイズ・nudge（ドメイン座標 + 座標変換ポート）と、選択矩形の**外側に描く装飾**（暗転マスク・外枠・ハイライト）の描画。 **サイズラベルとカーソル座標ラベルはテキスト要素として配置し**、操作・情報ラベルは本クラス（ホスト）が `Options.Labels` に従って描く |
| `LoupeControl` | LoupeControl | 拡大鏡（中心 = ドメイン座標、表示位置 = 座標変換ポートから自己計算）。 **中心座標ラベルはテキスト要素として配置する** |

### 6.5 公開イベント引数

| 型 | プロジェクト | 用途 |
| --- | --- | --- |
| `FadeMaskDragRectSelectionEventArgs` | 本プロジェクト | 確定要求イベントの引数（`SelectionDomainRect: Rect`（ドメイン座標）） |
| `FadeMaskDragRectPointEventArgs` | 本プロジェクト | ルーペ位置イベントの引数（`Position: Point`（ドメイン座標）） |
| `FadeMaskDragRectMode` | EsUtil.SaveEvidence.CaptureSession（コア） | 動作モード（描画モード・可視性・全面暗転の切替） |
| `FadeMaskDragRectOptions` | EsUtil.SaveEvidence.CaptureSession（コア） | 設定の単一値（選択矩形 / 暗転マスク / ルーペ / 選択矩形破線。 **色は含まない**） |
| `MarqueeAppearance` / `LoupeAppearance` | EsUtil.Feature.Marquee.Presenter.Setting / EsUtil.Feature.Loupe.Presenter.Setting（提示層） | 描画にだけ必要な外観の単一値（内包コントロールへ委譲する） |
| `FadeMaskDragRectChildValues` / `FadeMaskHostLabelRenderer` | EsUtil.SaveEvidence.FadeMaskDragRect.UIBase（UI 間の共有層） | 設定とモードから子コントロールへ適用する値の解決と、付加情報ラベルの描画 |
| `ICoordTransform` / `CoordTransform` | Platform.Core | ドメイン ↔ DIP の座標変換ポート |

**公開型のカプセル化**: 公開 API は内包コントロール（MarqueeControl / LoupeControl）の型を参照しない（イベント引数は独自型・状態はプロパティで提供）。

## 7. 依存ルール

- `FadeMaskDragRectControl` → `FadeMaskDragRectControl.UIBase` / `MarqueeControl` / `LoupeControl` / `Platform.AvaloniaUI` の**一方向依存**（逆依存なし）。 値の解決は `FadeMaskDragRectChildValues`（UIBase）が、付加情報ラベルの描画は `FadeMaskHostLabelRenderer`（UIBase）が単一所有し、本コントロールは代入と描画を行う。 描画にだけ必要な外観（`MarqueeAppearance` / `LoupeAppearance`）は内包コントロールの提示層が単一所有し、本コントロールはプロパティで転送するだけである（コアの設定値には色・表示可否を含めない）。
- 内包コントロールは上位 AP の設定型（CaptureSettings 等）に依存しない（コアの `FadeMaskDragRectOptions` が変換先の境界）。
- ホスト（上位 AP）は `FadeMaskDragRectControl` の公開 API のみを使用する（内包コントロールへの直接アクセスは internal 化され、外部からは不可能）。

## 8. ライフサイクル

```mermaid
sequenceDiagram
    participant H as ホスト（CropSession / OverlayWindow）
    participant C as FadeMaskDragRectControl
    participant DR as MarqueeControl
    
    participant LC as LoupeControl

    H->>C: Mode 設定
    H->>C: Options = settings（コアのマッパーで変換した設定値。色は含まない）
    H->>C: MarqueeAppearance / LoupeAppearance（描画にだけ必要な外観の単一値）
    H->>C: SetCoordTransform(port)
    H->>C: SetLoupeSource(bitmap)
    H->>C: Visual Tree へ配置
    C-->>LC: RequestRender（破線の色・表示可否は Loupe のプロパティに現れないため再生成を要求）
    DR-->>C: SelectionChanged（選択矩形）
    C-->>FM: LogicalSelectionRect / HasSelection 同期 + 再描画
    C-->>LC: Rendered イベントで選択矩形破線を描画（中心座標ラベルはコントロールが表示済み）
    DR-->>C: Rendered イベントで付加情報（操作・情報ラベル）を描画（UIBase のレンダラへ委譲。サイズ・座標ラベルはコントロールが表示済み）
    DR-->>C: LoupePositionMoved（中心）
    C-->>LC: CenterPoint 更新 + 表示開始
    DR-->>C: ConfirmRequested
    C-->>H: ConfirmRequested（中継）
    H->>C: 配置解除（後片付け完了）
```

## 9. 配線（状態同期の流れ）

| 内包イベント | 複合コントロールの処理 | 同期先 |
| --- | --- | --- |
| `SelectionChanged` | ルーペの再描画を要求（`RequestRender`） | 選択矩形と外側の装飾は `MarqueeControl` がセッションから直接読んで描画する（本クラスは同期しない）。 ルーペの選択矩形破線は Rendered イベント経由で描画し、描画の可否はコアの `ShowSelectionOverlay` が単一所有する（UI は派生値を保持しない） |
| `Rendered`（選択矩形コントロール） | 付加情報（操作・情報ラベル）を描く | 描画内容全体（`Content`）を受け取り、付加情報を描く。 **サイズ・座標ラベルはコントロールがテキスト要素として表示する**（文言はコアが、配置矩形は描画層 `Feature.Marquee.Presenter` の `MarqueePresenter`（`LabelPlan` partial） が単一所有する）。 付加情報の積み上げ（位置・表示可否）は `FadeMaskHostLabelRenderer`（UIBase）が単一所有し、本コントロールは描画ポートを用意して文言を渡すだけである |
| `Rendered`（ルーペ） | 破線を描く（中心座標ラベルはコントロールが表示する） | 選択矩形の破線。 ラベルの寸法・配置はコアが単一所有する |
| 表示の変化（ホスト通知） | `RefreshOverlay()`（再描画 + ルーペ位置再計算） | Marquee 再描画 + Loupe（RefreshPosition）。 選択矩形は表示の変化で不変のため再同期しない |
| `SelectionStarted` | （購読しない） | セッションが自身の選択を初期化するため同期不要（外側の装飾はセッションの状態をそのまま読む） |
| `SelectionCancelled` | （購読しない） | 同上 |
| `LoupePositionMoved` | `CenterPoint` 更新 + 表示可否の再判定 | Loupe + ホスト通知（`LoupePositionMoved`） |
| `ConfirmRequested` | ホストへ中継 | ホスト |
| `CancelRequested` | ホストへ中継 | ホスト |

**ホイール操作と表示の変化の追従は本クラスの配線ではありません**（コントロール化を前提とした処理）。
ホイールはホストが受け取ってビューポート（ズーム・パン）へ転送し、表示の変化はホストが検知して `RefreshOverlay()` を呼びます。
