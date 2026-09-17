using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Tinycast.DesignSystem;
using Tinycast.Platform;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;

namespace Tinycast;

public sealed partial class CameraWindow : Window
{
    readonly AppCore _core;
    MediaCapture? _capture;
    MediaFrameReader? _reader;
    SoftwareBitmapSource? _bitmapSource;
    int _presenting;
    int _generation;
    bool _closed;

    public CameraWindow(AppCore core)
    {
        _core = core;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        WindowChrome.ResizeDips(this, 720, 480);
        Closed += (_, _) => _ = StopAsync();
        _ = StartAsync();
    }

    async Task StartAsync()
    {
        if (!_core.Settings.CameraPreview)
        {
            Status.Text = "Camera preview is off in Settings.";
            return;
        }

        var gen = Interlocked.Increment(ref _generation);
        try
        {
            _capture = new MediaCapture();
            await _capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
            });
            if (_closed || gen != _generation)
            {
                await StopAsync();
                return;
            }

            var source = _capture.FrameSources.Values.FirstOrDefault(s => s.Info.MediaStreamType == MediaStreamType.VideoPreview)
                         ?? _capture.FrameSources.Values.FirstOrDefault(s => s.Info.MediaStreamType == MediaStreamType.VideoRecord);
            if (source is null)
            {
                Status.Text = "No camera source. Opening the Camera app instead.";
                await DisposeCaptureAsync();
                ProcessLauncher.OpenUri("microsoft.windows.camera:");
                return;
            }

            _reader = await _capture.CreateFrameReaderAsync(source);
            if (_closed || gen != _generation)
            {
                await StopAsync();
                return;
            }

            _reader.FrameArrived += OnFrame;
            var status = await _reader.StartAsync();
            if (_closed || gen != _generation)
            {
                await StopAsync();
                return;
            }

            Status.Text = status == MediaFrameReaderStartStatus.Success ? "" : "Camera reader: " + status;
        }
        catch (Exception ex)
        {
            Status.Text = "Camera unavailable: " + ex.Message;
            Log.Write("camera: " + ex);
            await DisposeCaptureAsync();
            ProcessLauncher.OpenUri("microsoft.windows.camera:");
        }
    }

    void OnFrame(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (_closed)
            return;
        if (Interlocked.CompareExchange(ref _presenting, 1, 0) != 0)
            return;
        using var frame = sender.TryAcquireLatestFrame();
        var bitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
        if (bitmap is null)
        {
            Interlocked.Exchange(ref _presenting, 0);
            return;
        }

        var display = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (_closed)
                    return;
                _bitmapSource ??= new SoftwareBitmapSource();
                await _bitmapSource.SetBitmapAsync(display);
                if (_closed)
                    return;
                Preview.Source = _bitmapSource;
            }
            catch (Exception)
            {
            }
            finally
            {
                display.Dispose();
                Interlocked.Exchange(ref _presenting, 0);
            }
        }))
        {
            display.Dispose();
            Interlocked.Exchange(ref _presenting, 0);
        }
    }

    async Task StopAsync()
    {
        _closed = true;
        Interlocked.Increment(ref _generation);
        await DisposeCaptureAsync();
    }

    async Task DisposeCaptureAsync()
    {
        try
        {
            if (_reader is not null)
            {
                _reader.FrameArrived -= OnFrame;
                try { await _reader.StopAsync(); } catch (Exception) { }
                _reader.Dispose();
                _reader = null;
            }

            _capture?.Dispose();
            _capture = null;
        }
        catch (Exception) { }
    }
}
