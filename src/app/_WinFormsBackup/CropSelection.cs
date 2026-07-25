namespace app.Models;

/// <summary>
/// プレビュー画像上の手動切り出し選択状態を管理するクラス。
/// ドラッグによる範囲選択、8方向ハンドルによるリサイズ、領域内ドラッグによる移動をサポートする。
/// </summary>
/// <remarks>
/// ログ出力は <see cref="ILogService"/> を介して行う。設定されていない場合は
/// <c>System.Diagnostics.Debug.WriteLine</c> にフォールバックする。<br/>
/// 選択領域は画像座標（ピクセル単位）で管理され、UIの拡大率とは独立している。<br/>
/// マウスイベントは画像座標に変換されてから本クラスに渡される。<br/>
/// </remarks>
public sealed class CropSelection
{
    private const int HandleSize = 24;

    private enum HandleType { None, TopLeft, TopCenter, TopRight, MiddleLeft, MiddleRight, BottomLeft, BottomCenter, BottomRight }

    // 新規ドラッグ
    private bool _isDragging;
    private Point _dragStart;

    // リサイズ
    private HandleType _activeHandle = HandleType.None;
    private Rectangle _resizeStartRect;
    private Point _resizeStartPoint;

    // 移動
    private bool _isMoving;
    private Point _moveStartPoint;
    private Rectangle _moveStartRect;

    /// <summary>
    /// CropSelection の新しいインスタンスを初期化する。
    /// </summary>
    public CropSelection()
    {
    }

    /// <summary>現在の選択領域（画像座標ピクセル単位）。未選択の場合は null。</summary>
    public Rectangle? SelectionRect { get; private set; }

    /// <summary>ハンドル（リサイズ用グリップ）がアクティブかどうか。</summary>
    public bool IsHandleActive => _activeHandle != HandleType.None;

    /// <summary>ドラッグ中かどうか（新規選択 or 移動）。</summary>
    public bool IsDragging => _isDragging || _isMoving;

    /// <summary>選択状態を全てリセットし、未選択状態に戻す。</summary>
    public void Reset()
    {
        SelectionRect = null;
        _isDragging = false;
        _activeHandle = HandleType.None;
        _isMoving = false;
    }

    /// <summary>外部から選択領域を設定する（自動検出結果の適用など）。</summary>
    /// <param name="rect">画像座標の矩形領域</param>
    public void SetSelection(Rectangle rect)
    {
        SelectionRect = rect;
    }

    /// <summary>
    /// マウスダウン処理（画像座標ベース）
    /// </summary>
    public void MouseDown(Point imgPoint, Size _)
    {

        if (SelectionRect.HasValue)
        {
            var sel = SelectionRect.Value;

            var handle = HitTestHandle(imgPoint, sel);
            if (handle != HandleType.None)
            {
                _activeHandle = handle;
                _resizeStartRect = sel;
                _resizeStartPoint = imgPoint;
                return;
            }

            if (IsInsideSelection(imgPoint, sel))
            {
                _isMoving = true;
                _moveStartPoint = imgPoint;
                _moveStartRect = sel;
                return;
            }

            SelectionRect = null;
        }

        _dragStart = imgPoint;
        _isDragging = true;
        SelectionRect = new Rectangle(imgPoint.X, imgPoint.Y, 1, 1);
    }

