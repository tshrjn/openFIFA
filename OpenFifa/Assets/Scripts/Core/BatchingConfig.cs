namespace OpenFifa.Core
{
    /// <summary>
    /// Configuration for draw call optimization and batching.
    /// Pure C# — no Unity dependency.
    /// </summary>
    public class BatchingConfig
    {
        /// <summary>Maximum target batch count during gameplay.</summary>
        public int MaxBatchTarget = 99;

        /// <summary>Whether GPU instancing should be enabled on materials.</summary>
        public bool EnableGPUInstancing = true;

        /// <summary>Whether static batching should be enabled for environment objects.</summary>
        public bool EnableStaticBatching = true;

        /// <summary>Whether dynamic batching should be enabled for moving objects.</summary>
        public bool EnableDynamicBatching = true;

        /// <summary>Number of shared team materials (one per team).</summary>
        public int TeamMaterialCount = 2;
    }

    /// <summary>
    /// Configuration for material property usage.
    /// </summary>
    public class MaterialPropertyConfig
    {
        /// <summary>
        /// Whether to use MaterialPropertyBlock for per-instance data.
        /// This avoids breaking GPU instancing when setting colors.
        /// </summary>
        public bool UsePropertyBlock = true;
    }
}
