# Platform.Native

`Platform.Core`（UI フレームワーク非依存の抽象・値型）に対する、**ネイティブ（OS）実装と実行時プラットフォームの既定実装ファクトリ**を提供するプロジェクトです。

## 目的

サービス抽象（`ICaptureService` / `ICursorService` / `IWindowService` / `IElementFinder` / `ICursorOverrideGuard` / `IGlobalHotKeyService<T>`）の
既定実装は、UI フレームワークに依存していません（`WinNative` の P/Invoke とコア型のみ）。
本プロジェクトへ分離することで、**Avalonia UI / MewUI のどちらからも同じ Win32 実装を再利用**できます。

本プロジェクトは UI フレームワークを一切参照しません（依存は `Platform.Core` と System.Drawing.Common のみ）。

## 提供機能

- **Factory Method パターン**: `PlatformFactory.CreateXxx()` で実行時プラットフォームに応じた既定実装を生成
- **Win32 実装**（Windows）: カーソル操作 / 要素検出 / カーソル上書き抑止 / 画面キャプチャ / ウィンドウ操作 / グローバルホットキー
- **NoOp 実装**（Linux / macOS など）: Win32 DLL 依存を避ける安全なフォールバック
- **画像変換**: `EsBitmapDrawingExtensions`（`System.Drawing.Bitmap` ⇔ `EsBitmap`。Windows 専用）

## 依存方向

```text
Platform.AvaloniaUI（Avalonia 実装）/ app.MewUI
  └──> Platform.Native（本プロジェクト）
         └──> Platform.Core
```

## ビルド

```powershell
dotnet build src/Platform.Native/src/Platform.Native.csproj
```

## テスト

`tests/` が本プロジェクトの単体テストを持ちます。

```powershell
dotnet test src/Platform.Native/tests/Platform.Native.Tests.csproj
```

Win32 API（実 OS 状態）を扱うテストは `Win32` コレクションで直列実行されます。

詳細な設計は `SPEC_System.md`、公開 API は `SPEC_ExtFunc.md` を参照してください。
