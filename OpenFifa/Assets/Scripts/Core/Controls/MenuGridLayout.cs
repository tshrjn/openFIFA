using System;

namespace OpenFifa.Core
{
    /// <summary>A rectangle in GUI units (origin top-left, y down).</summary>
    public struct MenuRect
    {
        public float X, Y, Width, Height;

        public MenuRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public float XMax => X + Width;
        public float YMax => Y + Height;
    }

    /// <summary>
    /// Lays a row of equal menu buttons (the seven difficulty levels) into the available width: one centred row with
    /// buttons shrunk down to <c>minWidth</c>, else as many rows as needed (7 = 4 + 3), each row centred. Engine-free so
    /// the iPad fit is unit-tested: a 4:3 iPad is about 961 (12.9") to 1031 (11") GUI units wide at 720-unit height.
    /// </summary>
    public static class MenuGridLayout
    {
        public const float DefaultMaxWidth = 150f;
        public const float DefaultMinWidth = 118f;
        public const float DefaultGap = 10f;
        public const float DefaultMargin = 16f;

        /// <summary>
        /// Fills <paramref name="into"/>[0..count) and returns the number of rows. <paramref name="top"/> is the y of
        /// the first row; the block's height is rows * height + (rows - 1) * gap (see <see cref="BlockHeight"/>).
        /// </summary>
        public static int Layout(int count, float availableWidth, float top, MenuRect[] into,
            float height = 64f, float maxWidth = DefaultMaxWidth, float minWidth = DefaultMinWidth,
            float gap = DefaultGap, float margin = DefaultMargin)
        {
            if (count <= 0) return 0;
            if (into == null || into.Length < count) throw new ArgumentException("into must hold count rects", nameof(into));
            float usable = Math.Max(1f, availableWidth - 2f * margin);

            int rows = 1, perRow = count;
            float bw = 0f;
            for (rows = 1; rows <= count; rows++)
            {
                perRow = (count + rows - 1) / rows;
                bw = Math.Min(maxWidth, (usable - (perRow - 1) * gap) / perRow);
                if (bw >= minWidth) break;
            }
            if (rows > count)
            {
                rows = count;
                perRow = 1;
                bw = Math.Min(maxWidth, usable);
            }
            bw = Math.Max(1f, bw);

            int i = 0;
            for (int r = 0; r < rows && i < count; r++)
            {
                int inRow = Math.Min(perRow, count - i);
                float rowWidth = inRow * bw + (inRow - 1) * gap;
                float x0 = (availableWidth - rowWidth) * 0.5f;
                float y = top + r * (height + gap);
                for (int k = 0; k < inRow; k++, i++)
                    into[i] = new MenuRect(x0 + k * (bw + gap), y, bw, height);
            }
            return rows;
        }

        public static float BlockHeight(int rows, float height = 64f, float gap = DefaultGap) =>
            rows <= 0 ? 0f : rows * height + (rows - 1) * gap;
    }
}
