# LineDetection

## 目次

- [LineDetection](#linedetection)
  - [目次](#目次)
  - [1. 概要](#1-概要)
  - [2. 機能一覧](#2-機能一覧)
  - [3. システム要件](#3-システム要件)
  - [4. インストール](#4-インストール)
  - [5. クイックスタート](#5-クイックスタート)
    - [ViewModel](#viewmodel)
    - [検出結果](#検出結果)
  - [6. サンプルアプリケーション](#6-サンプルアプリケーション)
  - [7. ドキュメント](#7-ドキュメント)
  - [8. プロジェクト構成](#8-プロジェクト構成)
  - [9. ライセンス](#9-ライセンス)

---

## 1. 概要

Avalonia UI 用の**ウィンドウ枠となる辺（線分）の検出ライブラリ**です。キャプチャ画像（スクリーンショット等）から、Windows のウィンドウ枠となる水平・垂直の辺を検出します。

- **ロジック方式（Canny + Hough）**: モデル不要の画像処理方式。Canny エッジ + HoughLinesP で水平・垂直の辺を検出し、矩形（ウィンドウの 4 辺）を優先出力します。

検出器は共通インターフェース `ILineDetector` で抽象化されるため、方式を差し替えて利用できます。

## 2. 機能一覧

- `ILineDetector` を実装した検出器（ロジック方式）を提供
- 水平・垂直の辺（線分）を `LineDetectionResult` として返却
- `Bitmap`（BGRA）を入力とし、画像のピクセルを解析
- ロジック方式:
  - Canny エッジ + HoughLinesP による水平・垂直の辺検出
  - 同一直線上の断片をクラスタリングして長い辺へ統合
  - 近接する平行線（透過枠で分裂した枠線等）の統合
  - 文字列（断続エッジ）の誤検出を除外（エッジ支持率）
  - 矩形（ウィンドウの 4 辺）を優先出力（対辺同長・コーナー近接）

## 3. システム要件

- .NET 10
- Avalonia 12.1 以降
- OpenCvSharp5（ロジック方式・内部利用）

## 4. インストール

`src/LineDetection/src/LineDetection.csproj` をプロジェクト参照に追加します。

```xml
<ProjectReference Include="..\LineDetection\src\LineDetection.csproj" />
```

## 5. クイックスタート

### ViewModel

```csharp
using Estable.AvaloniaUI.LineDetection;

ILineDetector detector = new LogicLineDetector();

// 検出器は Bitmap を直接入力とする（BGRA）
var lines = detector.Detect(bitmap);
```

### 検出結果

`Detect` は `IReadOnlyList<LineDetectionResult>` を返します。各結果は始点・終点・向き（水平 / 垂直）・スコア（長さ）を持ちます。

```csharp
foreach (var line in lines)
{
    Console.WriteLine($"{line.Orientation}: ({line.Start})-({line.End}) score={line.Score}");
}
```

## 6. サンプルアプリケーション

外観・機能を再現したサンプルアプリが `src/LineDetection/sample/` にあります。クリップボードから画像を貼り付けて検出結果（線分一覧・画像上のオーバーレイ・ルーペ拡大）を確認できます。

`RunSample.bat` または次で起動します。

```powershell
dotnet run --project src/LineDetection/sample/LineDetection.Sample.csproj
```

## 7. ドキュメント

- `SPEC_ExtFunc.md`: 外部機能仕様書（公開 API 仕様）
- `SPEC_System.md`: システム仕様書（アーキテクチャ・内部設計）

## 8. プロジェクト構成

```text
src/LineDetection/
├── src/          # ライブラリ本体（LineDetection.csproj）
├── sample/       # サンプルアプリケーション（LineDetection.Sample.csproj）
└── tests/        # テスト（LineDetection.Tests.csproj）
```

## 9. ライセンス

本ライブラリは SaveEvidence プロジェクトの一部として提供されます。
