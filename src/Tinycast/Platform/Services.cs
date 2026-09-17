using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using Tinycast.Features.Calculator;
using Tinycast.Features.Clipboard;
using Tinycast.Features.SystemActions;
using Tinycast.Features.WindowManagement;
using Tinycast.Platform;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace Tinycast;

public sealed class ClipboardManager
{
    readonly AppCore _core;
    bool _ignoreNext;

    public ClipboardManager(AppCore core) => _core = core;

    public void Start(IntPtr hwnd)
    {
        NativeMethods.AddClipboardFormatListener(hwnd);
        Capture();
    }

    public void Handle(uint msg)
    {
        if (msg == NativeMethods.WmClipboardUpdate)
            _ = CaptureAsync();
    }

    public void IgnoreNext() => _ignoreNext = true;

    public void Capture() => _ = CaptureAsync();

    async Task CaptureAsync()
    {
        if (_ignoreNext)
        {
            _ignoreNext = false;
            return;
        }

        if (!_core.Settings.ClipboardEnabled)
            return;

        try
        {
            var content = WinClipboard.GetContent();
            if (content.Contains("TinycastIgnore")
                || content.Contains("ExcludeClipboardContentFromMonitorProcessing"))
                return;

            var source = Paster.ForegroundProcessPath(IntPtr.Zero);
            var hasFiles = content.Contains(StandardDataFormats.StorageItems);
            var hasBitmap = content.Contains(StandardDataFormats.Bitmap);
            var hasText = content.Contains(StandardDataFormats.Text);
            string? text = null;
            if (hasText)
                text = await content.GetTextAsync();

            IReadOnlyList<string> durableFiles = [];
            if (hasFiles)
            {
                var items = await content.GetStorageItemsAsync();
                durableFiles = items
                    .Select(i => i.Path)
                    .Where(path => !ClipboardCapture.IsVolatilePath(path))
                    .Take(ClipboardCapture.MaxCapturedFiles)
                    .Reverse()
                    .ToList();
            }

            var kind = ClipboardCapture.Decide(durableFiles.Count > 0, hasBitmap, hasText, text);
            if (kind == ClipboardKind.File)
            {
                foreach (var path in durableFiles)
                    _core.ClipboardStore.Insert(ClipboardKind.File, path, filePath: path, sourceId: source);
                _core.ClipboardCoordinator.HistoryChanged();
                return;
            }

            if (kind == ClipboardKind.Image)
            {
                var bmp = await content.GetBitmapAsync();
                var path = await SaveBitmap(bmp);
                if (path is not null)
                {
                    var item = _core.ClipboardStore.Insert(ClipboardKind.Image, "", path, sourceId: source);
                    _ = RecognizeAsync(item.Id, path);
                    _core.ClipboardCoordinator.HistoryChanged();
                }

                return;
            }

            if (kind == ClipboardKind.Text
                && !string.IsNullOrWhiteSpace(text)
                && text.Length <= ClipboardCapture.MaxTextLength
                && !_core.ClipboardStore.IsDuplicateText(text))
            {
                _core.ClipboardStore.Insert(ClipboardKind.Text, text, sourceId: source);
                if (_core.Settings.QuickActionsEnabled)
                    _core.RememberSelection(text);
                _core.ClipboardCoordinator.HistoryChanged();
            }
        }
        catch (Exception ex)
        {
            Log.Write("clipboard: " + ex.Message);
        }
    }

    public void CopyText(string text)
    {
        IgnoreNext();
        var package = new DataPackage();
        package.SetText(text);
        package.SetData("TinycastIgnore", "1");
        WinClipboard.SetContent(package);
        WinClipboard.Flush();
    }

    public async Task CopyImageAsync(string path)
    {
        IgnoreNext();
        var file = await StorageFile.GetFileFromPathAsync(path);
        var package = new DataPackage();
        package.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
        package.SetData("TinycastIgnore", "1");
        WinClipboard.SetContent(package);
        WinClipboard.Flush();
    }

    public async Task CopyFileAsync(string path)
    {
        IgnoreNext();
        var file = await StorageFile.GetFileFromPathAsync(path);
        var package = new DataPackage();
        package.SetStorageItems([file]);
        package.SetText(path);
        package.SetData("TinycastIgnore", "1");
        WinClipboard.SetContent(package);
        WinClipboard.Flush();
    }

