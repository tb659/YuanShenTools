using System.Windows;
using System.Windows.Input;

namespace YuanShenTools
{
    public partial class MainWindow : Window
    {
        private HotKeyManager? _hotKeyManager;
        private AutoSkipService? _autoSkipService;
        private BookmarkWindow? _bookmarkWindow;
        private bool _isHidden;

        private const uint VK_5 = 0x35;
        private const uint VK_6 = 0x36;
        private const uint VK_7 = 0x37;
        private const uint VK_8 = 0x38;
        private const uint VK_9 = 0x39;
        private const uint VK_0 = 0x30;
        private const uint VK_OEM_3 = 0xC0;
        private const uint VK_OEM_MINUS = 0xBD;
        private const uint MOD_NONE = 0x0000;

        private const int ID_PLAY_PAUSE = 1;
        private const int ID_FORWARD = 2;
        private const int ID_REWIND = 3;
        private const int ID_OPACITY_DOWN = 4;
        private const int ID_OPACITY_UP = 5;
        private const int ID_HIDE_SHOW = 6;
        private const int ID_BOOKMARK = 7;
        private const int ID_AUTO_SKIP = 8;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await WebView.EnsureCoreWebView2Async();
            WebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            var cfg = Config.Load();
            Config.ApplyWindow(this, cfg);
            Opacity = cfg.Opacity;
            var lastUrl = cfg.LastUrl ?? "https://www.bilibili.com";
            UrlTextBox.Text = lastUrl;
            WebView.CoreWebView2.Navigate(lastUrl);

            WebView.CoreWebView2.NavigationCompleted += (_, _) => MakePageTransparent();

            _autoSkipService = new AutoSkipService(ExecuteScriptAsync);
            _autoSkipService.StatusChanged += (_, enabled) =>
            {
                Title = enabled ? "原神跟跑 [自动跳过: ON]" : "原神跟跑 [自动跳过: OFF]";
            };

            RegisterHotkeys();
            ShortcutHelper.EnsureShortcut();
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _autoSkipService?.Stop();
            _hotKeyManager?.Dispose();
            _bookmarkWindow?.Close();
            var cfg = Config.FromWindow(this, UrlTextBox.Text, Opacity);
            Config.Save(cfg);
        }

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

            _hotKeyManager.Register(ID_BOOKMARK, MOD_NONE, VK_0, () => ShowBookmarkWindow());
            _hotKeyManager.Register(ID_AUTO_SKIP, MOD_NONE, VK_OEM_MINUS, () => _autoSkipService?.Toggle());
        }

        private async void MakePageTransparent()
        {
            var o = (Opacity * 0.9 + 0.1).ToString("F2");
            var js = $@"
document.documentElement.style.opacity = '{o}';
document.body.style.backgroundColor = 'transparent';
var s = document.createElement('style');
s.id = '__ys_style';
s.textContent = 'html, body, #app, .bili-video-page {{ background: transparent !important; }}';
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

        private void ShowBookmarkWindow()
        {
            if (_bookmarkWindow != null && _bookmarkWindow.IsVisible)
            {
                _bookmarkWindow.Activate();
                return;
            }
            _bookmarkWindow = new BookmarkWindow { Owner = this };
            _bookmarkWindow.BookmarkSelected += url =>
            {
                WebView.CoreWebView2.Navigate(url);
                UrlTextBox.Text = url;
            };
            _bookmarkWindow.Show();
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

        private void DragBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
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
