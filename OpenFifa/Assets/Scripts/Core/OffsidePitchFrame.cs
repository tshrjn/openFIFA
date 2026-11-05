using System;

namespace OpenFifa.Core
{
    /// <summary>
    /// Maps the offside presentation's pitch coordinates onto world X/Z. "Depth" is a position along the pitch
    /// length (an <see cref="OffsideDecision"/>'s AttackerLineX / DefenderLineX / BallLineX); "across" is a position
    /// along the pitch width. Offside lines run across the full width at a fixed depth, the camera cut sits beyond the
    /// near touchline at the lines' depth looking across the pitch, and the assistant referee stands on a touchline
    /// level with the line.
    /// <see cref="NearSide"/> is the sign (along the width axis) of the touchline the camera works from: -1 for the
    /// 4v4 scene (camera on the -Z side), +1 for the 11v11 match (broadcast camera beyond the +X touchline).
    /// Pure C#: EditMode testable.
    /// </summary>
    public readonly struct OffsidePitchFrame
    {
        /// <summary>Default for the camera cut's look target: this fraction of the half width past the centre line.</summary>
        public const float LookAcrossFraction = 0.25f;

        public PitchLengthAxis LengthAxis { get; }
        public float HalfWidth { get; }
        /// <summary>+1 or -1: the touchline (along the width axis) the camera works from.</summary>
        public int NearSide { get; }

        public OffsidePitchFrame(PitchLengthAxis lengthAxis, float halfWidth, int nearSide)
        {
            LengthAxis = lengthAxis;
            HalfWidth = Math.Abs(halfWidth);
            NearSide = nearSide >= 0 ? 1 : -1;
        }

        /// <summary>4v4 scene (MatchOrchestrator): length along X, camera on the -Z touchline. The original behaviour.</summary>
        public static OffsidePitchFrame LengthAlongX(float halfWidth) =>
            new OffsidePitchFrame(PitchLengthAxis.X, halfWidth, -1);

        /// <summary>11v11 match sim: length along Z, camera beyond the +X touchline (see SinglePlayerCamera).</summary>
        public static OffsidePitchFrame LengthAlongZ(float halfWidth, int nearSide = 1) =>
            new OffsidePitchFrame(PitchLengthAxis.Z, halfWidth, nearSide);

        /// <summary>Frame for the match sim's pitch (length along Z), camera on the +X side.</summary>
        public static OffsidePitchFrame ForSimPitch(PitchSpec pitch) =>
            LengthAlongZ(pitch != null ? pitch.HalfWidth : PitchSpec.Standard.HalfWidth);

        /// <summary>World X/Z of a point given its depth along the length and its position across the width.</summary>
        public void ToWorld(float depth, float across, out float x, out float z)
        {
            if (LengthAxis == PitchLengthAxis.X)
            {
                x = depth;
                z = across;
            }
            else
            {
                x = across;
                z = depth;
            }
        }

        /// <summary>Depth (along the length) of a world X/Z point.</summary>
        public float DepthOf(float x, float z) => LengthAxis == PitchLengthAxis.X ? x : z;

        /// <summary>
        /// An offside line at <paramref name="depth"/>: it starts on the near touchline and, at
        /// <paramref name="reveal"/> = 1, ends on the far touchline (0 = zero length at the near touchline).
        /// </summary>
        public void LineEndpoints(float depth, float reveal, out float startX, out float startZ, out float endX, out float endZ)
        {
            if (reveal < 0f) reveal = 0f;
            if (reveal > 1f) reveal = 1f;
            float start = NearSide * HalfWidth;
            float end = start + (-2f * start) * reveal;
            ToWorld(depth, start, out startX, out startZ);
            ToWorld(depth, end, out endX, out endZ);
        }

        /// <summary>
        /// High side-on camera for the offside lines: <paramref name="sideOffset"/> beyond the near touchline,
        /// <paramref name="height"/> up, at the lines' <paramref name="depth"/>, aimed across the pitch along the lines
        /// so both read as parallel lines running up the frame.
        /// </summary>
        public void CameraPose(float depth, float height, float sideOffset,
            out float posX, out float posY, out float posZ, out float lookX, out float lookY, out float lookZ)
        {
            ToWorld(depth, NearSide * (HalfWidth + sideOffset), out posX, out posZ);
            posY = height;
            ToWorld(depth, -NearSide * HalfWidth * LookAcrossFraction, out lookX, out lookZ);
            lookY = 0f;
        }

        /// <summary>Where the assistant referee stands, level with <paramref name="depth"/>, outside a touchline.</summary>
        public void AssistantPosition(float depth, float sideOffset, bool farSide, out float x, out float z)
        {
            int side = farSide ? -NearSide : NearSide;
            ToWorld(depth, side * (HalfWidth + sideOffset), out x, out z);
        }

        /// <summary>Yaw (degrees, Unity convention: 0 = +Z, 90 = +X) for the assistant referee to face the pitch.</summary>
        public float AssistantFacingYaw(bool farSide)
        {
            int side = farSide ? -NearSide : NearSide;
            ToWorld(0f, -side, out float fx, out float fz);
            return (float)(Math.Atan2(fx, fz) * 180.0 / Math.PI);
        }
    }
}
