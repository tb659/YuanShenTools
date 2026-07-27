using System.IO;
using System.Text.Json;
using System.Windows;

namespace YuanShenTools
{
    public class WindowConfig
    {
        public double Left { get; set; } = double.NaN;
        public double Top { get; set; } = double.NaN;
        public double Width { get; set; } = 480;
        public double Height { get; set; } = 360;
        public double Opacity { get; set; } = 0.5;
        public string LastUrl { get; set; } = "https://www.bilibili.com";
        public int WindowState { get; set; } = 0; // 0=Normal, 1=Minimized, 2=Maximized
        public List<string> Bookmarks { get; set; } = [];
    }

    public static class Config
    {
        private static readonly string ConfigPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "YuanShenTools", "config.json");

        private static WindowConfig? _cached;

        public static WindowConfig Load()
        {
            if (_cached != null) return _cached;

            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    _cached = JsonSerializer.Deserialize<WindowConfig>(json) ?? new WindowConfig();
                    return _cached;
                }
            }
            catch
            {
            }

            _cached = new WindowConfig();
            return _cached;
        }

        public static void Save(WindowConfig config)
        {
            _cached = config;
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
            }
        }

        public static void ApplyWindow(Window window, WindowConfig config)
        {
            if (!double.IsNaN(config.Left) && !double.IsNaN(config.Top))
            {
                window.Left = config.Left;
                window.Top = config.Top;
            }
            window.Width = config.Width;
            window.Height = config.Height;
            if (config.WindowState == 2)
                window.WindowState = System.Windows.WindowState.Maximized;
        }

        public static WindowConfig FromWindow(Window window, string lastUrl, double dwmAlpha)
        {
            var bounds = window.RestoreBounds;
            return new WindowConfig
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                Opacity = dwmAlpha,
                LastUrl = lastUrl,
                WindowState = (int)window.WindowState,
            };
        }
    }
}
