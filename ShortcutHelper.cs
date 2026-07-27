using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace YuanShenTools
{
    public static class ShortcutHelper
    {
        private static readonly string ShortcutPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                         "原神（无边框）.lnk");

        private static readonly string[] CommonPaths =
        [
            @"C:\Program Files\Genshin Impact\GenshinImpact.exe",
            @"C:\Program Files (x86)\Genshin Impact\GenshinImpact.exe",
            @"D:\Program Files\Genshin Impact\GenshinImpact.exe",
            @"E:\Program Files\Genshin Impact\GenshinImpact.exe",
        ];

        public static bool ShortcutExists()
        {
            return File.Exists(ShortcutPath);
        }

        public static string? FindGenshinExe()
        {
            foreach (var path in CommonPaths)
            {
                if (File.Exists(path))
                    return path;
            }

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
                }
            }

            return null;
        }

        public static void CreateShortcut(string targetPath)
        {
            if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
                return;

            var shellType = Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8"));
            if (shellType == null) return;

            dynamic shell = Activator.CreateInstance(shellType)!;
            if (shell == null) return;

            try
            {
                var shortcut = shell.CreateShortcut(ShortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.Arguments = "-popupwindow";
                shortcut.Description = "原神（无边框窗口模式）";
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Save();
            }
            finally
            {
                Marshal.ReleaseComObject(shell);
            }
        }

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
