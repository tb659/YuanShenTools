using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace YuanShenTools
{
    /// <summary>
    /// 主窗口 — 原神 B 站视频跟跑工具的透明悬浮覆盖层
    /// 支持：WebView2 透明浏览、全局热键、窗口拖拽/缩放、沉浸模式、书签、自动跳过对话
    /// </summary>
    public partial class MainWindow : Window
    {
        // —— 核心服务 ——
        private HotKeyManager? _hotKeyManager;       // 全局热键管理器
        private AutoSkipService? _autoSkipService;    // 自动跳过对话服务

        // —— 状态标志 ——
        private bool _isHidden;       // 窗口是否隐藏（热键 9 切换）
        private bool _immersive;      // 沉浸模式：鼠标穿透到游戏
        private string _currentUrl = "https://www.bilibili.com"; // WebView 当前 URL

        // —— 虚拟键码（RegisterHotKey 使用） ——
        private const uint VK_5 = 0x35;
        private const uint VK_6 = 0x36;
        private const uint VK_7 = 0x37;
        private const uint VK_8 = 0x38;
        private const uint VK_9 = 0x39;
        private const uint VK_0 = 0x30;
        private const uint VK_OEM_3 = 0xC0;      // ` 键
        private const uint VK_OEM_MINUS = 0xBD;   // - 键
        private const uint VK_OEM_PLUS = 0xBB;    // =/+ 键
        private const uint MOD_NONE = 0x0000;     // 无修饰键（Alt/Ctrl/Shift）

        // —— 热键 ID（用于区分不同热键） ——
        private const int ID_PLAY_PAUSE = 1;
        private const int ID_FORWARD = 2;
        private const int ID_REWIND = 3;
        private const int ID_OPACITY_DOWN = 4;
        private const int ID_OPACITY_UP = 5;
        private const int ID_HIDE_SHOW = 6;
        private const int ID_IMMERSIVE = 7;
        private const int ID_AUTO_SKIP = 8;
        private const int ID_BOOKMARK = 9;

        // —— 窗口扩展样式常量（用于沉浸模式 WS_EX_TRANSPARENT） ——
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;

        public MainWindow()
        {
            InitializeComponent();
        }

        // =====================================================================
        //  窗口生命周期
        // =====================================================================

        /// <summary>窗口加载完成：初始化 WebView、恢复配置、注册热键</summary>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始化 WebView2 并使其背景透明
            await WebView.EnsureCoreWebView2Async();
            WebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            // 添加 WS_CAPTION 窗口标题栏样式（允许系统识别标题栏区域）
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var style = GetWindowLong(hwnd, GWL_STYLE);
            style |= WS_CAPTION;
            SetWindowLong(hwnd, GWL_STYLE, style);
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

            // 从配置文件恢复窗口位置、大小、透明度、URL、书签
            var cfg = Config.Load();
            Config.ApplyWindow(this, cfg);
            Opacity = cfg.Opacity;
            var lastUrl = cfg.LastUrl ?? "https://www.bilibili.com";
            UrlTextBox.Text = lastUrl;
            WebView.CoreWebView2.Navigate(lastUrl);

            // 每次页面加载完成后执行透明化脚本并记录当前 URL
            WebView.CoreWebView2.NavigationCompleted += (_, _) =>
            {
                MakePageTransparent();
                _currentUrl = WebView.CoreWebView2.Source;
                UrlTextBox.Text = _currentUrl;
            };

            // 初始化自动跳过对话服务
            _autoSkipService = new AutoSkipService(ExecuteScriptAsync);
            _autoSkipService.StatusChanged += (_, enabled) =>
            {
                Title = enabled ? "原神跟跑 [对话: 开]" : "原神跟跑 [对话: 关]";
            };

            RegisterHotkeys();
            ShortcutHelper.EnsureShortcut(); // 检查并创建原神无边框快捷方式
        }

        /// <summary>窗口关闭：保存配置、释放资源</summary>
        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _autoSkipService?.Stop();
            _hotKeyManager?.Dispose();
            var cfg = Config.FromWindow(this, _currentUrl, Opacity);
            Config.Save(cfg);
        }

        // =====================================================================
        //  Win32 P/Invoke（窗口样式操作）
        // =====================================================================

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        // —— 窗口样式常量 ——
        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020;

        // =====================================================================
        //  全局热键注册
        // =====================================================================

        /// <summary>注册所有全局热键（无需焦点即可响应）</summary>
        private void RegisterHotkeys()
        {
            _hotKeyManager = new HotKeyManager(this);

            // ` 播放/暂停
            _hotKeyManager.Register(ID_PLAY_PAUSE, MOD_NONE, VK_OEM_3, () =>
                ExecuteScript("document.querySelector('video')?.paused ? document.querySelector('video')?.play() : document.querySelector('video')?.pause()"));

            // 5 快退 10 秒
            _hotKeyManager.Register(ID_REWIND, MOD_NONE, VK_5, () =>
                ExecuteScript("var v = document.querySelector('video'); if(v) v.currentTime -= 10;"));

            // 6 快进 10 秒
            _hotKeyManager.Register(ID_FORWARD, MOD_NONE, VK_6, () =>
                ExecuteScript("var v = document.querySelector('video'); if(v) v.currentTime += 10;"));

            // 7 降低窗口透明度
            _hotKeyManager.Register(ID_OPACITY_DOWN, MOD_NONE, VK_7, () =>
            {
                Opacity = System.Math.Max(0.1, Opacity - 0.1);
                MakePageTransparent();
            });

            // 8 提高窗口透明度
            _hotKeyManager.Register(ID_OPACITY_UP, MOD_NONE, VK_8, () =>
            {
                Opacity = System.Math.Min(1.0, Opacity + 0.1);
                MakePageTransparent();
            });

            // 9 隐藏/显示窗口
            _hotKeyManager.Register(ID_HIDE_SHOW, MOD_NONE, VK_9, () =>
            {
                _isHidden = !_isHidden;
                Visibility = _isHidden ? Visibility.Hidden : Visibility.Visible;
                if (!_isHidden) Activate();
            });

            // 0 沉浸模式开关（鼠标穿透/正常）
            _hotKeyManager.Register(ID_IMMERSIVE, MOD_NONE, VK_0, () => ToggleImmersive());

            // - 自动跳过对话开关
            _hotKeyManager.Register(ID_AUTO_SKIP, MOD_NONE, VK_OEM_MINUS, () => _autoSkipService?.Toggle());

            // =/+ 打开书签面板
            _hotKeyManager.Register(ID_BOOKMARK, MOD_NONE, VK_OEM_PLUS, () => ShowBookmarkOverlay());
        }

        // =====================================================================
        //  页面透明化（注入 CSS + JS 使 B 站页面半透明）
        // =====================================================================

        /// <summary>通过 JavaScript 设置页面透明度和白色背景，使游戏画面可透过网页显示</summary>
        private async void MakePageTransparent()
        {
            // o: 页面元素透明度，随窗口透明度变化（0.1~1.0 范围）
            var o = (Opacity * 0.9 + 0.1).ToString("F2");
            // b: body 白色背景 alpha，也随透明度变化
            var b = (Opacity * 0.9 + 0.1).ToString("F2");
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

        // =====================================================================
        //  鼠标事件转发（WebView2 是子 HWND，需将 WPF 捕获的鼠标事件转发给网页 JS）
        // =====================================================================

        /// <summary>鼠标点击转发：通过 JS dispatchEvent 模拟点击到下方网页元素</summary>
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

        /// <summary>鼠标滚轮转发：映射到页面 scrollBy</summary>
        private void WebViewGrid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var delta = e.Delta;
            ExecuteScript($"window.scrollBy(0, {-delta})");
        }

        /// <summary>鼠标移动转发：使页面能响应 hover 效果</summary>
        private void WebViewGrid_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(WebView);
            var x = (int)pos.X;
            var y = (int)pos.Y;
            ExecuteScript($"document.elementFromPoint({x}, {y})?.dispatchEvent(new MouseEvent('mousemove', {{clientX: {x}, clientY: {y}}}));");
        }

        // =====================================================================
        //  沉浸模式（WS_EX_TRANSPARENT 使鼠标穿透到下层游戏窗口）
        // =====================================================================

        /// <summary>切换沉浸模式开关：开启后鼠标点击穿过悬浮窗直接操作游戏</summary>
        private void ToggleImmersive()
        {
            _immersive = !_immersive;
            var hwnd = new WindowInteropHelper(this).Handle;
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if (_immersive)
                exStyle |= WS_EX_TRANSPARENT;   // 添加穿透样式
            else
                exStyle &= ~WS_EX_TRANSPARENT;  // 移除穿透样式
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            Title = _immersive ? "原神跟跑 [沉浸: 开]" : "原神跟跑 [沉浸: 关]";
            LegendText.Text = _immersive
                ? " ` 播放/暂停 | 5 后退 10s | 6 前进 10s | 7 透明度 - | 8 透明度 + | 9 隐藏 | 0 沉浸 开 | - 跳过 | + 书签"
                : " ` 播放/暂停 | 5 后退 10s | 6 前进 10s | 7 透明度 - | 8 透明度 + | 9 隐藏 | 0 沉浸 关 | - 跳过 | + 书签";
        }

        // =====================================================================
        //  书签面板（窗口内覆盖层，隐藏 WebView 后显示）
        // =====================================================================

        /// <summary>显示书签面板：临时隐藏 WebView，显示书签覆盖层</summary>
        private void ShowBookmarkOverlay()
        {
            WebView.Visibility = Visibility.Collapsed;
            BookmarkOverlay.Visibility = Visibility.Visible;
            LoadBookmarkList();
        }

        /// <summary>隐藏书签面板：恢复 WebView 显示</summary>
        private void HideBookmarkOverlay()
        {
            BookmarkOverlay.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Visible;
        }

        /// <summary>从配置文件加载书签列表到 ListBox</summary>
        private void LoadBookmarkList()
        {
            var cfg = Config.Load();
            BookmarkListBox.Items.Clear();
            foreach (var url in cfg.Bookmarks)
                BookmarkListBox.Items.Add(new BookmarkItem { Title = url, Url = url });
        }

        /// <summary>添加书签：将输入框 URL 保存到配置文件并刷新列表</summary>
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

        /// <summary>双击书签项：导航到该 URL 并关闭面板</summary>
        private void BookmarkList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (BookmarkListBox.SelectedItem is BookmarkItem item)
            {
                WebView.CoreWebView2?.Navigate(item.Url);
                UrlTextBox.Text = item.Url;
                HideBookmarkOverlay();
            }
        }

        /// <summary>删除选中书签</summary>
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

        /// <summary>关闭书签面板</summary>
        private void BookmarkClose_Click(object sender, RoutedEventArgs e)
        {
            HideBookmarkOverlay();
        }

        /// <summary>书签列表项数据模型</summary>
        private class BookmarkItem
        {
            public required string Title { get; set; }
            public required string Url { get; set; }
            public override string ToString() => Title;
        }

        // =====================================================================
        //  JS 脚本执行工具方法
        // =====================================================================

        /// <summary>Fire-and-forget 执行 JS（不等待结果）</summary>
        private async void ExecuteScript(string script)
        {
            if (WebView.CoreWebView2 != null)
                await WebView.ExecuteScriptAsync(script);
        }

        /// <summary>async 执行 JS（可等待完成）</summary>
        private async Task ExecuteScriptAsync(string script)
        {
            if (WebView.CoreWebView2 != null)
                await WebView.ExecuteScriptAsync(script);
        }

        // =====================================================================
        //  Win32 P/Invoke（鼠标捕获、光标位置）
        // =====================================================================

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr SetCapture(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private const int VK_LBUTTON = 0x01;  // 鼠标左键虚拟键码

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        // —— 拖拽/缩放状态变量 ——
        private bool _isDragging;
        private string? _resizeEdge;              // 当前缩放的边/角名称
        private bool _isResizing;
        private double _dragStartLeft, _dragStartTop;
        private double _resizeStartLeft, _resizeStartTop, _resizeStartWidth, _resizeStartHeight;
        private System.Windows.Threading.DispatcherTimer? _dragTimer;
        private const double MIN_WIDTH = 200;     // 窗口最小宽度
        private const double MIN_HEIGHT = 100;    // 窗口最小高度

        // =====================================================================
        //  手动拖拽（替代 DragMove，因为 AllowsTransparency 下层叠窗口不支持 DragMove）
        // =====================================================================

        /// <summary>拖拽抓手鼠标按下：启动定时轮询实现窗口拖动</summary>
        private void DragBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            _isDragging = true;
            _dragStartLeft = Left;
            _dragStartTop = Top;
            var hwnd = new WindowInteropHelper(this).Handle;
            SetCapture(hwnd);               // 捕获鼠标（即使移出窗口也能跟踪）
            GetCursorPos(out var start);
            var startX = start.X; var startY = start.Y;

            // 10ms 定时器轮询鼠标位置并更新窗口位置
            _dragTimer = new System.Windows.Threading.DispatcherTimer();
            _dragTimer.Interval = TimeSpan.FromMilliseconds(10);
            _dragTimer.Tick += (s, args) =>
            {
                if (!_isDragging) { _dragTimer?.Stop(); return; }
                if ((GetAsyncKeyState(VK_LBUTTON) & 0x8000) == 0)  // 左键已释放
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

        // =====================================================================
        //  手动缩放（同样因为分层窗口不支持 WM_NCLBUTTONDOWN）
        // =====================================================================

        /// <summary>缩放边/角鼠标按下：启动定时轮询实现窗口缩放</summary>
        private void ResizeMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            _resizeEdge = ((FrameworkElement)sender).Name;  // 通过元素 Name 判断缩放方向
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

        /// <summary>根据鼠标当前位置和缩放方向计算新的窗口位置和大小</summary>
        private void UpdateResize(int cursorX, int cursorY)
        {
            double newLeft = Left, newTop = Top, newWidth = Width, newHeight = Height;
            var origRight = _resizeStartLeft + _resizeStartWidth;
            var origBottom = _resizeStartTop + _resizeStartHeight;

            switch (_resizeEdge)
            {
                case "ResizeLeft":       // 左边缘：改变 left + width
                    newWidth = origRight - cursorX;
                    if (newWidth < MIN_WIDTH) newWidth = MIN_WIDTH;
                    newLeft = origRight - newWidth;
                    break;
                case "ResizeRight":      // 右边缘：只改变 width
                    newWidth = cursorX - Left;
                    if (newWidth < MIN_WIDTH) newWidth = MIN_WIDTH;
                    break;
                case "ResizeTop":        // 上边缘：改变 top + height
                    newHeight = origBottom - cursorY;
                    if (newHeight < MIN_HEIGHT) newHeight = MIN_HEIGHT;
                    newTop = origBottom - newHeight;
                    break;
                case "ResizeBottom":     // 下边缘：只改变 height
                    newHeight = cursorY - Top;
                    if (newHeight < MIN_HEIGHT) newHeight = MIN_HEIGHT;
                    break;
                case "ResizeTopLeft":    // 左上角
                    newWidth = origRight - cursorX;
                    if (newWidth < MIN_WIDTH) newWidth = MIN_WIDTH;
                    newLeft = origRight - newWidth;
                    newHeight = origBottom - cursorY;
                    if (newHeight < MIN_HEIGHT) newHeight = MIN_HEIGHT;
                    newTop = origBottom - newHeight;
                    break;
                case "ResizeTopRight":   // 右上角
                    newWidth = cursorX - Left;
                    if (newWidth < MIN_WIDTH) newWidth = MIN_WIDTH;
                    newHeight = origBottom - cursorY;
                    if (newHeight < MIN_HEIGHT) newHeight = MIN_HEIGHT;
                    newTop = origBottom - newHeight;
                    break;
                case "ResizeBottomLeft": // 左下角
                    newWidth = origRight - cursorX;
                    if (newWidth < MIN_WIDTH) newWidth = MIN_WIDTH;
                    newLeft = origRight - newWidth;
                    newHeight = cursorY - Top;
                    if (newHeight < MIN_HEIGHT) newHeight = MIN_HEIGHT;
                    break;
                case "ResizeBottomRight": // 右下角
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

        // =====================================================================
        //  拖拽热键栏（功能说明整行可拖拽）
        // =====================================================================

        /// <summary>热键提示栏鼠标按下：同样使用定时轮询拖拽</summary>
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

        // =====================================================================
        //  窗口控制按钮（关闭、最小化、最大化）
        // =====================================================================

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
        private void MinButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaxButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            UpdateMaxButtonContent();
        }

        private void Window_StateChanged(object? sender, EventArgs e) => UpdateMaxButtonContent();

        /// <summary>更新最大化按钮图标：□ 表示恢复，❐ 表示最大化</summary>
        private void UpdateMaxButtonContent()
        {
            MaxButton.Content = WindowState == WindowState.Maximized ? "❐" : "□";
        }

        // =====================================================================
        //  URL 导航
        // =====================================================================

        private void GoButton_Click(object sender, RoutedEventArgs e) => NavigateToUrl();
        private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) NavigateToUrl();
        }

        /// <summary>导航到 URL 输入框中的地址（自动补全 https://）</summary>
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
