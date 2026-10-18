# Parts.CapturePreview.ControlWinForms

撮影スタック（`Parts.CapturePreview.Core`）を **WinForms / GDI+** で使うためのコントロール層です。 `Parts.CapturePreview.ControlMewUI` の WinForms 版であり、`Parts.CapturePreview.ControlAvaloniaUI` / `Parts.CapturePreview.ControlMewUI` と同じ UI 非依存コアを使います。

## 1. 目的

撮影スタックの UI 層（切り出しビュー・領域選択オーバーレイ・ズーム ビューポート・撮影セッション）を WinForms で提供します。

**機能は既存の UI 版と同等**です。 判断・値・計算はすべてコア（`Parts.CapturePreview.Core`）が単一所有し、本プロジェクトは「入力の受付」「効果の適用」「通知の転送」「描画」「ホスト結線」だけを行います。

| 機能 | 内容 |
| --- | --- |
| ズーム・パン・Fit | 画像ビューポート（`ZoomViewControl`）。 段階ズーム・ホイール操作・スクロールバーをオーバーレイ描画する |
| 切り出しビュー | 画像表示 + 外部提供矩形オーバーレイ + 強調表示の統合（`CropViewControl`） |
| 領域選択 | 暗転マスク + 選択矩形 + ルーペ（`SelectionOverlayControl`）。 `EsUtil.Feature.Marquee.ControlWinForms` と `EsUtil.Feature.Loupe.ControlWinForms` を内包する |
| 撮影セッション | 画面全体を覆う選択オーバーレイから切り出した画像を返す（`CaptureSessionControl`）。 キャプチャ除外が効いている環境ではオーナーを退避しない（オーバーレイが画面全体を覆うため見た目は変わらない。 退避すると再合成で一瞬明るい画面が見える）。 透明度の切り替えは使わず、下地色（撮影画像の平均色へ暗転色を重ねた色）を最初から最終の見た目に合わせる（透明度を切り替えると、未描画の面が一瞬白く合成される） |
| 切り出しセッション | 表示中の画像を対象にした切り出し（確定・キャンセル・非アクティブ化でキャンセル） |

**WinForms 固有の制約**: 子コントロールは親の描画を自分の背景として塗ります。 そのため**重ねた兄弟は下の兄弟の描画を覆い隠す**（覆われた側は描画がクリップされ、`WM_PAINT` すら届かない）。 本プロジェクトは「表示する必要のあるオーバーレイだけを表示する」ことでこれを避けます（例: 矩形が無いときの `RectOverlayCanvas` は非表示。 切り出し中の領域選択オーバーレイは最前面へ出す）。

## 2. プロジェクト構成

```text
Parts.CapturePreview.ControlWinForms/
  README.md
  docs/
    SPEC_API.md
    SPEC_System.md
  src/
    Crop/
    Overlay/
    SelectionOverlay/
    Session/
    ZoomView/
  tests/
    Crop/
    SelectionOverlay/
    Session/
    ZoomView/
```

## 3. 使い方

アプリ側で 2 つのオブジェクトを用意します。 撮影と切り出しは同時に 1 つだけ実行できます（排他はコアが単一所有します）。

```csharp
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinForms.Session;

// 1. 切り出しビュー（画像表示 + 領域選択）を画面へ置く
var cropView = new CropViewControl
{
    Dock = DockStyle.Fill,
    Source = currentImage,          // 切り出しの対象は「表示中の画像」である
};
previewHost.Controls.Add(cropView);

// 2. セッションの窓口（撮影サービスを内部で生成する）
using var capture = new CaptureSessionControl();

// 3. 切り出しを開始する（確定時は元画像上の範囲、キャンセル時は 0x0 の空矩形）
var bounds = await capture.StartCropAsync(cropView, mainForm);
if (bounds is { Width: > 0, Height: > 0 })
{
    ApplyCrop(bounds.Value);        // 適用（Undo 退避・画像の差し替え）はホストが行う
}
```

撮影は画像を返します。 画像の反映は `CaptureCompleted`（同期イベント）で行います。

```csharp
capture.CaptureCompleted += (_, e) =>
{
    // イベントはオーナーウィンドウの再表示より前に発火する（画像を差し替えてから再表示できる）
    if (e.Result is { } result)
    {
        SetImage(result.Image);
    }
};

var result = await capture.StartCaptureAsync(CaptureType.AreaSelect, mainForm);
```

## 4. ビルド

```powershell
dotnet build src/Parts.CapturePreview.ControlWinForms/src/Parts.CapturePreview.ControlWinForms.csproj
```

## 5. テスト

`tests/` が本プロジェクトの単体テストを持ちます。 描画は `Bitmap` と `Graphics.FromImage` で検証し、ハンドル（ウィンドウ）を生成する操作（`CreateControl` / `PointToScreen` 等）はテストしません。

```powershell
dotnet test src/Parts.CapturePreview.ControlWinForms/tests/Parts.CapturePreview.ControlWinForms.Tests.csproj
```

## 6. ドキュメント

- [docs/SPEC_API.md](docs/SPEC_API.md) — 公開 API のリファレンス
- [docs/SPEC_System.md](docs/SPEC_System.md) — アーキテクチャ・依存・設計判断

## 7. 依存

| 参照先 | 用途 |
| --- | --- |
| `Parts.CapturePreview.Core` | 撮影・切り出し・選択の共通規則と提示の写像（UI 非依存） |
| `Platform.WinForms` | コア型 ⇔ GDI+ 型の変換、DIP 換算、画像の表示ビューと描画プリミティブ |
| `EsUtil.Platform.Core` | コア型・画像・DPI 換算・Win32 サービス（キャプチャ・要素検出・ウィンドウ操作） |
| `EsUtil.Feature.Marquee.ControlWinForms` | 選択矩形コントロール（暗転マスク・ハンドル・ラベル） |
| `EsUtil.Feature.Loupe.ControlWinForms` | ルーペ コントロール（拡大鏡） |

## 8. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
