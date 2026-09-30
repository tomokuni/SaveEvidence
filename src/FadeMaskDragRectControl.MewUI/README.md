# FadeMaskDragRectControl.MewUI

`FadeMaskDragRectControl`（領域選択の複合コントロール）の **MewUI 版**です。 選択矩形（ドラッグ）・暗転マスク・
ルーペを 1 つのコントロールにまとめ、配線と状態同期を内側で完結させます。

## 特徴

- **Facade / Composite**: 2 つの MewUI コントロール（`MarqueeControl`（外側の装飾を含む） / `LoupeControl`）を
  重ねて配置し、ホストへ統一インターフェースを提供します。 ホストはライフサイクルとモード固有処理に専念できます。
- **モード切替**: `Mode` を設定するだけで、選択矩形とルーペの可視性・全面暗転が切り替わります
  （モードの解釈は `FadeMaskDragRectControl.Core` が単一所有）。
- **状態同期の内蔵**: 選択矩形の変更・開始・キャンセルを暗転マスクへ同期し、前の矩形の残留を防ぎます。
- **ルーペの自動配置**: 中心（ドメイン座標）+ オフセットから表示位置を計算し、親領域内へ補正します
  （計算はコアの公開ファサード `Loupe.ResolveLayout` が単一所有）。
- **コア型のイベント引数**: イベントの座標は UI 非依存のコア型（`EsPoint` / `EsRect`）で公開します。
- **外観は提示層が単一所有**: 描画にだけ必要な外観は `MarqueeAppearance` / `LoupeAppearance` を `MarqueeAppearance` / `LoupeAppearance` プロパティで与えます（コアの設定値には色・表示可否を含めません）。

## 使い方

```csharp
using EsUtil.SaveEvidence.FadeMaskDragRect;
using EsUtil.SaveEvidence.FadeMaskDragRect.MewUI;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Feature.Marquee.Presenter.Setting; // MarqueeAppearance（描画にだけ必要な外観）
using EsUtil.Feature.Loupe.Presenter.Setting;   // LoupeAppearance（描画にだけ必要な色）

var selection = new FadeMaskDragRectControl
{
    Mode = FadeMaskDragRectMode.Crop,   // プレビュー内の切出し
    // 設定はコアの単一値で渡す（範囲選択はオフセット 20、切り出しは設定値）
    Options = FadeMaskDragRectOptions.Default with { LoupeOffset = 20.0 },
};

// 色は描画にだけ必要な設定として別の値で与える（コアの設定値には含めない）
selection.MarqueeAppearance = MarqueeAppearance.Default with { OverlayColor = System.Drawing.Color.FromArgb(128, 0, 0, 0) };
selection.LoupeAppearance = LoupeAppearance.Default with { BackgroundColor = System.Drawing.Color.White };

// ドメイン ↔ ローカル DIP の変換ポートを設定する（暗転マスク・選択矩形・ルーペで共用）
selection.SetCoordTransform(new CoordTransform(dip => dip, domain => domain, () => new EsSize(width, height)));

// ルーペの拡大元画像を設定してから、中心を指定して表示する
selection.SetLoupeSource(bitmap);
selection.SetLoupePosition(new EsPoint(120, 340));

// 確定・キャンセルを購読する
selection.ConfirmRequested += (_, e) => Capture(e.SelectionDomainRect);
selection.CancelRequested += (_, _) => Cancel();

// ホイール操作（ズーム・パン）はホストが扱う
// 選択オーバーレイはポインター入力を占有するため、ホイールは本コントロール（またはその親）で受けて
// ビューポート（ZoomViewControl 等）へ転送する（解釈と換算はビューポートが単一所有する）。
selection.MouseWheel += (e) => ForwardWheelToViewport(e);
```

画像の上へ重ねる場合は、親の `Grid` へ追加します（本コントロールが選択の入力を受け取ります）。

```csharp
grid.Add(cropView);     // 画像（下）
grid.Add(selection);    // 暗転マスク + 選択矩形 + ルーペ（上）
```

## 動作モード

| モード | 選択矩形 | ルーペ | 全面暗転 | 用途 |
| --- | --- | --- | --- | --- |
| `Crop` | 有効 | 有効 | 有効 | プレビュー内の切出し |
| `AreaSelect` | 有効 | 有効 | 有効 | 全画面オーバーレイの領域選択 |
| `ScreenSelect` | 無効 | 無効 | 無効 | スクリーン選択（ハイライトのみ。 確定はホスト） |
| `WindowSelect` | 無効 | 無効 | 無効 | ウィンドウ選択（ハイライトのみ。 確定はホスト） |

