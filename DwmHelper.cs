using System.Runtime.InteropServices;

namespace YuanShenTools
{
    internal static class DwmHelper
    {
        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
        }

        private enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int nState;
            public int nFlags;
            public int nColor;
            public int nAnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttribData
        {
            public WindowCompositionAttribute Attrib;
            public IntPtr pvData;
            public int cbData;
        }

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

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
