namespace OpenFifa.Core
{
    /// <summary>
    /// Rest-pose joint positions of a humanoid in model space (metres), for <see cref="KitBodyMapBaker"/>.
    /// Arm chains: shoulder (upper arm joint), elbow, wrist, fingertips. Leg chains: hip, knee, ankle, toe tip.
    /// </summary>
    public sealed class KitBodySkeleton
    {
        public SimVec3 Hips;
        public SimVec3 Neck;
        public SimVec3[] ArmLeft = new SimVec3[4];
        public SimVec3[] ArmRight = new SimVec3[4];
        public SimVec3[] LegLeft = new SimVec3[4];
        public SimVec3[] LegRight = new SimVec3[4];
    }
}
