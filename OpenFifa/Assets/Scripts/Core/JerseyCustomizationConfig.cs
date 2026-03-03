using System.Collections.Generic;

namespace OpenFifa.Core
{
    /// <summary>
    /// Jersey pattern types for team kit customization.
    /// Maps to shader _PatternType int parameter.
    /// </summary>
    public enum JerseyPattern
    {
        Solid = 0,
        Stripes = 1,
        Hoops = 2,
        HalfAndHalf = 3,
        Gradient = 4,
        Pinstripes = 5,
        Chevron = 6
    }

    /// <summary>
    /// Collar style options for jersey design.
    /// Maps to shader _CollarStyle int parameter.
    /// </summary>
    public enum CollarStyle
    {
        VNeck = 0,
        Round = 1,
        Polo = 2
    }

    /// <summary>
    /// Sleeve length options for jersey design.
    /// </summary>
    public enum SleeveStyle
    {
        Short = 0,
        Long = 1,
        ThreeQuarter = 2
    }

    /// <summary>
    /// Placement of the player name on the jersey back.
    /// </summary>
    public enum NamePlacement
    {
        CenterBack = 0,
        UpperBack = 1
    }

    /// <summary>
    /// Placement of the team crest on the jersey front.
    /// </summary>
    public enum CrestPosition
    {
        LeftChest = 0,
        CenterChest = 1
    }

    /// <summary>
    /// Placement area for sponsor logos/text.
    /// </summary>
    public enum SponsorPlacement
    {
        FrontCenter = 0,
        BackUpper = 1,
        SleeveLeft = 2,
        SleeveRight = 3
    }

    /// <summary>
    /// Kit variant type for home/away/third kit selection.
    /// </summary>
    public enum KitVariant
    {
        Home = 0,
        Away = 1,
        Third = 2
    }

    /// <summary>
    /// Describes the full visual design of a jersey: colors, pattern, collar, and sleeves.
    /// Pure C# — no engine references. Used as input for JerseyRenderer.
    /// </summary>
    public class JerseyDesign
    {
        /// <summary>Primary jersey color (dominant).</summary>
        public SimpleColor PrimaryColor;

        /// <summary>Secondary jersey color (accents, stripes, etc.).</summary>
        public SimpleColor SecondaryColor;

        /// <summary>Tertiary jersey color (collar, cuffs, minor details).</summary>
        public SimpleColor TertiaryColor;

        /// <summary>The pattern type applied to the jersey body.</summary>
        public JerseyPattern Pattern;

        /// <summary>Scale factor for the pattern (e.g. stripe width). Default 1.0.</summary>
        public float PatternScale;

        /// <summary>Collar style.</summary>
        public CollarStyle Collar;

        /// <summary>Sleeve length style.</summary>
        public SleeveStyle Sleeves;

        public JerseyDesign()
        {
            PrimaryColor = SimpleColor.White;
            SecondaryColor = SimpleColor.Black;
            TertiaryColor = SimpleColor.White;
            Pattern = JerseyPattern.Solid;
            PatternScale = 1.0f;
            Collar = CollarStyle.VNeck;
            Sleeves = SleeveStyle.Short;
        }

        /// <summary>
        /// Creates a deep copy of this design.
        /// </summary>
        public JerseyDesign Clone()
        {
            return new JerseyDesign
            {
                PrimaryColor = PrimaryColor,
                SecondaryColor = SecondaryColor,
                TertiaryColor = TertiaryColor,
                Pattern = Pattern,
                PatternScale = PatternScale,
                Collar = Collar,
                Sleeves = Sleeves
            };
        }
    }

    /// <summary>
    /// Configuration for the player name printed on the jersey.
    /// </summary>
    public class PlayerNameConfig
    {
        /// <summary>Maximum number of characters allowed for a player name.</summary>
        public static readonly int MaxCharacters = 16;

        /// <summary>Player name string.</summary>
        public string Name;

        /// <summary>Font size in points for the name text.</summary>
        public float FontSize;

        /// <summary>Where the name is placed on the jersey back.</summary>
        public NamePlacement Placement;

        /// <summary>Letter spacing multiplier (1.0 = normal).</summary>
        public float LetterSpacing;

        public PlayerNameConfig()
        {
            Name = "";
            FontSize = 36f;
            Placement = NamePlacement.UpperBack;
            LetterSpacing = 1.0f;
        }

        public PlayerNameConfig(string name)
        {
            Name = name ?? "";
            FontSize = 36f;
            Placement = NamePlacement.UpperBack;
            LetterSpacing = 1.0f;
        }
    }

    /// <summary>
    /// Configuration for the player number printed on the jersey.
    /// </summary>
    public class PlayerNumberConfig
    {
        /// <summary>Minimum valid squad number.</summary>
        public static readonly int MinNumber = 1;

        /// <summary>Maximum valid squad number.</summary>
        public static readonly int MaxNumber = 99;

        /// <summary>The jersey number (1-99).</summary>
        public int Number;

        /// <summary>Font size in points for the number text.</summary>
        public float FontSize;

        /// <summary>Whether to show the number on the front of the jersey (smaller).</summary>
        public bool ShowOnFront;

        /// <summary>Whether to show the number on the back of the jersey (larger).</summary>
        public bool ShowOnBack;

        /// <summary>Whether to render an outline around the number for visibility.</summary>
        public bool OutlineEnabled;

        /// <summary>Color of the number outline if enabled.</summary>
        public SimpleColor OutlineColor;

