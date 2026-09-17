using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Relay.DesignSystem;
using Relay.Platform;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Relay;

public sealed partial class CameraWindow : Window
{
    readonly AppCore _core;
    MediaCapture? _capture;
    MediaFrameReader? _reader;
    SoftwareBitmapSource? _bitmapSource;
    SoftwareBitmap? _lastFrame;
    IReadOnlyList<DeviceInformation> _devices = [];
    int _deviceIndex;
    int _presenting;
    int _generation;
    bool _closed;
    bool _mirror;

    public CameraWindow(AppCore core)
    {
        _core = core;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        WindowChrome.ResizeDips(this, 720, 480);
        Activated += OnActivated;
        Closed += (_, _) => _ = StopAsync();
        _ = StartAsync();
    }

    void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState == WindowActivationState.Deactivated && !_closed)
            Close();
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
            _devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            if (_devices.Count == 0)
            {
                Status.Text = "No camera found. Opening the Camera app instead.";
                ProcessLauncher.OpenUri("microsoft.windows.camera:");
                return;
            }

            _deviceIndex = Math.Clamp(_deviceIndex, 0, _devices.Count - 1);
            _capture = new MediaCapture();
            await _capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                VideoDeviceId = _devices[_deviceIndex].Id,
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
                _lastFrame?.Dispose();
                _lastFrame = SoftwareBitmap.Copy(display);
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

    async void OnSnapshot(object sender, RoutedEventArgs e)
    {
        if (_lastFrame is null)
        {
            _core.ShowMessage("No frame yet.");
            return;
        }

        try
        {
            var folder = await KnownFolders.PicturesLibrary.CreateFolderAsync("Relay", CreationCollisionOption.OpenIfExists);
            var file = await folder.CreateFileAsync("snapshot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png", CreationCollisionOption.GenerateUniqueName);
            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetSoftwareBitmap(_lastFrame);
            await encoder.FlushAsync();
            await _core.Clipboard.CopyImageAsync(file.Path);
            _core.ShowMessage("Snapshot saved");
        }
        catch (Exception ex)
        {
            _core.ShowMessage(ex.Message, DialogTone.Danger);
        }
    }

    async void OnCycle(object sender, RoutedEventArgs e)
    {
        if (_devices.Count < 2)
        {
            _core.ShowMessage("Only one camera.");
            return;
        }

        _deviceIndex = (_deviceIndex + 1) % _devices.Count;
        await DisposeCaptureAsync();
        _closed = false;
        await StartAsync();
    }

    void OnMirror(object sender, RoutedEventArgs e)
    {
        _mirror = !_mirror;
        Preview.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        Preview.RenderTransform = new ScaleTransform { ScaleX = _mirror ? -1 : 1 };
    }

    async Task StopAsync()
    {
        _closed = true;
        Interlocked.Increment(ref _generation);
        await DisposeCaptureAsync();
        _lastFrame?.Dispose();
        _lastFrame = null;
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
