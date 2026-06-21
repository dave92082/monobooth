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
    private readonly Button _startButton = new();
    private readonly Label _info = new();
    private PictureBox[] _thumbs = Array.Empty<PictureBox>();

    private string _overlayText = "";
    private bool _flash;
    private bool _showingResult;
    private int _refreshQueued; // 0/1 guard so preview repaints don't pile up
    private Bitmap? _resultImage;

    public MainForm()
    {
        _settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
        _settings = AppSettings.Load(_settingsPath);
        _camera = new MediaCaptureCameraService(_settings.PreferredCamera);

        BuildLayout();

        _camera.FrameReady += OnCameraFrameReady;
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

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Padding = new Padding(16),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78f));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 140f));

        // Live preview with the countdown/flash overlay painted on top.
        _preview.Dock = DockStyle.Fill;
        _preview.BackColor = Color.Black;
        _preview.SizeMode = PictureBoxSizeMode.Zoom;
        _preview.Margin = new Padding(0, 0, 16, 0);
        _preview.Paint += DrawOverlay;
        root.Controls.Add(_preview, 0, 0);

        // Thumbnail strip down the right-hand side, one cell per frame.
        var thumbPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = _settings.FrameCount,
            BackColor = Color.Transparent,
        };
        _thumbs = new PictureBox[_settings.FrameCount];
        for (int i = 0; i < _settings.FrameCount; i++)
        {
            thumbPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / _settings.FrameCount));
            var box = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 36),
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(2),
            };
            _thumbs[i] = box;
            thumbPanel.Controls.Add(box, 0, i);
        }
        root.Controls.Add(thumbPanel, 1, 0);

        // Bottom bar: Start button + status line, spanning both columns.
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));

        _startButton.Text = "START";
        _startButton.Font = new Font("Segoe UI", 22f, FontStyle.Bold);
        _startButton.ForeColor = Color.White;
        _startButton.BackColor = Color.FromArgb(0, 150, 136);
        _startButton.FlatStyle = FlatStyle.Flat;
        _startButton.FlatAppearance.BorderSize = 0;
        _startButton.Size = new Size(280, 84);
        _startButton.Anchor = AnchorStyles.None; // centre in its cell
        _startButton.Cursor = Cursors.Hand;
        _startButton.Click += OnStartClicked;
        bottom.Controls.Add(_startButton, 0, 0);

        _info.Dock = DockStyle.Fill;
        _info.ForeColor = Color.Gainsboro;
        _info.TextAlign = ContentAlignment.MiddleCenter;
        _info.Text = "Press Esc to exit";
        bottom.Controls.Add(_info, 0, 1);

        root.Controls.Add(bottom, 0, 1);
        root.SetColumnSpan(bottom, 2);

        Controls.Add(root);
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

    private void OnCameraFrameReady(object? sender, EventArgs e)
    {
        // Coalesce: never queue a second repaint while one is still pending.
        if (Interlocked.Exchange(ref _refreshQueued, 1) == 1)
            return;

        try
        {
            BeginInvoke(UpdatePreview);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            _refreshQueued = 0; // form is closing
        }
    }

    private void UpdatePreview()
    {
        _refreshQueued = 0;
        if (_showingResult || IsDisposed)
            return;

        var frame = _camera.GetFrameCopy();
        if (frame is null)
            return;

        var previous = _preview.Image;
        _preview.Image = frame;
        previous?.Dispose();
    }

    private void DrawOverlay(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = _preview.ClientRectangle;

        if (_flash)
            g.FillRectangle(Brushes.White, rect);

        if (string.IsNullOrEmpty(_overlayText))
            return;

        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        float size = Math.Max(28f, rect.Height * 0.16f);
        using var font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        var shadow = new RectangleF(rect.X + 3, rect.Y + 3, rect.Width, rect.Height);
        g.DrawString(_overlayText, font, Brushes.Black, shadow, format);
        g.DrawString(_overlayText, font, Brushes.White, rect, format);
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
        _flash = true;
        _preview.Invalidate();
        _preview.Update();

        var timer = new System.Windows.Forms.Timer { Interval = 120 };
        timer.Tick += (_, _) =>
        {
            _flash = false;
            _preview.Invalidate();
            timer.Stop();
            timer.Dispose();
        };
        timer.Start();
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
        _showingResult = true;

        _resultImage?.Dispose();
        _resultImage = result.Filmstrip;

        var liveFrame = _preview.Image;
        _preview.Image = _resultImage;
        liveFrame?.Dispose();

        _overlayText = result.Printed ? "Printing!" : "All done!";
        _preview.Invalidate();
        _info.Text = $"Saved to {result.FilmstripPath}";

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
        _showingResult = false;
        _overlayText = "";

        if (_resultImage is not null && ReferenceEquals(_preview.Image, _resultImage))
            _preview.Image = null;
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

        try { _sessionCts?.Cancel(); } catch (ObjectDisposedException) { }
        _sessionCts?.Dispose();

        _camera.FrameReady -= OnCameraFrameReady;
        _camera.Dispose();

        _preview.Image?.Dispose();
        _resultImage?.Dispose();
        ClearThumbnails();
    }
}
