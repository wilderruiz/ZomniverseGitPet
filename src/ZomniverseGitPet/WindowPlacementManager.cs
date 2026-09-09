using System.Text.Json;

namespace ZomniverseGitPet;

internal sealed class WindowPlacementState
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool Maximized { get; set; }
}

internal static class WindowPlacementManager
{
    private static readonly object Gate = new();
    private static readonly string StorePath = Path.Combine(AppPaths.Root, "window-layout.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static Dictionary<string, WindowPlacementState>? _states;

    public static void Attach(Form form, string? key = null)
    {
        if (form.FormBorderStyle is FormBorderStyle.None or FormBorderStyle.FixedDialog or FormBorderStyle.FixedSingle or FormBorderStyle.Fixed3D or FormBorderStyle.FixedToolWindow)
            return;

        key ??= form.GetType().Name;
        var applied = false;

        form.Shown += (_, _) =>
        {
            if (applied || form.IsDisposed) return;
            applied = true;

            var owner = form.Owner;
            var screen = owner is not null && owner.IsHandleCreated
                ? Screen.FromControl(owner)
                : Screen.FromPoint(Cursor.Position);
            var working = screen.WorkingArea;

            var saved = TryGet(key);
            form.StartPosition = FormStartPosition.Manual;

            if (saved is not null && saved.Width > 0 && saved.Height > 0)
            {
                var desired = new Rectangle(saved.X, saved.Y, saved.Width, saved.Height);
                var targetScreen = Screen.AllScreens.FirstOrDefault(item => item.WorkingArea.IntersectsWith(desired)) ?? screen;
                form.Bounds = ClampToWorkingArea(desired, targetScreen.WorkingArea, form.MinimumSize, form.MaximumSize);
                if (saved.Maximized && form.MaximizeBox)
                    form.WindowState = FormWindowState.Maximized;
            }
            else
            {
                form.Bounds = CalculateFirstBounds(working, form.MinimumSize, form.MaximumSize);
            }
        };

        form.FormClosing += (_, _) =>
        {
            if (!applied || form.IsDisposed) return;
            var bounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            Save(key, new WindowPlacementState
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height,
                Maximized = form.WindowState == FormWindowState.Maximized && form.MaximizeBox
            });
        };
    }

    internal static Rectangle CalculateFirstBounds(Rectangle workingArea, Size minimumSize, Size maximumSize)
    {
        var maxWidth = maximumSize.Width > 0 ? Math.Min(maximumSize.Width, workingArea.Width) : workingArea.Width;
        var maxHeight = maximumSize.Height > 0 ? Math.Min(maximumSize.Height, workingArea.Height) : workingArea.Height;

        var width = Math.Min(maxWidth, Math.Max(minimumSize.Width, (int)Math.Round(workingArea.Width * 0.92)));
        var height = Math.Min(maxHeight, Math.Max(minimumSize.Height, (int)Math.Round(workingArea.Height * 0.88)));

        width = Math.Max(1, Math.Min(width, workingArea.Width));
        height = Math.Max(1, Math.Min(height, workingArea.Height));

        return new Rectangle(
            workingArea.Left + Math.Max(0, (workingArea.Width - width) / 2),
            workingArea.Top + Math.Max(0, (workingArea.Height - height) / 2),
            width,
            height);
    }

    internal static Rectangle ClampToWorkingArea(Rectangle desired, Rectangle workingArea, Size minimumSize, Size maximumSize)
    {
        var maxWidth = maximumSize.Width > 0 ? Math.Min(maximumSize.Width, workingArea.Width) : workingArea.Width;
        var maxHeight = maximumSize.Height > 0 ? Math.Min(maximumSize.Height, workingArea.Height) : workingArea.Height;

        var width = Math.Clamp(desired.Width, Math.Min(minimumSize.Width, maxWidth), maxWidth);
        var height = Math.Clamp(desired.Height, Math.Min(minimumSize.Height, maxHeight), maxHeight);
        var x = Math.Clamp(desired.X, workingArea.Left, workingArea.Right - width);
        var y = Math.Clamp(desired.Y, workingArea.Top, workingArea.Bottom - height);
        return new Rectangle(x, y, width, height);
    }

    private static WindowPlacementState? TryGet(string key)
    {
        lock (Gate)
        {
            EnsureLoaded();
            return _states!.TryGetValue(key, out var value) ? value : null;
        }
    }

    private static void Save(string key, WindowPlacementState state)
    {
        lock (Gate)
        {
            try
            {
                EnsureLoaded();
                _states![key] = state;
                Directory.CreateDirectory(AppPaths.Root);
                var temp = StorePath + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(_states, JsonOptions));
                File.Move(temp, StorePath, true);
            }
            catch
            {
                // Window placement is convenience state only; never block GitPet shutdown.
            }
        }
    }

    private static void EnsureLoaded()
    {
        if (_states is not null) return;
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            _states = File.Exists(StorePath)
                ? JsonSerializer.Deserialize<Dictionary<string, WindowPlacementState>>(File.ReadAllText(StorePath), JsonOptions)
                : null;
        }
        catch
        {
            _states = null;
        }
        _states ??= new Dictionary<string, WindowPlacementState>(StringComparer.Ordinal);
    }
}