    /// <summary>
    /// マウスの移動に応じて、アクティブな操作（新規選択、リサイズ、移動）を実行する。
    /// </summary>
    /// <param name="imgPoint">画像上の現在のマウス座標（ピクセル単位）</param>
    /// <param name="imageSize">画像サイズ（ピクセル単位）</param>
    /// <remarks>
    /// 以下の3種類の操作を自動的に振り分ける:<br/>
    /// - リサイズ中（_activeHandle != None）: 選択矩形を8方向ハンドルに従って変形<br/>
    /// - 移動中（_isMoving）: 選択矩形全体をマウス移動量に応じて平行移動<br/>
    /// - 新規選択中（_isDragging）: ドラッグ開始点から現在位置までの矩形を計算<br/>
    /// </remarks>
    public void MouseMove(Point imgPoint, Size imageSize)
    {
        if (_activeHandle != HandleType.None)
        {
            SelectionRect = ResizeRect(_resizeStartRect, _activeHandle, imgPoint, imageSize.Width, imageSize.Height);
            return;
        }

        if (_isMoving)
        {
            var dx = imgPoint.X - _moveStartPoint.X;
            var dy = imgPoint.Y - _moveStartPoint.Y;
            var iw = imageSize.Width;
            var ih = imageSize.Height;
            SelectionRect = new Rectangle(
                Math.Clamp(_moveStartRect.X + dx, 0, iw - _moveStartRect.Width + 1),
                Math.Clamp(_moveStartRect.Y + dy, 0, ih - _moveStartRect.Height + 1),
                _moveStartRect.Width,
                _moveStartRect.Height);
            return;
        }

        if (_isDragging)
        {
            SelectionRect = NormalizeRect(_dragStart, imgPoint);
            return;
        }
    }

    /// <summary>
    /// マウスボタンを離した時の処理。アクティブな操作（リサイズ／移動／新規選択）を確定する。
    /// </summary>
    /// <remarks>
    /// ドラッグ中だった場合は選択を終了する。<br/>
    /// 新規ドラッグの結果、選択矩形が5x5ピクセル未満の場合は選択を無効（null）とする。<br/>
    /// </remarks>
    public void MouseUp()
    {

        if (_activeHandle != HandleType.None)
        {
            _activeHandle = HandleType.None;
            return;
        }

        if (_isMoving)
        {
            _isMoving = false;
            return;
        }

        if (_isDragging)
        {
            _isDragging = false;
            if (SelectionRect.HasValue && (SelectionRect.Value.Width < 5 || SelectionRect.Value.Height < 5))
            {
                SelectionRect = null;
            }
            return;
        }
    }

    /// <summary>
    /// カーソル位置に対応する適切なカーソル形状を返す（画像座標ベース）。
    /// </summary>
    /// <param name="imgPoint">画像上のマウス座標（ピクセル単位）</param>
    /// <returns>該当するカーソル。選択領域外または未選択の場合は null。</returns>
    /// <remarks>
    /// カーソルがハンドル上にある場合はリサイズ方向に対応したカーソル、領域内の場合は SizeAll、
    /// 領域外の場合は null を返す。<br/>
    /// </remarks>
    public Cursor? GetCursor(Point imgPoint)
    {
        if (!SelectionRect.HasValue)
            return null;

        var sel = SelectionRect.Value;
        var handle = HitTestHandle(imgPoint, sel);
        if (handle != HandleType.None)
            return GetHandleCursor(handle);

        if (IsInsideSelection(imgPoint, sel))
            return Cursors.SizeAll;

        return null;
    }

    /// <summary>
    /// カーソル位置に対応する適切なカーソル形状を返す（クライアント座標ベース、固定ヒットサイズ）。
    /// </summary>
    /// <param name="clientPoint">クライアント座標（ピクセル単位）</param>
    /// <param name="clientHandlePoints"><c>GetHandleClientPoints</c> で算出した8ハンドルのクライアント座標配列</param>
    /// <param name="clientRect">選択領域のクライアント座標（画面の表示領域）</param>
    /// <returns>該当するカーソル。選択領域外または未選択の場合は null。</returns>
    /// <remarks>
    /// <see cref="GetCursor"/> とは異なり、拡大率に依存しない固定ピクセルサイズ（HandleSize=24）で
    /// ヒットテストを行う。これにより、ズーム倍率に関わらず一定の操作性を確保する。<br/>
    /// </remarks>
    public Cursor? GetCursorClient(Point clientPoint, Point[] clientHandlePoints, Rectangle clientRect)
    {
        if (!SelectionRect.HasValue)
            return null;

        // 固定サイズでヒットテスト（拡大率不変）
        var handleSize = HandleSize;
        for (var i = 0; i < clientHandlePoints.Length; i++)
        {
            var r = new Rectangle(
                clientHandlePoints[i].X - handleSize / 2,
                clientHandlePoints[i].Y - handleSize / 2,
                handleSize, handleSize);
            if (r.Contains(clientPoint))
            {
                var types = new[] { HandleType.TopLeft, HandleType.TopCenter, HandleType.TopRight, HandleType.MiddleLeft, HandleType.MiddleRight, HandleType.BottomLeft, HandleType.BottomCenter, HandleType.BottomRight };
                return GetHandleCursor(types[i]);
            }
        }

        // 領域内を固定マージンで判定
        if (!SelectionRect.HasValue) return null;
        var inner = Rectangle.Inflate(clientRect, -handleSize / 2, -handleSize / 2);
        if (inner.Contains(clientPoint))
            return Cursors.SizeAll;

        return null;
    }