    async Task<string?> SaveBitmap(RandomAccessStreamReference reference)
    {
        string? path = null;
        try
        {
            using var stream = await reference.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            path = Path.Combine(_core.ClipboardStore.ImageRoot, Guid.NewGuid().ToString("n") + ".png");
            using var file = File.OpenWrite(path);
            using var ras = file.AsRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ras);
            var pixels = await decoder.GetPixelDataAsync();
            encoder.SetPixelData(
                decoder.BitmapPixelFormat,
                decoder.BitmapAlphaMode,
                decoder.PixelWidth,
                decoder.PixelHeight,
                decoder.DpiX,
                decoder.DpiY,
                pixels.DetachPixelData());
            await encoder.FlushAsync();
            return path;
        }
        catch (Exception ex)
        {
            Log.Write("clipboard image: " + ex.Message);
            if (path is not null)
            {
                try { File.Delete(path); } catch (Exception) { }
            }

            return null;
        }
    }

    async Task RecognizeAsync(long id, string path)
    {
        try
        {
            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (engine is null)
                return;
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var bitmap = await decoder.GetSoftwareBitmapAsync();
            var result = await engine.RecognizeAsync(bitmap);
            if (!string.IsNullOrWhiteSpace(result.Text))
                _core.ClipboardStore.SetOcr(id, result.Text);
        }
        catch (Exception ex)
        {
            Log.Write("ocr: " + ex.Message);
        }
    }
}

