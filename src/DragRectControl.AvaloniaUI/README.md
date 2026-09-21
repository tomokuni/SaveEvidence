# DragRectControl

> プロジェクト名: `DragRectControl.AvaloniaUI`（Avalonia UI 版）。UI 非依存のロジックは `DragRectControl.Core`、MewUI 版は `DragRectControl.MewUI` にあります。

## 目次

- [DragRectControl](#dragrectcontrol)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. インストール](#4-インストール)
  - [5. クイックスタート](#5-クイックスタート)
    - [AXAML](#axaml)
    - [Code-behind（イベント購読・座標ポート設定）](#code-behindイベント購読座標ポート設定)
  - [6. 座標系](#6-座標系)
  - [7. サンプル](#7-サンプル)
  - [8. ドキュメント](#8-ドキュメント)
  - [9. プロジェクト構成](#9-プロジェクト構成)
  - [10. ライセンス](#10-ライセンス)

---

## 1. 概要

Avalonia UI 用のリサイズ可能な領域選択コントロールです。マウスドラッグ・キーボード微調整（nudge）・描画をコントロール内で完結します。

## 2. 機能一覧

- マウスドラッグによる領域選択
- 選択矩形のドラッグ移動
- 8方向ドラッグハンドルによるリサイズ
- 矢印キーによる1Pixel単位のキーボード微調整（nudge）
- ドメイン座標系で正確な nudge（矢印キー1回 = ドメイン1Pixel）
- 選択枠線・ハンドル・始点マーカーの自己描画
- カーソル座標ラベルの表示（任意設定・`(x, y)` 形式）
- ダブルクリックの確定/キャンセル判定（矩形内 = 確定 / 矩形外 = キャンセル）
- Avalonia Binding 対応の StyledProperty
- ドメイン↔DIP 座標変換ポート（`ICoordTransform` 直接注入。型は `Platform.Core` 提供）
- **閉区間描画（始点・終点を含む）**: 選択枠・ハンドルは終点（Right/Bottom）に +1 して DIP 変換（FadeMask の非暗転領域・キャプチャの閉区間 +1 と一致）
- **ホイール操作（祖先 ScrollViewer が存在する Crop モード）**: Ctrl+ホイールで `ZoomRequested` / 通常ホイールで ScrollViewer をスクロール（Shift 併用で横スクロール）
- **薄い UI 層**: 入力の解釈（nudge セッション・確定/キャンセル・スナップ・クリック検出・最小サイズ判定）はコアの状態管理クラス（`DragRectSession`）が担う。本コントロールは「入力の受付」「効果の適用」「通知の転送」「描画」のみを行う

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- C# 14

## 4. インストール

現在は NuGet パッケージではなく、プロジェクト参照で利用します。

```xml
<ProjectReference Include="..\DragRectControl.AvaloniaUI\src\DragRectControl.AvaloniaUI.csproj" />
```

必要な名前空間:

```csharp
using Avalonia;
using Estable.AvaloniaUI.DragRect;
using EsUtil.Platform.Core.Coordinate; // ICoordTransform / CoordTransform（コア提供）
using EsUtil.Platform.Core.Geometry;   // EsPoint / EsSize（コア提供）
```

## 5. クイックスタート

### AXAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:dragrect="clr-namespace:Estable.AvaloniaUI.DragRect;assembly=DragRectControl.AvaloniaUI">
    <Canvas>
        <dragrect:DragRectControl x:Name="SelectionOverlay"
                                   BorderColor="#FFA9A9A9"
                                   BorderThickness="2.0"
                                   HandleColor="White"
                                   HandleSize="10.0"
                                   HandleHitRadius="12.0"
                                   MinSelectionSize="6"
                                   MarkerColor="White"
                                   MarkerLength="16.0" />
    </Canvas>
</Window>
```

### Code-behind（イベント購読・座標ポート設定）

```csharp
var dragRect = new DragRectControl
{
    BorderColor = Colors.White,
    HandleSize = 10.0,
};

// 結果は公開イベントで通知される（マウス・キーイベントを直接処理しない）
dragRect.SelectionChanged += (_, e) => UpdatePreview(e.Rect);
dragRect.SelectionCompleted += (_, e) => ConfirmSelection(e.Rect);
dragRect.SelectionCancelled += (_, _) => CancelSelection();
dragRect.ConfirmRequested += (_, e) => ConfirmSelection(e.Rect);
dragRect.CancelRequested += (_, _) => CancelSelection();

// 座標ポートを設定（ドメイン = 画像ピクセル等）。ポート生成は Platform.AvaloniaUI の CoordTransform ファクトリを使用する。
dragRect.SetCoordinateTransform(CoordTransform.DomainScale(() => 2.0)); // 例: DPI 200%（Forward = DIP→ドメイン × 2）
// 複雑な変換は Compose / ScrollOffset / ImageLayout 等で組み立てる

panel.Children.Add(dragRect);
```

## 6. 座標系

DragRectControl は 3 つの座標系を使い分けます。

| 座標系 | 用途 |
| --- | --- |
| ドメイン座標 | 選択状態・nudge・イベント通知 |
| コントロールローカル DIP | マウス入力・描画 |
| 画面物理 Pixel | カーソル操作・画面変換 |

ドメイン ↔ DIP の変換は `SetCoordinateTransform(ICoordTransform?)` で注入します（Forward = DIP→ドメイン / Inverse = ドメイン→DIP / Bounds = ドメイン境界）。

## 7. サンプル

`sample/` に動作確認用のサンプルアプリが含まれています。マウスドラッグによる選択・nudge・外観変更・イベントログを確認できます。

```bash
dotnet run --project sample/DragRectControl.AvaloniaUI.Sample.csproj
```

詳細は [sample/README.md](sample/README.md) を参照してください。

## 8. ドキュメント

- [外部機能仕様書](SPEC_ExtFunc.md)
- [システム仕様書](SPEC_System.md)

## 9. プロジェクト構成

```text
DragRectControl/
├── src/
│   ├── DragRectControl.csproj     # 独立プロジェクト（Platform.AvaloniaUI + DragRectControl.Core を参照）
│   ├── DragRectControl.cs         # 公開 API + 効果の適用 + 通知の転送 + ポインター入力 + 描画
│   ├── DragRectControl.Input.cs   # キーボード入力（コア型 EsKey への変換と通知）
│   ├── DragRectControl.Visual.cs  # ビジュアルツリー接続・ScrollViewer 連携・描画
│   ├── SelectionRectEventArgs.cs  # 公開イベント引数（選択矩形通知）
│   ├── DragRectPointEventArgs.cs  # 公開イベント引数（座標通知）
│   └── Drawing/                   # 内部型（DragRectDrawer）
├── sample/                        # サンプルアプリ（sample/README.md 参照）
└── tests/                         # 単体テスト（公開 API 経由）
```

### 9.1 共有コア（`DragRectControl.Core`）

UI 非依存の幾何計算・状態機械・入力の解釈は `src/DragRectControl.Core` が単一所有します（Avalonia / MewUI で共有）。

```text
DragRectControl.Core/
├── src/
│   ├── DragHandle.cs                 # 公開型（ドラッグハンドル種別 enum）
│   ├── DragRectInteractionState.cs   # 公開型（対話状態 enum）
│   ├── DragRectSnapshot.cs           # 公開型（状態 Snapshot record。座標は EsRect / EsPoint）
│   ├── DragRectSession.cs            # 内部型（状態管理クラス。入力の解釈・通知・効果）
│   ├── DragRectNotice.cs             # 内部型（コア → UI の通知の種別と値）
│   ├── DragRectFeedback.cs           # 内部型（コア → UI の効果。カーソル操作を含む）
│   ├── DragRectLog.cs                # 内部型（入力の判断の LoggerMessage 定義）
│   ├── DragRectState.cs              # 内部型（状態保持 + 更新ロジック）
│   └── Selection/                    # 内部型（SelectionGeometry）
└── tests/                            # 単体テスト（UI 非依存・並列実行）
```

## 10. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
