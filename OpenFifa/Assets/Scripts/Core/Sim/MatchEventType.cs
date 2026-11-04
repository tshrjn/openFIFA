namespace OpenFifa.Core
{
    /// <summary>Typed events in the per-tick stream. See <see cref="MatchEvent"/> for the meaning of Value/Detail.</summary>
    public enum MatchEventType
    {
        KickOff = 0,
        /// <summary>A kick or tackle started its wind-up. Detail = contact tick (for animation sync), Action set.</summary>
        KickWindup,
        /// <summary>A pass left the foot. Other = intended receiver (-1 none). Value = launch speed.</summary>
        Pass,
        /// <summary>A pass reached a teammate. Player = receiver, Other = passer.</summary>
        PassCompleted,
        /// <summary>An opponent won a pass. Player = interceptor, Other = passer.</summary>
        Interception,
        /// <summary>Shot taken. Value = xG estimate (0..1), Speed = launch speed.</summary>
        Shot,
        /// <summary>Goalkeeper save. Player = keeper, Other = shooter, Detail = 1 caught, 0 parried.</summary>
        Save,
        /// <summary>Goal. Player = scorer (last touch), Other = assist (-1 none), Team = scoring team, Detail = 1 own goal.</summary>
        Goal,
        PostHit,
        CrossbarHit,
        /// <summary>Dribble touch at a contact frame. Value = touch quality 0..1 (1 = tight).</summary>
        DribbleTouch,
        /// <summary>First touch on receiving. Value = quality 0..1.</summary>
        FirstTouch,
        /// <summary>Team in possession changed. Player = new owner.</summary>
        PossessionChange,
        /// <summary>Tackle resolved. Player = tackler, Other = carrier, Detail = 1 won ball, 0 missed; Action = Tackle/SlideTackle.</summary>
        Tackle,
        /// <summary>Foul. Player = offender, Other = fouled player, Detail = card (0 none, 1 yellow, 2 red).</summary>
        Foul,
        YellowCard,
        RedCard,
        /// <summary>Ball left the pitch. Team = team that last touched it. Position = exit point.</summary>
        OutOfPlay,
        /// <summary>Restart awarded (ThrowIn/GoalKick/Corner/FreeKick/Penalty events). Team = restarting team.</summary>
        ThrowIn,
        GoalKick,
        Corner,
        FreeKick,
        Penalty,
        /// <summary>Offside called. Player = offside player, Other = passer, Position = player position at the pass, Value = margin (m),
        /// DefenderLineZ / BallLineZ = the second-last defender and ball lines at the pass (world Z).</summary>
        Offside,
        /// <summary>A restart was taken (ball back in play). Detail = (int)RestartType.</summary>
        RestartTaken,
        /// <summary>The kick contact frame found no ball within reach.</summary>
        Whiff,
        HalfTime,
        FullTime,
        /// <summary>The loose ball bounced off a player's body. Player = that player, Other = previous toucher, Speed = ball speed after.</summary>
        Deflection,
    }
}
