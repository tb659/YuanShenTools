using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace YuanShenTools
{
    public partial class MainWindow : Window
    {
        private HotKeyManager? _hotKeyManager;
        private AutoSkipService? _autoSkipService;
        private bool _isHidden;
        private bool _immersive;
        private string _currentUrl = "https://www.bilibili.com";

        private const uint VK_5 = 0x35;
        private const uint VK_6 = 0x36;
        private const uint VK_7 = 0x37;
        private const uint VK_8 = 0x38;
        private const uint VK_9 = 0x39;
        private const uint VK_0 = 0x30;
        private const uint VK_OEM_3 = 0xC0;
        private const uint VK_OEM_MINUS = 0xBD;
        private const uint VK_OEM_PLUS = 0xBB;
        private const uint MOD_NONE = 0x0000;

        private const int ID_PLAY_PAUSE = 1;
        private const int ID_FORWARD = 2;
        private const int ID_REWIND = 3;
        private const int ID_OPACITY_DOWN = 4;
        private const int ID_OPACITY_UP = 5;
        private const int ID_HIDE_SHOW = 6;
        private const int ID_IMMERSIVE = 7;
        private const int ID_AUTO_SKIP = 8;
        private const int ID_BOOKMARK = 9;

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await WebView.EnsureCoreWebView2Async();
            WebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var style = GetWindowLong(hwnd, GWL_STYLE);
            style |= WS_CAPTION;
            SetWindowLong(hwnd, GWL_STYLE, style);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

            var cfg = Config.Load();
            Config.ApplyWindow(this, cfg);
            Opacity = cfg.Opacity;
            var lastUrl = cfg.LastUrl ?? "https://www.bilibili.com";
            UrlTextBox.Text = lastUrl;
            WebView.CoreWebView2.Navigate(lastUrl);

            WebView.CoreWebView2.NavigationCompleted += (_, _) =>
            {
                MakePageTransparent();
                _currentUrl = WebView.CoreWebView2.Source;
                UrlTextBox.Text = _currentUrl;
            };

            _autoSkipService = new AutoSkipService(ExecuteScriptAsync);
            _autoSkipService.StatusChanged += (_, enabled) =>
            {
                Title = enabled ? "原神跟跑 [对话: 开]" : "原神跟跑 [对话: 关]";
            };

            RegisterHotkeys();
            ShortcutHelper.EnsureShortcut();
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _autoSkipService?.Stop();
            _hotKeyManager?.Dispose();
            var cfg = Config.FromWindow(this, _currentUrl, Opacity);
            Config.Save(cfg);
        }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private void RegisterHotkeys()
        {
            _hotKeyManager = new HotKeyManager(this);

            _hotKeyManager.Register(ID_PLAY_PAUSE, MOD_NONE, VK_OEM_3, () =>
                ExecuteScript("document.querySelector('video')?.paused ? document.querySelector('video')?.play() : document.querySelector('video')?.pause()"));

            _hotKeyManager.Register(ID_FORWARD, MOD_NONE, VK_6, () =>
                ExecuteScript("var v = document.querySelector('video'); if(v) v.currentTime += 10;"));

            _hotKeyManager.Register(ID_REWIND, MOD_NONE, VK_5, () =>
                ExecuteScript("var v = document.querySelector('video'); if(v) v.currentTime -= 10;"));

            _hotKeyManager.Register(ID_OPACITY_DOWN, MOD_NONE, VK_7, () =>
            {
                Opacity = System.Math.Max(0.1, Opacity - 0.1);
                MakePageTransparent();
            });

            _hotKeyManager.Register(ID_OPACITY_UP, MOD_NONE, VK_8, () =>
            {
                Opacity = System.Math.Min(1.0, Opacity + 0.1);
                MakePageTransparent();
            });

            _hotKeyManager.Register(ID_HIDE_SHOW, MOD_NONE, VK_9, () =>
            {
                _isHidden = !_isHidden;
                Visibility = _isHidden ? Visibility.Hidden : Visibility.Visible;
                if (!_isHidden) Activate();
            });

            _hotKeyManager.Register(ID_IMMERSIVE, MOD_NONE, VK_0, () => ToggleImmersive());
            _hotKeyManager.Register(ID_AUTO_SKIP, MOD_NONE, VK_OEM_MINUS, () => _autoSkipService?.Toggle());
            _hotKeyManager.Register(ID_BOOKMARK, MOD_NONE, VK_OEM_PLUS, () => ShowBookmarkOverlay());
        }

        private async void MakePageTransparent()
        {
            var o = (Opacity * 0.9 + 0.1).ToString("F2");
            var b = ((Opacity * 0.9 + 0.1)).ToString("F2");
            var js = $@"
document.documentElement.style.opacity = '{o}';
document.body.style.backgroundColor = 'rgba(255,255,255,{b})';
var s = document.createElement('style');
s.id = '__ys_style';
s.textContent = 'html, #app, .bili-video-page {{ background: transparent !important; }}';
var old = document.getElementById('__ys_style');
if (old) old.remove();
document.head.appendChild(s);
";
            await ExecuteScriptAsync(js);
        }

        private void WebViewGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(WebView);
            var x = (int)pos.X;
            var y = (int)pos.Y;
            var btn = e.ChangedButton == MouseButton.Right ? 2 : 0;
            var js = $@"(function() {{
    var el = document.elementFromPoint({x}, {y});
    if (!el) return;
    ['mousedown','mouseup','click'].forEach(function(t) {{
        el.dispatchEvent(new MouseEvent(t, {{bubbles:true,cancelable:true,clientX:{x},clientY:{y},button:{btn}}}));
    }});
}})()";
            ExecuteScript(js);
        }

        private void WebViewGrid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var delta = e.Delta;
            ExecuteScript($"window.scrollBy(0, {-delta})");
        }

        private void WebViewGrid_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(WebView);
            var x = (int)pos.X;
            var y = (int)pos.Y;
            ExecuteScript($"document.elementFromPoint({x}, {y})?.dispatchEvent(new MouseEvent('mousemove', {{clientX: {x}, clientY: {y}}}));");
        }

        private void ToggleImmersive()
        {
            _immersive = !_immersive;
            var hwnd = new WindowInteropHelper(this).Handle;
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (_immersive)
                exStyle |= WS_EX_TRANSPARENT;
            else
                exStyle &= ~WS_EX_TRANSPARENT;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            Title = _immersive ? "原神跟跑 [沉浸: 开]" : "原神跟跑 [沉浸: 关]";
            LegendText.Text = _immersive
                ? " ` 播放/暂停 | 5 后退 10s | 6 前进 10s | 7 透明度 - | 8 透明度 + | 9 隐藏 | 0 沉浸 开 | - 跳过 | + 书签"
                : " ` 播放/暂停 | 5 后退 10s | 6 前进 10s | 7 透明度 - | 8 透明度 + | 9 隐藏 | 0 沉浸 关 | - 跳过 | + 书签";
        }

        private void ShowBookmarkOverlay()
        {
            WebView.Visibility = Visibility.Collapsed;
            BookmarkOverlay.Visibility = Visibility.Visible;
            LoadBookmarkList();
        }

        private void HideBookmarkOverlay()
        {
            BookmarkOverlay.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
        }

        private void LoadBookmarkList()
        {
            var cfg = Config.Load();
            BookmarkListBox.Items.Clear();
            foreach (var url in cfg.Bookmarks)
                BookmarkListBox.Items.Add(new BookmarkItem { Title = url, Url = url });
        }

        private void BookmarkAdd_Click(object sender, RoutedEventArgs e)
        {
            var url = BookmarkUrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "https://" + url;

            var cfg = Config.Load();
            if (!cfg.Bookmarks.Contains(url))
            {
                cfg.Bookmarks.Add(url);
                Config.Save(cfg);
                BookmarkListBox.Items.Add(new BookmarkItem { Title = url, Url = url });
            }
            BookmarkUrlTextBox.Clear();
        }

        private void BookmarkList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (BookmarkListBox.SelectedItem is BookmarkItem item)
            {
                WebView.CoreWebView2?.Navigate(item.Url);
                UrlTextBox.Text = item.Url;
                HideBookmarkOverlay();
            }
        }

        private void BookmarkDelete_Click(object sender, RoutedEventArgs e)
        {
            if (BookmarkListBox.SelectedItem is BookmarkItem item)
            {
                var cfg = Config.Load();
                cfg.Bookmarks.Remove(item.Url);
                Config.Save(cfg);
                BookmarkListBox.Items.Remove(item);
            }
        }

        private void BookmarkClose_Click(object sender, RoutedEventArgs e)
        {
            HideBookmarkOverlay();
        }

        private class BookmarkItem
        {
            public required string Title { get; set; }
            public required string Url { get; set; }
            public override string ToString() => Title;
        }

        private async void ExecuteScript(string script)
        {
            if (WebView.CoreWebView2 != null)
                await WebView.ExecuteScriptAsync(script);
        }

        private async Task ExecuteScriptAsync(string script)
        {
            if (WebView.CoreWebView2 != null)
                await WebView.ExecuteScriptAsync(script);
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr SetCapture(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_LBUTTON = 0x01;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private bool _isDragging;
        private string? _resizeEdge;
        private bool _isResizing;
        private double _dragStartLeft, _dragStartTop;
        private double _resizeStartLeft, _resizeStartTop, _resizeStartWidth, _resizeStartHeight;
        private System.Windows.Threading.DispatcherTimer? _dragTimer;
        private const double MIN_WIDTH = 200;
        private const double MIN_HEIGHT = 100;

        private void DragBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            _isDragging = true;
            _dragStartLeft = Left;
            _dragStartTop = Top;
            var hwnd = new WindowInteropHelper(this).Handle;
            SetCapture(hwnd);
            GetCursorPos(out var start);
            var startX = start.X; var startY = start.Y;

            _dragTimer = new System.Windows.Threading.DispatcherTimer();
            _dragTimer.Interval = TimeSpan.FromMilliseconds(10);
            _dragTimer.Tick += (s, args) =>
            {
                if (!_isDragging) { _dragTimer?.Stop(); return; }
                if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0)
                {
                    _isDragging = false;
                    ReleaseCapture();
                    _dragTimer?.Stop();
                    return;
                }
                GetCursorPos(out var cur);
                var dx = cur.X - startX;
                var dy = cur.Y - startY;
                SetWindowPos(hwnd, IntPtr.Zero,
                    (int)(_dragStartLeft + dx), (int)(_dragStartTop + dy),
                    0, 0, SWP_NOSIZE | SWP_NOZORDER);
            };
            _dragTimer.Start();
        }

        private void ResizeMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            _resizeEdge = ((FrameworkElement)sender).Name;
            _resizeStartLeft = Left;
            _resizeStartTop = Top;
            _resizeStartWidth = Width;
            _resizeStartHeight = Height;
            _isResizing = true;
            var hwnd = new WindowInteropHelper(this).Handle;
            SetCapture(hwnd);
            GetCursorPos(out var start);
            var startX = start.X; var startY = start.Y;

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(10);
            timer.Tick += (s, args) =>
            {
                if (!_isResizing) { timer?.Stop(); return; }
                if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0)
                {
                    _isResizing = false;
                    ReleaseCapture();
                    timer?.Stop();
                    return;
                }
                GetCursorPos(out var cur);
                UpdateResize(cur.X, cur.Y);
            };
            timer.Start();
        }

        private void UpdateResize(int cursorX, int cursorY)
        {
            double newLeft = Left, newTop = Top, newWidth = Width, newHeight = Height;
            var origRight = _resizeStartLeft + _resizeStartWidth;
            var origBottom = _resizeStartTop + _resizeStartHeight;

            switch (_resizeEdge)
            {
                case "ResizeLeft":
                    newWidth = origRight - cursorX;
                    if (newWidth < MIN_WIDTH) newWidth = MIN_WIDTH;
                    newLeft = origRight - newWidth;
                    break;
                case "ResizeRight":
                    newWidth = cursorX - Left;
                    if (newWidth < MIN_WIDTH) newWidth = MIN_WIDTH;
                    break;
                case "ResizeTop":
                    newHeight = origBottom - cursorY;
                    if (newHeight < MIN_HEIGHT) newHeight = MIN_HEIGHT;
                    newTop = origBottom - newHeight;
                    break;
                case "ResizeBottom":
                    newHeight = cursorY - Top;
                    if (newHeight < MIN_HEIGHT) newHeight = MIN_HEIGHT;
                    break;
                case "ResizeTopLeft":
                    newWidth = origRight - cursorX;
                    if (newWidth < MIN_WIDTH) newWidth = MIN_WIDTH;
                    newLeft = origRight - newWidth;
                    newHeight = origBottom - cursorY;
                    if (newHeight < MIN_HEIGHT) newHeight = MIN_HEIGHT;
                    newTop = origBottom - newHeight;
                    break;
                case "ResizeTopRight":
                    newWidth = cursorX - Left;
                    if (newWidth < MIN_WIDTH) newWidth = MIN_WIDTH;
                    newHeight = origBottom - cursorY;
                    if (newHeight < MIN_HEIGHT) newHeight = MIN_HEIGHT;
                    newTop = origBottom - newHeight;
                    break;
                case "ResizeBottomLeft":
                    newWidth = origRight - cursorX;
                    if (newWidth < MIN_WIDTH) newWidth = MIN_WIDTH;
                    newLeft = origRight - newWidth;
                    newHeight = cursorY - Top;
                    if (newHeight < MIN_HEIGHT) newHeight = MIN_HEIGHT;
                    break;
                case "ResizeBottomRight":
                    newWidth = cursorX - Left;
                    if (newWidth < MIN_WIDTH) newWidth = MIN_WIDTH;
                    newHeight = cursorY - Top;
                    if (newHeight < MIN_HEIGHT) newHeight = MIN_HEIGHT;
                    break;
            }
            SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero,
                (int)newLeft, (int)newTop, (int)newWidth, (int)newHeight,
                SWP_NOZORDER);
        }

        private void LegendGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            _isDragging = true;
            _dragStartLeft = Left;
            _dragStartTop = Top;
            var hwnd = new WindowInteropHelper(this).Handle;
            SetCapture(hwnd);
            GetCursorPos(out var start);
            var startX = start.X; var startY = start.Y;

            _dragTimer = new System.Windows.Threading.DispatcherTimer();
            _dragTimer.Interval = TimeSpan.FromMilliseconds(10);
            _dragTimer.Tick += (s, args) =>
            {
                if (!_isDragging) { _dragTimer?.Stop(); return; }
                if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0)
                {
                    _isDragging = false;
                    ReleaseCapture();
                    _dragTimer?.Stop();
                    return;
                }
                GetCursorPos(out var cur);
                var dx = cur.X - startX;
                var dy = cur.Y - startY;
                SetWindowPos(hwnd, IntPtr.Zero,
                    (int)(_dragStartLeft + dx), (int)(_dragStartTop + dy),
                    0, 0, SWP_NOSIZE | SWP_NOZORDER);
            };
            _dragTimer.Start();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
        private void MinButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaxButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            UpdateMaxButtonContent();
        }

        private void Window_StateChanged(object? sender, EventArgs e) => UpdateMaxButtonContent();

        private void UpdateMaxButtonContent()
        {
            MaxButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        }

        private void GoButton_Click(object sender, RoutedEventArgs e) => NavigateToUrl();
        private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) NavigateToUrl();
        }

        private void NavigateToUrl()
        {
            var url = UrlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "https://" + url;
            WebView.CoreWebView2.Navigate(url);
        }
    }
}
