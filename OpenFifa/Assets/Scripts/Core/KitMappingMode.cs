namespace OpenFifa.Core
{
    /// <summary>
    /// How OpenFifaKit.shader maps the kit onto a mesh. Values match the shader's _MappingMode.
    /// </summary>
    public enum KitMappingMode
    {
        /// <summary>
        /// Object-space cylindrical mapping around local Y with shirt/shorts/socks bands.
        /// For the capsule placeholder players and any upright proxy body.
        /// </summary>
        ObjectSpace = 0,

        /// <summary>Mesh UV mapping for a skinned jersey mesh; print panels are UV rects.</summary>
        MeshUV = 1
    }
}
