using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Relay.Platform;

namespace Relay.Platform.Harness;

internal static class WindowHookRegressionTests
{
    const uint ProbeMessage = 0x8000 + 173;
    const uint NestedMessage = ProbeMessage + 1;
    const uint WmNcDestroy = 0x0082;
    const int BaseResult = 71;
    static readonly WindowProc BaseCallback = BaseWndProc;
    static readonly string ClassName = "Relay.WindowHookRegression." + Guid.NewGuid().ToString("N");
    static int _baseCalls;
    static int _destroyCalls;

    public static void Run(Action<string, bool, string?> check)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Window hook regressions require Windows.");
        var instance = GetModuleHandle(null);
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            Procedure = Marshal.GetFunctionPointerForDelegate(BaseCallback),
            Instance = instance,
            ClassName = ClassName,
        };
        if (RegisterClassEx(ref windowClass) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            RunCase("window hook forwards unhandled and preserves handled results", Forwarding);
            RunCase("window hook removes nested hooks out of order", OutOfOrderRemoval);
            RunCase("window hook supports LIFO removal", LifoRemoval);
            RunCase("window hook survives GC without caller ownership and releases on destruction", ForgottenHook);
            RunCase("window hook releases its filter after dispose", DisposeReleasesFilter);
            RunCase("window hook contains filter exceptions and continues forwarding", ThrowingFilter);
            RunCase("window hook cannot swallow native destruction", HandledDestruction);
            RunCase("window hook contains destruction exceptions and releases roots", ThrowingDestruction);
            RunCase("window hook supports self-dispose with nested dispatch", ReentrantDispose);
            RunCase("window hook supports removing an earlier hook during dispatch", RemoveEarlierDuringDispatch);
            RunCase("window hook supports destruction inside its filter", ReentrantDestruction);
            RunCase("window hook rejects invalid handles and null filters", InvalidInstallation);
            RunCase("window hook rejects cross-thread install and dispose without detaching", WrongThread);
        }
        finally
        {
            if (!UnregisterClass(ClassName, instance))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        void RunCase(string name, Action test)
        {
            try
            {
                test();
                check(name, true, null);
            }
            catch (Exception exception)
            {
                check(name, false, exception.ToString());
            }
        }
    }

    static void Forwarding()
    {
        using var window = new HiddenWindow();
        var calls = 0;
        using var hook = new WindowProcedureHook(window.Handle, (_, msg, w, l) =>
        {
            if (msg != ProbeMessage)
                return null;
            calls++;
            Require(w == (IntPtr)13 && l == (IntPtr)29, "Message arguments changed.");
            return calls == 1 ? null : (IntPtr)101;
        });
        var before = _baseCalls;
        Require(Probe(window.Handle) == (IntPtr)BaseResult, "Unhandled message did not reach the window procedure.");
        Require(Probe(window.Handle) == (IntPtr)101, "Handled result was not returned.");
        Require(calls == 2 && _baseCalls == before + 1, "Unexpected dispatch counts.");
    }

    static void OutOfOrderRemoval()
    {
        using var window = new HiddenWindow();
        var order = new List<int>();
        using var first = Observe(window.Handle, 1, order);
        using var middle = Observe(window.Handle, 2, order);
        using var last = Observe(window.Handle, 3, order);
        Probe(window.Handle);
        Require(order.SequenceEqual(new[] { 3, 2, 1 }), "Initial subclass order is incorrect.");
        middle.Dispose();
        order.Clear();
        Collect();
        Require(Probe(window.Handle) == (IntPtr)BaseResult, "Base procedure was lost after middle removal.");
        Require(order.SequenceEqual(new[] { 3, 1 }), "Middle removal corrupted the chain.");
        first.Dispose();
        order.Clear();
        Probe(window.Handle);
        Require(order.SequenceEqual(new[] { 3 }), "Oldest removal corrupted the newest hook.");
        last.Dispose();
        middle.Dispose();
        order.Clear();
        Require(Probe(window.Handle) == (IntPtr)BaseResult && order.Count == 0, "Removed hooks were resurrected.");
    }

    static void LifoRemoval()
    {
        using var window = new HiddenWindow();
        var order = new List<int>();
        using var first = Observe(window.Handle, 1, order);
        using var last = Observe(window.Handle, 2, order);
        last.Dispose();
        Probe(window.Handle);
        Require(order.SequenceEqual(new[] { 1 }), "Newest removal lost the older hook.");
        first.Dispose();
        order.Clear();
        Require(Probe(window.Handle) == (IntPtr)BaseResult && order.Count == 0, "LIFO removal lost the base procedure.");
    }

    static WindowProcedureHook Observe(IntPtr hwnd, int id, List<int> order) => new(hwnd, (_, msg, _, _) =>
    {
        if (msg == ProbeMessage)
            order.Add(id);
        return null;
    });

    static void ForgottenHook()
    {
        using var window = new HiddenWindow();
        var weak = InstallForgotten(window.Handle, false);
        Collect();
        Require(weak.IsAlive, "Native registration did not root the hook.");
        for (var i = 0; i < 20; i++)
        {
            Require(Probe(window.Handle) == (IntPtr)103, "Collected callback failed to handle the message.");
            Collect();
        }
        var before = _destroyCalls;
        window.Dispose();
        Require(_destroyCalls == before + 1, "Destruction did not reach the base window procedure.");
        Collect();
        Require(!weak.IsAlive, "Destruction leaked the native-lifetime root.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference InstallForgotten(IntPtr hwnd, bool throwOnDestroy)
    {
        var hook = new WindowProcedureHook(hwnd, (_, msg, _, _) =>
        {
            if (msg == WmNcDestroy && throwOnDestroy)
                throw new InvalidOperationException("Expected destruction regression exception.");
            return msg == ProbeMessage ? (IntPtr)103 : null;
        });
        return new WeakReference(hook);
    }

    static void DisposeReleasesFilter()
    {
        using var window = new HiddenWindow();
        var (hook, weak) = InstallCaptured(window.Handle);
        Collect();
        Require(weak.IsAlive, "Filter target was collected while installed.");
        hook.Dispose();
        Collect();
        Require(!weak.IsAlive, "Disposed hook retained the filter target.");
        Require(Probe(window.Handle) == (IntPtr)BaseResult, "Disposed hook was still invoked.");
        GC.KeepAlive(hook);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static (WindowProcedureHook Hook, WeakReference Target) InstallCaptured(IntPtr hwnd)
    {
        var target = new FilterTarget();
        return (new WindowProcedureHook(hwnd, target.Filter), new WeakReference(target));
    }

    sealed class FilterTarget
    {
        public IntPtr? Filter(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam) => msg == ProbeMessage ? (IntPtr)107 : null;
    }

    static void ThrowingFilter()
    {
        using var window = new HiddenWindow();
        var calls = 0;
        using var hook = new WindowProcedureHook(window.Handle, (_, msg, _, _) =>
        {
            if (msg == ProbeMessage)
            {
                calls++;
                throw new InvalidOperationException("Expected filter regression exception.");
            }
            return null;
        });
        Require(Probe(window.Handle) == (IntPtr)BaseResult, "Exception prevented native forwarding.");
        Require(Probe(window.Handle) == (IntPtr)BaseResult && calls == 2, "Exception corrupted later dispatch.");
    }

    static void HandledDestruction()
    {
        using var window = new HiddenWindow();
        var observed = 0;
        using var first = new WindowProcedureHook(window.Handle, (_, msg, _, _) =>
        {
            if (msg == WmNcDestroy)
                observed++;
            return null;
        });
        using var last = new WindowProcedureHook(window.Handle, (_, msg, _, _) =>
        {
            if (msg == WmNcDestroy)
            {
                observed++;
                return (IntPtr)109;
            }
            return null;
        });
        var before = _destroyCalls;
        window.Dispose();
        Require(observed == 2 && _destroyCalls == before + 1, "A filter swallowed WM_NCDESTROY.");
        last.Dispose();
        first.Dispose();
    }

    static void ThrowingDestruction()
    {
        using var window = new HiddenWindow();
        var weak = InstallForgotten(window.Handle, true);
        var before = _destroyCalls;
        window.Dispose();
        Collect();
        Require(!weak.IsAlive && _destroyCalls == before + 1, "Throwing destruction leaked the hook or blocked the chain.");
    }

    static void ReentrantDispose()
    {
        using var window = new HiddenWindow();
        WindowProcedureHook? hook = null;
        var calls = 0;
        hook = new WindowProcedureHook(window.Handle, (hwnd, msg, _, _) =>
        {
            if (msg == ProbeMessage)
            {
                calls++;
                hook!.Dispose();
                Collect();
                SendMessage(hwnd, NestedMessage, IntPtr.Zero, IntPtr.Zero);
            }
            return null;
        });
        using (hook)
        {
            var before = _baseCalls;
            Require(Probe(window.Handle) == (IntPtr)BaseResult, "Self-dispose lost outer forwarding.");
            Require(_baseCalls == before + 2 && calls == 1, "Nested dispatch invoked a disposed hook or lost forwarding.");
            Require(Probe(window.Handle) == (IntPtr)BaseResult && calls == 1, "Self-disposed hook was resurrected.");
        }
    }

    static void RemoveEarlierDuringDispatch()
    {
        using var window = new HiddenWindow();
        var order = new List<int>();
        using var first = Observe(window.Handle, 1, order);
        using var last = new WindowProcedureHook(window.Handle, (_, msg, _, _) =>
        {
            if (msg == ProbeMessage)
            {
                order.Add(2);
                first.Dispose();
                Collect();
            }
            return null;
        });
        Require(Probe(window.Handle) == (IntPtr)BaseResult, "Removing an earlier hook lost native forwarding.");
        Require(order.SequenceEqual(new[] { 2 }), "A removed hook ran during dispatch.");
    }

    static void ReentrantDestruction()
    {
        using var window = new HiddenWindow();
        var observed = 0;
        WindowProcedureHook? hook = null;
        hook = new WindowProcedureHook(window.Handle, (_, msg, _, _) =>
        {
            if (msg == ProbeMessage)
                window.Dispose();
            if (msg == WmNcDestroy)
            {
                observed++;
                hook!.Dispose();
                Collect();
            }
            return null;
        });
        using (hook)
        {
            var before = _baseCalls;
            Probe(window.Handle);
            Require(observed == 1 && _baseCalls == before, "Dispatch continued against a destroyed HWND.");
        }
    }

    static void InvalidInstallation()
    {
        Expect<ArgumentException>(() => new WindowProcedureHook(IntPtr.Zero, (_, _, _, _) => null));
        using var window = new HiddenWindow();
        Expect<ArgumentNullException>(() => new WindowProcedureHook(window.Handle, null!));
        var stale = window.Handle;
        window.Dispose();
        Expect<ArgumentException>(() => new WindowProcedureHook(stale, (_, _, _, _) => null));
    }

    static void WrongThread()
    {
        using var window = new HiddenWindow();
        using var hook = new WindowProcedureHook(window.Handle, (_, msg, _, _) => msg == ProbeMessage ? (IntPtr)113 : null);
        Exception? installFailure = null;
        Exception? disposeFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var other = new WindowProcedureHook(window.Handle, (_, _, _, _) => null);
            }
            catch (Exception exception)
            {
                installFailure = exception;
            }
            try
            {
                hook.Dispose();
            }
            catch (Exception exception)
            {
                disposeFailure = exception;
            }
        });
        thread.IsBackground = true;
        thread.Start();
        Require(thread.Join(TimeSpan.FromSeconds(10)), "Cross-thread operation deadlocked.");
        Require(installFailure is InvalidOperationException && disposeFailure is InvalidOperationException, "Cross-thread operation was accepted.");
        Collect();
        Require(Probe(window.Handle) == (IntPtr)113, "Rejected cross-thread dispose detached the hook.");
        hook.Dispose();
        Require(Probe(window.Handle) == (IntPtr)BaseResult, "Owner-thread dispose failed after rejection.");
    }

    static void Expect<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    static void Collect()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
    }

    static IntPtr Probe(IntPtr hwnd) => SendMessage(hwnd, ProbeMessage, (IntPtr)13, (IntPtr)29);

    static IntPtr BaseWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == ProbeMessage || msg == NestedMessage)
        {
            _baseCalls++;
            return (IntPtr)BaseResult;
        }
        if (msg == WmNcDestroy)
            _destroyCalls++;
        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    sealed class HiddenWindow : IDisposable
    {
        public IntPtr Handle { get; private set; }

        public HiddenWindow()
        {
            Handle = CreateWindowEx(0, ClassName, "", 0, 0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
            if (Handle == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public void Dispose()
        {
            if (Handle == IntPtr.Zero)
                return;
            if (!DestroyWindow(Handle))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            Handle = IntPtr.Zero;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WindowClass
    {
        public uint Size;
        public uint Style;
        public IntPtr Procedure;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr SmallIcon;
    }

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern ushort RegisterClassEx(ref WindowClass windowClass);

    [DllImport("user32.dll", EntryPoint = "UnregisterClassW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool UnregisterClass(string className, IntPtr instance);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll", EntryPoint = "SendMessageW", ExactSpelling = true)]
    static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW", ExactSpelling = true)]
    static extern IntPtr DefWindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandle(string? moduleName);
}