    /// <summary>
    /// 選択領域と8方向ハンドルを画像上に描画する。
    /// </summary>
    /// <param name="g">描画先の Graphics オブジェクト</param>
    /// <param name="imageSize">画像サイズ（ピクセル単位）</param>
    /// <remarks>
    /// 未選択時は全面を半透明黒（Alpah=80）で暗転表示する。<br/>
    /// 選択時は選択領域外のみを半透明黒（Alpha=100）で暗転し、選択領域を白枠で囲む。<br/>
    /// 8つのハンドル（白塗り＋黒枠）を各頂点と辺の中点に描画する。<br/>
    /// </remarks>
    public void Draw(Graphics g, Size imageSize)
    {
        if (!SelectionRect.HasValue)
        {
            // 全面暗転
            using var dimBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
            g.FillRectangle(dimBrush, 0, 0, imageSize.Width, imageSize.Height);
            return;
        }

        var sel = SelectionRect.Value;

        // 選択領域外を暗転
        using var dim = new SolidBrush(Color.FromArgb(100, 0, 0, 0));
        if (sel.Top > 0)
            g.FillRectangle(dim, 0, 0, imageSize.Width, sel.Top);
        if (sel.Bottom < imageSize.Height)
            g.FillRectangle(dim, 0, sel.Bottom, imageSize.Width, imageSize.Height - sel.Bottom);
        if (sel.Left > 0)
            g.FillRectangle(dim, 0, sel.Top, sel.Left, sel.Height);
        if (sel.Right < imageSize.Width)
            g.FillRectangle(dim, sel.Right, sel.Top, imageSize.Width - sel.Right, sel.Height);

        // 選択枠（1px、境界線上も対象領域）
        using var selPen = new Pen(Color.White, 1);
        g.DrawRectangle(selPen, sel);

        // ハンドル
        foreach (var pt in GetHandlePoints(sel))
        {
            var r = new Rectangle(pt.X - HandleSize / 2, pt.Y - HandleSize / 2, HandleSize, HandleSize);
            g.FillRectangle(Brushes.White, r);
            using var p = new Pen(Color.Black, 1);
            g.DrawRectangle(p, r);
        }
    }

    // ---- private メソッド ----

    /// <summary>選択矩形の8つのハンドル位置（画像座標）を算出する。</summary>
    /// <param name="rect">選択矩形</param>
    /// <returns>8ハンドルの座標配列（左上、上中央、右上、左中央、右中央、左下、下中央、右下の順）</returns>
    private static Point[] GetHandlePoints(Rectangle rect)
    {
        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        return
        [
            new(rect.X, rect.Y), new(cx, rect.Y), new(rect.Right, rect.Y),
            new(rect.X, cy), new(rect.Right, cy),
            new(rect.X, rect.Bottom), new(cx, rect.Bottom), new(rect.Right, rect.Bottom),
        ];
    }

