namespace app.Enum;

/// <summary>ルーペ（拡大鏡）の表示モードを指定する列挙型。</summary>
/// <remarks>
/// ルーペの表示状態を常時表示、常時非表示、範囲選択中のみ自動表示の3パターンから選択する。<br/>
/// <see cref="Auto"/> モードでは、ユーザーが画像上でマウスドラッグによる範囲選択を行っている間のみルーペが表示される。<br/>
/// </remarks>
public enum LoupeMode
{
    /// <summary>常に表示</summary>
    Show = 0,

    /// <summary>常に非表示</summary>
    Hide = 1,

    /// <summary>範囲選択中のみ自動表示</summary>
    Auto = 2,
}
