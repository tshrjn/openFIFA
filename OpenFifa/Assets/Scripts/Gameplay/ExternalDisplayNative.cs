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
        [DllImport("__Internal")] private static extern void OFExternalDisplay_AttachWindow();
        [DllImport("__Internal")] private static extern int OFExternalDisplay_HasScene();
        [DllImport("__Internal")] private static extern int OFExternalDisplay_MaxFramesPerSecond();
        [DllImport("__Internal")] private static extern void OFExternalDisplay_NativeSize(out int width, out int height);

        public static void AttachWindow() => OFExternalDisplay_AttachWindow();
        public static bool HasExternalScene => OFExternalDisplay_HasScene() != 0;
        public static int MaxFramesPerSecond => OFExternalDisplay_MaxFramesPerSecond();

        public static void NativeSize(out int width, out int height) => OFExternalDisplay_NativeSize(out width, out height);
#else
        public static void AttachWindow() { }
        public static bool HasExternalScene => false;
        public static int MaxFramesPerSecond => 0;

        public static void NativeSize(out int width, out int height)
        {
            width = 0;
            height = 0;
        }
#endif
    }
}