internal static class SystemActionRunner
{
    public static void Run(string id, AppCore core)
    {
        switch (id)
        {
            case "lock-screen": NativeMethods.LockWorkStation(); break;
            case "sleep": NativeMethods.SetSuspendState(false, false, false); break;
            case "sleep-displays":
                NativeMethods.SendMessage((IntPtr)0xFFFF, NativeMethods.WmSysCommand, (IntPtr)NativeMethods.ScMonitorPower, (IntPtr)NativeMethods.MonitorOff);
                break;
            case "restart": Process.Start(new ProcessStartInfo("shutdown", "/r /t 0") { UseShellExecute = true }); break;
            case "shut-down": Process.Start(new ProcessStartInfo("shutdown", "/s /t 0") { UseShellExecute = true }); break;
            case "log-out": NativeMethods.ExitWindowsEx(0, 0); break;
            case "show-screen-saver":
                NativeMethods.SendMessage((IntPtr)0xFFFF, NativeMethods.WmSysCommand, (IntPtr)0xF140, IntPtr.Zero);
                break;
            case "play-pause": NativeMethods.SendVk(NativeMethods.VkMediaPlayPause); break;
            case "next-track": NativeMethods.SendVk(NativeMethods.VkMediaNext); break;
            case "previous-track": NativeMethods.SendVk(NativeMethods.VkMediaPrev); break;
            case "toggle-mute": NativeMethods.SendVk(NativeMethods.VkVolumeMute); core.ShowMessage("Mute"); break;
            case "volume-up": NativeMethods.SendVk(NativeMethods.VkVolumeUp); core.ShowMessage("Volume up"); break;
            case "volume-down": NativeMethods.SendVk(NativeMethods.VkVolumeDown); core.ShowMessage("Volume down"); break;
            case "volume-0": SendVolumeKeys(0); core.ShowMessage("Volume 0%"); break;
            case "volume-25": SendVolumeKeys(4); core.ShowMessage("Volume 25%"); break;
            case "volume-50": SendVolumeKeys(8); core.ShowMessage("Volume 50%"); break;
            case "volume-75": SendVolumeKeys(12); core.ShowMessage("Volume 75%"); break;
            case "volume-100": SendVolumeKeys(16); core.ShowMessage("Volume 100%"); break;
            case "show-desktop":
                NativeMethods.keybd_event((byte)NativeMethods.VkLwin, 0, 0, UIntPtr.Zero);
                NativeMethods.keybd_event((byte)NativeMethods.VkD, 0, 0, UIntPtr.Zero);
                NativeMethods.keybd_event((byte)NativeMethods.VkD, 0, NativeMethods.KeyeventfKeyUp, UIntPtr.Zero);
                NativeMethods.keybd_event((byte)NativeMethods.VkLwin, 0, NativeMethods.KeyeventfKeyUp, UIntPtr.Zero);
                break;
            case "toggle-system-appearance": ToggleAppearance(); break;
            case "open-trash": ProcessLauncher.Open("shell:RecycleBinFolder"); break;
            case "empty-trash": NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null, 0x00000001 | 0x00000002 | 0x00000004); break;
            case "eject-all-disks":
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable && d.IsReady))
                    Process.Start(new ProcessStartInfo("powershell", $"-NoProfile -Command \"(New-Object -com Shell.Application).Namespace(17).ParseName('{drive.Name[..1]}:').InvokeVerb('Eject')\"") { UseShellExecute = false, CreateNoWindow = true });
                break;
            case "toggle-hidden-files": ToggleHiddenFiles(); break;
            case "hide-all-apps-except-frontmost": HideOthers(core); break;
            case "unhide-all-hidden-apps":
                foreach (var w in WindowInventory.Enumerate())
                    NativeMethods.ShowWindow(w.Hwnd, NativeMethods.SwRestore);
                break;
            case "quit-all-apps": QuitOthers(); break;
            case "dismiss-notifications":
                NativeMethods.keybd_event((byte)NativeMethods.VkLwin, 0, 0, UIntPtr.Zero);
                NativeMethods.keybd_event((byte)0x41, 0, 0, UIntPtr.Zero); // A
                NativeMethods.keybd_event((byte)0x41, 0, NativeMethods.KeyeventfKeyUp, UIntPtr.Zero);
                NativeMethods.keybd_event((byte)NativeMethods.VkLwin, 0, NativeMethods.KeyeventfKeyUp, UIntPtr.Zero);
                break;
            case "toggle-bluetooth": ProcessLauncher.OpenUri("ms-settings:bluetooth"); break;
            case "set-volume":
                core.ShowMessage("Use volume 0–100 commands, or the HUD keys.", DialogTone.Neutral);
                break;
        }
    }

    static void SendVolumeKeys(int ups)
    {
        NativeMethods.SendVk(NativeMethods.VkVolumeMute);
        NativeMethods.SendVk(NativeMethods.VkVolumeMute);
        for (var i = 0; i < 50; i++)
            NativeMethods.SendVk(NativeMethods.VkVolumeDown);
        for (var i = 0; i < ups; i++)
            NativeMethods.SendVk(NativeMethods.VkVolumeUp);
    }

    static void ToggleAppearance()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", true);
        if (key is null)
            return;
        var current = (int)(key.GetValue("AppsUseLightTheme") ?? 0);
        var next = current == 0 ? 1 : 0;
        key.SetValue("AppsUseLightTheme", next);
        key.SetValue("SystemUsesLightTheme", next);
    }

    static void ToggleHiddenFiles()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", true);
        if (key is null)
            return;
        var current = (int)(key.GetValue("Hidden") ?? 2);
        key.SetValue("Hidden", current == 1 ? 2 : 1);
    }

    static void HideOthers(AppCore core)
    {
        var front = NativeMethods.GetForegroundWindow();
        foreach (var w in WindowInventory.Enumerate())
        {
            if (w.Hwnd != front)
                NativeMethods.ShowWindow(w.Hwnd, NativeMethods.SwMinimize);
        }
    }

    static void QuitOthers()
    {
        foreach (var w in WindowInventory.Enumerate())
        {
            try
            {
                NativeMethods.GetWindowThreadProcessId(w.Hwnd, out var pid);
                if (pid == (uint)Environment.ProcessId)
                    continue;
                Process.GetProcessById((int)pid).CloseMainWindow();
            }
            catch (Exception) { }
        }
    }
}

internal static class WindowMover
{
    public static readonly WindowActionMemory Memory = new();

