using Estable.Platform.Geometry;
using Estable.Platform.Imaging;

namespace Estable.CaptureSession;

/// <summary>撮影・切り出しの結果を適用する対象（アプリ側が実装する）。</summary>
/// <remarks>
/// 撮影・切り出しのセッションは UI 非依存にできるが、結果の反映（Undo 履歴への退避・未保存化・<br/>
/// ステータス通知・画像の差し替え）はアプリのドメイン操作のため、アプリ側に残す。<br/>
/// 本ポートがその境界になり、セッションはアプリの型（ViewModel・設定サービス等）を知らずに結果を渡せる。<br/>
/// <br/>
/// <b>実装側の責務</b>:<br/>
/// 1. <see cref="CurrentImage"/> で現在の画像を返す（切り出しの対象・ルーペのソースに使う）。<br/>
/// 2. <see cref="ImageChanged"/> で画像の差し替えを通知する（セッションはルーペのソースと座標変換を更新する）。<br/>
/// 3. <see cref="ApplyCrop"/> で切り出しを適用する（Undo 履歴への退避・未保存化・画像の差し替え）。
/// </remarks>
public interface ICaptureTarget
{
    /// <summary>現在の画像を取得する。</summary>
    /// <value>表示中の画像。未設定の場合は null。</value>
    /// <remarks>所有権は実装側にあり、セッションは破棄しない。</remarks>
    EsBitmap? CurrentImage { get; }

    /// <summary>画像が差し替わったときに発生する。</summary>
    /// <remarks>
    /// セッションは本イベントでルーペのソース（切り出し対象の画像）と座標変換を更新する。<br/>
    /// <see cref="ApplyCrop"/> の実装が画像を差し替えた場合も発行すること。
    /// </remarks>
    event EventHandler? ImageChanged;

    /// <summary>切り出しを適用する。</summary>
    /// <param name="bounds">切り出し範囲（元画像のピクセル座標・閉区間済み）。</param>
    /// <remarks>
    /// 範囲の計算（クランプ・丸め・閉区間化・最小サイズ判定）は <see cref="CropBoundsMath"/> が済ませているため、<br/>
    /// 実装側は受け取った範囲で画像を切り出して差し替えるだけでよい。<br/>
    /// Undo 履歴への退避・未保存化・ステータス通知は実装側の責務とする（アプリのドメイン操作のため）。
    /// </remarks>
    void ApplyCrop(EsPixelRect bounds);
}
