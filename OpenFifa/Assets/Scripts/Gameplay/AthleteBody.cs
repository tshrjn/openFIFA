using OpenFifa.Core;
using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Helpers for the Blender athlete body (see <see cref="AthleteHeadSpace"/>): detection, the kit shader's head
    /// parameters, and the transform that carries mannequin head-bone-space meshes (hair caps) onto the athlete head.
    /// </summary>
    public static class AthleteBody
    {
        /// <summary>
        /// Stride of the locomotion clips on the athlete relative to the UAL2 mannequin (foot fore-aft excursion per
        /// cycle, LocomotionClipMeasure -measureDump, 2026-11-14: walk 1.025, jog 1.049, sprint 1.057). The athlete's
        /// legs are longer (hip to ankle 0.93 m vs 0.83 m) and Humanoid retargeting keeps joint angles.
        /// </summary>
        public const float GaitScale = 1.05f;

        private static readonly int HeadParamsId = Shader.PropertyToID("_HeadParams");

        /// <summary>True when any skinned renderer under the model is an athlete body LOD.</summary>
        public static bool IsAthlete(GameObject model)
        {
            if (model == null) return false;
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null && smr.sharedMesh.name.StartsWith(AthleteHeadSpace.MeshPrefix)) return true;
            return false;
        }

        /// <summary>Kit shader head parameters (centre y, centre z, half width, half depth) for the athlete head.</summary>
        public static Vector4 HeadParams =>
            new Vector4(AthleteHeadSpace.CentreKitY, AthleteHeadSpace.CentreKitZ, AthleteHeadSpace.HalfWidth, AthleteHeadSpace.HalfDepth);

        public static void ApplyHeadParams(Material material)
        {
            if (material != null && material.HasProperty(HeadParamsId)) material.SetVector(HeadParamsId, HeadParams);
        }

        /// <summary>
        /// Local pose for a child of the athlete head bone whose mesh was built in mannequin head-bone space around the
        /// mannequin head centre: maps the mannequin head frame onto the athlete head frame (same kit-space offsets).
        /// </summary>
        public static void MannequinHeadToAthlete(out Vector3 localPosition, out Quaternion localRotation)
        {
            var mFront = new Vector3(0f, MannequinHeadSpace.FrontBoneY, MannequinHeadSpace.FrontBoneZ);
            var mUp = new Vector3(0f, MannequinHeadSpace.UpBoneY, MannequinHeadSpace.UpBoneZ);
            var aFront = new Vector3(0f, AthleteHeadSpace.FrontBoneY, AthleteHeadSpace.FrontBoneZ);
            var aUp = new Vector3(0f, AthleteHeadSpace.UpBoneY, AthleteHeadSpace.UpBoneZ);
            localRotation = Quaternion.LookRotation(aFront, aUp) * Quaternion.Inverse(Quaternion.LookRotation(mFront, mUp));
            var mCentre = new Vector3(MannequinHeadSpace.CentreBoneX, MannequinHeadSpace.CentreBoneY, MannequinHeadSpace.CentreBoneZ);
            var aCentre = new Vector3(AthleteHeadSpace.CentreBoneX, AthleteHeadSpace.CentreBoneY, AthleteHeadSpace.CentreBoneZ);
            localPosition = aCentre - localRotation * mCentre;
        }
    }
}
