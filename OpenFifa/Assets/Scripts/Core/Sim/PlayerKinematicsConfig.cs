namespace OpenFifa.Core
{
    /// <summary>
    /// Tuning for attribute-driven player movement. APPROXIMATIONS chosen for game feel: elite sprinters in
    /// football top out near 9.5-10 m/s; the numbers below map 1-99 ratings into that range.
    /// </summary>
    public sealed class PlayerKinematicsConfig
    {
        /// <summary>Sprint top speed = SprintSpeedMin + SprintSpeedRange * pace.</summary>
        public float SprintSpeedMin = 6.6f;
        public float SprintSpeedRange = 3.1f;

        /// <summary>Jog (no sprint) top speed as a fraction of sprint speed.</summary>
        public float JogFraction = 0.62f;

        /// <summary>Top speed fraction while dribbling: close control and sprint-dribble.</summary>
        public float DribbleJogFraction = 0.55f;
        public float DribbleSprintFraction = 0.90f;

        /// <summary>Speed caps for jockeying and shielding (m/s).</summary>
        public float JockeySpeed = 3.6f;
        public float ShieldSpeed = 2.6f;

        /// <summary>Peak forward acceleration = AccelMin + AccelRange * acceleration (m/s^2).</summary>
        public float AccelMin = 3.2f;
        public float AccelRange = 4.2f;

        /// <summary>Braking = DecelMin + DecelRange * agility (m/s^2).</summary>
        public float DecelMin = 6.0f;
        public float DecelRange = 4.0f;

        /// <summary>Turn rate at standstill = TurnRateMin + TurnRateRange * agility (rad/s); falls with speed.</summary>
        public float TurnRateMin = 6.0f;
        public float TurnRateRange = 6.0f;

        /// <summary>Speed (m/s) at which turn rate is halved.</summary>
        public float TurnRateHalfSpeed = 3.5f;

        /// <summary>
        /// Max sideways (centripetal) acceleration while running = LateralAccelMin + LateralAccelRange * agility (m/s^2).
        /// Caps the turn rate at speed so a sprinting player has to slow down to change direction.
        /// </summary>
        public float LateralAccelMin = 6.0f;
        public float LateralAccelRange = 5.0f;

        /// <summary>Stamina drain per second at full sprint for a 0-stamina player; scaled down by the attribute.</summary>
        public float SprintDrainPerSecond = 0.012f;

        /// <summary>Stamina recovered per second when moving below jog pace.</summary>
        public float RecoveryPerSecond = 0.004f;

        /// <summary>At zero stamina, top speed and acceleration are multiplied by this.</summary>
        public float ExhaustedFactor = 0.8f;

        public PlayerKinematicsConfig Clone() => (PlayerKinematicsConfig)MemberwiseClone();
    }
}
