using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace YuanShenTools
{
    /// <summary>
    /// 原神无边框快捷方式工具
    /// 在桌面创建"原神（无边框）.lnk"，启动参数添加 -popupwindow，
    /// 使游戏以无边框窗口模式运行，方便悬浮窗覆盖。
    /// </summary>
    public static class ShortcutHelper
    {
        // 桌面快捷方式路径
        private static readonly string ShortcutPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                         "原神（无边框）.lnk");

        // 常见原神安装路径
        private static readonly string[] CommonPaths =
        [
            @"C:\Program Files\Genshin Impact\GenshinImpact.exe",
            @"C:\Program Files (x86)\Genshin Impact\GenshinImpact.exe",
            @"D:\Program Files\Genshin Impact\GenshinImpact.exe",
            @"E:\Program Files\Genshin Impact\GenshinImpact.exe",
        ];

        /// <summary>检查桌面快捷方式是否已存在</summary>
        public static bool ShortcutExists()
        {
            return File.Exists(ShortcutPath);
        }

        /// <summary>搜索原神可执行文件路径</summary>
        public static string? FindGenshinExe()
        {
            // 先在常见路径查找
            foreach (var path in CommonPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            // 全盘搜索（仅遍历就绪的驱动器）
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => d.RootDirectory.FullName);

            foreach (var drive in drives)
            {
                try
                {
                    var found = Directory.EnumerateFiles(drive, "GenshinImpact.exe", SearchOption.AllDirectories)
                        .FirstOrDefault();
                    if (found != null) return found;
                }
                catch
                {
                    // 无权限访问的目录跳过
                }
            }

            return null;
        }

        /// <summary>创建快捷方式（通过 COM Windows Script Host Shell）</summary>
        public static void CreateShortcut(string targetPath)
        {
            if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
                return;

            // CLSID: Windows Script Host Shell Object
            var shellType = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8"));
            if (shellType == null) return;

            dynamic shell = Activator.CreateInstance(shellType)!;
            if (shell == null) return;

            try
            {
                var shortcut = shell.CreateShortcut(ShortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.Arguments = "-popupwindow";      // 无边框窗口参数
                shortcut.Description = "原神（无边框窗口模式）";
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Save();
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }
        }

        /// <summary>确保快捷方式存在：不存在则自动搜索并创建</summary>
        public static void EnsureShortcut()
        {
            if (ShortcutExists()) return;

            var exePath = FindGenshinExe();
            if (exePath != null)
            {
                CreateShortcut(exePath);
            }
        }
    }
}
