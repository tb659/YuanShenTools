using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace YuanShenTools
{
    /// <summary>
    /// 全局热键管理器
    /// 封装 RegisterHotKey / UnregisterHotKey Win32 API，
    /// 通过 WPF HwndSource 的消息钩子接收 WM_HOTKEY 并分发给注册的回调。
    /// </summary>
    public sealed class HotKeyManager : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;  // 系统热键消息 ID

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private readonly IntPtr _hWnd;                        // 目标窗口句柄
        private readonly Dictionary<int, Action> _hotkeyActions = [];  // id → 回调
        private bool _disposed;

        /// <param name="window">接收热键消息的 WPF 窗口</param>
        public HotKeyManager(Window window)
        {
            _hWnd = new WindowInteropHelper(window).Handle;
            var source = HwndSource.FromHwnd(_hWnd);
            source?.AddHook(WndProc);  // 挂接窗口消息处理
        }

        /// <summary>注册全局热键</summary>
        /// <param name="id">唯一标识，用于注销</param>
        /// <param name="modifiers">修饰键组合（MOD_ALT/MOD_CONTROL/MOD_SHIFT/MOD_NONE）</param>
        /// <param name="vk">虚拟键码</param>
        /// <param name="action">触发时执行的回调</param>
        public void Register(int id, uint modifiers, uint vk, Action action)
        {
            if (!RegisterHotKey(_hWnd, id, modifiers, vk))
            {
                throw new InvalidOperationException($"RegisterHotKey failed for id={id}, vk=0x{vk:X}");
            }
            _hotkeyActions[id] = action;
        }

        /// <summary>注销指定热键</summary>
        public void Unregister(int id)
        {
            UnregisterHotKey(_hWnd, id);
            _hotkeyActions.Remove(id);
        }

        /// <summary>WndProc 消息钩子：拦截 WM_HOTKEY 并分发到对应回调</summary>
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

        /// <summary>释放所有热键</summary>
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
