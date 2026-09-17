using System.Runtime.InteropServices;

namespace Tinycast.Platform;

internal static class VirtualDesktop
{
    public static void Next() => Chord(true);
    public static void Previous() => Chord(false);

    static void Chord(bool next)
    {
        NativeMethods.keybd_event((byte)NativeMethods.VkLwin, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)NativeMethods.VkControl, 0, 0, UIntPtr.Zero);
        var vk = next ? (byte)0x27 : (byte)0x25;
        NativeMethods.keybd_event(vk, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(vk, 0, NativeMethods.KeyeventfKeyUp, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)NativeMethods.VkControl, 0, NativeMethods.KeyeventfKeyUp, UIntPtr.Zero);
        NativeMethods.keybd_event((byte)NativeMethods.VkLwin, 0, NativeMethods.KeyeventfKeyUp, UIntPtr.Zero);
    }
}
