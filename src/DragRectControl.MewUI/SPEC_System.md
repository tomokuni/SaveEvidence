# DragRectControl.MewUI システム仕様書

DragRectControl（MewUI 版）のシステム概要・アーキテクチャ・依存関係・設計判断を記載します。

## システム概要

`DragRectControl.MewUI` は、Avalonia 版 `DragRectControl` と**同一の UI 非依存コア（`DragRectControl.Core`）**を使い、
描画層・入力処理・カーソル制御のみを MewUI（SkiaSharp）で実装したドラッグ矩形コントロールです。

コアが担う範囲（選択矩形の正規化・ハンドル位置・ヒットテスト・ドラッグ変形・nudge・境界クリップ・対話状態の合成）は
両 UI で完全に共通であり、本プロジェクトはコアの状態機械を入力で駆動し、その結果をキャンバスへ描画します。

## 技術スタック

| 区分 | 採用技術 |
| --- | --- |
| 対象フレームワーク | .NET 10（`net10.0`） |
| 言語 | C# 14 |
| UI フレームワーク | MewUI（`Aprillz.MewUI.Skia` **0.21.1 固定**） |
| 描画 | SkiaSharp（`DrawRect` / `DrawLine` / `DrawText`） |
| 基底コントロール | `Aprillz.MewUI.Skia.Controls.SkiaCanvasView` |
| プロパティシステム | `MewProperty<T>`（`MewPropertyOptions.AffectsRender`） |
| カーソル操作 | `ICursorService`（`PlatformFactory.Native` の Win32 実装） |

## アーキテクチャ

### 依存方向（一方向を厳守）

```text
┌───────────────────────┐        ┌───────────────────────┐
│ DragRectControl.MewUI │        │   DragRectControl     │
│ （MewUI の描画・入力）│        │ （Avalonia の描画・入力）│
└───────────┬───────────┘        └───────────┬───────────┘
            │                                │
            └───────────┬────────────────────┘
                        ▼
            ┌───────────────────────┐
            │  DragRectControl.Core │  幾何計算・状態機械（単一所有）
            └───────────┬───────────┘
                        ▼
            ┌───────────────────────┐
            │  PlatformFactory.Core │  コア幾何型・座標変換ポート・カーソル抽象
            └───────────┬───────────┘
                        ▼
            ┌───────────────────────┐
            │ PlatformFactory.Native│  Win32 / NoOp 実装（UI 非依存）
            └───────────────────────┘
```

### 責務の分担

| 層 | 責務 |
| --- | --- |
| `DragRectControl.Core` | 選択矩形の幾何計算、ヒットテスト、ドラッグ変形、nudge、境界クリップ、対話状態の合成、スナップショット生成 |
| `DragRectControl.MewUI` | 入力（マウス・キー）をコアの状態機械へ流す、コアの結果を `SKCanvas` へ描画する、カーソル形状と表示の制御、DPI の追従 |
| ホスト | 座標変換ポートの設定、選択の復元・確定・キャンセル、重ね順の管理、ビューポート変化の通知 |

### 設計判断

| 判断 | 理由 |
| --- | --- |
| 状態機械（`DragRectState`）をコアに置き、UI 側は入力の変換のみを行う | 本コントロールの本体は「状態遷移 + 幾何」であり、UI に残すと MewUI 版で丸ごと書き直しになる |
| イベント引数をコア型（`EsPoint` / `EsRect`）で公開する | コアの状態機械と同一の型になり境界変換が不要になる。MewUI の `Rect` は負のサイズを 0 にクランプするため、`EsRect` のほうが安全 |
| カーソルサービスの既定実装の解決を UI 層で行う | コアは `PlatformFactory.Native` を参照できない（循環参照）。コアは抽象と `null` のみを扱う |
| `SkiaCanvasView`（`Control` 系）を基底にする | 画像の上へ重ねるオーバーレイであり、子要素を持たない。背景を塗らないため下の画像が透ける |
| `ScrollViewer` を自動購読しない | MewUI 版の画面構成（`Grid` 重ね合わせ）では祖先探索の前提が異なる。ビューポート変化はホストが `NotifyViewportChanged()` で通知する |
| マウスダウン・アップのイベントを改名する | `UIElement` が同名メンバーを持つため、メンバーの隠蔽を避けて `DragMouseDown` / `DragMouseUp` とする |
| ログの出力先を持つ | 入力を持つコントロールは操作ログが必要（上位 AP はドラッグ・nudge 用のキー・マウスイベントを直接検出しないため）。既定は `NullLogger` で、ホストが `Log` プロパティで注入する |

