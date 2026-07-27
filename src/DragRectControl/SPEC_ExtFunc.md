# DragRectControl 外部機能仕様書

## 目次

- [1. 文書概要](#1-文書概要)
- [2. 目的](#2-目的)
- [3. 外部利用条件](#3-外部利用条件)
- [4. 基本的な使用方法](#4-基本的な使用方法)
- [5. 公開API](#5-公開api)
  - [5.1 プロパティ](#51-プロパティ)
  - [5.2 メソッド](#52-メソッド)
  - [5.3 イベント](#53-イベント)
  - [5.4 型](#54-型)
- [6. 座標系](#6-座標系)
- [7. Nudge（キーボード微調整）](#7-nudgeキーボード微調整)
- [8. ポインター操作](#8-ポインター操作)
- [9. ライフサイクルと所有権](#9-ライフサイクルと所有権)
- [10. 制約事項](#10-制約事項)
- [11. 最適化施策](#11-最適化施策)

---

## 1. 文書概要

| 項目 | 内容 |
| --- | --- |
| 対象 | `DragRectControl` |
| プロジェクト | `DragRectControl/DragRectControl.csproj` |
| 名前空間 | `Estable.AvaloniaUI.DragRect` |
| 対象フレームワーク | `net10.0` |
| UIフレームワーク | Avalonia 12.1 |
| 主要基底クラス | `Avalonia.Controls.Control` |
| 主な用途 | 領域選択ドラッグ矩形の UI・操作・描画を自己完結するコントロール |

## 2. 目的

`DragRectControl` は、マウス操作・キーボード操作（nudge）・描画をコントロール内で完結する独立した Avalonia コントロールです。

次の機能を提供します。

- マウスドラッグによる領域選択
- 既存選択のドラッグ移動
- ドラッグハンドルによるリサイズ
- 矢印キーによるキーボード微調整（nudge）
- 選択枠線・ハンドル・始点マーカーの描画
- ダブルクリックの確定/キャンセル判定（矩形内 = 確定 / 矩形外 = キャンセル）
- ドメイン座標系での正確な nudge（矢印キー1回 = ドメイン1Pixel）

## 3. 外部利用条件

### 3.1 プロジェクト参照

```xml
<ProjectReference Include="..\DragRectControl\src\DragRectControl.csproj" />
```

### 3.2 必要な名前空間

```csharp
using Avalonia;
using Estable.AvaloniaUI.DragRect;
```

座標ポート（`ICoordinatePort`）を使用する場合は、`PlatformFactory` の名前空間も必要です。

```csharp
using Estable.AvaloniaUI.Platform;
```

### 3.3 Visual Tree への配置

`DragRectControl` は `Control` であり、フォーカス可能です。

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:dragrect="clr-namespace:Estable.AvaloniaUI.DragRect;assembly=DragRectControl">
    <Canvas>
        <dragrect:DragRectControl x:Name="SelectionOverlay"
                                   BorderColor="White"
                                   BorderThickness="1"
                                   HandleSize="10" />
    </Canvas>
</Window>
```

## 4. 基本的な使用方法

### 4.1 コードからの生成

```csharp
var dragRect = new DragRectControl
{
    IsHitTestVisible = true,
    ZIndex = 60,
    BorderColor = Colors.White,
    BorderThickness = 2.0,
    HandleColor = Colors.White,
    HandleSize = 10.0,
    HandleHitRadius = 12.0,
    MinSelectionSize = 1.0
};

parentPanel.Children.Add(dragRect);
```

### 4.2 結果イベントの購読

操作の結果は公開イベントで通知されます。上位 AP はマウス・キーイベントを直接処理する必要はありません。

```csharp
dragRect.SelectionChanged += (_, e) => UpdatePreview(e.Rect);
dragRect.SelectionStarted += (_, e) => OnDragStarted(e.Point);
dragRect.SelectionCompleted += (_, e) => OnSelectionCompleted(e.Rect);
dragRect.SelectionCancelled += (_, _) => OnSelectionCancelled();
dragRect.LoupePositionMoved += (_, e) => UpdateLoupe(e.Point);
dragRect.ConfirmRequested += (_, e) => ConfirmSelection(e.Rect);
dragRect.CancelRequested += (_, _) => CancelSelection();
```

`Escape` / `Enter` キーはコントロールが自己処理し、それぞれ `CancelRequested` / `ConfirmRequested` を発火します。上位はこのイベントを購読して後始末（確定・キャンセル後の処理）を行います。

### 4.3 座標ポートの設定

選択状態と nudge はドメイン座標で動作し、マウス入力・描画はコントロールローカル DIP で動作します。両者を変換する座標ポート（`ICoordinatePort`）をホストが設定します。ポートの生成は PlatformFactory の `CoordinatePort` ファクトリ（`Identity` / `Compose` / `ScrollOffset` / `Scale` / `DomainScale` / `ImageLayout`）やホスト固有の実装で行います。

```csharp
// AreaSelect 例: ドメイン = 仮想スクリーンピクセル（= DIP × scaling・動的評価）
control.SetCoordinateTransform(CoordinatePort.DomainScale(() => scaling));

// 等価変換（1 ドメイン = 1 DIP）
control.SetCoordinateTransform(null);
```

未設定時は等価変換（1 ドメイン = 1 DIP）が使用されます。

### 4.4 状態の操作

選択状態（選択矩形・nudge 状態）は公開イベント（`SelectionChanged` 等）の Snapshot で通知されます。ホストは状態をポーリングせず、イベント駆動で把握します。

```csharp
control.SetSelection(rect);         // 外部からの復元
control.ResetSelection();           // 状態と nudge の初期化
```

外部復元した矩形（`SetSelection`）は、**`ClampToDomainBounds` が有効な場合は画像外にはみ出す矩形を境界内にクリップしてから設定**します（「画像外の選択を禁止」を外部復元でも尊重）。また、`ClampToDomainBounds` が ON に切り替わった瞬間にも、既存の選択矩形は境界内へ即時クリップされます（ドラッグ開始を待たない）。継続的なクリップは座標ポートの `Bounds` + `ClampToDomainBounds` が担います。

## 5. 公開API

公開 API は「プロパティ / メソッド / イベント / 型」の 4 分類で構成されます。
座標を扱う API は、ドメイン座標系 / DIP 座標系のどちらであるかを明記します。

### 5.1 プロパティ

#### 5.1.1 外観

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `BorderColor` | `Color` | `DarkGray（#FFA9A9A9）` | 選択枠線の色（不透明度は色のアルファに統合） |
| `BorderThickness` | `double` | `2.0` | 選択枠線の太さ（FadeMaskRenderer / App の既定と一致） |
| `HandleColor` | `Color` | `White` | ドラッグハンドルの色 |
| `HandleSize` | `double` | `10.0` | ドラッグハンドルの一辺サイズ（DIP 座標系） |
| `MarkerColor` | `Color` | `White` | nudge 中の十字マーカーの色 |
| `MarkerLength` | `double` | `16.0` | nudge 中の十字マーカーの長さ（**全長** = 中心から両端までの長さ・DIP 座標系） |
| `CoordLabelColor` | `Color` | `White` | カーソル座標ラベルの文字色 |
| `CoordLabelBackgroundColor` | `Color` | `#99000000`（黒 60%） | カーソル座標ラベルの背景色（不透明度はアルファに統合） |
| `CoordLabelFontSize` | `double` | `12.0` | カーソル座標ラベルのフォントサイズ（6〜48 に補正） |

#### 5.1.2 動作設定

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `HandleHitRadius` | `double` | `12.0` | ハンドルヒットテスト半径（**DIP 座標系**。ズーム・縮小表示でも画面上の大きさが一定） |
| `MinSelectionSize` | `double` | `6.0` | 選択矩形の最小サイズ（ドメイン座標系） |
| `ClampToDomainBounds` | `bool` | `true` | 画像外の選択を禁止するかどうか（ON: 領域選択・移動・nudge を画像境界内にクリップ / OFF: 画像外も選択可能） |
| `ShowCoordLabel` | `bool` | `false` | カーソル座標ラベル（`(x, y)`）をカーソルの右側に表示するかどうか |

両プロパティは変更時に即時反映されます（静的コンストラクタの Changed ハンドラで `InvalidateVisual` を呼ぶ）。

#### 5.1.3 状態

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `CurrentSnapshot` | `DragRectSnapshot` | - | 現在の選択状態のスナップショット（**ポーリング不要の完全状態**）。`State` / `SelectionRect` / `ActiveHandle` / `MousePosition` / `IsMouseDown` / `IsDragging` / `NudgeActive` / `HasSelection` を公開。イベント引数の Snapshot と同一形式。選択状態の判定（確定・キャンセル等の操作分岐）は `CurrentSnapshot.HasSelection` を使用 |

#### 5.1.4 ログ

| プロパティ | 型 | 説明 |
| --- | --- | --- |
| `Log` | `ILogger` | 操作ログ用ロガー（既定は `NullLogger`） |

ログ定義は `Logging/DragRectLog.cs`（LoggerMessage ソース生成）に集約し、`Log` プロパティの `ILogger` 経由で出力する。
出力先・レベル制御はホストアプリケーションが所有する（本クラスは定義のみ）。

##### LogLevel 設定ポリシー

トレースログは LogLevel.Trace に限定せず、「重要度 × 発生頻度」でレベルを使い分ける。

| LogLevel | 対象 | 本番での意図 |
| --- | --- | --- |
| `Error` | 例外・失敗（処理継続不可） | 常時出力し、障害を確実に残す |
| `Warning` | 異常だがフォールバックで継続 | 常時出力し、異常発生の痕跡を残す |
| `Information` | ユーザー操作の主要イベント（低頻度） | 常時出力し、操作手順の再現を可能にする |
| `Debug` | 操作イベントの詳細（マウス / キー / 設定変更） | 障害時のみ有効化して解析 |
| `Trace` | 高頻度・極詳細（ホイール・Down 直前/Up 直後の移動位置） | 特別な性能・座標解析時のみ有効化 |

判断基準:

1. 例外・警告は常時残す（`Error` / `Warning`）
2. 操作の起点イベント（低頻度）は `Information`
3. マウス・キー等の操作イベント（中頻度）は `Debug`
4. 高頻度・極詳細（マウス移動相当・ホイール連続）は `Trace`
5. 設定変更（変更前後）は `Debug`

マウス移動の高頻度ログは行わない。ただし、Down 直前・Up 直後の位置は Down / Up イベント内で記録する（`Trace`）。

##### ログ定義一覧

| レベル | メソッド | 出力内容 |
| --- | --- | --- |
| `Debug` | `DragRectLog.LogMouseDown` | ポインター押下（位置 / クリック数 / 選択有無） |
| `Debug` | `DragRectLog.LogMouseUp` | ポインター解放（位置 / ドラッグ有無 / 選択有無） |
| `Debug` | `DragRectLog.LogClick` | クリック（位置） |
| `Debug` | `DragRectLog.LogDoubleClick` | ダブルクリック（位置 / 選択内側か） |
| `Debug` | `DragRectLog.LogDragStart` | ドラッグ開始（ハンドル / 位置） |
| `Debug` | `DragRectLog.LogKeyDown` | キー入力（キー名 / 処理有無） |
| `Debug` | `DragRectLog.LogConfirmRequested` | Enter キーによる確定要求（矩形） |
| `Debug` | `DragRectLog.LogCancelRequested` | Escape キーによるキャンセル要求 |
| `Debug` | `DragRectLog.LogZoomRequested` | Ctrl+ホイールによるズーム要求（拡大/縮小） |
| `Debug` | `DragRectLog.LogSettingChanged` | 設定値変更（変更前後） |
| `Trace` | `DragRectLog.LogPositionBeforeDown` | Down 直前のマウス位置 |
| `Trace` | `DragRectLog.LogPositionAfterUp` | Up 直後のマウス位置 |
| `Trace` | `DragRectLog.LogWheelScroll` | 通常ホイールによるスクロール（移動量 / オフセット） |

### 5.2 メソッド

| メソッド | 説明 |
| --- | --- |
| `SetSelection(Rect)` | 選択矩形をドメイン座標で復元する（`SelectionChanged` で通知。`ClampToDomainBounds` 有効時は境界内へクリップ） |
| `ResetSelection()` | 選択状態と nudge 状態を初期化する |
| `SetCoordinateTransform(ICoordinatePort?)` | ドメイン↔DIP 座標変換ポートを設定する（null = 等価変換） |
| `AttachScrollViewer(ScrollViewer?)` | 連携する ScrollViewer を明示的に設定する（スクロール・ズーム検知 + ホイール操作。null で解除。祖先探索は行わない） |
| `RequestCancel()` | キャンセル要求を処理する（Escape キーと同じ条件。選択破棄後に `CancelRequested` を発火） |
| `RequestConfirm()` | 確定要求を処理する（Enter キーと同じ条件。ドラッグ中は無視し `ConfirmRequested` を発火） |

クリップ（画像境界内への制限）は座標ポートの `Bounds` + `ClampToDomainBounds` が担います。

### 5.3 イベント

| イベント | 引数 | 説明 |
| --- | --- | --- |
| `SelectionChanged` | `SelectionRectEventArgs` | 選択矩形が変更された（`Rect` はドメイン座標） |
| `ViewportChanged` | `SelectionRectEventArgs` | ズーム・スクロール（ScrollViewer の ScrollChanged）を検知して選択矩形 + Snapshot を再通知 |
| `SelectionStarted` | `DragRectPointEventArgs` | 新規選択のドラッグ開始（始点） |
| `SelectionCompleted` | `SelectionRectEventArgs` | 選択矩形が確定した |
| `SelectionCancelled` | `EventArgs` | 領域がキャンセルされた |
| `MouseDown` | `DragRectPointEventArgs` | 左ボタン押下（ドメイン座標） |
| `MouseUp` | `DragRectPointEventArgs` | 左ボタン解放（ドメイン座標） |
| `MouseClicked` | `DragRectPointEventArgs` | クリック（ドメイン座標） |
| `MouseDoubleClicked` | `DragRectPointEventArgs` | ダブルクリック検出（ドメイン座標） |
| `MouseMoved` | `DragRectPointEventArgs` | マウス移動ごとのポインタ位置（ドメイン座標） |
| `LoupePositionMoved` | `DragRectPointEventArgs` | ドラッグ・nudge 中のルーペ中心位置（ドメイン座標。リサイズ: アクティブハンドル点 / 移動: 掴んだマウス位置 / 新規選択・nudge: 終点） |
| `ZoomRequested` | `EventHandler<bool>` | Ctrl+ホイールによるズーム要求（true = ズームイン / false = ズームアウト。祖先 ScrollViewer が存在する Crop モードのみ発火） |
| `ConfirmRequested` | `SelectionRectEventArgs` | Enter キーによる確定要求 |
| `CancelRequested` | `EventArgs` | Escape キーによるキャンセル要求 |

すべての座標イベントはドメイン座標で通知されます。

### 5.4 型

公開型は `DragRectControl` とイベント引数（`DragRectEventArgs.cs` / `DragRectSnapshot.cs`）です。
内部実装（状態・純粋計算・描画）はすべて internal（非公開）です。

#### 5.4.1 DragRectControl（`DragRectControl.cs`）

公開 API は「5.1 プロパティ」「5.2 メソッド」「5.3 イベント」に記載の通りです。
StyledProperty は同ファイルに定義されます（Avalonia のバインディング・スタイル設定に必要なため public）。

#### 5.4.2 イベント引数（`DragRectEventArgs.cs` / `DragRectSnapshot.cs`）

| 型 | 説明 |
| --- | --- |
| `DragRectInteractionState` enum | 操作状態（NoSelection / StartPointNudging / Dragging / DraggingNudging / Selected / SelectedNudging） |
| `DragRectSnapshot` record | 状態 Snapshot（State / SelectionRect / ActiveHandle / MousePosition / IsMouseDown / IsDragging / NudgeActive。`HasSelection` は導出プロパティ）。値セマンティクスで比較可能 |
| `SelectionRectEventArgs` | 選択矩形通知（`Rect` はドメイン座標系。`Snapshot` を同梱） |
| `DragRectPointEventArgs` | 座標通知（`Point` はドメイン座標系。`Snapshot` を同梱） |
| `DragHandle` enum | ドラッグハンドル種別（カーソル形状マッピング用） |

## 6. 座標系

### 6.1 3 層の座標

| 座標系 | 用途 |
| --- | --- |
| ドメイン座標 | 選択状態・nudge・イベント通知 |
| コントロールローカル DIP | マウス入力・描画 |
| 画面物理 Pixel | カーソル操作・画面変換 |

### 6.2 座標ポート

`ICoordinatePort`（PlatformFactory 提供）がドメイン↔DIP 変換とドメイン境界を提供します。

| メンバー | 説明 |
| --- | --- |
| `Forward(Point)` | DIP → ドメイン |
| `Inverse(Point)` | ドメイン → DIP |
| `Bounds` | ドメインのクリップ範囲（null = 無制限） |

変換関数は呼び出し毎に評価されるため、ズーム・スクロールの動的変化を反映できます。

### 6.3 画像外選択の可否（`ClampToDomainBounds`）

`ClampToDomainBounds` プロパティで、画像（ドメイン境界）の外側を矩形選択可能にするか制御します。

| 値 | 動作 |
| --- | --- |
| `true`（既定） | 選択を画像範囲内に制限する |
| `false` | 画像外も選択可能。ドラッグで画像外に矩形を作成・移動できる |

`true` の場合のクリップ仕様:

| 操作 | クリップ動作 |
| --- | --- |
| 領域選択（新規選択ドラッグ） | 終点（ドラッグ位置）を境界内にクリップしてから矩形を作成 |
| 領域選択（ハンドル変形） | 固定角（ハンドルと反対側の角）を動かさず、動く辺のみ境界内に制限 |
| 領域移動（Move ドラッグ・nudge） | 矩形全体が境界内に収まるようにシフト（サイズは維持） |

クリップ範囲は `SetCoordinateTransform` のポート `Bounds`（`ICoordinatePort.Bounds`）で指定します（null = 無制限の場合はクリップされません）。

## 7. Nudge（キーボード微調整）

### 7.1 動作

`DragRectControl` がフォーカスを持つ状態で矢印キーを押すと、選択状態に応じて次の動作を行います。

| 状態 | 動作 |
| --- | --- |
| 未選択 | 基点（始点）を移動 |
| ドラッグ中・矩形確定後 | 終点を移動 |
| ハンドル選択中 | ハンドル方向に矩形を変形 |

### 7.2 ドメイン Pixel 保証

nudge はドメイン座標の整数加算のみで計算します。

```text
矢印キー1回 = ドメイン座標の1Pixel移動
```

これは次の設計で保証されます。

- 移動量 = 差分（±1）の整数加算のみ（FP 誤差を累積しない）
- クリップ時は実移動量のみ反映

### 7.3 nudge の許可条件

- 領域作成前: 始点決めの nudge として常に許可。
- 領域作成後: マウスボタン押下中（ドラッグ中・ハンドル選択中）のみ許可。ドラッグ完了後（マウス Down していない状態）の nudge は無効。

### 7.4 十字マーカー（nudge 位置表示）の描画位置

nudge 中は十字マーカー（`MarkerColor` / `MarkerLength` で外観を指定）を **ドラッグ中のハンドル位置** に描画します（`GetMarkerPosition`）。ルーペ位置（`LoupePositionMoved`）も同じ位置を追従します。

ドラッグ中も同じ位置（`GetMarkerPosition`）をルーペ中心に通知します。これにより、リサイズ中のハンドル点・移動中の掴んだマウス位置・新規選択の終点がルーペで拡大確認できます。

| 状態 | 十字マーカー・ルーペの追従位置 |
| --- | --- |
| リサイズ（8 方向ハンドル選択中） | アクティブハンドルの現在位置（`SelectionGeometry.GetHandlePosition`） |
| 移動（Move ドラッグ中） | **掴んだマウス位置（`LastMousePosition`）**（矩形中心に飛ばず、マウスポインタに追従する） |
| 新規選択ドラッグ中・nudge | 終点（ドラッグ中の位置） |
| 未選択（始点決め nudge） | 始点 |

十字マーカーとルーペ中心は、矩形の固定位置（`GetOppositeCorner` で得られる左上等の隅）ではなく**ドラッグ中の操作点（ハンドル / 掴んだ位置 / 終点）**を追従します。これにより、マーカーは常にドラッグ中の操作位置に表示されます。

### 7.5 選択領域の閉区間描画（始点・終点を含む）

選択領域は **閉区間（始点・終点の両ピクセルを含む）** を意図する仕様です。

選択矩形はドメイン座標で半開区間（`幅 = 終点 − 始点`、.NET `Rect` と同一）で保持されますが、描画（`Render`）では **終点（`Right` / `Bottom`）に +1 してから** `Inverse` で DIP 変換します。

```text
描画用 DIP 矩形 = Rect(Inverse(始点), Inverse(終点 + (1, 1)))
```

これにより、選択枠線・ドラッグハンドルは終点のピクセルも含む矩形に描画され、FadeMask（非暗転領域）・キャプチャ（閉区間 +1）と一致します。

## 8. ポインター操作

- 左ボタン押下: 新規選択・ハンドルドラッグ・移動ドラッグの開始（クリック回数 2 以上はダブルクリック検出）
- 左ボタン移動: ドラッグ中の選択矩形更新
- 左ボタン解放: 選択確定（最小サイズ未満はキャンセル）
- マウスホイール（祖先 ScrollViewer が存在する場合・Crop モード）:
  - **Ctrl + ホイール**: `ZoomRequested` を発火（ズームの実処理は上位 AP が実施）
  - **通常ホイール**: 祖先 `ScrollViewer` をスクロール（Shift 併用で横スクロール。48DIP/段）
  - ScrollViewer が存在しない場合（AreaSelect の OverlayWindow 等）は無効（既定動作に委譲）

## 9. ライフサイクルと所有権

- コントロール自身が保持する状態（選択矩形・nudge 状態）は `ResetSelection()` で初期化します。
- 上位 AP はコントロールを Visual Tree から除去するだけで破棄できます。
- ビジュアルツリー切断時は TopLevel キー購読・ScrollViewer 購読を解除し、nudge 中ならポインターを再表示します。

## 10. 制約事項

- 矢印キーによる nudge は、コントロールがフォーカスを持っている場合に動作します（フォーカス非依存の nudge は TopLevel キー購読で動作。Drag 開始前のマウス押下前は TopLevel 購読、押下後はフォーカス経由）。
- `Escape` / `Enter` はコントロールが自己処理し、`CancelRequested` / `ConfirmRequested` を発火します（上位はイベント購読で後始末を行う）。プログラムからは `RequestCancel()` / `RequestConfirm()` で同一条件の要求を発行できます。
- `HandleHitRadius` の単位は **DIP 座標**、`MinSelectionSize` の単位はドメイン座標です。
- 既定のカーソルサービスは Windows 実装（`WinCursorService`）です。他プラットフォームでは `ICursorService` を注入してください。
- リサイズハンドル（8 方向）でマウス Down すると、ポインタ（OS カーソル）がハンドル点へスナップされます（ポインタ・矩形の角・ルーペを一体にするため）。移動（Move）の Down ではスナップされません。
- nudge 中のカーソル移動は OS カーソル位置を変更します。

## 11. 最適化施策

| 施策 | 内容 |
| --- | --- |
| 状態の単一クラス集約 | 選択・ドラッグ・nudge 状態を `DragRectState` 1 クラスに集約し、オブジェクト生成・委譲のオーバーヘッドを削減 |
| nudge の整数加算 | ドメイン座標の差分（±1）整数加算のみで nudge を計算し、FP 誤差の累積を排除（基点更新＋オフセットリセットが不要） |
| 再描画の最小化 | 外観プロパティ変更時のみ `InvalidateVisual` を呼び、不要な再描画を抑制（Changed ハンドラで明示制御） |
| イベント駆動・ポーリング排除 | 選択状態・nudge 状態はイベントの Snapshot で通知し、ホストは状態をポーリングしない |
| 座標変換の遅延評価 | `ICoordinatePort` の変換関数は呼び出し毎に評価されるため、ズーム・スクロールの動的変化を反映しつつ変換結果をキャッシュしない（メモリ消費なし） |
