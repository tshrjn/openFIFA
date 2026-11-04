#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Bridge to Plugins/iOS/OpenFootballExternalDisplay.mm. Every call is a safe no-op off iOS devices.
    /// </summary>
    public static class ExternalDisplayNative
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int OFExternalDisplay_AttachWindow();
        [DllImport("__Internal")] private static extern int OFExternalDisplay_IsAttached();
        [DllImport("__Internal")] private static extern int OFExternalDisplay_HasScene();
        [DllImport("__Internal")] private static extern int OFExternalDisplay_MaxFramesPerSecond();
        [DllImport("__Internal")] private static extern void OFExternalDisplay_NativeSize(out int width, out int height);
        [DllImport("__Internal")] private static extern int OFExternalDisplay_ThermalState();

        public static bool IsSupported => true;
        public static bool AttachWindow() => OFExternalDisplay_AttachWindow() != 0;
        public static bool IsAttached => OFExternalDisplay_IsAttached() != 0;
        public static bool HasExternalScene => OFExternalDisplay_HasScene() != 0;
        public static int MaxFramesPerSecond => OFExternalDisplay_MaxFramesPerSecond();
        public static int ThermalState => OFExternalDisplay_ThermalState();

        public static void NativeSize(out int width, out int height) => OFExternalDisplay_NativeSize(out width, out height);
#else
        public static bool IsSupported => false;
        public static bool AttachWindow() => true;
        public static bool IsAttached => true;
        public static bool HasExternalScene => false;
        public static int MaxFramesPerSecond => 0;
        public static int ThermalState => 0;

        public static void NativeSize(out int width, out int height)
        {
            width = 0;
            height = 0;
        }
#endif
    }
}