## コンポーネント構成

```text
src/DragRectControl.MewUI/
├── src/
│   ├── DragRectControl.cs          公開 API・入力処理・カーソル制御・描画の起点
│   ├── DragRectSkiaRenderer.cs     枠線・ハンドル・十字マーカー・座標ラベルの描画
│   ├── DragRectPointEventArgs.cs   座標通知のイベント引数
│   └── SelectionRectEventArgs.cs   選択矩形通知のイベント引数
└── tests/
    ├── TestHelper.cs                     テスト用の描画先（SKBitmap + SKCanvas）と画素数え上げ
    ├── DragRectSkiaRenderer.Tests.cs     描画と配置計算の検証（16 件）
    └── DragRectControl.Tests.cs          公開 API・プロパティ・イベントの検証（12 件）
```

| コンポーネント | 責務 |
| --- | --- |
| `DragRectControl` | `MewProperty` の定義、入力のドメイン変換、コアの状態機械の駆動、カーソル形状・表示の制御、DPI 変更の追従 |
| `DragRectSkiaRenderer` | 枠線・8 方向ハンドル・十字マーカー・座標ラベルの描画と、ラベル配置の純粋計算 |
| `DragRectPointEventArgs` / `SelectionRectEventArgs` | 通知時点の座標（矩形）とスナップショットの組 |
| `RenderTarget`（テスト） | ウィンドウ不要の描画先。`SKBitmap` と `SKCanvas` を保持し、画素を読み取る |

## クラス図

```mermaid
classDiagram
    class SkiaCanvasView {
        <<Aprillz.MewUI.Skia>>
        +PaintSurface
        +ContinuousAnimation
        +MeasureContent(size) Size
        +OnMouseDown(e) void
        +OnMouseMove(e) void
        +OnMouseUp(e) void
        +OnMouseWheel(e) void
        +OnKeyDown(e) void
        +OnDpiChanged(oldDpi, newDpi) void
    }
    class DragRectControl {
        +System.Drawing.Color BorderColor
        +double BorderThickness
        +double HandleSize
        +double HandleHitRadius
        +double MinSelectionSize
        +bool ClampToDomainBounds
        +bool ShowCoordLabel
        +double CoordLabelFontSize
        +double RenderScaling
        +DragRectSnapshot CurrentSnapshot
        +SetCoordinateTransform(port) void
        +SetSelection(rect) void
        +ResetSelection() void
        +RequestConfirm() void
        +RequestCancel() void
        +NotifyViewportChanged() void
        -OnPaintSurface(canvas, info) void
        -HandleArrowKey(key) bool
        -EndNudge() void
        -CaptureMouse(capture) void
        -ToDipRect(rect) EsRect
    }
    class DragRectSkiaRenderer {
        +DrawBorder(canvas, rect, color, thickness)$ void
        +DrawHandles(canvas, rect, size, color, border)$ void
        +DrawStartMarker(canvas, center, color, thickness, size)$ void
        +DrawCoordLabel(canvas, cursor, domain, clientSize, ...)$ void
        +ComputeCoordLabelPosition(cursor, clientSize, w, h)$ EsPoint
    }
    class DragRectState {
        <<DragRectControl.Core>>
        +EsRect SelectionRect
        +ICoordTransform Port
        +GetInteractionState() DragRectInteractionState
        +CreateSnapshot(mousePosition) DragRectSnapshot
        +HitTest(pos, radius) DragHandle
        +StartNewSelection(pos) void
        +StartDrag(handle, pos) void
        +UpdateDrag(pos, minSize, clamp) void
        +EndDrag() void
        +Nudge(dx, dy, minSize, clamp) void
        +GetMarkerPosition() EsPoint
        +ClampSelectionToBounds() bool
    }
    class SelectionGeometry {
        <<DragRectControl.Core>>
        +GetHandlePosition(rect, handle)$ EsPoint
        +IsNear(p1, p2, radius)$ bool
        +ComputeDragRect(...)$ EsRect
        +ClampRectToBounds(...)$ EsRect
        +ClampRectToDomainBounds(...)$ EsRect
    }
    class ICursorService {
        <<PlatformFactory.Core>>
        +GetCursorScreenPoint() EsPixelPoint
        +SetCursorScreenPoint(point) void
        +ShowCursor(show) void
    }

    SkiaCanvasView <|-- DragRectControl
    DragRectControl *-- DragRectState : 状態機械
    DragRectControl ..> DragRectSkiaRenderer : 描画を委譲
    DragRectControl ..> ICursorService : カーソル操作
    DragRectState ..> SelectionGeometry : 幾何計算
    DragRectSkiaRenderer ..> SelectionGeometry : ハンドル位置
```

