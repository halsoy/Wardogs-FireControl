using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WardogsFireControl;

public sealed class HotkeyService : IDisposable
{
    public const int TargetHotkeyId = 0x5101;
    public const int GunHotkeyId = 0x5102;
    private const uint ModNoRepeat = 0x4000;
    private readonly IntPtr _windowHandle;

    public HotkeyService(IntPtr windowHandle) => _windowHandle = windowHandle;

    public void Register(HotkeySettings settings)
    {
        Unregister();
        var target = new HotkeyBinding(settings.TargetKey, settings.TargetModifiers);
        var gun = new HotkeyBinding(settings.GunKey, settings.GunModifiers);
        if (target == gun)
            throw new InvalidOperationException("Target and gun hotkeys must be different.");

        if (!RegisterHotKey(_windowHandle, TargetHotkeyId, ModNoRepeat | (uint)target.Modifiers, (uint)target.Key))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not register {target}.");

        if (!RegisterHotKey(_windowHandle, GunHotkeyId, ModNoRepeat | (uint)gun.Modifiers, (uint)gun.Key))
        {
            UnregisterHotKey(_windowHandle, TargetHotkeyId);
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not register {gun}.");
        }
    }

    public void Unregister()
    {
        UnregisterHotKey(_windowHandle, TargetHotkeyId);
        UnregisterHotKey(_windowHandle, GunHotkeyId);
    }

    public void Dispose() => Unregister();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
