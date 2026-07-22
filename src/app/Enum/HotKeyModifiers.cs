namespace app.Enum;

/// <summary>ホットキーの修飾キーを指定するためのフラグ列挙型。</summary>
/// <remarks>
/// ビットフラグとして設計されており、複数の修飾キーを組み合わせて使用する。
/// </remarks>
[Flags]
public enum HotKeyModifiers
{
    /// <summary>修飾キーなし</summary>
    None = 0,

    /// <summary>Alt キー</summary>
    Alt = 0x0001,

    /// <summary>Ctrl キー</summary>
    Control = 0x0002,

    /// <summary>Shift キー</summary>
    Shift = 0x0004,

    /// <summary>Windows キー</summary>
    Windows = 0x0008
}
