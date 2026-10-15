# Platform.WinUI3

`EsUtil.Platform.Core`（UI フレームワーク非依存の抽象・値型）と **WinUI 3**（Windows App SDK・`Microsoft.UI.Xaml`）の型を相互変換する Interop 層です。
撮影スタック（`Parts.CapturePreview.*`）とアプリ（`App.WinUI3`）が、コア型と WinUI 3 の型を境界で 1 行変換するために使います。

## 1. 目的

コアロジック（`EsPoint` / `EsKey` / `EsWindowState` 等）は UI フレームワークに依存しないため、UI 境界でのみ型変換が必要です。
MewUI 版の `Platform.MewUI` と同じ役割を WinUI 3 向けに提供します。

## 2. 収録する型

| 型 | 内容 |
| --- | --- |
| `EsKeyWinUI3Interop` | `EsKey` ⇔ `VirtualKey`（`ToVirtualKey()` / `ToEsKey()` / `IsRepresentable()`） |
| `EsGeometryWinUI3Interop` | `EsPoint` / `EsPointInt` / `EsSize` / `EsVector` / `EsRect` ⇔ 対応する型。 色（`System.Drawing.Color` ⇔ 色型）の変換も含む |
| `EsWindowStateWinUI3Interop` | `EsWindowState` ⇔ ウィンドウの提示（`AppWindowPresenterKind`）と重ね表示の状態（`OverlappedPresenterState`） |
| `EsPlatformHandleWinUI3Interop` | ウィンドウ → `EsPlatformHandle`（Win32 の `HWND` を返す） |
| `WindowPlacementMath` | ウィンドウ配置の DIP ⇔ 画面物理ピクセルの変換、スケールの正規化、復元可否の判定（純粋関数）。 **換算と正規化は `EsUtil.Platform.Core` の `DpiScaler` へ委譲する** |
| `Imaging.SkiaImageView` | `EsReadOnlyBitmap` を親が割り当てた領域へ描画する表示ビュー（`SKXamlCanvas` 派生。 `Source` を設定するだけで描く） |
| `Imaging.SkiaImageRenderer`（内部・描画） | `EsReadOnlyBitmap` を Skia キャンバスへ敷く描画層（`SkiaImageView` が使う。 画素で検証できる） |
| `Imaging.ClientPixelScale` | 描画面（物理ピクセル）と表示領域（DIP）の比から、描画に使う実効倍率を解決する。 **表示倍率の変更通知は描画の前に届かないことがあるため、保持値だけに頼ると 100% で描いて画像とずれる** |
| `Imaging.ImageDrawMath` / `Imaging.ImageDrawMode` | 画像を描画先の領域へ配置する矩形計算（縦横比を保つ収め方 / 領域全体への引き伸ばし） |
| `Imaging.WinUI3TextMeasure` | WinUI 3 のテキストエンジンで文字列の実寸を測る（ラベルを要素として置く場合の入口。 テキストエンジンを持たない環境では Skia の実測で代用する） |

依存方向:

```text
Platform.WinUI3（本プロジェクト）
  └──> EsUtil.Platform.Core（コア型・画像の配置計算）
  └──> Platform.Skia（SKCanvas への描画と画像の相互変換）
  └──> Windows App SDK（Microsoft.UI.Xaml）
  └──> SkiaSharp.Views.WinUI（SKXamlCanvas）
```

## 3. WinUI 3 固有の挙動（注意点）

| 項目 | 内容 |
| --- | --- |
| 幾何型の記憶域 | WinUI 3 の座標型・サイズ型・矩形型は公開プロパティが `double` だが**内部の記憶域は `float`** である。 往復変換では有効桁が `float` まで落ちる |
| 負のサイズ | サイズ型・矩形型のコンストラクターは**負の幅・高さを拒否して例外を投げる**（実測）。 本層は負のサイズを **0 へクランプ**する（座標 X / Y は保持される） |
| 色 | 色型（`Windows.UI.Color`）の成分は `byte` のため、ARGB は変換で失われない |
| ウィンドウ状態 | WinUI 3 は**1 つの列挙型でウィンドウ状態を表さない**。 全画面は提示（`AppWindowPresenterKind`）、通常・最小化・最大化は重ね表示の状態（`OverlappedPresenterState`）が担うため、2 系統の写像を提供する |
| キー | `EsKey` は 223 メンバー、`VirtualKey` は 172 メンバーで体系が異なる（例: `D0` ⇔ `Number0`、`NumLock` ⇔ `NumberKeyLock`）。 **値ではなく名前**で変換する |
| テキスト計測 | 撮影スタックのラベルは Skia のキャンバスへ描くため、`SkiaText`（描画層が所有する測定）で測る。 `Imaging.WinUI3TextMeasure` は**ラベルを要素として置く表示**（XAML のレイアウトへ寸法を渡す場合）で使う |

## 4. ビルド

```powershell
dotnet build src/Platform.WinUI3/src/Platform.WinUI3.csproj
```

## 5. テスト

`tests/` が本プロジェクトの単体テストを持ちます。 値型の変換・純粋計算・Skia の描画だけを検証するため、
XAML の初期化を必要とせず、すべて `[Fact]` / `[Theory]` で並列実行されます。

```powershell
dotnet test src/Platform.WinUI3/tests/Platform.WinUI3.Tests.csproj
```

**検証できない範囲**: XAML の型（`Microsoft.UI.Xaml` のコントロール）は、XAML のランタイムが初期化されていない
プロセスでは生成できません（実測: `COMException` 0x80040154 `REGDB_E_CLASSNOTREG`）。 したがって
`Imaging.SkiaImageView` のインスタンス生成とウィンドウ連携（`EsPlatformHandleWinUI3Interop`）は、
アプリ（`App.WinUI3`）での動作確認に委ねます。 詳細は [docs/SPEC_System.md](docs/SPEC_System.md) を参照してください。

## 6. ドキュメント

- [docs/SPEC_API.md](docs/SPEC_API.md) — 公開 API のリファレンス
- [docs/SPEC_System.md](docs/SPEC_System.md) — 構造・依存・設計判断

## 7. 使い方

コア型と WinUI 3 の型は**拡張メソッド**で相互変換します（`using EsUtil.SaveEvidence.Platform.WinUI3;` の 1 行で使えます）。

```csharp
// キー（名前が異なる組は別名表が吸収する。 未定義値・未対応は None）
VirtualKey key = esKey.ToVirtualKey();
EsKey back = key.ToEsKey();

// 幾何・色（コア型 ⇔ WinUI 3 の型）
Windows.Foundation.Point point = esPoint.ToWinUIPoint();
Windows.UI.Color color = drawingColor.ToWinUIColor();

// ウィンドウ状態（全画面は提示、それ以外は重ね表示の状態へ写す）
AppWindowPresenterKind kind = state.ToAppWindowPresenterKind();
OverlappedPresenterState overlapped = state.ToOverlappedPresenterState();

// ウィンドウ配置（設定は画面物理ピクセル、ウィンドウは DIP）
EsPointInt physical = WindowPlacementMath.ToPhysicalPixelPoint(dipPoint, scale);
```

## 8. ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
