using System.Drawing;
using System.Drawing.Text;
using MonoBooth.Camera;
using MonoBooth.Configuration;
using MonoBooth.Session;

namespace MonoBooth;

/// <summary>
/// The whole booth UI: a full-screen kiosk with a live camera preview, a countdown overlay drawn
/// straight onto the preview (never into the saved frame), a filling thumbnail strip, and a big
/// Start button. The form is built in code — no designer/resx — so the layout is easy to follow.
/// </summary>
public sealed class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly string _settingsPath;
    private readonly ICameraService _camera;
    private PhotoBoothSession? _session;
    private CancellationTokenSource? _sessionCts;

    private readonly PictureBox _preview = new();
    private readonly TableLayoutPanel _stripPanel = new();
    private readonly Button _startButton = new();
    private readonly Label _info = new();
    private readonly System.Windows.Forms.Timer _renderTimer = new() { Interval = 33 }; // ~30 fps
    private PictureBox[] _thumbs = Array.Empty<PictureBox>();

    private string _overlayText = "";
    private long _flashUntil;        // Environment.TickCount64 deadline for the white flash
    private bool _showingResult;
    private Bitmap? _resultImage;

    // Right-drag-to-reposition state (for arranging the preview/strip over a custom background).
    private Control? _dragTarget;
    private Point _dragStartScreen;
    private Point _dragOrigin;

    public MainForm()
    {
        _settingsPath = AppPaths.SettingsFile;
        _settings = AppSettings.Load(_settingsPath);
        _camera = new MediaCaptureCameraService(_settings.PreferredCamera);

        BuildLayout();

        // A single steady timer drives every preview repaint. Crucially we do NOT post a repaint
        // per camera frame — at 30-60fps that floods the message queue and starves the low-priority
        // paints/timers (thumbnails, flash) until the burst stops. The timer keeps the queue calm.
        _renderTimer.Tick += (_, _) => _preview.Invalidate();

        Load += OnLoadAsync;
        FormClosing += OnFormClosing;
        KeyDown += OnKeyDown;
    }

    // ----- Layout -------------------------------------------------------------------------------

    private void BuildLayout()
    {
        Text = "MonoBooth";
        KeyPreview = true;
        BackColor = Color.FromArgb(18, 18, 22);
        DoubleBuffered = true;

        if (_settings.FullScreen)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            ClientSize = new Size(1100, 720);
            StartPosition = FormStartPosition.CenterScreen;
        }

        TrySetBackgroundImage();

        // Everything is positioned absolutely over the (stretched) background image, and sized
        // smaller than the screen, so the background frames the preview + strip and can advertise
        // the event. LayoutControls() applies the configured percentages; right-drag repositions.

        // Live preview: owner-drawn in PaintPreview (camera frame + flash + overlay), so the
        // countdown text can never bleed into a saved photo. Transparent background so any
        // letterbox around the video shows the framing artwork instead of black bars.
        _preview.BackColor = Color.Transparent;
        _preview.Paint += PaintPreview;
        EnableDrag(_preview, _preview);
        Controls.Add(_preview);

        // Photo strip: one transparent cell per frame so the background shows between shots.
        _stripPanel.ColumnCount = 1;
        _stripPanel.RowCount = _settings.FrameCount;
        _stripPanel.BackColor = Color.Transparent;
        EnableDrag(_stripPanel, _stripPanel);
        _thumbs = new PictureBox[_settings.FrameCount];
        for (int i = 0; i < _settings.FrameCount; i++)
        {
            _stripPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / _settings.FrameCount));
            var box = new PictureBox
            {
                Dock = DockStyle.Fill,
                // White so empty cells / letterbox bars blend into the white box on the background.
                BackColor = Color.White,
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(3),
            };
            EnableDrag(box, _stripPanel); // grab any thumbnail to move the whole strip
            _thumbs[i] = box;
            _stripPanel.Controls.Add(box, 0, i);
        }
        Controls.Add(_stripPanel);

        _startButton.Text = "START";
        _startButton.Font = new Font("Segoe UI", 22f, FontStyle.Bold);
        _startButton.ForeColor = Color.White;
        _startButton.BackColor = Color.FromArgb(0, 150, 136);
        _startButton.FlatStyle = FlatStyle.Flat;
        _startButton.FlatAppearance.BorderSize = 0;
        _startButton.Size = new Size(280, 84);
        _startButton.Cursor = Cursors.Hand;
        _startButton.Click += OnStartClicked;
        Controls.Add(_startButton);

        _info.AutoSize = false;
        _info.BackColor = Color.Transparent;
        _info.ForeColor = Color.Gainsboro;
        _info.TextAlign = ContentAlignment.MiddleCenter;
        _info.Text = "Right-drag the preview or strip to arrange them  •  Esc to exit";
        Controls.Add(_info);

        Resize += (_, _) => LayoutControls();
        LayoutControls();
        _renderTimer.Start();
    }

    /// <summary>Positions the preview, strip, Start button and status line from the current config.</summary>
    private void LayoutControls()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0 || _dragTarget is not null)
            return;

        _preview.Bounds = _settings.PreviewArea.ToPixels(ClientSize);
        _stripPanel.Bounds = _settings.StripArea.ToPixels(ClientSize);

        int bw = _startButton.Width, bh = _startButton.Height;
        _startButton.Location = new Point(
            (ClientSize.Width - bw) / 2,
            ClientSize.Height - bh - Math.Max(36, (int)(ClientSize.Height * 0.05)));
        _info.Bounds = new Rectangle(0, ClientSize.Height - 28, ClientSize.Width, 24);
    }

    // ----- Right-drag to reposition -------------------------------------------------------------

    private void EnableDrag(Control handle, Control target)
    {
        handle.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right)
                return;
            _dragTarget = target;
            _dragStartScreen = Cursor.Position;
            _dragOrigin = target.Location;
        };
        handle.MouseMove += (_, e) =>
        {
            if (_dragTarget != target || e.Button != MouseButtons.Right)
                return;
            target.Location = new Point(
                _dragOrigin.X + (Cursor.Position.X - _dragStartScreen.X),
                _dragOrigin.Y + (Cursor.Position.Y - _dragStartScreen.Y));
        };
        handle.MouseUp += (_, e) =>
        {
            if (_dragTarget != target || e.Button != MouseButtons.Right)
                return;
            _dragTarget = null;
            var area = ReferenceEquals(target, _preview) ? _settings.PreviewArea : _settings.StripArea;
            area.SetFromPixels(target.Bounds, ClientSize);
        };
    }

    private void TrySetBackgroundImage()
    {
        var path = !string.IsNullOrWhiteSpace(_settings.BackgroundImagePath)
            ? _settings.BackgroundImagePath
            : Path.Combine(AppContext.BaseDirectory, "Resources", "Background-generic.jpg");

        try
        {
            if (File.Exists(path))
            {
                using var fromDisk = new Bitmap(path);
                BackgroundImage = new Bitmap(fromDisk); // copy so the file isn't locked
                BackgroundImageLayout = ImageLayout.Stretch;
            }
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            // A missing/garbled background is purely cosmetic — ignore.
        }
    }

    // ----- Startup ------------------------------------------------------------------------------

    private async void OnLoadAsync(object? sender, EventArgs e)
    {
        LayoutControls(); // the form now has its final (maximized) size
        _info.Text = "Starting camera…";
        var started = await _camera.StartAsync();

        if (!started)
        {
            _startButton.Enabled = false;
            _startButton.BackColor = Color.Gray;
            _overlayText = "No camera found";
            _preview.Invalidate();
            _info.Text = "No camera detected. Check the connection and " +
                         "Settings ▸ Privacy ▸ Camera ▸ \"Let desktop apps access your camera\". Esc to exit.";
            return;
        }

        _session = new PhotoBoothSession(_camera, _settings);
        _session.StatusChanged += OnStatusChanged;
        _session.FlashRequested += OnFlashRequested;
        _session.FrameCaptured += OnFrameCaptured;
        _session.Completed += OnSessionCompleted;
        _session.Failed += OnSessionFailed;

        _info.Text = $"Camera: {_camera.DeviceName}  •  Press Esc to exit";
    }

    // ----- Live preview -------------------------------------------------------------------------

    private void PaintPreview(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = _preview.ClientRectangle;

        // 1) Background: the result strip while reviewing, otherwise the live camera frame.
        if (_showingResult && _resultImage is not null)
            DrawImageFitted(g, _resultImage, rect);
        else
            _camera.TryRenderLatest(g, rect);

        // 2) The capture flash, time-boxed so the render timer clears it on its own.
        if (Environment.TickCount64 < _flashUntil)
            g.FillRectangle(Brushes.White, rect);

        // 3) The countdown / status text.
        if (!string.IsNullOrEmpty(_overlayText))
            DrawOverlayText(g, rect, _overlayText);
    }

    private static void DrawImageFitted(Graphics g, Image image, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        float scale = Math.Min((float)bounds.Width / image.Width, (float)bounds.Height / image.Height);
        int width = Math.Max(1, (int)(image.Width * scale));
        int height = Math.Max(1, (int)(image.Height * scale));
        int x = bounds.X + (bounds.Width - width) / 2;
        int y = bounds.Y + (bounds.Height - height) / 2;
        g.DrawImage(image, new Rectangle(x, y, width, height));
    }

    private static void DrawOverlayText(Graphics g, Rectangle rect, string text)
    {
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        float size = Math.Max(28f, rect.Height * 0.16f);
        using var font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        var shadow = new RectangleF(rect.X + 3, rect.Y + 3, rect.Width, rect.Height);
        g.DrawString(text, font, Brushes.Black, shadow, format);
        g.DrawString(text, font, Brushes.White, rect, format);
    }

    // ----- Session orchestration ----------------------------------------------------------------

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        if (_session is null || _session.IsRunning || _showingResult)
            return;

        ClearThumbnails();
        _startButton.Visible = false;
        _info.Text = "";

        _sessionCts?.Dispose();
        _sessionCts = new CancellationTokenSource();

        await _session.RunAsync(_sessionCts.Token);

        if (!_showingResult)
            ReturnToLive();
    }

    private void OnStatusChanged(string text)
    {
        _overlayText = text;
        _preview.Invalidate();
    }

    private void OnFlashRequested()
    {
        // Time-boxed flash; the render timer repaints and clears it ~150 ms later.
        _flashUntil = Environment.TickCount64 + 150;
        _preview.Invalidate();
    }

    private void OnFrameCaptured(int index, Bitmap thumbnail)
    {
        if (index < 0 || index >= _thumbs.Length)
        {
            thumbnail.Dispose();
            return;
        }

        var previous = _thumbs[index].Image;
        _thumbs[index].Image = thumbnail;
        previous?.Dispose();
    }

    private void OnSessionCompleted(SessionResult result)
    {
        _resultImage?.Dispose();
        _resultImage = result.Filmstrip;
        _showingResult = true; // PaintPreview now draws the strip instead of the live feed

        _overlayText = "";
        _info.Text = (result.Printed ? "Printing!  " : "All done!  ") + $"Saved to {result.FilmstripPath}";

        var hold = new System.Windows.Forms.Timer { Interval = 6000 };
        hold.Tick += (_, _) =>
        {
            hold.Stop();
            hold.Dispose();
            ReturnToLive();
        };
        hold.Start();
    }

    private void OnSessionFailed(string message)
    {
        _overlayText = "";
        _info.Text = message;
        ReturnToLive();
    }

    private void ReturnToLive()
    {
        _showingResult = false; // PaintPreview switches back to the live feed
        _overlayText = "";

        _resultImage?.Dispose();
        _resultImage = null;

        _startButton.Visible = true;
        _startButton.Enabled = _session is not null;
        _info.Text = _session is not null
            ? $"Camera: {_camera.DeviceName}  •  Press Esc to exit"
            : _info.Text;
        _preview.Invalidate();
    }

    private void ClearThumbnails()
    {
        foreach (var box in _thumbs)
        {
            var previous = box.Image;
            box.Image = null;
            previous?.Dispose();
        }
    }

    // ----- Shutdown -----------------------------------------------------------------------------

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.X)
            Close();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _settings.Save(_settingsPath);

        _renderTimer.Stop();
        _renderTimer.Dispose();

        try { _sessionCts?.Cancel(); } catch (ObjectDisposedException) { }
        _sessionCts?.Dispose();

        _camera.Dispose();

        _resultImage?.Dispose();
        ClearThumbnails();
    }
}
