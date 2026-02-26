namespace OpenFifa.Core
{
    /// <summary>
    /// Pure C# resolution and UI scaling configuration.
    /// Supports macOS window sizes and iPad resolutions.
    /// </summary>
    public class ResolutionConfig
    {
        /// <summary>Minimum macOS window width.</summary>
        public int MinWindowWidth = 1280;

        /// <summary>Minimum macOS window height.</summary>
        public int MinWindowHeight = 720;

        /// <summary>Minimum interactive UI element size in points.</summary>
        public int MinUITargetPoints = 44;

        /// <summary>CanvasScaler matchWidthOrHeight for balanced scaling.</summary>
        public float MatchWidthOrHeight = 0.5f;

        /// <summary>Reference resolution width.</summary>
        public int ReferenceWidth = 1920;

        /// <summary>Reference resolution height.</summary>
        public int ReferenceHeight = 1080;

        /// <summary>
        /// Check if a resolution is supported (meets minimum requirements).
        /// </summary>
        public bool IsResolutionSupported(int width, int height)
        {
            // Both dimensions must meet minimum
            int smallerDim = width < height ? width : height;
            return smallerDim >= 720;
        }
    }

    /// <summary>
    /// Pure C# safe area anchor calculation.
    /// </summary>
    public class SafeAreaLogic
    {
        /// <summary>
        /// Calculate RectTransform anchors from safe area and screen size.
        /// </summary>
        public void CalculateAnchors(
            float safeX, float safeY, float safeWidth, float safeHeight,
            float screenWidth, float screenHeight,
            out float anchorMinX, out float anchorMinY,
            out float anchorMaxX, out float anchorMaxY)
        {
            anchorMinX = safeX / screenWidth;
            anchorMinY = safeY / screenHeight;
            anchorMaxX = (safeX + safeWidth) / screenWidth;
            anchorMaxY = (safeY + safeHeight) / screenHeight;

            // Clamp
            if (anchorMinX < 0f) anchorMinX = 0f;
            if (anchorMinY < 0f) anchorMinY = 0f;
            if (anchorMaxX > 1f) anchorMaxX = 1f;
            if (anchorMaxY > 1f) anchorMaxY = 1f;
        }
    }
}