    public static void Run(string commandId, IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            hwnd = NativeMethods.GetForegroundWindow();
        var catalog = WindowCommandCatalog.Find(commandId);
        if (catalog is null)
            return;
        if (catalog.Kind == WindowCommandKind.Fullscreen)
        {
            Paster.FocusTarget(hwnd);
            NativeMethods.SendVk(NativeMethods.VkF11);
            return;
        }

        if (catalog.Kind == WindowCommandKind.Space)
        {
            NativeMethods.keybd_event((byte)NativeMethods.VkLwin, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event((byte)0x11, 0, 0, UIntPtr.Zero);
            var vk = commandId == "next-space" ? (byte)0x27 : (byte)0x25;
            NativeMethods.keybd_event(vk, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(vk, 0, NativeMethods.KeyeventfKeyUp, UIntPtr.Zero);
            NativeMethods.keybd_event((byte)0x11, 0, NativeMethods.KeyeventfKeyUp, UIntPtr.Zero);
            NativeMethods.keybd_event((byte)NativeMethods.VkLwin, 0, NativeMethods.KeyeventfKeyUp, UIntPtr.Zero);
            return;
        }

        var frame = WindowInventory.Frame(hwnd);
        var last = Memory.LastCommand(hwnd);
        if (catalog.Kind != WindowCommandKind.Restore && Memory.Restore(hwnd) is null)
            Memory.Remember(hwnd, frame);
        var input = new PlacementInput
        {
            Command = commandId,
            WindowFrame = frame,
            Screens = WindowInventory.Screens(),
            Gap = 8,
            Step = Memory.NextStep(hwnd, commandId),
            Cycle = catalog.CyclesOnRepeat ? WindowCycle.Sizes : WindowCycle.Off,
            RestoreFrame = Memory.Restore(hwnd),
            LastTileCommand = last,
        };
        var placement = WindowPlacementEngine.PlacementFor(input);
        if (placement is { } p)
            WindowInventory.Place(hwnd, p.Frame);
        if (catalog.Kind == WindowCommandKind.Restore)
            Memory.Forget(hwnd);
    }
}

internal static class CurrencyRateStore
{
    static CurrencyRates? _cached;
    static DateTime _fetched;

    public static CurrencyRates? Current => _cached;

    public static async Task RefreshAsync()
    {
        if (_cached is not null && DateTime.UtcNow - _fetched < TimeSpan.FromHours(6))
            return;
        try
        {
            using var client = new HttpClient(new SocketsHttpHandler { UseCookies = false })
            {
                Timeout = TimeSpan.FromSeconds(8),
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Tinycast/0.1");
            var json = await client.GetStringAsync("https://open.er-api.com/v6/latest/USD");
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("rates", out var rates))
                return;
            var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in rates.EnumerateObject())
            {
                if (prop.Value.TryGetDouble(out var value) && value > 0)
                    map[prop.Name] = value;
            }

            map["USD"] = 1;
            var crypto = await TryCryptoRatesAsync(client);
            var includesCrypto = false;
            if (crypto is not null)
            {
                foreach (var (code, perUsd) in crypto)
                    map[code] = perUsd;
                includesCrypto = crypto.Count > 0;
            }

            _cached = new CurrencyRates(map, DateTime.UtcNow, includesCrypto);
            _fetched = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Log.Write("rates: " + ex.Message);
        }
    }

    static readonly (string Code, string Id)[] CryptoIds =
    [
        ("BTC", "bitcoin"), ("ETH", "ethereum"), ("SOL", "solana"), ("XRP", "ripple"),
        ("DOGE", "dogecoin"), ("ADA", "cardano"), ("LTC", "litecoin"), ("DOT", "polkadot"),
        ("AVAX", "avalanche"), ("SHIB", "shiba-inu"), ("XMR", "monero"), ("USDT", "tether"),
        ("BNB", "binancecoin"), ("TRX", "tron"), ("XLM", "stellar"), ("BCH", "bitcoin-cash"),
        ("ETC", "ethereum-classic"), ("NEAR", "near"), ("POL", "matic-network"),
    ];

    static async Task<Dictionary<string, double>?> TryCryptoRatesAsync(HttpClient client)
    {
        try
        {
            var ids = string.Join(",", CryptoIds.Select(c => c.Id));
            var json = await client.GetStringAsync("https://api.coingecko.com/api/v3/simple/price?ids=" + ids + "&vs_currencies=usd");
            using var doc = JsonDocument.Parse(json);
            var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var (code, id) in CryptoIds)
            {
                if (!doc.RootElement.TryGetProperty(id, out var coin)
                    || !coin.TryGetProperty("usd", out var usd)
                    || !usd.TryGetDouble(out var price)
                    || price <= 0)
                    continue;
                map[code] = 1.0 / price;
            }

            return map.Count == 0 ? null : map;
        }
        catch (Exception ex)
        {
            Log.Write("crypto rates: " + ex.Message);
            return null;
        }
    }
}