    /// <summary>指定された点がどのハンドル領域内にあるかを判定する。</summary>
    /// <param name="pt">判定する点（画像座標）</param>
    /// <param name="rect">選択矩形</param>
    /// <returns>該当する HandleType。いずれのハンドルにも当たらない場合は <see cref="HandleType.None"/>。</returns>
    private static HandleType HitTestHandle(Point pt, Rectangle rect)
    {
        var pts = GetHandlePoints(rect);
        var types = new[]
        {
            HandleType.TopLeft, HandleType.TopCenter, HandleType.TopRight,
            HandleType.MiddleLeft, HandleType.MiddleRight,
            HandleType.BottomLeft, HandleType.BottomCenter, HandleType.BottomRight
        };
        for (var i = 0; i < pts.Length; i++)
        {
            var r = new Rectangle(pts[i].X - HandleSize / 2, pts[i].Y - HandleSize / 2, HandleSize, HandleSize);
            if (r.Contains(pt)) return types[i];
        }
        return HandleType.None;
    }

    /// <summary>指定された点が選択領域の内部（ハンドル領域を除く）にあるかどうかを判定する。</summary>
    /// <param name="pt">判定する点（画像座標）</param>
    /// <param name="sel">選択矩形</param>
    /// <returns>内部にある場合は true</returns>
    private static bool IsInsideSelection(Point pt, Rectangle sel)
    {
        if (HitTestHandle(pt, sel) != HandleType.None)
            return false;
        var inner = Rectangle.Inflate(sel, -HandleSize / 2, -HandleSize / 2);
        return inner.Contains(pt);
    }

    /// <summary>ハンドルの種類に対応するマウスカーソル形状を取得する。</summary>
    /// <param name="handle">ハンドルの種類</param>
    /// <returns>対応する Cursor オブジェクト</returns>
    private static Cursor GetHandleCursor(HandleType handle) => handle switch
    {
        HandleType.TopLeft or HandleType.BottomRight => Cursors.SizeNWSE,
        HandleType.TopRight or HandleType.BottomLeft => Cursors.SizeNESW,
        HandleType.TopCenter or HandleType.BottomCenter => Cursors.SizeNS,
        HandleType.MiddleLeft or HandleType.MiddleRight => Cursors.SizeWE,
        _ => Cursors.SizeAll
    };

    /// <summary>選択矩形を指定されたハンドル方向にリサイズする。最小サイズは5x5ピクセル。</summary>
    /// <param name="original">元の選択矩形</param>
    /// <param name="handle">操作中のハンドル</param>
    /// <param name="newPos">新しいマウス位置（画像座標）</param>
    /// <param name="imgW">画像幅</param>
    /// <param name="imgH">画像高さ</param>
    /// <returns>リサイズ後の矩形</returns>
    private static Rectangle ResizeRect(Rectangle original, HandleType handle, Point newPos, int imgW, int imgH)
    {
        var l = original.Left;
        var t = original.Top;
        var r = original.Right;
        var b = original.Bottom;

        switch (handle)
        {
            case HandleType.TopLeft:   l = newPos.X; t = newPos.Y; break;
            case HandleType.TopCenter: t = newPos.Y; break;
            case HandleType.TopRight:  r = newPos.X; t = newPos.Y; break;
            case HandleType.MiddleLeft: l = newPos.X; break;
            case HandleType.MiddleRight: r = newPos.X; break;
            case HandleType.BottomLeft: l = newPos.X; b = newPos.Y; break;
            case HandleType.BottomCenter: b = newPos.Y; break;
            case HandleType.BottomRight: r = newPos.X; b = newPos.Y; break;
        }

        l = Math.Clamp(l, 0, imgW - 1);
        t = Math.Clamp(t, 0, imgH - 1);
        r = Math.Clamp(r, l + 4, imgW - 1);
        b = Math.Clamp(b, t + 4, imgH - 1);

        return new Rectangle(l, t, r - l + 1, b - t + 1);
    }

    /// <summary>2点を正規化し、両方の点を含むインクルーシブな矩形を返す（幅・高さは1以上を保証）。</summary>
    /// <param name="p1">1点目の座標</param>
    /// <param name="p2">2点目の座標</param>
    /// <returns>両点を含む Rectangle</returns>
    private static Rectangle NormalizeRect(Point p1, Point p2)
    {
        var x = Math.Min(p1.X, p2.X);
        var y = Math.Min(p1.Y, p2.Y);
        return new Rectangle(x, y, Math.Abs(p1.X - p2.X) + 1, Math.Abs(p1.Y - p2.Y) + 1);
    }
}
