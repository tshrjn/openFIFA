namespace OpenFifa.Core
{
    /// <summary>
    /// Why an offside-positioned player was penalised.
    /// </summary>
    public enum OffsideReason
    {
        None = 0,
        /// <summary>Played or touched the ball (interfering with play).</summary>
        PlayingBall = 1,
        /// <summary>Challenged an opponent near the ball (interfering with an opponent).</summary>
        ChallengingOpponent = 2
    }
}
