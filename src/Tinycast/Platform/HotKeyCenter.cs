using System.Text;
using System.Runtime.InteropServices;
using Tinycast.Features.HotKeys;

namespace Tinycast.Platform;

internal sealed class HotKeyCenter : IDisposable
{
    public const int TogglePaletteId = 1;
    const int ExtraIdBase = 100;

    readonly IntPtr _hwnd;
    readonly Dictionary<int, HotKeyBinding> _registered = [];
    NativeMethods.LowLevelKeyboardProc? _hookProc;
    IntPtr _hook;
    Action<HotKeyChord>? _recording;
    readonly DoubleTapDetector _doubleTap = new();
    List<HotKeyBinding> _bindings = [];
    List<(string Keyword, string Id)> _keywords = [];
    readonly StringBuilder _typed = new();

    public event Action? TogglePalette;
    public event Action<string>? Command;
    public event Action<string, string>? ExpandSnippet;
    public event Action<string>? RegistrationFailed;
    readonly List<HotKeyBinding> _hookBindings = [];
    bool _toggleHookFallback;

    public HotKeyCenter(IntPtr hwnd) => _hwnd = hwnd;

    public void SetSnippetKeywords(IEnumerable<(string Keyword, string Id)> keywords)
    {
        _keywords = keywords.Where(k => !string.IsNullOrWhiteSpace(k.Keyword)).ToList();
        SyncHook();
    }

    public void ReplaceBindings(IEnumerable<HotKeyBinding> bindings)
    {
        Pause();
        _bindings = bindings.Where(b => !b.IsEmpty).ToList();
        Start();
    }

    public void Start()
    {
        Pause();
        _hookBindings.Clear();
        var toggleOk = Register(TogglePaletteId, NativeMethods.ModAlt | NativeMethods.ModNoRepeat, NativeMethods.VkSpace);
        if (!toggleOk && !_toggleHookFallback)
            RegistrationFailed?.Invoke("Alt+Space is already in use. Tinycast is watching it from the keyboard hook.");
        _toggleHookFallback = !toggleOk;

        var extra = ExtraIdBase;
        foreach (var binding in _bindings)
        {
            if (binding.Chord is not { } chord)
            {
                extra++;
                continue;
            }

            if (UsesHook(binding) || !Register(extra, chord.Modifiers | NativeMethods.ModNoRepeat, chord.VirtualKey))
            {
                _hookBindings.Add(binding);
                if (!UsesHook(binding))
                    RegistrationFailed?.Invoke("Hotkey unavailable (conflict): " + chord.Label);
            }
            else
            {
                _registered[extra] = binding;
            }

            extra++;
        }

        SyncHook();
    }

    public void Pause()
    {
        NativeMethods.UnregisterHotKey(_hwnd, TogglePaletteId);
        foreach (var id in _registered.Keys)
            NativeMethods.UnregisterHotKey(_hwnd, id);
        _registered.Clear();
        RemoveHook();
    }

    public void BeginRecord(Action<HotKeyChord> done)
    {
        _recording = done;
        Pause();
        EnsureHook();
    }

    public void CancelRecord()
    {
        _recording = null;
        Start();
    }

    public bool HandleMessage(uint msg, IntPtr wParam)
    {
        if (msg == NativeMethods.WmResumeHotKeys)
        {
            Start();
            return true;
        }

        if (msg != NativeMethods.WmHotkey)
            return false;
        var id = wParam.ToInt32();
        if (id == TogglePaletteId)
        {
            TogglePalette?.Invoke();
            return true;
        }

        if (_registered.TryGetValue(id, out var binding) && AppAllowed(binding))
        {
            Command?.Invoke(binding.CommandId);
            return true;
        }

        return false;
    }

    bool Register(int id, uint modifiers, uint vk)
    {
        if (NativeMethods.RegisterHotKey(_hwnd, id, modifiers, vk))
            return true;
        Log.Write("RegisterHotKey failed id=" + id + " vk=0x" + vk.ToString("X") + " err=" + Marshal.GetLastWin32Error());
        return false;
    }

    static bool UsesHook(HotKeyBinding binding) =>
        binding.DoubleTap is not null
        || !string.IsNullOrEmpty(binding.AppPath)
        || binding.Chord?.IsHyper == true;

    bool HookRequired() =>
        _recording is not null
        || _keywords.Count > 0
        || _toggleHookFallback
        || _hookBindings.Count > 0
        || _bindings.Any(b => b.DoubleTap is not null);

    void SyncHook()
    {
        if (HookRequired())
            EnsureHook();
        else
            RemoveHook();
    }

