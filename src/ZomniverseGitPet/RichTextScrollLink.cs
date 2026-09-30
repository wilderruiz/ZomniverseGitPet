using System.Runtime.InteropServices;

namespace ZomniverseGitPet;

internal readonly record struct RichTextScrollPosition(int X, int Y);

internal readonly record struct RichTextScrollSnapshot(
    RichTextScrollPosition Left,
    RichTextScrollPosition Right);

internal sealed class RichTextScrollLink : IDisposable
{
    private const int WmUser = 0x0400;
    private const int EmGetScrollPos = WmUser + 221;
    private const int EmSetScrollPos = WmUser + 222;

    private readonly RichTextBox _left;
    private readonly RichTextBox _right;
    private bool _syncing;
    private bool _disposed;

    public RichTextScrollLink(RichTextBox left, RichTextBox right)
    {
        _left = left;
        _right = right;
        _left.VScroll += LeftScrolled;
        _left.HScroll += LeftScrolled;
        _right.VScroll += RightScrolled;
        _right.HScroll += RightScrolled;
    }

    public bool Enabled { get; set; } = true;

    public RichTextScrollSnapshot Capture()
    {
        if (_disposed)
            return default;

        return new RichTextScrollSnapshot(
            ReadPosition(_left),
            ReadPosition(_right));
    }

    public void Restore(RichTextScrollSnapshot snapshot)
    {
        if (_disposed) return;

        try
        {
            _syncing = true;
            WritePosition(_left, snapshot.Left);
            WritePosition(_right, snapshot.Right);
        }
        finally
        {
            _syncing = false;
        }
    }

    private static RichTextScrollPosition ReadPosition(RichTextBox box)
    {
        if (!box.IsHandleCreated || box.IsDisposed)
            return default;

        var point = new NativePoint();
        SendMessage(box.Handle, EmGetScrollPos, IntPtr.Zero, ref point);
        return new RichTextScrollPosition(point.X, point.Y);
    }

    private static void WritePosition(
        RichTextBox box,
        RichTextScrollPosition position)
    {
        if (!box.IsHandleCreated || box.IsDisposed)
            return;

        var point = new NativePoint
        {
            X = Math.Max(0, position.X),
            Y = Math.Max(0, position.Y)
        };
        SendMessage(box.Handle, EmSetScrollPos, IntPtr.Zero, ref point);
        box.Invalidate();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _left.VScroll -= LeftScrolled;
        _left.HScroll -= LeftScrolled;
        _right.VScroll -= RightScrolled;
        _right.HScroll -= RightScrolled;
    }

    private void LeftScrolled(object? sender, EventArgs e) => Sync(_left, _right);
    private void RightScrolled(object? sender, EventArgs e) => Sync(_right, _left);

    private void Sync(RichTextBox source, RichTextBox target)
    {
        if (!Enabled || _syncing || _disposed ||
            !source.IsHandleCreated || !target.IsHandleCreated)
            return;

        try
        {
            _syncing = true;
            var point = new NativePoint();
            SendMessage(source.Handle, EmGetScrollPos, IntPtr.Zero, ref point);
            SendMessage(target.Handle, EmSetScrollPos, IntPtr.Zero, ref point);
            target.Invalidate();
        }
        finally
        {
            _syncing = false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        ref NativePoint lParam);
}
