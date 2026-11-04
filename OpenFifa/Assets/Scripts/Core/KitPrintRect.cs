namespace OpenFifa.Core
{
    /// <summary>
    /// Axis-aligned rectangle in print-atlas pixels (origin bottom-left, y up,
    /// matching texture UV orientation).
    /// </summary>
    public struct KitPrintRect
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public KitPrintRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float XMax => X + Width;
        public float YMax => Y + Height;
        public float CenterX => X + Width * 0.5f;

        /// <summary>True when the point is inside (edges inclusive).</summary>
        public bool Contains(float px, float py)
        {
            return px >= X && px <= XMax && py >= Y && py <= YMax;
        }
    }
}
