namespace OpenFifa.Core
{
    /// <summary>
    /// A gamepad button map. Face buttons get an attack <see cref="FaceRole"/>; defence follows the role
    /// (Shoot button = tackle, Lob button = slide, Pass button held = contain), as in the Classic layout.
    /// Shot modifier results are data so both presets below can be expressed.
    /// </summary>
    public sealed class ControlPreset
    {
        public string Name;
        public GamepadButton PassButton;
        public GamepadButton LobButton;
        public GamepadButton ThroughButton;
        public GamepadButton ShootButton;

        /// <summary>Shot with left shoulder held.</summary>
        public ControlAction ShotWithLeft;
        /// <summary>Shot with right shoulder held.</summary>
        public ControlAction ShotWithRight;
        /// <summary>Shot with both shoulders held.</summary>
        public ControlAction ShotWithBoth;
        /// <summary>Shot with the left trigger pressed during the charge (overrides the shoulders). Knuckleball in both presets.</summary>
        public ControlAction ShotWithLeftTrigger = ControlAction.KnuckleShot;

        /// <summary>
        /// Default. Follows the FC 26 Classic table in the research note (FIFPlay source):
        /// South pass, West lob/cross, North through, East shoot; LB+shoot chip, RB+shoot finesse,
        /// LB+RB+shoot power (driven) shot; shoot twice = low shot. shoot then LT (pressed during the charge) = knuckleball (a combo the reference
        /// layout leaves free: LT is shield/jockey and not a shot modifier there).
        /// </summary>
        public static ControlPreset Classic() => new ControlPreset
        {
            Name = "Classic",
            PassButton = GamepadButton.South,
            LobButton = GamepadButton.West,
            ThroughButton = GamepadButton.North,
            ShootButton = GamepadButton.East,
            ShotWithLeft = ControlAction.ChipShot,
            ShotWithRight = ControlAction.FinesseShot,
            ShotWithBoth = ControlAction.DrivenShot,
        };

        /// <summary>
        /// Alternate: shoot on West and lob/cross on East (the other common console football layout),
        /// with LB+shoot finesse, RB+shoot driven, LB+RB+shoot chip; LT+shoot knuckleball as in Classic.
        /// </summary>
        public static ControlPreset Alternate() => new ControlPreset
        {
            Name = "Alternate",
            PassButton = GamepadButton.South,
            LobButton = GamepadButton.East,
            ThroughButton = GamepadButton.North,
            ShootButton = GamepadButton.West,
            ShotWithLeft = ControlAction.FinesseShot,
            ShotWithRight = ControlAction.DrivenShot,
            ShotWithBoth = ControlAction.ChipShot,
        };

        public FaceRole RoleOf(GamepadButton button)
        {
            if (button == PassButton) return FaceRole.Pass;
            if (button == LobButton) return FaceRole.Lob;
            if (button == ThroughButton) return FaceRole.Through;
            if (button == ShootButton) return FaceRole.Shoot;
            return FaceRole.None;
        }

        public GamepadButton ButtonOf(FaceRole role)
        {
            switch (role)
            {
                case FaceRole.Pass: return PassButton;
                case FaceRole.Lob: return LobButton;
                case FaceRole.Through: return ThroughButton;
                case FaceRole.Shoot: return ShootButton;
                default: return GamepadButton.None;
            }
        }

        /// <summary>
        /// Resolves an attacking face-button press plus shoulder (and, for shots, left-trigger) modifiers into an action.
        /// </summary>
        public ControlAction ResolveAttack(FaceRole role, bool leftShoulder, bool rightShoulder, bool inCrossingZone,
            bool leftTrigger = false)
        {
            bool both = leftShoulder && rightShoulder;
            switch (role)
            {
                case FaceRole.Pass:
                    // RB + pass = driven pass. LB + pass is pass-and-go in the reference layout; reported as a pass.
                    return rightShoulder && !leftShoulder ? ControlAction.DrivenPass : ControlAction.Pass;
                case FaceRole.Through:
                    if (both) return ControlAction.DrivenThrough;
                    if (leftShoulder) return ControlAction.LobbedThrough;
                    return ControlAction.ThroughBall;
                case FaceRole.Lob:
                    if (both) return ControlAction.DrivenLob;
                    return inCrossingZone ? ControlAction.Cross : ControlAction.Lob;
                case FaceRole.Shoot:
                    if (leftTrigger && ShotWithLeftTrigger != ControlAction.None) return ShotWithLeftTrigger;
                    if (both) return ShotWithBoth;
                    if (leftShoulder) return ShotWithLeft;
                    if (rightShoulder) return ShotWithRight;
                    return ControlAction.Shoot;
                default:
                    return ControlAction.None;
            }
        }

        /// <summary>One-shot defensive action for a face-button press (held contain is handled separately).</summary>
        public static ControlAction ResolveDefencePress(FaceRole role)
        {
            switch (role)
            {
                case FaceRole.Shoot: return ControlAction.Tackle;
                case FaceRole.Lob: return ControlAction.SlideTackle;
                default: return ControlAction.None;
            }
        }
    }
}
