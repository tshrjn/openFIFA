namespace OpenFifa.Core
{
    /// <summary>
    /// Result of resolving which kits two teams wear in a match.
    /// </summary>
    public class MatchKitSelection
    {
        /// <summary>Kit worn by the home side (null if the team has no kits).</summary>
        public KitDefinitionData HomeKit;

        /// <summary>Kit worn by the away side (null if the team has no kits).</summary>
        public KitDefinitionData AwayKit;

        /// <summary>Variant chosen for the home side.</summary>
        public KitVariant HomeVariant;

        /// <summary>Variant chosen for the away side.</summary>
        public KitVariant AwayVariant;

        /// <summary>True when every away variant clashed and the fallback was used.</summary>
        public bool ClashUnresolved;

        /// <summary>Home goalkeeper kit (null until <see cref="KitLibraryLogic.ResolveGoalkeeperKits"/> ran).</summary>
        public KitDefinitionData HomeKeeperKit;

        /// <summary>Away goalkeeper kit (null until <see cref="KitLibraryLogic.ResolveGoalkeeperKits"/> ran).</summary>
        public KitDefinitionData AwayKeeperKit;
    }
}