## シーケンス図（新規選択のドラッグ）

```mermaid
sequenceDiagram
    participant Host as ホスト
    participant Ctl as DragRectControl
    participant St as DragRectState
    participant Cur as ICursorService

    Host->>Ctl: SetCoordinateTransform(port)
    Ctl->>St: Port = port
    Note over Ctl: 左ボタン押下
    Ctl->>Ctl: ToDomain(e.GetPosition)
    Ctl->>St: HitTest(pos, ToDomainRadius())
    alt ハンドルなし
        Ctl->>St: StartNewSelection(pos)
        Ctl->>Host: SelectionStarted
    else ハンドルあり
        Ctl->>St: StartDrag(handle, pos)
        Ctl->>Cur: SetCursorScreenPoint(ToScreen(handlePos))
    end
    Ctl->>Ctl: CaptureMouse(true)
    loop ドラッグ中
        Ctl->>St: UpdateDrag(pos, MinSelectionSize, ClampToDomainBounds)
        Ctl->>Host: SelectionChanged
        Ctl->>Host: LoupePositionMoved(GetMarkerPosition())
    end
    Note over Ctl: 左ボタン解放
    Ctl->>Ctl: CaptureMouse(false)
    alt 最小サイズ未満
        Ctl->>St: Reset()
        Ctl->>Host: SelectionCancelled
    else 確定
        Ctl->>St: EndDrag()
        Ctl->>Host: SelectionCompleted
    end
```

## シーケンス図（矢印キー nudge）

```mermaid
sequenceDiagram
    participant Host as ホスト
    participant Ctl as DragRectControl
    participant Cur as ICursorService
    participant St as DragRectState

    Note over Ctl: 矢印キー押下（フォーカス保持時）
    Ctl->>Cur: GetCursorScreenPoint()
    Ctl->>Ctl: ScreenToClient → Port.Forward（基点の記録）
    alt nudge 未開始
        Ctl->>St: NudgeActive = true / CrosshairVisible = true
        Ctl->>Cur: ShowCursor(false)
    end
    Ctl->>St: Nudge(dx, dy, MinSelectionSize, ClampToDomainBounds)
    Ctl->>Host: SelectionChanged
    Ctl->>Host: LoupePositionMoved(GetMarkerPosition())
    Ctl->>Cur: SetCursorScreenPoint(ToScreen(marker))
    Note over Ctl: マウス操作またはフォーカス喪失
    Ctl->>Cur: ShowCursor(true)
    Ctl->>St: NudgeActive = false
```

## 状態遷移

コアの `DragRectState.GetInteractionState()` が合成する対話状態です（nudge が最優先）。

