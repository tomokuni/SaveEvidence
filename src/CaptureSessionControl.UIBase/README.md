# CaptureSessionControl.UIBase

`CaptureSessionControl`（撮影・切り出しのセッション）の **UI 間で共有する処理**です。Avalonia 版と MewUI 版が
同じ設定写像を使うことを保証します。

## 目的

- **設定から描画の設定（色）への写像を 1 箇所へ集約**する。色は描画にだけ必要な設定であり、描画層（UIBase）の
  `MarqueePaint` / `LoupeAppearance` が単一所有するため、写像も 1 つでなければならない（写像漏れはビルドが通り、
  色が無言で効かなくなる）。
- **製品の設定型を再利用ライブラリへ持ち込まない**。写像元がセッションの設定型（`CaptureSettings`）であるため、
  本層は `CaptureSessionControl` 側の共有層として置く。

## 層の位置付け

```text
CaptureSessionControl.Core ─────────────┐（設定型・寸法・表示可否）
Feature.Marquee.Presenter / Feature.Loupe.Presenter ─┐（描画の設定＝色）
                                             ↓
                              CaptureSessionControl.UIBase（色の写像）
                                             ↓
              CaptureSessionControl.AvaloniaUI / .MewUI（ホスト・配線）
```

- `CaptureSessionControl.Core` は描画層を参照できない（Core → UIBase の逆流になる）。したがって**色の写像は
  コアに置けない**。
- `FadeMaskDragRectControl.UIBase` は再利用ライブラリであるため、製品の設定型（`CaptureSettings`）へ依存させない。
  よって本層は `CaptureSessionControl` 側に置く。
- **寸法・表示可否は本層が写像しない**（`FadeMaskDragRectOptionsMapper`（コア）が単一所有する）。本層は色だけを扱う。

## 構成

| 型 | 可視性 | 責務 |
| --- | --- | --- |
| `CapturePaintMapper` | `internal static` | `CaptureSettings` → `MarqueePaint` / `LoupeAppearance` の写像 |

## 依存関係

```text
CaptureSessionControl.UIBase
    ├── CaptureSessionControl.Core（CaptureSettings）
    ├── Feature.Marquee.Presenter（MarqueePaint）
    └── Feature.Loupe.Presenter（LoupeAppearance）
```

UI フレームワークへ依存しません。外部パッケージへの直接依存もありません。

`internal` は `InternalsVisibleTo`（`CaptureSessionControl.AvaloniaUI` / `CaptureSessionControl.MewUI` /
`CaptureSessionControl.UIBase.Tests`）で公開します。

## 主な API

```csharp
MarqueePaint marquee = CapturePaintMapper.ToMarqueePaint(settings);           // 設定 → 選択矩形の色（9 項目）
LoupeAppearance loupe = CapturePaintMapper.ToLoupeAppearance(settings, background);     // 設定 → ルーペの色（7 項目）
```

画像外（ペーストボード）の色は**設定（`LoupePasteboardColor`）が単一所有**します（描画層は本色で描画面全体を塗ってから拡大画像を重ねるため、画像の無い部分が本色の無地になります）。
透かすため透明、プレビューの切り出しはテーマ背景色）。

## 設計上の約束

1. **色だけを写像する**。寸法・表示可否はコアの設定マッパーが単一所有する。
2. **枠線の色は 1 つ**: 選択枠線（ハンドルの枠線）と暗転マスクの穴あけ枠は同じ矩形に重なるため、同じ設定値
   （`BorderColor`）を両方へ割り当てる。
3. **内側の塗り・内側の枠線は写像しない**（描画の可否は `MarqueeDecoOptions` のフラグが単一所有する）。
4. **画像外の色は設定**（`LoupePasteboardColor`。ホストは色を引数で渡さない）。
5. 引数の `settings` は `ToNonNull` 済みを前提とする（null は例外）。

## テスト

```text
tests/
└── CapturePaintMapper.Tests.cs  # 写像の網羅・枠線の共有・画像外の色・引数の検証（6 件）
```

```powershell
dotnet test src/CaptureSessionControl.UIBase/tests/CaptureSessionControl.UIBase.Tests.csproj
```

UI 依存の検証（ホストの配線・実際の描画）は各 UI のテストプロジェクトが担います。

## ライセンス

MIT License（全文は [`../../LICENSE`](../../LICENSE) を参照）。
