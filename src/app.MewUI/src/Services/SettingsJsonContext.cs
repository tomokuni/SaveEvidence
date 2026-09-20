using System.Text.Json.Serialization;
using Estable.SaveEvidence.Models;

namespace SaveEvidence.MewUI.Services;

/// <summary>設定（<see cref="Settings"/>）の JSON ソース生成コンテキスト。</summary>
/// <remarks>
/// <b>目的</b>: Native AOT / トリミングで配布しても設定の読み書きが動作するようにする。<br/>
/// 反射ベースのシリアル化はトリミングで生成プロパティが削除され、設定が空（<c>{}</c>）になる（実測）。<br/>
/// <br/>
/// <b>定義場所</b>: 本コンテキストは <c>Settings</c> を参照する側（アプリ本体）に置く必要がある。<br/>
/// <c>Settings</c> の公開プロパティは CommunityToolkit.Mvvm のソース生成が <c>app.Core</c> の<br/>
/// コンパイル時に生成するため、<c>app.Core</c> 自身に置くと「ソース生成は互いの出力を見られない」制約に<br/>
/// より認識されない。参照アセンブリのメタデータとしては存在するため、本アセンブリからは認識できる。<br/>
/// <br/>
/// <b>書式</b>: インデント・コメント許容・末尾カンマ許容・enum の名前保存は<br/>
/// <c>SettingsService</c> が生成するオプション側で指定する（本コンテキストはそこへ型情報を提供するだけ）。
/// </remarks>
[JsonSerializable(typeof(Settings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
