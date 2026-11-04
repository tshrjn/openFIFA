namespace OpenFifa.Core
{
    /// <summary>Presentation-side action families that need contact-synchronised animation.</summary>
    public enum AnimActionKind
    {
        None = 0,
        Pass = 1,
        Shot = 2,
        Cross = 3,
        Header = 4,
        StandingTackle = 5,
        SlideTackle = 6,
        GoalkeeperDive = 7,
        FirstTouch = 8,
        Fall = 9,
        Celebrate = 10,
        GoalkeeperThrow = 11,
        /// <summary>
        /// Running dribble touch: no clip and no wind-up stop. Played as strike-foot IK toward the ball at the contact
        /// tick, layered on locomotion (the player keeps running). See <see cref="DribbleTouchSolver"/>.
        /// </summary>
        DribbleTouch = 12,
    }

    /// <summary>Which foot (or hand side, for dives) performs the action.</summary>
    public enum ActionSide
    {
        Right = 0,
        Left = 1,
    }

    /// <summary>Animator state and parameter names shared by the runtime driver and the editor clip/controller builder.</summary>
    public static class AnimStateNames
    {
        public const string Locomotion = "Locomotion";
        public const string Pass = "Pass";
        public const string Shot = "Shot";
        public const string Cross = "Cross";
        public const string Header = "Header";
        public const string StandingTackle = "TackleStand";
        public const string SlideTackle = "TackleSlide";
        public const string GoalkeeperDive = "GkDive";
        public const string FirstTouch = "FirstTouch";
        public const string Fall = "Fall";
        public const string GetUp = "GetUp";
        public const string Celebrate = "Celebrate";
        public const string GoalkeeperThrow = "GkThrow";

        // Parameters
        public const string ParamSpeed = "Speed";
        public const string ParamForward = "Forward";
        public const string ParamStrafe = "Strafe";
        public const string ParamTurnRate = "TurnRate";
        public const string ParamLocoRate = "LocoRate";
        public const string ParamActionRate = "ActionRate";
        public const string ParamMirror = "Mirror";

        /// <summary>Animation event function name fired on the contact frame of action clips.</summary>
        public const string ContactEvent = "OnActionContact";

        public static string StateFor(AnimActionKind kind)
        {
            switch (kind)
            {
                case AnimActionKind.Pass: return Pass;
                case AnimActionKind.Shot: return Shot;
                case AnimActionKind.Cross: return Cross;
                case AnimActionKind.Header: return Header;
                case AnimActionKind.StandingTackle: return StandingTackle;
                case AnimActionKind.SlideTackle: return SlideTackle;
                case AnimActionKind.GoalkeeperDive: return GoalkeeperDive;
                case AnimActionKind.FirstTouch: return FirstTouch;
                case AnimActionKind.Fall: return Fall;
                case AnimActionKind.Celebrate: return Celebrate;
                case AnimActionKind.GoalkeeperThrow: return GoalkeeperThrow;
                case AnimActionKind.DribbleTouch: return Locomotion; // layered on locomotion, no state of its own
                default: return Locomotion;
            }
        }
    }
}
