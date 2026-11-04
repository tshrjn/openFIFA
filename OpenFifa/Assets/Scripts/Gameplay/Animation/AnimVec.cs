using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>Conversions between the engine-free SimVec3 and UnityEngine.Vector3 (same axes).</summary>
    public static class AnimVec
    {
        public static SimVec3 ToSim(this Vector3 v) => new SimVec3(v.x, v.y, v.z);
        public static Vector3 ToUnity(this SimVec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
