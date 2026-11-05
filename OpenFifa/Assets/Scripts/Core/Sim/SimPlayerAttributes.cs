namespace OpenFifa.Core
{
    /// <summary>
    /// Attribute ratings (1-99) the sim reads. Original OpenFifa ratings model, not EA data.
    /// Defaults describe an average professional outfield player.
    /// </summary>
    public sealed class SimPlayerAttributes
    {
        public int Pace = 70;
        public int Acceleration = 70;
        public int Agility = 70;
        public int Balance = 70;
        public int Stamina = 70;
        public int Strength = 70;
        public int Reactions = 70;

        public int Dribbling = 70;
        public int BallControl = 70;
        public int ShortPassing = 70;
        public int LongPassing = 70;
        public int Crossing = 65;
        public int Vision = 65;
        public int Finishing = 65;
        public int ShotPower = 70;
        public int LongShots = 65;
        public int Curve = 65;
        public int Heading = 65;
        public int Composure = 65;

        public int StandingTackle = 60;
        public int SlidingTackle = 55;
        public int Aggression = 60;

        public int GkDiving = 20;
        public int GkHandling = 20;
        public int GkReflexes = 20;
        public int GkPositioning = 20;

        /// <summary>Weak-foot rating, 1-5 stars.</summary>
        public int WeakFoot = 3;

        /// <summary>Skill-move rating, 1-5 stars (gates <see cref="SkillMoveCatalog"/> moves).</summary>
        public int SkillMoves = 2;

        /// <summary>Body mass in kg; 0 derives it from height and strength (<see cref="BodyMass"/>).</summary>
        public float Weight;

        /// <summary>Mass used for body contact: <see cref="Weight"/>, or a height/strength estimate (about 60-95 kg).</summary>
        public float BodyMass => Weight > 0f ? Weight : 22f * Height * Height * (0.92f + 0.16f * N(Strength));

        public SimFoot PreferredFoot = SimFoot.Right;

        /// <summary>Height in metres (drives the acceleration-type derivation and aerial reach).</summary>
        public float Height = 1.80f;

        public bool IsGoalkeeper;

        /// <summary>When set, overrides <see cref="DeriveAccelerationType"/>.</summary>
        public SimAccelerationType? AccelerationTypeOverride;

        public SimAccelerationType AccelerationType => AccelerationTypeOverride ?? DeriveAccelerationType();

        /// <summary>
        /// FC 26-style derivation (approximate, from public descriptions): short, agile players with high
        /// acceleration are Explosive; tall, strong players are Lengthy; everyone else is Controlled.
        /// </summary>
        public SimAccelerationType DeriveAccelerationType()
        {
            if (Acceleration >= 80 && Agility - Strength >= 20 && Height <= 1.82f) return SimAccelerationType.Explosive;
            if (Acceleration >= 55 && Strength - Agility >= 4 && Height >= 1.83f) return SimAccelerationType.Lengthy;
            return SimAccelerationType.Controlled;
        }

        public SimPlayerAttributes Clone() => (SimPlayerAttributes)MemberwiseClone();

        public static SimPlayerAttributes DefaultGoalkeeper() => new SimPlayerAttributes
        {
            IsGoalkeeper = true,
            Pace = 50, Acceleration = 55, Agility = 60, Dribbling = 30, ShortPassing = 55, LongPassing = 60,
            Finishing = 20, StandingTackle = 20, SlidingTackle = 20, Height = 1.90f, Strength = 70,
            GkDiving = 78, GkHandling = 76, GkReflexes = 80, GkPositioning = 76,
        };

        /// <summary>Normalises a 1-99 rating to 0..1.</summary>
        public static float N(int rating) => SimMath.Clamp01(rating / 99f);
    }
}
