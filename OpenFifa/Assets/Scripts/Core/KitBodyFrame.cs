namespace OpenFifa.Core
{
    /// <summary>
    /// Kit space of a body (see <see cref="KitBodyMapBaker"/>): origin at the neck joint, then re-centred on the
    /// torso axis (<see cref="TorsoCentre"/>), with the measured torso half extents.
    /// </summary>
    public sealed class KitBodyFrame
    {
        public SimVec3 Origin;
        public SimVec3 Right = new SimVec3(1f, 0f, 0f);
        public SimVec3 Up = SimVec3.Up;
        public SimVec3 Forward = new SimVec3(0f, 0f, 1f);

        /// <summary>Torso axis offset (kit X/Z) applied after <see cref="ToKit"/>.</summary>
        public SimVec3 TorsoCentre;
        public float TorsoHalfWidth;
        public float TorsoHalfDepth;

        /// <summary>Kit-space Y of the soles (lowest vertex).</summary>
        public float FloorY;

        /// <summary>Model-space point to kit space about <see cref="Origin"/> (before torso centring).</summary>
        public SimVec3 ToKit(SimVec3 p)
        {
            var d = p - Origin;
            return new SimVec3(SimVec3.Dot(d, Right), SimVec3.Dot(d, Up), SimVec3.Dot(d, Forward));
        }
    }
}
