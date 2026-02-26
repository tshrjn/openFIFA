namespace OpenFifa.Core
{
    /// <summary>
    /// Configuration for the soccer ball 3D model and PBR material.
    /// Defines polygon budget, texture names, shader settings, and collider radius.
    /// </summary>
    public class BallModelConfig
    {
        /// <summary>Maximum triangle count for mobile performance.</summary>
        public int MaxTriangles = 999;

        /// <summary>Albedo texture name (classic black/white pentagon pattern).</summary>
        public string AlbedoTextureName = "SoccerBall_Albedo";

        /// <summary>Normal map name for panel seam detail.</summary>
        public string NormalMapName = "SoccerBall_Normal";

        /// <summary>Roughness/smoothness texture name.</summary>
        public string SmoothnessMapName = "SoccerBall_Smoothness";

        /// <summary>Material smoothness value (slight sheen).</summary>
        public float Smoothness = 0.4f;

        /// <summary>URP Lit shader for PBR rendering.</summary>
        public string ShaderName = "Universal Render Pipeline/Lit";

        /// <summary>Whether the mesh pivot is at the center (required for correct rotation).</summary>
        public bool PivotAtCenter = true;

        /// <summary>SphereCollider radius matching the visual mesh bounds.</summary>
        public float SphereColliderRadius = 0.11f;

        /// <summary>Whether visual rotation is driven by Rigidbody angular velocity.</summary>
        public bool RotationFromAngularVelocity = true;

        /// <summary>Model import path relative to Assets folder.</summary>
        public string ModelPath = "Models/Ball/SoccerBall.fbx";
    }
}
