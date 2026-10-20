# Platform.WinForms

`EsUtil.Platform.Core`（UI フレームワーク非依存の抽象・値型）と **WinForms / GDI+**（`System.Windows.Forms` / `System.Drawing`）の型を相互変換し、GDI+ で描くための表示ビューと描画プリミティブを提供するプラットフォーム層です。

## 1. 目的

コアロジック（`EsPoint` / `EsKey` / `EsWindowState` 等）は UI フレームワークに依存しないため、UI 境界でのみ型変換が必要です。 `Platform.AvaloniaUI` / `Platform.MewUI` と同じ役割を WinForms 向けに提供します。

**MewUI 版と同じく Skia を使いません**（WinUI 3 版も使いません）。 描画は WinForms 自身の描画 API（GDI+ / `System.Drawing`）で行います。

## 2. 収録する型

| 型 | 内容 |
| --- | --- |
| `EsKeyWinFormsInterop` | `EsKey` ⇔ `System.Windows.Forms.Keys`（`ToWinFormsKeys()` / `ToEsKey()` / `IsRepresentable()`） |
| `EsGeometryWinFormsInterop` | `EsPoint` / `EsSize` / `EsVector` / `EsRect` ⇔ GDI+ の対応型（`PointF` / `SizeF` / `RectangleF` と整数版） |
| `EsWindowStateWinFormsInterop` | `EsWindowState` ⇔ `System.Windows.Forms.FormWindowState` |
| `EsPlatformHandleWinFormsInterop` | WinForms の `Control` → `EsPlatformHandle`（`Control.Handle` は `nint`。 **未生成のハンドルを生成しない**） |
| `WindowPlacementMath` | ウィンドウ配置の DIP ⇔ 画面物理ピクセルの変換、スケールの正規化、復元可否の判定（純粋関数）。 **換算と正規化は `EsUtil.Platform.Core` の `DpiScaler`（UI 共通の単一所有者）へ委譲する** |
| `MonitorDpi` | 指定位置のモニターの DPI を照会する（`GetDpiForMonitor`）。 **WinForms の公開 API にモニター単位の DPI 照会が無いため本層が担う** |
| `Imaging.GdiImageView` | `EsReadOnlyBitmap` を親が割り当てた領域へ描画する表示ビュー（`Control` 派生。 `Source` を設定するだけで描く） |
| `Imaging.GdiImageRenderer` | GDI+ の描画面へ画像を 1 回の描画で敷く描画層（`GdiImageView` と `EsReadOnlyBitmap` を敷く各コントロールが共用。 画素で検証できる） |
| `Imaging.EsBitmapDrawingInterop` | `EsReadOnlyBitmap` を GDI+ の `Bitmap` へ複製する（**描画に直接使える形へ移す唯一の入口**） |
| `Imaging.GdiText` / `Imaging.GdiFill` / `Imaging.GdiLabel` | GDI+ のテキスト測定・描画、矩形の塗り（角丸を含む）、ラベル 1 件（背景 + 文字）の描画 |
| `Imaging.ClientPixelScale` | 描画面（物理ピクセル）と表示領域（DIP）の比から、描画に使う実効倍率を解決する。 **DPI の通知はハンドル生成より前に届かないため、保持値だけに頼ると 100% で描いて画像とずれる** |
| `Imaging.ImageDrawMath` / `Imaging.ImageDrawMode` | 画像を描画先の領域へ配置する矩形計算（縦横比を保つ / 引き伸ばす） |

依存方向:

```text
Platform.WinForms（本プロジェクト）
  └──> EsUtil.Platform.Core（コア型・画像・DPI 換算）
  └──> System.Windows.Forms / System.Drawing（WinForms のフレームワーク）
```

UI 実装（WinForms ホスト）は本プロジェクトを参照し、境界で 1 行変換します。

## 3. 非対称性と MewUI 版との差

| 項目 | 内容 |
| --- | --- |
| キーの対応付け | **値ではなく名前**で行う（`EsKey` は序数、`Keys` は Win32 仮想キーコード。 採番体系が異なる） |
| 名前が異なるキー | 15 組（`LeftShift` ⇔ `LShiftKey` / `OemTilde` ⇔ `Oemtilde` / `ImeConvert` ⇔ `IMEConvert` 等）を別名表で吸収する |
| 対応先がないキー | WinForms が表現できない `EsKey`（`DbeRoman` / `FnLeftArrow` / `MediaHome` 等）は `Keys.None` へフォールバックする |
| ウィンドウ状態 | 全画面（`EsWindowState.FullScreen`）は WinForms の `FormWindowState` に無い。 `IsRepresentable()` で判定でき、写像時は `Normal` へ落ちる |
| 画像の描画 | Skia のキャンバスを持たないため、`EsReadOnlyBitmap` を GDI+ の `Bitmap` へ複製してから描く（**複製は全面のコピーであり高価**なので、画像の差し替え時に 1 度だけ作る） |
| 描画の補間 | 拡大・縮小は最近傍で行う（Skia の既定の描画と同じ見え方。 縁の 1 画素が薄くならないよう `PixelOffsetMode.Half` を指定する） |
| 色 | コアの色型は `System.Drawing.Color` そのものであるため、色の変換を持たない |

## 4. ビルド

```powershell
dotnet build src/Platform.WinForms/src/Platform.WinForms.csproj
```

## 5. テスト

`tests/` が本プロジェクトの単体テストを持ちます。 描画の検証はオフスクリーンの `Bitmap` と `Graphics.FromImage` で行うため、ウィンドウ（ハンドル）を生成せずに画素を実測できます。

```powershell
dotnet test src/Platform.WinForms/tests/Platform.WinForms.Tests.csproj
```

## 6. 使い方

コア型と GDI+ の型は拡張メソッドで相互変換します。 `using EsUtil.SaveEvidence.Platform.WinForms;` のみで利用できます。

```csharp
// キー（EsKey.Back ⇔ Keys.Back。 未定義値・未対応は Keys.None）
Keys key = esKey.ToWinFormsKeys();

// 幾何（コア型 ⇔ GDI+ の型）
PointF point = esPoint.ToPointF();
EsRect rect = rectangleF.ToEsRect();

// 画像（コアの画像 → GDI+ のビットマップ。 破棄は呼び出し側）
using Bitmap bitmap = EsBitmapDrawingInterop.ToDrawingBitmap(readOnlyBitmap);
```

画像を領域いっぱいへ敷くだけの場合は、表示ビューをそのまま使います。

```csharp
var view = new GdiImageView { Dock = DockStyle.Fill, Source = readOnlyBitmap };
parent.Controls.Add(view);
```

詳細は [docs/SPEC_API.md](docs/SPEC_API.md) を参照してください。

## 7. ドキュメント

- [docs/SPEC_API.md](docs/SPEC_API.md) — 公開 API のリファレンス
- [docs/SPEC_System.md](docs/SPEC_System.md) — アーキテクチャ・依存・設計判断

## 8. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
