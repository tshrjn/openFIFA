namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Where the offside lower-third is drawn. <see cref="OffsideBanner"/> builds its own overlay canvas (4v4 scene);
    /// the single-player match draws it on its uGUI HUD canvas instead (SinglePlayerOffsideBanner).
    /// All motion must use unscaled time: the banner plays while the game is in slow motion or frozen.
    /// </summary>
    public interface IOffsideBannerView
    {
        bool IsShowing { get; }
        void Show(OffsideBannerInfo info);
        void Hide();
        void HideImmediate();
    }
}
