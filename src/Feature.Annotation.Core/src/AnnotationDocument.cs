using System.Collections.Immutable;

namespace EsUtil.Feature.Annotation;

/// <summary>画像に重ねる注釈の集合（JSON ファイルの内容）。</summary>
/// <remarks>
/// <b>並び順が重なり順</b>である（先頭が最背面、末尾が最前面）。<br/>
/// <b>画像の寸法を持つ</b>: 注釈の座標は元画像ピクセルであり、別の大きさの画像へ読み込むと位置がずれる。
/// 読み込み時に寸法を照合し、違えば警告する（<see cref="AnnotationJson"/>）。<br/>
/// <b>画像そのものは持たない</b>（注釈は画像に焼き込まない。 保存時に合成した画像を別ファイルへ書く）。
/// </remarks>
public sealed class AnnotationDocument
{
    /// <summary>現在の JSON スキーマの版を取得する。</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>スキーマの版を取得または設定する。</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>元画像の幅（ピクセル）を取得または設定する。</summary>
    public int ImageWidth { get; set; }

    /// <summary>元画像の高さ（ピクセル）を取得または設定する。</summary>
    public int ImageHeight { get; set; }

    /// <summary>元画像のファイル名を取得または設定する（同じフォルダからの再編集の手掛かり）。</summary>
    public string? ImageFileName { get; set; }

    /// <summary>注釈の並び（先頭が最背面、末尾が最前面）を取得または設定する。</summary>
    public ImmutableArray<AnnotationItem> Items { get; set; } = [];

    /// <summary>画像の寸法を指定して初期化する。</summary>
    /// <param name="imageWidth">元画像の幅（ピクセル）。</param>
    /// <param name="imageHeight">元画像の高さ（ピクセル）。</param>
    /// <param name="imageFileName">元画像のファイル名。</param>
    public AnnotationDocument(int imageWidth, int imageHeight, string? imageFileName = null)
    {
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        ImageFileName = imageFileName;
    }

    /// <summary>空の注釈集合で初期化する（JSON の読み込み用）。</summary>
    public AnnotationDocument()
    {
    }

    /// <summary>注釈を最前面へ追加した新しい文書を返す。</summary>
    /// <param name="item">追加する注釈。</param>
    /// <returns>追加後の文書（元の文書は変更しない）。</returns>
    public AnnotationDocument Add(AnnotationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return WithItems(Items.Add(item));
    }

    /// <summary>指定された識別子の注釈を差し替えた新しい文書を返す。</summary>
    /// <param name="item">差し替える注釈（<see cref="AnnotationItem.Id"/> で位置を決める）。</param>
    /// <returns>差し替え後の文書（見つからない場合は元の文書と同じ内容）。</returns>
    public AnnotationDocument Replace(AnnotationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var index = IndexOf(item.Id);
        return index < 0 ? this : WithItems(Items.SetItem(index, item));
    }

    /// <summary>指定された識別子の注釈を除いた新しい文書を返す。</summary>
    /// <param name="id">除く注釈の識別子。</param>
    /// <returns>削除後の文書（見つからない場合は元の文書と同じ内容）。</returns>
    public AnnotationDocument Remove(string id)
    {
        var index = IndexOf(id);
        return index < 0 ? this : WithItems(Items.RemoveAt(index));
    }

    /// <summary>指定された識別子の注釈を取得する。</summary>
    /// <param name="id">注釈の識別子。</param>
    /// <returns>見つかった注釈。 無い場合は <see langword="null"/>。</returns>
    public AnnotationItem? Find(string? id)
        => string.IsNullOrEmpty(id) ? null : Items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));

    /// <summary>本文の書式を指定された範囲で差し替えた新しい文書を返す（<see cref="BubbleAnnotation"/> だけが対象）。</summary>
    /// <param name="bubble">対象の吹き出し（現在の文書に含まれるもの）。</param>
    /// <param name="text">差し替える本文。</param>
    /// <returns>差し替え後の文書。</returns>
    /// <remarks>本文の編集は吹き出しの一部を置き換えるだけであり、位置・外観・しっぽは変えない。</remarks>
    public AnnotationDocument WithText(BubbleAnnotation bubble, RichText text)
    {
        ArgumentNullException.ThrowIfNull(bubble);
        return Replace(new BubbleAnnotation
        {
            Id = bubble.Id,
            Bounds = bubble.Bounds,
            Appearance = bubble.Appearance,
            BodyPadding = bubble.BodyPadding,
            Tail = bubble.Tail,
            Text = text,
        });
    }

    /// <summary>指定された識別子の位置（重なり順）を取得する。</summary>
    /// <param name="id">注釈の識別子。</param>
    /// <returns>位置（先頭が 0）。 無い場合は -1。</returns>
    public int IndexOf(string id)
    {
        for (var i = 0; i < Items.Length; i++)
        {
            if (string.Equals(Items[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>注釈の並びだけを差し替えた複製を返す。</summary>
    /// <param name="items">新しい並び。</param>
    /// <returns>複製。</returns>
    private AnnotationDocument WithItems(ImmutableArray<AnnotationItem> items) => new(ImageWidth, ImageHeight, ImageFileName)
    {
        SchemaVersion = SchemaVersion,
        Items = items,
    };
}
