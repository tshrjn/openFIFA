namespace OpenFifa.Core
{
    /// <summary>A normalized camera viewport rect (0..1), used for pillarbox / letterbox.</summary>
    public readonly struct ViewportRect
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;

        public ViewportRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public bool IsFull => X <= 0f && Y <= 0f && Width >= 1f && Height >= 1f;

        public static ViewportRect Full => new ViewportRect(0f, 0f, 1f, 1f);
    }
}
