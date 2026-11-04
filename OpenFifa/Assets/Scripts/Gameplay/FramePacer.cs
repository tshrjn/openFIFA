using System.Runtime.InteropServices;
using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Sets the frame-rate target at startup and keeps it in line with the device's thermal state.
    /// Without this, iOS runs Unity at 30 fps. Other systems that need a specific rate (for example the
    /// external display) call <see cref="SetOverride"/> and <see cref="ClearOverride"/> instead of writing
    /// Application.targetFrameRate directly, so the thermal back-off still applies to them.
    /// </summary>
    public sealed class FramePacer : MonoBehaviour
    {
        private const float PollSeconds = 2f;

        private static FramePacer _instance;
        private static int _override = -1;
        private static FrameRatePreference _preference = FrameRatePreference.Auto;

        private float _nextPoll;
        private int _applied = int.MinValue;

        /// <summary>The target the pacer last applied (-1 = vSync-paced desktop).</summary>
        public static int CurrentTarget => _instance != null ? _instance._applied : Application.targetFrameRate;

        public static FrameRatePreference Preference
        {
            get => _preference;
            set { _preference = value; if (_instance != null) _instance.Apply(); }
        }

        /// <summary>Request a specific rate (still lowered when the device runs hot). Pass -1 or call ClearOverride to release.</summary>
        public static void SetOverride(int fps)
        {
            _override = fps;
            if (_instance != null) _instance.Apply();
        }

        public static void ClearOverride() => SetOverride(-1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[FramePacer]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FramePacer>();
            _instance.Apply();
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + PollSeconds;
            Apply();
        }

        private void Apply()
        {
            var inputs = new FramePacingInputs
            {
                Mobile = Application.isMobilePlatform,
                DisplayRefreshHz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value),
                Preference = _preference,
                Thermal = (DeviceThermalLevel)Native.ThermalState(),
                LowPowerMode = Native.LowPowerMode(),
                Override = _override,
            };
            int target = FramePacingPolicy.Choose(inputs);
            if (target == _applied) return;
            _applied = target;
            if (target > 0) Application.targetFrameRate = target;
        }

        private static class Native
        {
#if UNITY_IOS && !UNITY_EDITOR
            [DllImport("__Internal")] private static extern int OFThermalState();
            [DllImport("__Internal")] private static extern int OFLowPowerMode();
            public static int ThermalState() => OFThermalState();
            public static bool LowPowerMode() => OFLowPowerMode() != 0;
#else
            public static int ThermalState() => 0;
            public static bool LowPowerMode() => false;
#endif
        }
    }
}
