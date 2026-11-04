using UnityEngine;
using OpenFifa.Core;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// ScriptableObject wrapper for OffsideConfigData (Law 11 rules tuning).
    /// </summary>
    [CreateAssetMenu(fileName = "OffsideConfig", menuName = "OpenFifa/Config/Offside Config")]
    public class OffsideConfig : ScriptableObject
    {
        [Header("When offside applies")]
        [Tooltip("Apply offside in 11v11 matches.")]
        [SerializeField] private bool _enabledFullSided = true;
        [Tooltip("Apply offside in small-sided matches (4v4 / 5v5 Rush).")]
        [SerializeField] private bool _enabledSmallSided = true;
        [Tooltip("Teams with at least this many players per side count as full-sided.")]
        [SerializeField] private int _fullSidedMinPlayers = 11;

        [Header("Geometry (metres)")]
        [Tooltip("Horizontal radius of head/body/feet (arms excluded) added toward the goal line.")]
        [SerializeField] private float _bodyRadius = 0.25f;
        [SerializeField] private float _ballRadius = 0.11f;
        [Tooltip("Within this distance a player is level, and level is onside.")]
        [SerializeField] private float _levelTolerance = 0.02f;

        [Header("Interfering with an opponent")]
        [Tooltip("An offside-positioned player challenging an opponent within this distance of the ball commits an offence. 0 disables.")]
        [SerializeField] private float _interferenceRadius = 2f;
        [Tooltip("How close an offside-positioned player must get to the opponent on the ball to count as a challenge.")]
        [SerializeField] private float _challengeRadius = 1.2f;

        [Header("Indirect free kick")]
        [Tooltip("Attackers are moved at least this far from the ball for the free kick (9.15 m on a full pitch).")]
        [SerializeField] private float _freeKickClearance = 4f;

        public float ChallengeRadius => _challengeRadius;
        public float FreeKickClearance => _freeKickClearance;

        /// <summary>Runtime defaults when no asset is assigned.</summary>
        public static OffsideConfig CreateDefault()
        {
            var cfg = CreateInstance<OffsideConfig>();
            cfg.name = "OffsideConfig (Runtime Default)";
            return cfg;
        }

        public OffsideConfigData ToData(float halfwayLineX = 0f, bool teamAAttacksPositiveX = true)
        {
            return new OffsideConfigData(
                enabledFullSided: _enabledFullSided,
                enabledSmallSided: _enabledSmallSided,
                fullSidedMinPlayers: _fullSidedMinPlayers,
                bodyRadius: _bodyRadius,
                ballRadius: _ballRadius,
                levelTolerance: _levelTolerance,
                interferenceRadius: _interferenceRadius,
                halfwayLineX: halfwayLineX,
                teamAAttacksPositiveX: teamAAttacksPositiveX);
        }
    }
}
