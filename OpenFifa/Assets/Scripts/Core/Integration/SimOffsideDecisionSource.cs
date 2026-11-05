using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Bridges the match sim's offside calls to the offside presentation: every
    /// <see cref="MatchEventType.Offside"/> event fed to <see cref="Process"/> is raised as an
    /// <see cref="OffsideDecision"/> on <see cref="OnOffence"/>, so <c>OffsidePresentation</c> (or anything else
    /// listening to an <see cref="IOffsideDecisionSource"/>) can show the sim's decisions without re-deciding them.
    /// Home maps to <see cref="TeamIdentifier.TeamA"/>, Away to <see cref="TeamIdentifier.TeamB"/>.
    /// Axis note: the sim's pitch length runs along world Z, so the decision's "line X" values
    /// (AttackerLineX, DefenderLineX, BallLineX) carry world Z positions here; FreeKickX/Z are the plain world X/Z
    /// of the offside position. The presentation maps that axis with <see cref="OffsidePitchFrame.ForSimPitch"/>.
    /// </summary>
    public sealed class SimOffsideDecisionSource : IOffsideDecisionSource
    {
        public event Action<OffsideDecision> OnOffence;

        /// <summary>The most recent decision raised (None until the first offside).</summary>
        public OffsideDecision Last { get; private set; } = OffsideDecision.None;

        public static TeamIdentifier ToTeamIdentifier(SimTeam team) =>
            team == SimTeam.Away ? TeamIdentifier.TeamB : TeamIdentifier.TeamA;

        /// <summary>Converts one sim Offside event; returns <see cref="OffsideDecision.None"/> for any other event.</summary>
        public static OffsideDecision ToDecision(in MatchEvent e)
        {
            if (e.Type != MatchEventType.Offside) return OffsideDecision.None;
            // The sim calls offside when the offside-positioned player touches the ball (Law 11.2 "playing the ball").
            return new OffsideDecision(OffsideReason.PlayingBall, ToTeamIdentifier(e.Team), e.PlayerId,
                e.OtherPlayerId, e.Position.X, e.Position.Z,
                e.Position.Z, e.DefenderLineZ, e.BallLineZ);
        }

        /// <summary>Feed every sim event (in order); raises <see cref="OnOffence"/> for offside calls.</summary>
        public void Process(in MatchEvent e)
        {
            if (e.Type != MatchEventType.Offside) return;
            Last = ToDecision(e);
            OnOffence?.Invoke(Last);
        }
    }
}
