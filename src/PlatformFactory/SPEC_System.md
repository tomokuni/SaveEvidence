# PlatformFactory システム仕様書（SPEC_System）

## 目次

- [PlatformFactory システム仕様書（SPEC_System）](#platformfactory-システム仕様書spec_system)
  - [目次](#目次)
  - [1. システム概要](#1-システム概要)
  - [2. 技術スタック](#2-技術スタック)
  - [3. アーキテクチャ](#3-アーキテクチャ)
  - [4. 例外処理ポリシー](#4-例外処理ポリシー)
  - [5. コンポーネント図](#5-コンポーネント図)
  - [6. コンポーネント一覧](#6-コンポーネント一覧)
    - [6.1 PlatformFactory](#61-platformfactory)
    - [6.2 各インターフェース（抽象）](#62-各インターフェース抽象)
    - [6.3 CoordinatePort](#63-coordinateport)
    - [6.4 ScreenConverter](#64-screenconverter)
    - [6.5 Watchdog](#65-watchdog)
    - [6.6 その他の公開型](#66-その他の公開型)
    - [6.7 内部実装（Win 系）](#67-内部実装win-系)
    - [6.8 内部実装（NoOp 系）](#68-内部実装noop-系)
    - [6.9 内部ユーティリティ](#69-内部ユーティリティ)
  - [7. 依存ルール](#7-依存ルール)
  - [8. ライフサイクル](#8-ライフサイクル)

---

## 1. システム概要

SaveEvidence は、Windows デスクトップ上の画面をキャプチャし、プレビュー・編集・保存するための Avalonia UI アプリケーションです（.NET 10.0 / C# 14 / MVVM）。

本プロジェクト（PlatformFactory）は、このシステムのうち**プラットフォーム抽象層**を担います。実行時プラットフォーム（Windows / 他環境）に応じた既定実装（カーソル操作・要素検出・キャプチャ・ウィンドウ操作）の生成と、座標変換・画面キャプチャ・要素検出の抽象化 API を提供します。他プロジェクト（DragRectControl / SnipCaptureUI / app）からインターフェース経由で利用されます。

## 2. 技術スタック

| 項目 | 内容 |
| --- | --- |
| **プロジェクト** | `PlatformFactory` |
| **ルート名前空間** | `Estable.AvaloniaUI.Platform` |
| **言語** | C# 14（.NET 10.0） |
| **UI フレームワーク** | Avalonia UI 12.1（Bitmap / PixelPoint / PixelRect / TopLevel 等の標準型） |
| **ターゲットフレームワーク** | net10.0 |
| **ビルドツール** | MSBuild / .NET SDK 10.0 |
| **ログ** | Microsoft.Extensions.Logging.Abstractions（LoggerMessage ソース生成） |
| **画像処理** | System.Drawing.Common（Win キャプチャ実装） |
| **テスト** | xUnit v3 / Avalonia.Headless.XUnit |
| **依存プロジェクト** | なし（他プロジェクトから参照される基盤ライブラリ） |

## 3. アーキテクチャ

### 3.1 レイヤー構造

```text
公開ファクトリ（PlatformFactory）
  → 各インターフェース（抽象: ICursorService / IElementFinder / ICaptureService / IWindowService 等）
    → Win 実装 / NoOp 実装（internal）
```

### 3.2 依存の方向

- 依存は**一方向の階層構造**（公開ファクトリ → インターフェース → Win / NoOp 実装）。
- 上位 AP はインターフェース経由で利用し、具象型（Win / NoOp）に依存しない（DIP）。
- 公開 API はプラットフォーム非依存型のみを使用し、Win32 ハンドル・System.Drawing 型を公開しない（カプセル化）。
- プラットフォーム固有実装は本ファクトリが唯一の生成経路となるため、internal に隠蔽できる（公開 API 最小化）。

## 4. 例外処理ポリシー

### 4.1 基本方針

- ライブラリは例外を原則伝播し、上位 AP（app）で処理する。
- UI レイヤー（app）ではユーザー操作起因の例外を各ハンドラで catch し、UI の応答性を維持する。
- ログは LoggerMessage ソース生成（`PlatformLog.cs` / `WinCaptureService.Log.cs`）で出力する。高頻度イベントは Debug、障害は Error / Warning で記録する。

### 4.2 本プロジェクト固有の例外処理

- キャプチャはバックグラウンド + タイムアウト（上位 AP が制御）で実行し、UI スレッドをブロックしない。
- 他環境（Linux / macOS）では NoOp 実装（安全なフォールバック）で例外なく動作する（Win32 DLL 依存を回避）。
- UI スレッド完全ハング時は Watchdog がバックグラウンドからオーバーレイを退避する（最終保険）。

## 5. コンポーネント図

```mermaid
flowchart TB
    subgraph PF["PlatformFactory プロジェクト (PlatformFactory)"]
        Fac["PlatformFactory<br/>public static<br/>PlatformFactory.cs<br/>（Factory Method パターン）"]
        ICr["ICursorService<br/>public interface"]
        IEl["IElementFinder<br/>public interface"]
        ICg["ICursorOverrideGuard<br/>public interface"]
        ICp["ICaptureService<br/>public interface"]
        IWd["IWindowService<br/>public interface"]
        IGH["IGlobalHotKeyService&lt;T&gt;<br/>public interface"]
        ICd["ICoordinatePort<br/>public interface"]
        Cpt["CoordinatePort<br/>public sealed<br/>Coordinate/CoordinatePort.cs<br/>（Func 委譲の具象実装）"]
        Scn["ScreenConverter<br/>public static<br/>Coordinate/ScreenConverter.cs"]
        Wdg["Watchdog<br/>public sealed<br/>Watchdog.cs"]
        Efr["ElementFinderResults<br/>public readonly record struct"]
        Bmx["BitmapExtensions<br/>public static<br/>Imaging/BitmapExtensions.cs"]
        Win["Win 実装群<br/>internal sealed<br/>Win/（Win32 API・P-Invoke 集約）"]
        NoOp["NoOp 実装群<br/>internal sealed<br/>NoOp/（シングルトン）"]
        Mth["CoordinateMath<br/>internal static<br/>Coordinate/CoordinateMath.cs"]
    end
    subgraph Dep["外部依存"]
        Av["Avalonia 12.1<br/>Bitmap / PixelPoint / PixelRect / TopLevel"]
        Lg["Microsoft.Extensions.Logging.Abstractions"]
        Sd["System.Drawing.Common"]
    end
    subgraph Host["上位AP（消費側）"]
        DR["DragRectControl<br/>ICoordinatePort / ICursorService / ScreenConverter"]
        SC["SnipCaptureUI<br/>ICaptureService / IWindowService / IElementFinder / Watchdog / CoordinatePort"]
    end

    Fac --> ICr
    Fac --> IEl
    Fac --> ICg
    Fac --> ICp
    Fac --> IWd
    Fac --> IGH
    ICr --> Win
    IEl --> Win
    ICg --> Win
    ICp --> Win
    IWd --> Win
    IGH --> Win
    ICr --> NoOp
    IEl --> NoOp
    ICg --> NoOp
    ICp --> NoOp
    IWd --> NoOp
    IGH --> NoOp
    ICd --> Cpt
    Cpt --> Mth
    Wdg --> IWd
    Scn --> ICd
    Win --> Av
    Win --> Sd
    DR -->|"ファクトリ / インターフェース / 座標ポート"| Fac
    SC -->|"ファクトリ / インターフェース / Watchdog / CoordinatePort"| Fac
```

## 6. コンポーネント一覧

### 6.1 PlatformFactory

- **分類**: 公開静的ファクトリ（`public static class`・Factory Method パターン）
- **責務**:
  - 実行時プラットフォーム（Windows / 他環境）に応じた既定実装の生成
  - `OperatingSystem.IsWindows()` によるプラットフォーム判定の一元化（各プロジェクトの重複分岐を排除・DRY）
  - 生成結果は各インターフェース（抽象）で返し、具象型への依存を排除（DIP）
- **公開メソッド**:
  - `CreateCursorService()` → `ICursorService`（Windows: `WinCursorService` / 他: `NoOpCursorService.Instance`）
  - `CreateElementFinder()` → `IElementFinder`（Windows: `WinElementFinder` / 他: `NoOpElementFinder.Instance`）
  - `CreateCursorOverrideGuard()` → `ICursorOverrideGuard`（Windows: `WinCursorOverrideGuard` / 他: `NoOpCursorOverrideGuard`）
  - `CreateCaptureService(ILogger? logger = null)` → `ICaptureService`（Windows: `WinCaptureService` / 他: `NoOpCaptureService.Instance`）
  - `CreateWindowService()` → `IWindowService`（Windows: `WinWindowService` / 他: `NoOpWindowService.Instance`）
  - `CreateGlobalHotKeyService<T>() where T : notnull` → `IGlobalHotKeyService<T>`（Windows: `WinGlobalHotKeyService<T>` / 他: `NoOpGlobalHotKeyService<T>`）
- **可視性**: public
- **依存**: 各インターフェース / Win 実装 / NoOp 実装

### 6.2 各インターフェース（抽象）

| コンポーネント | メンバー | 説明 |
| --- | --- | --- |
| `ICursorService` | `GetCursorScreenPoint()` / `SetCursorScreenPoint(PixelPoint)` / `ShowCursor(bool)` | カーソル位置の取得・移動・表示切替 |
| `IElementFinder` | `FindWindowUnderCursor(PixelPoint)` / `FindScreenUnderCursor(PixelPoint)` / `ListScreens()` / `ListWindows()` | 指定位置のウィンドウ / スクリーン検出・一覧取得 |
| `ICursorOverrideGuard` | `IsAttached` / `Attach(TopLevel)` | カーソル上書き抑止（WndProc サブクラス化。アタッチ中は常に抑止） |
| `ICaptureService` | `CaptureArea(PixelRect)` / `CaptureVirtualScreen(out PixelPoint)` | 画面領域・仮想スクリーンのキャプチャ |
| `IWindowService` | `HideFromTaskSwitcher(IPlatformHandle)` / `SetCaptureExclusion(IPlatformHandle, bool)` / `EvacuateWindow(IPlatformHandle)` | タスクスイッチャー非表示・キャプチャ除外・退避 |
| `IGlobalHotKeyService<T>` | `Initialize(IntPtr)` / `RegisterAll(...)` / `UnregisterAll()` / `event Action<T>? HotKeyPressed` | グローバルホットキーの登録・WM_HOTKEY 受信・アクション配信 |
| `ICoordinatePort` | `Forward(Point)` / `Inverse(Point)` / `Bounds`（`Size?`） | 座標系 A ↔ B の変換ポート |

- **可視性**: public
- **依存**: Avalonia 標準型（`Bitmap` / `PixelPoint` / `PixelRect` / `TopLevel` / `IPlatformHandle`）

### 6.3 CoordinatePort

- **分類**: 公開座標変換ポート（`public sealed class : ICoordinatePort`）
- **責務**: `Func` 委譲による座標系 A ↔ B の変換と境界の提供
- **公開メンバー**:
  - `Identity`（静的プロパティ）: 等価変換の既定ポート
  - `Compose(outer, inner)`（静的メソッド）: 2 ポート合成（inner 先行 → outer 後続。Bounds は outer 優先）
  - `ScrollOffset(Func<Vector>)`（静的メソッド）: スクロールオフセット平行移動
  - `Scale(double)`（静的メソッド）: 原点基準の等方スケーリング（factor ≤ 0 は 1.0 扱い・固定値）
  - `DomainScale(Func<double>)`（静的メソッド）: ドメイン = ローカル DIP × 倍率（呼び出し毎評価）
  - `ImageLayout(Func<Size>, Size, Func<bool>?)`（静的メソッド）: 画像レイアウト（レターボックス・スケール）の動的ポート
  - `ImageLayoutScaleX(Func<Size>, Size, Func<bool>?)`（静的メソッド）: 画像レイアウトの X スケール（ズーム率導出用）
  - コンストラクタ: `CoordinatePort(Func<Point, Point> forward, Func<Point, Point> inverse, Func<Size?> bounds)`
  - `Forward` / `Inverse` / `Bounds`: `ICoordinatePort` の実装
- **可視性**: public
- **依存**: `CoordinateMath`（internal。画像レイアウト計算の委譲）

### 6.4 ScreenConverter

- **分類**: 公開静的クラス（`public static class`）
- **責務**: コントロールローカル DIP ↔ 画面物理 Pixel の変換と変換ポートの生成
- **公開メソッド**: `LocalDipToScreen(Visual, Point)` / `ScreenToLocalDip(Visual, PixelPoint)` / `CreateVisualDipPort(Visual)`
- **可視性**: public
- **依存**: `ICoordinatePort`

### 6.5 Watchdog

- **分類**: 公開クラス（`public sealed class`・Primary Constructor `Watchdog(ILogger?, IWindowService)`）
- **責務**: UI スレッド完全ハング時の最終保険。バックグラウンドスレッドから `IWindowService.EvacuateWindow` でオーバーレイを退避
- **公開メンバー**: `Start(TopLevel)` / `Stop()` / `Pulse()`
- **可視性**: public
- **依存**: `IWindowService` / `PlatformLog`（internal・ログ定義）

### 6.6 その他の公開型

| コンポーネント | 説明 |
| --- | --- |
| `ElementFinderResults` record struct | 検出要素の結果（`Bounds` / `IsDetected` / `Title` / `Handle` / `ClassName`） |
| `BitmapExtensions.CopyRegionWithPadding` / `ExtractRegion` | 範囲外許容の領域コピー / 矩形切り出し |
| `ColorExtensions.FromAColor` | C# 14 静的拡張メンバー。基準色（名前付き色等）とアルファ値から色を生成（RGB 直指定の可読性問題を解消） |

### 6.7 内部実装（Win 系）

| コンポーネント | 説明 |
| --- | --- |
| `Win\WinCaptureService` | CopyFromScreen による画面・仮想スクリーンキャプチャ（System.Drawing 内包） |
| `Win\WinCursorService` | Win32（GetCursorPos / SetCursorPos / ShowCursor）によるカーソル操作 |
| `Win\WinElementFinder` | Win32（EnumWindows / EnumDisplayMonitors）による要素検出（自ウィンドウ除外を自己解決） |
| `Win\WinWindowService` | タスクスイッチャー非表示 / キャプチャ除外 / 退避 |
| `Win\WinCursorOverrideGuard` | WndProc サブクラス化で WM_SETCURSOR 抑止 |
| `Win\WinGlobalHotKeyService<T>` | RegisterHotKey / WndProc サブクラス化で WM_HOTKEY を受信し、アクションを配信 |
| `Win\WinNative` | P/Invoke 集約（struct / delegate / const / Win32 API） |

- **可視性**: すべて internal（本ファクトリが唯一の生成経路）

### 6.8 内部実装（NoOp 系）

| コンポーネント | 説明 |
| --- | --- |
| `NoOp\NoOpCaptureService` | 常に null を返す（`Instance` シングルトン） |
| `NoOp\NoOpCursorOverrideGuard` | 抑止なし（IsAttached = false のまま） |
| `NoOp\NoOpCursorService` | 何もしない（Get = default / Set = 無視） |
| `NoOp\NoOpElementFinder` | 常に未検出（`Instance` シングルトン） |
| `NoOp\NoOpWindowService` | 何もしない（`SetCaptureExclusion` のみ false） |
| `NoOp\NoOpGlobalHotKeyService<T>` | ホットキー機能なし（HotKeyPressed は発生しない） |

- **可視性**: すべて internal（他環境での安全なフォールバック。Win32 DLL 依存を回避）

### 6.9 内部ユーティリティ

| コンポーネント | 説明 |
| --- | --- |
| `CoordinateMath` | 画像レイアウト計算の一元化（`CalculateUniformLayout` / `ComputeImageRenderInfo`）。`CoordinatePort.ImageLayout` から委譲される |
| `PlatformLog` | LoggerMessage ソース生成によるログ定義集約（`LogWatchdogEvacuate` 等） |

- **可視性**: すべて internal

## 7. 依存ルール

- 依存方向は**一方向の階層構造**（公開ファクトリ → インターフェース → Win / NoOp 実装）。
- 上位 AP（DragRectControl / SnipCaptureUI 等）は本ライブラリの**インターフェース経由**で利用し、具象型（Win / NoOp）に依存しない（DIP）。
- 公開 API はプラットフォーム非依存型（`Bitmap` / `PixelPoint` / `PixelRect` / `IPlatformHandle`）のみを使用し、Win32 ハンドル・System.Drawing 型を公開しない（カプセル化）。
- プラットフォーム固有実装は本ファクトリが唯一の生成経路となるため、internal に隠蔽できる（公開 API 最小化）。

## 8. ライフサイクル

```mermaid
sequenceDiagram
    participant AP as 上位AP（SnipCaptureUI 等）
    participant Fac as PlatformFactory
    participant If as 各インターフェース
    participant Impl as Win / NoOp 実装

    AP->>Fac: CreateCaptureService() / CreateWindowService() / ...
    Fac->>If: プラットフォーム判定（OperatingSystem.IsWindows）
    Fac->>Impl: 対応する実装を生成
    Fac-->>AP: インターフェースで返却（具象型は見えない）

    AP->>If: CaptureArea / SetCaptureExclusion / ...
    If->>Impl: Win32 API / フォールバックへ委譲
    Impl-->>AP: 結果を返却
```
