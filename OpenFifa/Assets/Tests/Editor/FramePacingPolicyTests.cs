using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    public class FramePacingPolicyTests
    {
        private static FramePacingInputs Ipad(int hz = 60, FrameRatePreference pref = FrameRatePreference.Auto,
            DeviceThermalLevel thermal = DeviceThermalLevel.Nominal, bool lowPower = false, int over = -1) =>
            new FramePacingInputs { Mobile = true, DisplayRefreshHz = hz, Preference = pref, Thermal = thermal, LowPowerMode = lowPower, Override = over };

        [Test] public void Ipad_DefaultsTo60_NotUnitysIos30() => Assert.AreEqual(60, FramePacingPolicy.Choose(Ipad()));
        [Test] public void ProMotion_Auto_Stays60() => Assert.AreEqual(60, FramePacingPolicy.Choose(Ipad(120)));
        [Test] public void ProMotion_Max_Runs120WhenCool() => Assert.AreEqual(120, FramePacingPolicy.Choose(Ipad(120, FrameRatePreference.Max)));
        [Test] public void ProMotion_Max_FairStill120() => Assert.AreEqual(120, FramePacingPolicy.Choose(Ipad(120, FrameRatePreference.Max, DeviceThermalLevel.Fair)));
        [Test] public void ProMotion_Max_SeriousDropsTo60() => Assert.AreEqual(60, FramePacingPolicy.Choose(Ipad(120, FrameRatePreference.Max, DeviceThermalLevel.Serious)));
        [Test] public void ProMotion_Max_LowPowerDropsTo60() => Assert.AreEqual(60, FramePacingPolicy.Choose(Ipad(120, FrameRatePreference.Max, lowPower: true)));
        [Test] public void Critical_FallsBackTo30() => Assert.AreEqual(30, FramePacingPolicy.Choose(Ipad(120, FrameRatePreference.Max, DeviceThermalLevel.Critical)));
        [Test] public void UnknownRefresh_Uses60() => Assert.AreEqual(60, FramePacingPolicy.Choose(Ipad(0)));
        [Test] public void DisplayCappedBelow60_UsesDisplayRate() => Assert.AreEqual(50, FramePacingPolicy.Choose(Ipad(50)));
        [Test] public void Override_IsHonoured() => Assert.AreEqual(30, FramePacingPolicy.Choose(Ipad(over: 30)));
        [Test] public void Override_Above60_CappedWhenHot() => Assert.AreEqual(60, FramePacingPolicy.Choose(Ipad(120, over: 120, thermal: DeviceThermalLevel.Serious)));
        [Test] public void Desktop_LeavesPacingToVSync() => Assert.AreEqual(-1, FramePacingPolicy.Choose(new FramePacingInputs { Mobile = false, DisplayRefreshHz = 120 }));
    }
}
