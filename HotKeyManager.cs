using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace YuanShenTools
{
    public sealed class HotKeyManager : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly IntPtr _hWnd;
        private readonly Dictionary<int, Action> _hotkeyActions = [];
        private bool _disposed;

        public HotKeyManager(Window window)
        {
            _hWnd = new WindowInteropHelper(window).Handle;
            var source = HwndSource.FromHwnd(_hWnd);
            source?.AddHook(WndProc);
        }

        public void Register(int id, uint modifiers, uint vk, Action action)
        {
            if (!RegisterHotKey(_hWnd, id, modifiers, vk))
            {
                throw new InvalidOperationException($"RegisterHotKey failed for id={id}, vk=0x{vk:X}");
            }
            _hotkeyActions[id] = action;
        }

        public void Unregister(int id)
        {
            UnregisterHotKey(_hWnd, id);
            _hotkeyActions.Remove(id);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_hotkeyActions.TryGetValue(id, out var action))
                {
                    action();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                foreach (var id in _hotkeyActions.Keys.ToList())
                {
                    UnregisterHotKey(_hWnd, id);
                }
                _hotkeyActions.Clear();
                _disposed = true;
            }
        }
    }
}
