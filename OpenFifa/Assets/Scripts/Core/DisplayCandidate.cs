namespace OpenFifa.Core
{
    /// <summary>A monitor the desktop build could run on: its name and native pixel size.</summary>
    public readonly struct DisplayCandidate
    {
        public readonly string Name;
        public readonly int Width;
        public readonly int Height;

        public DisplayCandidate(string name, int width, int height)
        {
            Name = name ?? string.Empty;
            Width = width;
            Height = height;
        }

        public long PixelCount => (long)Width * Height;
    }
}