## 主な API

| メンバー | 説明 |
| --- | --- |
| `Mode` | 動作モード（既定は `AreaSelect`） |
| `Options` | 設定の単一値（選択矩形 / 暗転マスク / ルーペ / 選択矩形破線。 コアの `FadeMaskDragRectOptions`。 **色は含まない**） |
| `MarqueeAppearance` / `LoupeAppearance` | 描画にだけ必要な外観の単一値（提示層の `MarqueeAppearance` / `LoupeAppearance`。 内包コントロールへ委譲） |
| `HasSelection` / `SelectionDomainRect` | 現在の選択状態（ドメイン座標） |
| `HighlightDomainRect` | ハイライト（`ScreenSelect` / `WindowSelect` の注目矩形。 null はハイライトなし） |
| `ShowFullOverlay` | 全面暗転の有無（モードから導出） |
| `LoupeCenterPoint` | ルーペ中心（ドメイン座標） |
| `OperationTextProvider` / `InfoTextProvider` | 操作ラベル（2 行目）・情報ラベル（3 行目）の文言を描画時に生成するデリゲート（文言はホストが与える） |
| `SizeLabelOverrideProvider` | サイズラベルの文言を描画時に生成するデリゲート（文言はホストが与える） |
| `DipToDomainScaling` / `DisplayScaleScaling` / `LoupeDipToDeviceScaling` | DPI スケーリング（設定には含めない環境値。 コードから動的設定） |
| `Log` | 操作ログの出力先（null は `NullLogger`。 内包するドラッグ選択コントロールとルーペへ伝搬する） |
| `SetCoordTransform(ICoordTransform?)` | 座標変換ポートを一括設定（ドラッグ選択・暗転マスクへ分配。 null の場合は倍率からコアが解決する） |
| `SetSelection` / `ClearSelection` | 選択状態の外部設定・クリア |
| `SetLoupeSource(EsBitmap?)` / `SetLoupePosition(EsPoint)` / `ClearLoupe` | ルーペの制御 |
| `RequestConfirm` / `RequestCancel` | Enter / Escape 相当の確定・キャンセル要求 |
| `RefreshOverlay` / `InvalidateOverlay` | 表示の変化の通知・暗転マスクの再描画要求 |

## イベント

`ConfirmRequested` / `CancelRequested` / `LoupePositionMoved`

ホイール操作（ズーム・パン）と表示の変化の追従は本コントロールの配線ではありません（コントロール化を前提とした処理）。
ホイールはホストが受け取ってビューポートへ転送し、表示の変化はホストが検知して `RefreshOverlay()` を呼びます。

## Avalonia 版との違い

| 項目 | Avalonia 版 | MewUI 版 |
| --- | --- | --- |
| イベント引数の座標 | Avalonia の `Point` / `Rect` | **コア型の `EsPoint` / `EsRect`** |
| 外観設定 | コアの `FadeMaskDragRectOptions` を 1 プロパティとして公開する（既定値・分配規則はコアが単一所有）。 外観は `MarqueeAppearance` / `LoupeAppearance` プロパティで与える | 同じコアの設定値を 1 プロパティとして公開する（同一の型・同一の既定値）。 外観のプロパティも同じである |
| ルーペの位置決め | ルーペコントロール自身が座標変換ポートから算出 | **本コントロールが `ResolveLayout`（コアのファサード）で配置**（MewUI のルーペは位置を持たない） |
| サイズラベル・操作ラベル・情報ラベル | サイズラベルはコントロールがテキスト要素として表示し、操作・情報ラベルはホスト（`FadeMaskDragRectControl`）が `Options.Labels` の外観で描く（文言は `CaptureLabelTexts` が与える） | 同左（コアの同じ設定値を使うため見た目も一致する） |
| ルーペ画像上の選択矩形破線 | 描画完了イベント経由で描画 | 同じコアの破線幾何を使い、ルーペの `Rendered` で同一のキャンバスへ描画する（見た目は一致） |
| ホイール操作 | **扱わない**（ホストが購読） | **扱わない**（ホストが `MouseWheel` を購読） |
| ログ | `ILogger` を注入して内包コントロールへ伝搬 | `Log` プロパティで同様に伝搬する（書式は同一） |

## テスト

```powershell
dotnet test src/FadeMaskDragRectControl.MewUI/tests/FadeMaskDragRectControl.MewUI.Tests.csproj
```

モード切替・選択状態の同期・外観の委譲・座標変換ポート・ルーペの配置（オフセットと位置の補正）・ホストへの通知を検証します。

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
