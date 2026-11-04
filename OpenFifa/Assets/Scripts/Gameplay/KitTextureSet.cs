using UnityEngine;

namespace OpenFifa.Gameplay
{
    /// <summary>
    /// Resolved licensed textures for a kit (any may be null).
    /// </summary>
    public struct KitTextureSet
    {
        public Texture2D Albedo;
        public Texture2D Normal;
        public Texture2D Mask;
        public Texture2D Crest;
        public Texture2D Sponsor;

        public bool HasAlbedo => Albedo != null;
    }
}
