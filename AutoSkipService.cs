using System.Windows.Threading;

namespace YuanShenTools
{
    /// <summary>
    /// 自动跳过对话服务
    /// 定时（每 2 秒）向 WebView 注入 JS，移除 B 站视频中的弹窗、问答、广告等遮挡元素，
    /// 使游戏画面不被页面 UI 遮挡。
    /// </summary>
    public sealed class AutoSkipService
    {
        private readonly DispatcherTimer _timer;
        private readonly Func<string, Task> _executeScript;  // 执行 JS 的委托
        private bool _enabled;

        /// <summary>当前是否启用自动跳过</summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                StatusChanged?.Invoke(this, _enabled);
                if (_enabled)
                    _timer.Start();
                else
                    _timer.Stop();
            }
        }

        /// <summary>启用状态变化通知（用于更新 UI）</summary>
        public event EventHandler<bool>? StatusChanged;

        public AutoSkipService(Func<string, Task> executeScript)
        {
            _executeScript = executeScript;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)  // 每 2 秒扫描一次
            };
            _timer.Tick += async (_, _) =>
            {
                if (_enabled)
                {
                    // 移除 B 站常见弹窗/问答/广告容器
                    await _executeScript(
                        "document.querySelector('.bili-dialog, .bili-popup, .bili-mini-qa')?.remove();" +
                        "document.querySelector('.video-small-dialog')?.remove();" +
                        "document.querySelector('.ad-report')?.remove();"
                    );
                }
            };
        }

        /// <summary>切换开关状态</summary>
        public void Toggle()
        {
            Enabled = !Enabled;
        }

        /// <summary>停止定时器</summary>
        public void Stop()
        {
            _timer.Stop();
            _enabled = false;
        }
    }
}
