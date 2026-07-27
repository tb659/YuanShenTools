using System.Runtime.InteropServices;

namespace YuanShenTools
{
    /// <summary>
    /// DWM（桌面窗口管理器）透明效果辅助类
    /// 通过 SetWindowCompositionAttribute 设置窗口亚克力/模糊/透明渐变效果。
    /// 当前未在主窗口中使用（因为 DWM 效果在 Windows 10/11 上存在兼容性问题），
    /// 保留以备后续实验。
    /// </summary>
    internal static class DwmHelper
    {
        /// <summary>亚克力效果类型枚举</summary>
        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,              // 纯色渐变
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,    // 透明渐变
            ACCENT_ENABLE_BLURBEHIND = 3,             // 模糊背景
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,      // 亚克力效果
        }

        private enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19,
        }

        /// <summary>亚克力策略参数</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int nState;       // AccentState
            public int nFlags;
            public int nColor;       // ABGR 格式颜色
            public int nAnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttribData
        {
            public WindowCompositionAttribute Attrib;
            public IntPtr pvData;    // 指向 AccentPolicy 的指针
            public int cbData;       // AccentPolicy 结构体大小
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

        /// <summary>为窗口启用透明渐变效果（实验性，可能在某些系统上无效）</summary>
        public static void SetTransparent(IntPtr hwnd)
        {
            var accent = new AccentPolicy
            {
                nState = (int)AccentState.ACCENT_ENABLE_TRANSPARENTGRADIENT,
                nFlags = 0,
                nColor = 0x00000000,
                nAnimationId = 0,
            };

            var data = new WindowCompositionAttribData
            {
                Attrib = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                pvData = Marshal.AllocHGlobal(Marshal.SizeOf(accent)),
                cbData = Marshal.SizeOf(accent),
            };

            try
            {
                Marshal.StructureToPtr(accent, data.pvData, false);
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(data.pvData);
            }
        }
    }
}
