# DragRectControl.MewUI

`DragRectControl`（リサイズ可能なドラッグ矩形）の **MewUI 版**です。Avalonia 版と同一の UI 非依存コア（`DragRectControl.Core`）を使い、
描画層・入力処理・カーソル制御のみを MewUI で実装しています。

## 特徴

- **コア共有**: 選択矩形の幾何計算・状態機械（ヒットテスト・ドラッグ変形・nudge・境界クリップ）は `DragRectControl.Core` が単一所有します。
  Avalonia 版と MewUI 版の挙動は同じ計算に基づきます。
- **直接描画**: 中間ビットマップを経由せず、`SkiaCanvasView` のキャンバスへ枠線・ハンドル・十字マーカー・座標ラベルを描画します。
- **DPI 追従**: `OnDpiChanged` でスケーリング係数を更新し、DIP 座標で描画します（枠線・ハンドルの画面上の大きさを保ちます）。
- **コア型のイベント引数**: イベントの座標は UI 非依存のコア型（`EsPoint` / `EsRect`）で公開します（境界変換が不要）。

## インストール

```xml
<PackageReference Include="Aprillz.MewUI.Skia" Version="0.21.1" />
<ProjectReference Include="..\DragRectControl.MewUI\src\DragRectControl.MewUI.csproj" />
```

MewUI は API が安定化中（0.x）のため、パッケージのバージョンは固定してください（`MewUIVersion` プロパティで一元管理）。

## 使い方

```csharp
using Estable.DragRect;
using Estable.MewUI.DragRect;
using Estable.Platform.Coordinate;
using Estable.Platform.Geometry;

// 1. 生成（カーソルサービスは省略すると Windows 既定実装になる）
var dragRect = new DragRectControl { ShowCoordLabel = true };

// 2. 座標変換ポートを設定する（Forward = ローカル DIP → ドメイン / Bounds = ドメイン境界）
dragRect.SetCoordinateTransform(new CoordTransform(dip => dip, domain => domain, () => new EsSize(1920, 1080)));

// 3. イベントを購読する
dragRect.SelectionChanged += (_, e) => Console.WriteLine($"選択: {e.Rect}");
dragRect.LoupePositionMoved += (_, e) => UpdateLoupe(e.Point);
dragRect.SelectionCompleted += (_, e) => Capture(e.Rect);

// 4. 外部状態から選択を復元する（境界外は自動でクリップされる）
dragRect.SetSelection(new EsRect(100, 100, 400, 300));
```

画像の上へ重ねる場合は、親の `Grid` に追加したうえで入力の優先順を管理します（本コントロールが入力を受け取ります）。

```csharp
grid.Add(cropView);     // 画像（下）
grid.Add(dragRect);     // 選択オーバーレイ（上。入力を受け取る）
```

## 操作

| 操作 | 動作 |
| --- | --- |
| 左ドラッグ（矩形外） | 新規選択の開始 |
| 左ドラッグ（ハンドル上） | リサイズ（角・辺の 8 方向） |
| 左ドラッグ（矩形内） | 移動 |
| ダブルクリック（矩形内） | 確定要求（`ConfirmRequested`） |
| ダブルクリック（矩形外） | キャンセル要求（`CancelRequested`） |
| 矢印キー | nudge（1 ドメインピクセル単位の微調整。カーソルを非表示にして十字マーカーを表示） |
| Enter | 確定要求（`ConfirmRequested`） |
| Escape | キャンセル要求（`CancelRequested`） |
| Ctrl + ホイール | ズーム要求（`ZoomRequested`） |

矢印キーの nudge は本コントロールにフォーカスがある場合のみ動作します（マウス押下時に自動でフォーカスを取得します）。

## Avalonia 版との違い

| 項目 | Avalonia 版 | MewUI 版 |
| --- | --- | --- |
| イベント引数の座標 | Avalonia の `Point` / `Rect` | **コア型の `EsPoint` / `EsRect`** |
| マウスダウン・アップのイベント名 | `MouseDown` / `MouseUp` | **`DragMouseDown` / `DragMouseUp`**（基底 `UIElement` の同名メンバーと衝突するため） |
| ビューポート変化 | `AttachScrollViewer` で `ScrollViewer` を自動購読 | **`NotifyViewportChanged()` をホストが呼ぶ** |
| nudge のキー入力 | フォーカス非依存（`TopLevel` を購読） | フォーカス依存（本コントロールにフォーカスがある場合のみ） |
| ログ | `ILogger` を注入して出力 | `Log` プロパティで同様に出力する（書式・レベルは同一。通常ホイールのスクロールログのみ持たない） |
| 描画 | 中間ビットマップ経由 | キャンバスへ直接描画 |

