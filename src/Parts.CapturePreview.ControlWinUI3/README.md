# Parts.CapturePreview.ControlWinUI3

画面キャプチャの撮影スタック（撮影セッション・切り出しビュー・領域選択・ズーム表示）を **WinUI 3** で提供する複合コントロール集です。
UI 非依存のコア（`Parts.CapturePreview.Core`）と同じ規則を使い、表示だけを Composition と XAML の要素で行います。

## 1. 目的

撮影と切り出しの状態・判断・計算は `Parts.CapturePreview.Core` が所有します。 本プロジェクトは
**入力の受付・効果の適用・描画・配線**だけを担い、非公開の WinUI 3 のコントロールとして振る舞います。
アプリ（`App.WinUI3`）は本プロジェクトの公開 API（`Session.CaptureSessionControl` と各コントロール）だけで撮影スタックを使えます。

## 2. 収録するコントロール

| 型 | 内容 |
| --- | --- |
| `Session.CaptureSessionControl` | 撮影と切り出しのFacade。 **本プロジェクトの公開 API の中心**（セッションの生成・排他・破棄を所有する） |
| `Crop.CropViewControl` | 画像表示（拡大・パン・Fit）＋外部提供矩形のオーバーレイ＋領域選択の複合コントロール |
| `ZoomView.ZoomViewControl` | ズーム・パン可能な画像表示コントロール（Composition の画像と XAML の矩形で表示する） |
| `SelectionOverlay.SelectionOverlayControl` | 領域選択の複合コントロール（選択矩形 + 暗転マスク + ルーペ） |
| `SelectionOverlay.LoupePositionEventArgs` / `SelectionOverlay.SelectionConfirmedEventArgs` / `ZoomView.ZoomChangedEventArgs` | 公開イベントの引数 |

内部の型（描画層・セッションの実装）は公開しません（単体テストだけが参照します）。

## 3. 公開 API の使い方

```csharp
using EsUtil.SaveEvidence.Parts.CapturePreview.Core.Capture;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Crop;
using EsUtil.SaveEvidence.Parts.CapturePreview.WinUI3.Session;

// 1. 撮影スタックを生成する（サービスの生成と破棄は Facade が担う）
using var capture = new CaptureSessionControl(logger: logger);

// 2. 撮影する（キャンセル・失敗は null）
var result = await capture.StartCaptureAsync(CaptureType.AreaSelect, window);
if (result?.Image is { } image)
{
    cropView.Source = image; // プレビューへ反映する
}

// 3. 切り出しはビューの表示中の画像を対象にする（確定時は範囲、キャンセル時は 0x0）
var bounds = await capture.StartCropAsync(cropView, window);
```

## 4. ビルド

```powershell
dotnet build src/Parts.CapturePreview.ControlWinUI3/src/Parts.CapturePreview.ControlWinUI3.csproj
```

## 5. テスト

`tests/` が本プロジェクトの単体テストを持ちます。 描画リストの内容と Facade の状態だけを検証するため、
XAML の初期化を必要とせず、すべて `[Fact]` / `[Theory]` で並列実行されます。

```powershell
dotnet test src/Parts.CapturePreview.ControlWinUI3/tests/Parts.CapturePreview.ControlWinUI3.Tests.csproj
```

**検証できない範囲**: WinUI 3 のコントロールは、XAML のランタイムを初期化していないプロセスでは生成できません
（実測: `COMException` 0x80040154 `REGDB_E_CLASSNOTREG`）。 そのためコントロールのインスタンスを要する
検証（入力・配置・重なり順）はアプリでの動作確認に委ねます。 詳細は [docs/SPEC_System.md](docs/SPEC_System.md) を参照してください。

## 6. ドキュメント

- [docs/SPEC_API.md](docs/SPEC_API.md) — 公開 API のリファレンス
- [docs/SPEC_System.md](docs/SPEC_System.md) — 構造・依存・設計判断

## 7. 注意点

| 項目 | 内容 |
| --- | --- |
| 切り出しの対象 | `Crop.CropViewControl` が**表示している画像**（`Source`）から解決する。 画像を別引数で渡さない（表示している画像と切り出す画像を二重に持たない） |
| 実行の排他 | 撮影と切り出しは同時に 1 つだけ実行できる。 実行中の要求は無視する（既存のセッションを中断しない） |
| 切り出しの結果 | 確定は元画像上の範囲（`EsRectInt`）、キャンセルは 0x0 の空矩形、開始できなかった場合は null |
| 画像の反映 | 確定した画像の反映（取り消しの退避・プレビューの差し替え）はアプリが行う（本ライブラリは行わない） |
| ウィンドウ | 撮影はオーナーウィンドウを必要とする（撮影中は画面外へ退避し、終了後に戻す） |
| 描画の座標 | コントロールは DIP で扱い、描画面（物理ピクセル）との倍率を描画のたびに解決する |

## 8. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
