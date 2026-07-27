using System.IO;
using System.Text.Json;
using System.Windows;

namespace YuanShenTools
{
    /// <summary>
    /// 窗口配置数据模型（序列化为 JSON 持久化）
    /// </summary>
    public class WindowConfig
    {
        public double Left { get; set; } = double.NaN;    // 窗口 X 坐标（NaN 表示首次启动，居中显示）
        public double Top { get; set; } = double.NaN;     // 窗口 Y 坐标
        public double Width { get; set; } = 480;
        public double Height { get; set; } = 360;
        public double Opacity { get; set; } = 0.5;        // 窗口不透明度
        public string LastUrl { get; set; } = "https://www.bilibili.com";
        public int WindowState { get; set; } = 0;         // 0=Normal, 1=Minimized, 2=Maximized
        public List<string> Bookmarks { get; set; } = []; // 书签 URL 列表
    }

    /// <summary>
    /// 配置读写工具：JSON 文件存储到 %LOCALAPPDATA%/YuanShenTools/config.json
    /// </summary>
    public static class Config
    {
        // 配置文件路径：C:\Users\<用户名>\AppData\Local\YuanShenTools\config.json
        private static readonly string ConfigPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "YuanShenTools", "config.json");

        // 内存缓存，避免重复读盘
        private static WindowConfig? _cached;

        /// <summary>从 JSON 文件加载配置，失败则返回默认值</summary>
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
                // 文件损坏等情况：忽略，使用默认配置
            }

            _cached = new WindowConfig();
            return _cached;
        }

        /// <summary>将配置保存到 JSON 文件</summary>
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
                // 写入失败（权限、磁盘满等）：静默忽略
            }
        }

        /// <summary>将配置应用到 WPF 窗口（位置、大小、最大化状态）</summary>
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

        /// <summary>从当前窗口状态创建配置快照（使用 RestoreBounds 确保最大化时位置准确）</summary>
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
