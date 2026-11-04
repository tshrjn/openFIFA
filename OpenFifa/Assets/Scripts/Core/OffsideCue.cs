namespace OpenFifa.Core
{
    /// <summary>
    /// Presentation beats of an offside call, in the order they normally fire.
    /// </summary>
    public enum OffsideCue
    {
        Whistle = 0,
        FlagRaise = 1,
        SlowMotionStart = 2,
        BannerShow = 3,
        FreezeStart = 4,
        CameraCutIn = 5,
        LinesShow = 6,
        LinesHide = 7,
        CameraCutOut = 8,
        TimeRestore = 9,
        BannerHide = 10,
        Complete = 11
    }
}