        public PlayerNumberConfig()
        {
            Number = 1;
            FontSize = 72f;
            ShowOnFront = true;
            ShowOnBack = true;
            OutlineEnabled = false;
            OutlineColor = SimpleColor.Black;
        }

        public PlayerNumberConfig(int number)
        {
            Number = number;
            FontSize = 72f;
            ShowOnFront = true;
            ShowOnBack = true;
            OutlineEnabled = false;
            OutlineColor = SimpleColor.Black;
        }
    }

    /// <summary>
    /// Configuration for the team crest/badge displayed on the jersey.
    /// </summary>
    public class CrestConfig
    {
        /// <summary>Position of the crest on the jersey front.</summary>
        public CrestPosition Position;

        /// <summary>Size of the crest in normalized UV space (0-1). Default 0.08.</summary>
        public float Size;

        /// <summary>Team name associated with this crest.</summary>
        public string TeamName;

        /// <summary>Optional path to the crest texture asset.</summary>
        public string TexturePath;

        public CrestConfig()
        {
            Position = CrestPosition.LeftChest;
            Size = 0.08f;
            TeamName = "";
            TexturePath = "";
        }

        public CrestConfig(string teamName)
        {
            Position = CrestPosition.LeftChest;
            Size = 0.08f;
            TeamName = teamName ?? "";
            TexturePath = "";
        }
    }

    /// <summary>
    /// Configuration for sponsor branding on the jersey.
    /// </summary>
    public class SponsorConfig
    {
        /// <summary>Sponsor display text (if text-based).</summary>
        public string Text;

        /// <summary>Placement area on the jersey.</summary>
        public SponsorPlacement Placement;

        /// <summary>Optional path to a sponsor logo texture.</summary>
        public string TexturePath;

        /// <summary>Size of the sponsor area in normalized UV space (0-1).</summary>
        public float Size;

        public SponsorConfig()
        {
            Text = "";
            Placement = SponsorPlacement.FrontCenter;
            TexturePath = "";
            Size = 0.12f;
        }
    }

    /// <summary>
    /// Complete jersey configuration combining design, name, number, crest, and sponsors.
    /// Represents a single player's full jersey setup.
    /// </summary>
    public class FullJerseyConfig
    {
        public JerseyDesign Design;
        public PlayerNameConfig NameConfig;
        public PlayerNumberConfig NumberConfig;
        public CrestConfig Crest;
        public List<SponsorConfig> Sponsors;

        public FullJerseyConfig()
        {
            Design = new JerseyDesign();
            NameConfig = new PlayerNameConfig();
            NumberConfig = new PlayerNumberConfig();
            Crest = new CrestConfig();
            Sponsors = new List<SponsorConfig>();
        }
    }

    /// <summary>
    /// Validation utilities for jersey customization inputs.
    /// All methods are static and pure — no side effects.
    /// </summary>
    public static class JerseyValidation
    {
        /// <summary>
        /// Validates that a squad number is within the allowed range (1-99).
        /// </summary>
        public static bool IsNumberValid(int number)
        {
            return number >= PlayerNumberConfig.MinNumber && number <= PlayerNumberConfig.MaxNumber;
        }

        /// <summary>
        /// Validates that a player name is non-null, non-empty, and within max length.
        /// </summary>
        public static bool IsNameValid(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.Length > PlayerNameConfig.MaxCharacters) return false;
            return true;
        }

        /// <summary>
        /// Checks whether a color channel value is within the valid 0-1 range.
        /// </summary>
        public static bool IsColorChannelValid(float value)
        {
            return value >= 0f && value <= 1f;
        }

        /// <summary>
        /// Validates that all channels of a SimpleColor are within 0-1.
        /// </summary>
        public static bool IsColorValid(SimpleColor color)
        {
            return IsColorChannelValid(color.R)
                && IsColorChannelValid(color.G)
                && IsColorChannelValid(color.B)
                && IsColorChannelValid(color.A);
        }

        /// <summary>
        /// Validates that a JerseyDesign has valid colors and a positive pattern scale.
        /// </summary>
        public static bool IsDesignValid(JerseyDesign design)
        {
            if (design == null) return false;
            if (!IsColorValid(design.PrimaryColor)) return false;
            if (!IsColorValid(design.SecondaryColor)) return false;
            if (!IsColorValid(design.TertiaryColor)) return false;
            if (design.PatternScale <= 0f) return false;
            return true;
        }

        /// <summary>
        /// Validates that a FullJerseyConfig is complete and internally consistent:
        /// valid design, valid number, valid name (if set), valid crest.
        /// </summary>
        public static bool IsDesignComplete(FullJerseyConfig config)
        {
            if (config == null) return false;
            if (!IsDesignValid(config.Design)) return false;
            if (!IsNumberValid(config.NumberConfig.Number)) return false;
            // Name is optional, but if set, must be valid
            if (!string.IsNullOrEmpty(config.NameConfig.Name)
                && !IsNameValid(config.NameConfig.Name))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Validates that a crest size is within a reasonable range (0.01 to 0.5).
        /// </summary>
        public static bool IsCrestSizeValid(float size)
        {
            return size >= 0.01f && size <= 0.5f;
        }

        /// <summary>
        /// Validates a pattern scale is positive and within a reasonable range (0.1 to 10.0).
        /// </summary>
        public static bool IsPatternScaleValid(float scale)
        {
            return scale >= 0.1f && scale <= 10.0f;
        }
    }
}
