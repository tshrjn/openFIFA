// Thermal state and Low Power Mode for FramePacer (OpenFifa.Gameplay).
#import <Foundation/Foundation.h>

extern "C" int OFThermalState(void)
{
    return (int)[[NSProcessInfo processInfo] thermalState];
}

extern "C" int OFLowPowerMode(void)
{
    return [[NSProcessInfo processInfo] isLowPowerModeEnabled] ? 1 : 0;
}