```mermaid
stateDiagram-v2
    [*] --> NoSelection
    NoSelection --> StartPointNudging : 矢印キー
    NoSelection --> Dragging : 左ドラッグ（新規選択）
    Dragging --> DraggingNudging : 矢印キー
    Dragging --> Selected : 解放（最小サイズ以上）
    Dragging --> NoSelection : 解放（最小サイズ未満・キャンセル）
    Selected --> SelectedNudging : 矢印キー
    Selected --> NoSelection : RequestConfirm / Escape
    SelectedNudging --> Selected : nudge 終了
    DraggingNudging --> Dragging : nudge 終了
    StartPointNudging --> Selected : ドラッグ確定
```

## 描画フロー

```text
OnPaintSurface(canvas, info)
  ├─ canvas.Scale(RenderScaling)                     … DPI の適用（境界 1 箇所）
  ├─ HasSelection なら
  │    ├─ ToDipRect(SelectionRect)                   … Inverse 変換（終点は +1 して閉区間を表現）
  │    ├─ DragRectSkiaRenderer.DrawBorder            … 枠線
  │    └─ DragRectSkiaRenderer.DrawHandles           … SelectionGeometry から 8 方向の中心を取得
  ├─ NudgeActive かつ CrosshairVisible なら
  │    └─ DragRectSkiaRenderer.DrawStartMarker       … GetMarkerPosition() の位置
  └─ ShowCoordLabel なら
       └─ DragRectSkiaRenderer.DrawCoordLabel        … CoordinateFormatter（コア）で文字列化
```

背景は塗りません（透明のままにして下の画像を透かします）。

## エラー処理ポリシー

| 条件 | 動作 |
| --- | --- |
| カーソルサービスが null | `PlatformFactory.CreateCursorService()` の既定実装を使用する |
| ウィンドウを特定できない（未レイアウト・未表示） | マウスキャプチャとカーソル移動を行わない（例外にしない） |
| `CoordLabelFontSize` に NaN / ∞ | 既定値（12）へ補正する |
| 破棄済みの画像を描画しようとした | 本コントロールは画像を扱わない（対象外） |

## 実装上の注意（実測・設計から得た知見）

1. **`UIElement` は `MouseDown` / `MouseUp` メンバーを持つ**。同名の公開イベントを定義すると `CS0108`（メンバーの隠蔽）になるため、
   本コントロールでは `DragMouseDown` / `DragMouseUp` の名で公開する。
2. **MewUI の `Rect` / `Size` は負のサイズを 0 にクランプする**。コアは `EsRect` を使うため影響を受けないが、
   MewUI 型へ変換する場合は値が変わりうる（本コントロールはイベント引数も `EsRect` で公開して回避する）。
3. **`MouseEventArgs.ScreenPosition` は画面座標（デバイスピクセル）**、**`Window.Position` / `ClientToScreen` の入力は DIP** である。
   カーソル移動（nudge）の座標変換はこの非対称性を踏まえて実装する。
4. **スナップショットはドラッグ状態の解除前に生成する**。`EndDrag` の後ではマーカー位置が固定コーナーを返すため、
   nudge 位置は解放処理の冒頭で計算する。
5. **ストロークの座標は「経路の中心」に描かれる**。太さ 2 の枠線は辺の座標を挟む 2 ピクセル（例: `[x-1, x+1)`）を覆う。
   描画テストの期待値はこの前提で書く。

## 未実装（今後の判断）

| 項目 | 状況 |
| --- | --- |
| `ScrollViewer` の自動連携 | ホストが `NotifyViewportChanged()` を呼ぶ方式で代替 |
| フォーカス非依存の矢印キー nudge | フォーカス依存で実装（`TopLevel` のキー購読は行わない） |
| 通常ホイールのスクロールログ | スクロールの実処理はホストが担うため出力しない（他の操作ログは書式・レベルとも Avalonia 版と同一） |
| 実ウィンドウへの組み込み | `FadeMaskDragRectControl.MewUI` を経由して配線済み（撮影・切り出しの両方） |