    void EnsureHook()
    {
        if (_hook != IntPtr.Zero)
            return;
        _hookProc = Hook;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            _hookProc,
            NativeMethods.GetModuleHandle(null),
            0);
        if (_hook != IntPtr.Zero)
            return;
        Log.Write("SetWindowsHookEx failed err=" + Marshal.GetLastWin32Error());
        _hookProc = null;
        RegistrationFailed?.Invoke("Keyboard hook failed to install.");
    }

    void RemoveHook()
    {
        if (_hook == IntPtr.Zero)
            return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        _hookProc = null;
    }

    IntPtr Hook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var info = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
            var message = (uint)wParam;
            var down = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
            var up = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;
            if ((down || up) && HandleKey(info.VkCode, down))
                return (IntPtr)1;
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    bool HandleKey(uint vk, bool down)
    {
        var modifiers = CurrentModifiers();
        if (_recording is not null && down && !IsModifier(vk))
        {
            var chord = new HotKeyChord(modifiers, vk);
            var done = _recording;
            _recording = null;
            done(chord);
            NativeMethods.PostMessage(_hwnd, NativeMethods.WmResumeHotKeys, IntPtr.Zero, IntPtr.Zero);
            return true;
        }

        if (down && TryExpandSnippet(vk))
            return true;

        if (down && _toggleHookFallback
            && modifiers == NativeMethods.ModAlt && vk == NativeMethods.VkSpace)
        {
            TogglePalette?.Invoke();
            return true;
        }

        if (down)
        {
            foreach (var binding in _hookBindings)
            {
                if (binding.Chord is not { } chord || chord.Modifiers != modifiers || chord.VirtualKey != vk)
                    continue;
                if (!AppAllowed(binding))
                    return false;
                Command?.Invoke(binding.CommandId);
                return true;
            }
        }

        var held = new List<DoubleTapModifier>();
        if (Down(NativeMethods.VkControl) || Down(NativeMethods.VkLcontrol) || Down(NativeMethods.VkRcontrol))
            held.Add(DoubleTapModifier.Control);
        if (Down(NativeMethods.VkMenu) || Down(NativeMethods.VkLmenu) || Down(NativeMethods.VkRmenu))
            held.Add(DoubleTapModifier.Alt);
        if (Down(NativeMethods.VkShift) || Down(NativeMethods.VkLshift) || Down(NativeMethods.VkRshift))
            held.Add(DoubleTapModifier.Shift);
        if (Down(NativeMethods.VkLwinKey) || Down(NativeMethods.VkRwin))
            held.Add(DoubleTapModifier.Win);

        var tapped = _doubleTap.Handle(held, hasOtherModifiers: !IsModifier(vk) && down, DateTime.UtcNow);
        if (tapped is { } modifier)
        {
            var match = _bindings.FirstOrDefault(b => b.DoubleTap == modifier && AppAllowed(b));
            if (match is not null)
                Command?.Invoke(match.CommandId);
        }

        return false;
    }

    bool TryExpandSnippet(uint vk)
    {
        if (_keywords.Count == 0 || CurrentModifiers() != 0)
            return false;
        if (vk is 0x08)
        {
            if (_typed.Length > 0)
                _typed.Length--;
            return false;
        }

        if (vk is 0x20 or 0x09 or 0x0D)
        {
            var token = _typed.ToString();
            _typed.Clear();
            var hit = _keywords.FirstOrDefault(k => k.Keyword.Equals(token, StringComparison.OrdinalIgnoreCase));
            if (hit.Id is null)
                return false;
            ExpandSnippet?.Invoke(hit.Id, hit.Keyword);
            return true;
        }

        if (vk is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A)
        {
            if (_typed.Length < 32)
                _typed.Append(char.ToLowerInvariant((char)vk));
            return false;
        }

        _typed.Clear();
        return false;
    }

    static uint CurrentModifiers()
    {
        uint mods = 0;
        if (Down(NativeMethods.VkControl) || Down(NativeMethods.VkLcontrol) || Down(NativeMethods.VkRcontrol))
            mods |= NativeMethods.ModControl;
        if (Down(NativeMethods.VkMenu) || Down(NativeMethods.VkLmenu) || Down(NativeMethods.VkRmenu))
            mods |= NativeMethods.ModAlt;
        if (Down(NativeMethods.VkShift) || Down(NativeMethods.VkLshift) || Down(NativeMethods.VkRshift))
            mods |= NativeMethods.ModShift;
        if (Down(NativeMethods.VkLwinKey) || Down(NativeMethods.VkRwin))
            mods |= NativeMethods.ModWin;
        return mods;
    }

    static bool Down(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

    static bool IsModifier(uint vk) => vk is
        NativeMethods.VkShift or NativeMethods.VkLshift or NativeMethods.VkRshift
        or NativeMethods.VkControl or NativeMethods.VkLcontrol or NativeMethods.VkRcontrol
        or NativeMethods.VkMenu or NativeMethods.VkLmenu or NativeMethods.VkRmenu
        or NativeMethods.VkLwinKey or NativeMethods.VkRwin or NativeMethods.VkCapital;

    static bool AppAllowed(HotKeyBinding binding)
    {
        if (string.IsNullOrEmpty(binding.AppPath))
            return true;
        var path = Paster.ForegroundProcessPath(IntPtr.Zero);
        if (path is null)
            return false;
        return path.Equals(binding.AppPath, StringComparison.OrdinalIgnoreCase)
               || Path.GetFileNameWithoutExtension(path).Equals(binding.AppPath, StringComparison.OrdinalIgnoreCase)
               || Path.GetFileName(path).Equals(binding.AppPath, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _recording = null;
        Pause();
    }
}