## 主なプロパティ

| プロパティ | 型 | 既定値 | 説明 |
| --- | --- | --- | --- |
| `BorderColor` | `System.Drawing.Color` | `#FFA9A9A9` | 選択枠線の色（アルファで不透明度） |
| `BorderThickness` | `double` | `2.0` | 選択枠線の太さ（DIP） |
| `HandleColor` | `System.Drawing.Color` | 白 | ドラッグハンドルの塗り色 |
| `HandleSize` | `double` | `10.0` | ハンドルの一辺サイズ（DIP） |
| `HandleHitRadius` | `double` | `12.0` | ハンドルのヒットテスト半径（DIP） |
| `MinSelectionSize` | `double` | `6.0` | 選択矩形の最小サイズ（ドメイン座標） |
| `ClampToDomainBounds` | `bool` | `true` | ドメイン境界外の選択を禁止する |
| `MarkerColor` / `MarkerLength` | `System.Drawing.Color` / `double` | 白 / `16.0` | 十字マーカーの色と全長（DIP） |
| `ShowCoordLabel` | `bool` | `false` | カーソル座標ラベルの表示 |
| `CoordLabelColor` / `CoordLabelBackgroundColor` | `System.Drawing.Color` | 白 / 黒 60% | ラベルの文字色と背景色 |
| `CoordLabelFontSize` | `double` | `12.0` | ラベルのフォントサイズ（6〜48 に補正） |
| `RenderScaling` | `double` | `1.0` | DIP → 物理ピクセルの倍率（DPI 変更で自動更新） |

## 主なメソッド

| メソッド | 説明 |
| --- | --- |
| `SetCoordinateTransform(ICoordTransform?)` | ドメイン ↔ ローカル DIP の変換ポートを設定する（null は等価変換） |
| `SetSelection(EsRect)` | 選択矩形を復元する（`ClampToDomainBounds` 有効時は境界内へクリップ） |
| `ResetSelection()` | 選択状態と nudge 状態を初期化する |
| `RequestConfirm()` / `RequestCancel()` | Enter / Escape と同じ条件で確定・キャンセルを要求する |
| `NotifyViewportChanged()` | ビューポートの変化を通知する（`ViewportChanged` を発火） |
| `CurrentSnapshot` | 現在の状態のスナップショット（`DragRectSnapshot`） |

## イベント

`SelectionStarted` / `SelectionChanged` / `SelectionCompleted` / `SelectionCancelled` / `DragMouseDown` / `DragMouseUp` /
`MouseClicked` / `MouseDoubleClicked` / `MouseMoved` / `LoupePositionMoved` / `ViewportChanged` / `ZoomRequested` /
`ConfirmRequested` / `CancelRequested`

## テスト

```powershell
dotnet test src/DragRectControl.MewUI/tests/DragRectControl.MewUI.Tests.csproj
```

- `DragRectSkiaRenderer.Tests.cs`: 枠線・ハンドル・十字マーカー・座標ラベルの画素検証と配置計算（純粋関数）の検証。
- `DragRectControl.Tests.cs`: 依存プロパティの既定値・補正、`CurrentSnapshot`、選択の復元・境界クリップ、確定・キャンセル、ビューポート通知、`Log` のフォールバックと設定変更ログ。
- `LogMessage.Tests.cs`: 操作ログ（LoggerMessage）の出力書式（`RecordingLogger` と `NullLogger` の両経路）。

## 操作ログ

`Log` プロパティへ `ILogger` を設定すると、マウス・キー操作と設定変更を出力します（既定は出力なし）。書式・レベルは Avalonia 版と同一です。

| レベル | 出力内容 |
| --- | --- |
| Debug | MouseDown / MouseUp / Click / DoubleClick / DragStart / KeyDown / ConfirmRequested / CancelRequested / ZoomRequested / 設定変更 |
| Trace | MouseDown 直前の位置 / MouseUp 直後の位置 |

## 未対応

- `ScrollViewer` の自動連携（ホストが `NotifyViewportChanged()` を呼ぶ）。
- フォーカス非依存の矢印キー nudge。
- 通常ホイールのスクロールログ（スクロールの実処理はホストが担うため、Avalonia 版の `LogWheelScroll` に相当する出力を持たない）。

## ライセンス

このリポジトリのライセンス方針に従います。ライセンスが明示されていない環境では、再配布・商用利用の条件を確認してから利用してください。
