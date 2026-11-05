using System;

namespace OpenFifa.Core
{
    /// <summary>A render target size in pixels.</summary>
    public readonly struct PixelSize : IEquatable<PixelSize>
    {
        public readonly int Width;
        public readonly int Height;

        public PixelSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public float Aspect => Height > 0 ? (float)Width / Height : 0f;

        public bool Equals(PixelSize other) => Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is PixelSize other && Equals(other);
        public override int GetHashCode() => (Width * 397) ^ Height;
        public override string ToString() => $"{Width}x{Height}";
    }
}
