using System.Windows.Threading;

namespace YuanShenTools
{
    public sealed class AutoSkipService
    {
        private readonly DispatcherTimer _timer;
        private readonly Func<string, Task> _executeScript;
        private bool _enabled;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                StatusChanged?.Invoke(this, _enabled);
                if (_enabled)
                {
                    _timer.Start();
                }
                else
                {
                    _timer.Stop();
                }
            }
        }

        public event EventHandler<bool>? StatusChanged;

        public AutoSkipService(Func<string, Task> executeScript)
        {
            _executeScript = executeScript;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _timer.Tick += async (_, _) =>
            {
                if (_enabled)
                {
                    await _executeScript(
                        "document.querySelector('.bili-dialog, .bili-popup, .bili-mini-qa')?.remove();" +
                        "document.querySelector('.video-small-dialog')?.remove();" +
                        "document.querySelector('.ad-report')?.remove();"
                    );
                }
            };
        }

        public void Toggle()
        {
            Enabled = !Enabled;
        }

        public void Stop()
        {
            _timer.Stop();
            _enabled = false;
        }
    }
}
