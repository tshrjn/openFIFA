using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Discrete action the controlled player should perform. One-shot actions (passes, shots, tackles,
    /// switches, skill moves) appear on the single tick they fire. Held modes (jockey, shield, contain,
    /// teammate press) are also reported as the action on ticks with no one-shot, and always in
    /// <see cref="ControlIntent.Held"/>.
    /// </summary>
    public enum ControlAction
    {
        None = 0,

        // Attack: passing
        Pass,
        DrivenPass,
        ThroughBall,
        LobbedThrough,
        DrivenThrough,
        Lob,
        Cross,
        DrivenLob,

        // Attack: shooting
        Shoot,
        FinesseShot,
        DrivenShot,
        LowShot,
        ChipShot,
        /// <summary>Knuckleball: struck with almost no spin so it wobbles and dips (<see cref="KickStyle.Knuckle"/>).</summary>
        KnuckleShot,

        // Attack: dribbling
        SkillMove,
        Shield,

        // Defence
        Tackle,
        SlideTackle,
        Jockey,
        Contain,
        TeammatePress,
        SwitchPlayer,
    }

    /// <summary>Modes that stay active while a control is held.</summary>
    [Flags]
    public enum ControlHeld
    {
        None = 0,
        Sprint = 1 << 0,
        Shield = 1 << 1,
        Jockey = 1 << 2,
        Contain = 1 << 3,
        TeammatePress = 1 << 4,
    }

    /// <summary>Team instructions issued from the d-pad (generic names).</summary>
    public enum TacticCommand
    {
        None = 0,
        OffsideTrap,
        TeamPress,
        ExtraStriker,
        GetInTheBox,
        MentalityBalanced,
        MentalityDefensive,
        MentalityAttacking,
        Substitution,
    }

    /// <summary>Eight-way direction in stick space (+Y is up/forward on screen).</summary>
    public enum Direction8
    {
        None = 0,
        Up,
        UpRight,
        Right,
        DownRight,
        Down,
        DownLeft,
        Left,
        UpLeft,
    }

    /// <summary>Platform-neutral gamepad controls (South = A / Cross, East = B / Circle, ...).</summary>
    [Flags]
    public enum GamepadButton
    {
        None = 0,
        South = 1 << 0,
        East = 1 << 1,
        West = 1 << 2,
        North = 1 << 3,
        LeftShoulder = 1 << 4,
        RightShoulder = 1 << 5,
        LeftTrigger = 1 << 6,
        RightTrigger = 1 << 7,
        DpadUp = 1 << 8,
        DpadDown = 1 << 9,
        DpadLeft = 1 << 10,
        DpadRight = 1 << 11,
        LeftStickPress = 1 << 12,
        RightStickPress = 1 << 13,
        Start = 1 << 14,
        Select = 1 << 15,
    }

    /// <summary>What a face button does in attack. Defence derives from it (see <see cref="ControlPreset"/>).</summary>
    public enum FaceRole
    {
        None = 0,
        Pass,
        Lob,
        Through,
        Shoot,
    }

    /// <summary>The four context buttons of the touch scheme.</summary>
    public enum TouchSlot
    {
        None = 0,
        /// <summary>Attack: Pass. Defence: Switch.</summary>
        Primary,
        /// <summary>Attack: Shoot. Defence: Tackle.</summary>
        Strike,
        /// <summary>Attack: Through. Defence: Press.</summary>
        Through,
        /// <summary>Sprint in both phases.</summary>
        Sprint,
    }

    /// <summary>Gesture made on a touch button between press and release.</summary>
    public enum TouchGesture
    {
        Tap = 0,
        SwipeUp,
        SwipeDown,
        SwipeLeft,
        SwipeRight,
    }

    public enum ControlTouchPhase
    {
        Began,
        Moved,
        Stationary,
        Ended,
        Canceled,
    }

    /// <summary>Game state the control layer needs each tick. Filled in by the glue code from the sim.</summary>
    public struct ControlContext
    {
        /// <summary>Seconds since match start (deterministic sim clock).</summary>
        public float Time;
        /// <summary>True when the user's team has the ball (attack); false on defence.</summary>
        public bool InPossession;
        /// <summary>True on the tick the controlled player can strike the ball (ball at feet / contact frame).</summary>
        public bool CanStrike;
        /// <summary>True when the controlled player is in a wide crossing zone: lob becomes cross.</summary>
        public bool InCrossingZone;

        public static ControlContext Attack(float time, bool canStrike = true, bool crossingZone = false)
            => new ControlContext { Time = time, InPossession = true, CanStrike = canStrike, InCrossingZone = crossingZone };

        public static ControlContext Defence(float time)
            => new ControlContext { Time = time, InPossession = false, CanStrike = false };
    }

    /// <summary>
    /// Output of the control layer for one tick. Directions are in stick / screen space (+Y up the screen);
    /// the glue code rotates them into pitch space using the camera. The coordinator maps this to the sim's intent.
    /// </summary>
    public struct ControlIntent
    {
        /// <summary>Movement, magnitude 0..1 after dead zone.</summary>
        public ControlVec2 Move;
        public bool Sprint;
        public ControlAction Action;
        /// <summary>Kick power 0..1 for pass/shot actions (0 for other actions).</summary>
        public float Power;
        /// <summary>Aim direction for kicks, or the flick / swipe direction for a directional switch. Zero = no aim.</summary>
        public ControlVec2 Aim;
        public ControlHeld Held;
        /// <summary>Direction of a skill-move flick.</summary>
        public Direction8 SkillDirection;
        /// <summary>True when <see cref="ControlAction.SwitchPlayer"/> should pick by <see cref="Aim"/> rather than smart choice.</summary>
        public bool DirectionalSwitch;
        public TacticCommand Tactic;
        /// <summary>Current charge 0..1 of a held kick button, for the HUD power bar (0 when not charging).</summary>
        public float Charge;
        /// <summary>True while a released kick is waiting for the contact frame.</summary>
        public bool KickBuffered;

        public bool IsOneShot => IsOneShotAction(Action);

        public static bool IsOneShotAction(ControlAction a)
        {
            switch (a)
            {
                case ControlAction.None:
                case ControlAction.Shield:
                case ControlAction.Jockey:
                case ControlAction.Contain:
                case ControlAction.TeammatePress:
                    return false;
                default:
                    return true;
            }
        }

        public static bool IsKickAction(ControlAction a) => a >= ControlAction.Pass && a <= ControlAction.KnuckleShot;

        public static bool IsShotAction(ControlAction a) => a >= ControlAction.Shoot && a <= ControlAction.KnuckleShot;
    }

    /// <summary>Tunable feel settings shared by gamepad and touch.</summary>
    public sealed class ControlSettings
    {
        /// <summary>0 = manual aim, 1 = fully assisted.</summary>
        public float AssistLevel = 0.6f;
        public float StickDeadZone = 0.15f;
        /// <summary>Hold time for a full pass power bar.</summary>
        public float PassFullChargeSeconds = 0.8f;
        /// <summary>Hold time for a full shot power bar.</summary>
        public float ShotFullChargeSeconds = 1.0f;
        /// <summary>Power produced by an instant tap.</summary>
        public float MinPower = 0.1f;
        /// <summary>How long a released kick waits for the contact frame before it is dropped.</summary>
        public float BufferWindowSeconds = 0.5f;
        /// <summary>Second shoot press within this window of the first release turns the shot into a low shot.</summary>
        public float DoubleTapSeconds = 0.25f;
        /// <summary>Right-stick magnitude that counts as a flick (from a rest below <see cref="FlickRest"/>).</summary>
        public float FlickThreshold = 0.75f;
        public float FlickRest = 0.3f;
        /// <summary>Time allowed between the two d-pad presses of a tactic.</summary>
        public float TacticSequenceSeconds = 1.0f;
        /// <summary>Touch: sprint when the floating stick is pushed to the rim.</summary>
        public bool AutoSprintFromStick = false;
        public float AutoSprintThreshold = 0.95f;
    }
}
