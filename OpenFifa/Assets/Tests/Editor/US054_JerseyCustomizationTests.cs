using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("US054")]
    [Category("Art")]
    public class US054_JerseyCustomizationTests
    {
        // ─── JerseyPattern Enum Tests ────────────────────────────────────

        [Test]
        public void JerseyPattern_HasExpectedValues()
        {
            var values = Enum.GetValues(typeof(JerseyPattern));
            Assert.AreEqual(7, values.Length,
                "JerseyPattern should have 7 values: Solid, Stripes, Hoops, HalfAndHalf, Gradient, Pinstripes, Chevron.");
        }

        [Test]
        public void JerseyPattern_SolidIsDefault()
        {
            Assert.AreEqual(0, (int)JerseyPattern.Solid,
                "Solid should be the default pattern (value 0).");
        }

        [Test]
        public void JerseyPattern_AllValuesAreDistinct()
        {
            var values = Enum.GetValues(typeof(JerseyPattern)).Cast<int>().ToList();
            Assert.AreEqual(values.Count, values.Distinct().Count(),
                "All JerseyPattern values should be unique.");
        }

        // ─── JerseyDesign Tests ──────────────────────────────────────────

        [Test]
        public void JerseyDesign_Defaults_PrimaryIsWhite()
        {
            var design = new JerseyDesign();
            Assert.AreEqual(1f, design.PrimaryColor.R);
            Assert.AreEqual(1f, design.PrimaryColor.G);
            Assert.AreEqual(1f, design.PrimaryColor.B);
        }

        [Test]
        public void JerseyDesign_Defaults_PatternIsSolid()
        {
            var design = new JerseyDesign();
            Assert.AreEqual(JerseyPattern.Solid, design.Pattern);
        }

        [Test]
        public void JerseyDesign_Defaults_PatternScaleIsOne()
        {
            var design = new JerseyDesign();
            Assert.AreEqual(1.0f, design.PatternScale,
                "Default pattern scale should be 1.0.");
        }

        [Test]
        public void JerseyDesign_Clone_CreatesIndependentCopy()
        {
            var original = new JerseyDesign
            {
                PrimaryColor = new SimpleColor(0.5f, 0.5f, 0.5f),
                Pattern = JerseyPattern.Stripes,
                PatternScale = 2.0f
            };

            var clone = original.Clone();
            clone.PrimaryColor = new SimpleColor(0.9f, 0.1f, 0.1f);
            clone.Pattern = JerseyPattern.Hoops;

            Assert.AreEqual(0.5f, original.PrimaryColor.R,
                "Modifying the clone should not affect the original.");
            Assert.AreEqual(JerseyPattern.Stripes, original.Pattern);
        }

        [Test]
        public void JerseyDesign_ColorValidation_ValidColorsPassValidation()
        {
            var design = new JerseyDesign
            {
                PrimaryColor = new SimpleColor(0.1f, 0.2f, 0.85f),
                SecondaryColor = new SimpleColor(1f, 1f, 1f),
                TertiaryColor = new SimpleColor(0f, 0f, 0f)
            };
            Assert.IsTrue(JerseyValidation.IsDesignValid(design),
                "Design with valid RGB colors in [0,1] should pass validation.");
        }

        // ─── PlayerNameConfig Tests ──────────────────────────────────────

        [Test]
        public void PlayerNameConfig_MaxChars_Is16()
        {
            Assert.AreEqual(16, PlayerNameConfig.MaxCharacters,
                "Max player name characters should be 16.");
        }

        [Test]
        public void PlayerNameConfig_DefaultPlacement_IsUpperBack()
        {
            var config = new PlayerNameConfig();
            Assert.AreEqual(NamePlacement.UpperBack, config.Placement);
        }

        [Test]
        public void PlayerNameConfig_ConstructorWithName_SetsName()
        {
            var config = new PlayerNameConfig("RONALDO");
            Assert.AreEqual("RONALDO", config.Name);
        }

        [Test]
        public void PlayerNameConfig_NullName_DefaultsToEmpty()
        {
            var config = new PlayerNameConfig(null);
            Assert.AreEqual("", config.Name,
                "Null name should default to empty string.");
        }

        // ─── PlayerNumberConfig Tests ────────────────────────────────────

        [Test]
        public void PlayerNumberConfig_ValidRange_1To99()
        {
            Assert.AreEqual(1, PlayerNumberConfig.MinNumber);
            Assert.AreEqual(99, PlayerNumberConfig.MaxNumber);
        }

        [Test]
        public void PlayerNumberConfig_DefaultShowsFrontAndBack()
        {
            var config = new PlayerNumberConfig();
            Assert.IsTrue(config.ShowOnFront);
            Assert.IsTrue(config.ShowOnBack);
        }

        [Test]
        public void JerseyValidation_IsNumberValid_ValidNumbers_ReturnsTrue()
        {
            Assert.IsTrue(JerseyValidation.IsNumberValid(1));
            Assert.IsTrue(JerseyValidation.IsNumberValid(10));
            Assert.IsTrue(JerseyValidation.IsNumberValid(99));
        }

        [Test]
        public void JerseyValidation_IsNumberValid_InvalidNumbers_ReturnsFalse()
        {
            Assert.IsFalse(JerseyValidation.IsNumberValid(0),
                "0 is not a valid squad number.");
            Assert.IsFalse(JerseyValidation.IsNumberValid(-1),
                "Negative numbers are invalid.");
            Assert.IsFalse(JerseyValidation.IsNumberValid(100),
                "100 exceeds the max squad number of 99.");
        }

        // ─── CrestConfig Tests ───────────────────────────────────────────

        [Test]
        public void CrestConfig_DefaultPosition_IsLeftChest()
        {
            var crest = new CrestConfig();
            Assert.AreEqual(CrestPosition.LeftChest, crest.Position);
        }

        [Test]
        public void CrestConfig_DefaultSize_IsReasonable()
        {
            var crest = new CrestConfig();
            Assert.That(crest.Size, Is.InRange(0.01f, 0.5f),
                "Default crest size should be within a reasonable range.");
        }

        [Test]
        public void CrestConfig_ConstructorWithTeamName_SetsTeamName()
        {
            var crest = new CrestConfig("FC Barcelona");
            Assert.AreEqual("FC Barcelona", crest.TeamName);
        }

        // ─── SquadNumberAssigner Tests ───────────────────────────────────

        [Test]
        public void SquadNumberAssigner_AutoAssign_5Players_Returns1Through5()
        {
            var numbers = SquadNumberAssigner.AutoAssign(5);
            Assert.AreEqual(5, numbers.Length);
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(i + 1, numbers[i],
                    $"Player at index {i} should get number {i + 1}.");
            }
        }

        [Test]
        public void SquadNumberAssigner_AutoAssign_ZeroPlayers_ReturnsEmpty()
        {
            var numbers = SquadNumberAssigner.AutoAssign(0);
            Assert.AreEqual(0, numbers.Length);
        }

        [Test]
        public void SquadNumberAssigner_AutoAssign_NoDuplicates()
        {
            var numbers = SquadNumberAssigner.AutoAssign(11);
            Assert.IsFalse(SquadNumberAssigner.HasDuplicates(numbers),
                "Auto-assigned numbers should never contain duplicates.");
        }

        [Test]
        public void SquadNumberAssigner_HasDuplicates_WithDuplicates_ReturnsTrue()
        {
            Assert.IsTrue(SquadNumberAssigner.HasDuplicates(new[] { 1, 2, 3, 2 }),
                "Should detect duplicate number 2.");
        }

        [Test]
        public void SquadNumberAssigner_HasDuplicates_NoDuplicates_ReturnsFalse()
        {
            Assert.IsFalse(SquadNumberAssigner.HasDuplicates(new[] { 1, 2, 3, 4, 5 }));
        }

        [Test]
        public void SquadNumberAssigner_AssignWithPreferences_RespectsValidPreferences()
        {
            var preferred = new[] { 10, 7, 9, 0, 0 };
            var numbers = SquadNumberAssigner.AssignWithPreferences(5, preferred);

            Assert.AreEqual(5, numbers.Length);
            Assert.AreEqual(10, numbers[0], "First player should get preferred #10.");
            Assert.AreEqual(7, numbers[1], "Second player should get preferred #7.");
            Assert.AreEqual(9, numbers[2], "Third player should get preferred #9.");
            Assert.IsFalse(SquadNumberAssigner.HasDuplicates(numbers),
                "Assigned numbers should be unique.");
        }

        [Test]
        public void SquadNumberAssigner_AssignWithPreferences_HandlesDuplicatePreference()
        {
            var preferred = new[] { 10, 10, 10 };
            var numbers = SquadNumberAssigner.AssignWithPreferences(3, preferred);

            Assert.AreEqual(3, numbers.Length);
            Assert.AreEqual(10, numbers[0], "First player gets #10.");
            Assert.IsFalse(SquadNumberAssigner.HasDuplicates(numbers),
                "Duplicate preferences should be auto-resolved to unique numbers.");
        }

        [Test]
        public void SquadNumberAssigner_ValidateSquad_ValidSquad_ReturnsTrue()
        {
            Assert.IsTrue(SquadNumberAssigner.ValidateSquad(new[] { 1, 2, 3, 4, 5 }));
        }

        [Test]
        public void SquadNumberAssigner_ValidateSquad_WithInvalidNumber_ReturnsFalse()
        {
            Assert.IsFalse(SquadNumberAssigner.ValidateSquad(new[] { 1, 0, 3 }),
                "Squad with number 0 should fail validation.");
        }

        [Test]
        public void SquadNumberAssigner_ValidateSquad_WithDuplicate_ReturnsFalse()
        {
            Assert.IsFalse(SquadNumberAssigner.ValidateSquad(new[] { 1, 2, 2 }),
                "Squad with duplicate numbers should fail validation.");
        }

        // ─── KitVariantManager Tests ─────────────────────────────────────

        [Test]
        public void KitVariantManager_SetAndGetKit_ReturnsCorrectDesign()
        {
            var manager = new KitVariantManager();
            var homeDesign = new JerseyDesign { PrimaryColor = new SimpleColor(1f, 0f, 0f) };
            manager.SetKit(KitVariant.Home, homeDesign);

            var result = manager.GetKit(KitVariant.Home);
            Assert.IsNotNull(result);
            Assert.AreEqual(1f, result.PrimaryColor.R);
        }

        [Test]
        public void KitVariantManager_GetKit_Unregistered_ReturnsNull()
        {
            var manager = new KitVariantManager();
            Assert.IsNull(manager.GetKit(KitVariant.Away),
                "Unregistered kit variant should return null.");
        }

        [Test]
        public void KitVariantManager_HasKit_RegisteredVariant_ReturnsTrue()
        {
            var manager = new KitVariantManager();
            manager.SetKit(KitVariant.Home, new JerseyDesign());
            Assert.IsTrue(manager.HasKit(KitVariant.Home));
            Assert.IsFalse(manager.HasKit(KitVariant.Away));
        }

        [Test]
        public void KitVariantManager_SelectNonClashing_HomeDifferent_ReturnsHome()
        {
            var manager = new KitVariantManager();
            manager.SetKit(KitVariant.Home, new JerseyDesign { PrimaryColor = new SimpleColor(1f, 0f, 0f) });
            manager.SetKit(KitVariant.Away, new JerseyDesign { PrimaryColor = new SimpleColor(1f, 1f, 1f) });

            // Opponent in blue — red home kit should not clash
            var opponentColor = new SimpleColor(0f, 0f, 1f);
            var selected = manager.SelectNonClashingVariant(opponentColor);
            Assert.AreEqual(KitVariant.Home, selected,
                "Home kit (red) should not clash with blue opponent.");
        }

        [Test]
        public void KitVariantManager_SelectNonClashing_HomeClashes_ReturnsAway()
        {
            var manager = new KitVariantManager();
            // Home is red, opponent is also red — clash
            manager.SetKit(KitVariant.Home, new JerseyDesign { PrimaryColor = new SimpleColor(0.85f, 0.1f, 0.1f) });
            manager.SetKit(KitVariant.Away, new JerseyDesign { PrimaryColor = new SimpleColor(1f, 1f, 1f) });

            var opponentColor = new SimpleColor(0.9f, 0.1f, 0.1f);
            var selected = manager.SelectNonClashingVariant(opponentColor);
            Assert.AreEqual(KitVariant.Away, selected,
                "Should switch to away kit when home clashes with opponent.");
        }

        // ─── JerseyComparer Tests ────────────────────────────────────────

        [Test]
        public void JerseyComparer_ColorDistance_IdenticalColors_ReturnsZero()
        {
            var red = new SimpleColor(1f, 0f, 0f);
            float distance = JerseyComparer.ColorDistance(red, red);
            Assert.AreEqual(0f, distance, 0.001f,
                "Distance between identical colors should be zero.");
        }

        [Test]
        public void JerseyComparer_ColorDistance_BlackAndWhite_ReturnsMaxDistance()
        {
            float distance = JerseyComparer.ColorDistance(SimpleColor.Black, SimpleColor.White);
            float expected = (float)Math.Sqrt(3.0); // sqrt(1+1+1)
            Assert.AreEqual(expected, distance, 0.01f,
                $"Black-to-white distance should be sqrt(3) ~= {expected:F2}.");
        }

        [Test]
        public void JerseyComparer_IsClashing_SimilarColors_ReturnsTrue()
        {
            var kitA = new JerseyDesign { PrimaryColor = new SimpleColor(0.8f, 0.1f, 0.1f) };
            var kitB = new JerseyDesign { PrimaryColor = new SimpleColor(0.85f, 0.1f, 0.1f) };
            Assert.IsTrue(JerseyComparer.IsClashing(kitA, kitB),
                "Two similar red kits should be detected as clashing.");
        }

        [Test]
        public void JerseyComparer_IsClashing_DifferentColors_ReturnsFalse()
        {
            var kitA = new JerseyDesign { PrimaryColor = new SimpleColor(1f, 0f, 0f) };
            var kitB = new JerseyDesign { PrimaryColor = new SimpleColor(0f, 0f, 1f) };
            Assert.IsFalse(JerseyComparer.IsClashing(kitA, kitB),
                "Red vs blue should not be detected as clashing.");
        }

        [Test]
        public void JerseyComparer_SuggestTextColor_DarkBackground_ReturnsWhite()
        {
            var darkBg = new SimpleColor(0.1f, 0.05f, 0.1f);
            var textColor = JerseyComparer.SuggestTextColor(darkBg);
            Assert.AreEqual(1f, textColor.R, "White text for dark backgrounds.");
            Assert.AreEqual(1f, textColor.G);
            Assert.AreEqual(1f, textColor.B);
        }

        [Test]
        public void JerseyComparer_SuggestTextColor_LightBackground_ReturnsBlack()
        {
            var lightBg = new SimpleColor(0.9f, 0.95f, 0.9f);
            var textColor = JerseyComparer.SuggestTextColor(lightBg);
            Assert.AreEqual(0f, textColor.R, "Black text for light backgrounds.");
            Assert.AreEqual(0f, textColor.G);
            Assert.AreEqual(0f, textColor.B);
        }

        [Test]
        public void JerseyComparer_GetLuminance_White_IsOne()
        {
            float lum = JerseyComparer.GetLuminance(SimpleColor.White);
            Assert.AreEqual(1f, lum, 0.01f);
        }

        [Test]
        public void JerseyComparer_GetLuminance_Black_IsZero()
        {
            float lum = JerseyComparer.GetLuminance(SimpleColor.Black);
            Assert.AreEqual(0f, lum, 0.01f);
        }

        // ─── NameFormatter Tests ─────────────────────────────────────────

        [Test]
        public void NameFormatter_FormatForJersey_UppercasesAndTrims()
        {
            string result = NameFormatter.FormatForJersey("  messi  ");
            Assert.AreEqual("MESSI", result);
        }

        [Test]
        public void NameFormatter_FormatForJersey_TruncatesToMaxLength()
        {
            string longName = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string result = NameFormatter.FormatForJersey(longName);
            Assert.AreEqual(PlayerNameConfig.MaxCharacters, result.Length,
                $"Name should be truncated to {PlayerNameConfig.MaxCharacters} chars.");
        }

        [Test]
        public void NameFormatter_FormatForJersey_EmptyInput_ReturnsEmpty()
        {
            Assert.AreEqual("", NameFormatter.FormatForJersey(""));
            Assert.AreEqual("", NameFormatter.FormatForJersey(null));
        }

        [Test]
        public void NameFormatter_Sanitize_RemovesInvalidChars()
        {
            string result = NameFormatter.Sanitize("O'Brien#@!");
            Assert.AreEqual("O'Brien", result,
                "Sanitize should keep letters, digits, hyphens, apostrophes, spaces, periods.");
        }

        [Test]
        public void NameFormatter_Sanitize_KeepsAllowedSpecialChars()
        {
            string result = NameFormatter.Sanitize("De-Bruyne Jr.");
            Assert.AreEqual("De-Bruyne Jr.", result);
        }

        [Test]
        public void NameFormatter_FormatAndSanitize_CombinesSteps()
        {
            string result = NameFormatter.FormatAndSanitize("  o'brien#@  ");
            Assert.AreEqual("O'BRIEN", result);
        }

        [Test]
        public void NameFormatter_Truncate_WithinLimit_ReturnsOriginal()
        {
            Assert.AreEqual("MESSI", NameFormatter.Truncate("MESSI", 10));
        }

        [Test]
        public void NameFormatter_Truncate_ExceedsLimit_Truncates()
        {
            Assert.AreEqual("MES", NameFormatter.Truncate("MESSI", 3));
        }

        [Test]
        public void NameFormatter_ContainsOnlyAllowedChars_ValidName_ReturnsTrue()
        {
            Assert.IsTrue(NameFormatter.ContainsOnlyAllowedChars("O'Brien-Smith Jr."));
        }

        [Test]
        public void NameFormatter_ContainsOnlyAllowedChars_InvalidChars_ReturnsFalse()
        {
            Assert.IsFalse(NameFormatter.ContainsOnlyAllowedChars("Name#123!"));
        }

        // ─── JerseyBuilder Tests ─────────────────────────────────────────

        [Test]
        public void JerseyBuilder_Build_ReturnsCompleteConfig()
        {
            var config = new JerseyBuilder()
                .WithNumber(10)
                .WithName("MESSI")
                .WithCrest("FC Barcelona")
                .Build();

            Assert.IsNotNull(config);
            Assert.AreEqual(10, config.NumberConfig.Number);
            Assert.AreEqual("MESSI", config.NameConfig.Name);
            Assert.AreEqual("FC Barcelona", config.Crest.TeamName);
        }

        [Test]
        public void JerseyBuilder_BuildValidated_ValidConfig_ReturnsConfig()
        {
            var config = new JerseyBuilder()
                .WithNumber(7)
                .WithName("RONALDO")
                .Build();

            var validated = new JerseyBuilder()
                .WithNumber(7)
                .WithName("RONALDO")
                .BuildValidated();

            Assert.IsNotNull(validated,
                "A valid config should be returned by BuildValidated.");
        }

        [Test]
        public void JerseyBuilder_BuildValidated_InvalidNumber_ReturnsNull()
        {
            var config = new JerseyBuilder()
                .WithNumber(0) // invalid
                .BuildValidated();

            Assert.IsNull(config,
                "BuildValidated should return null for invalid config (number 0).");
        }

        [Test]
        public void JerseyBuilder_WithDesign_SetsDesign()
        {
            var design = new JerseyDesign
            {
                PrimaryColor = new SimpleColor(0.5f, 0.5f, 0f),
                Pattern = JerseyPattern.Chevron
            };

            var config = new JerseyBuilder()
                .WithDesign(design)
                .WithNumber(1)
                .Build();

            Assert.AreEqual(JerseyPattern.Chevron, config.Design.Pattern);
            Assert.AreEqual(0.5f, config.Design.PrimaryColor.R);
        }

        // ─── JerseyValidation Tests ──────────────────────────────────────

        [Test]
        public void JerseyValidation_IsNameValid_ValidName_ReturnsTrue()
        {
            Assert.IsTrue(JerseyValidation.IsNameValid("MESSI"));
        }

        [Test]
        public void JerseyValidation_IsNameValid_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(JerseyValidation.IsNameValid(""));
            Assert.IsFalse(JerseyValidation.IsNameValid(null));
        }

        [Test]
        public void JerseyValidation_IsNameValid_TooLong_ReturnsFalse()
        {
            var longName = new string('A', PlayerNameConfig.MaxCharacters + 1);
            Assert.IsFalse(JerseyValidation.IsNameValid(longName));
        }

        [Test]
        public void JerseyValidation_IsDesignComplete_ValidConfig_ReturnsTrue()
        {
            var config = new FullJerseyConfig();
            config.NumberConfig.Number = 10;
            Assert.IsTrue(JerseyValidation.IsDesignComplete(config));
        }

        [Test]
        public void JerseyValidation_IsDesignComplete_NullConfig_ReturnsFalse()
        {
            Assert.IsFalse(JerseyValidation.IsDesignComplete(null));
        }

        [Test]
        public void JerseyValidation_IsColorValid_ValidColor_ReturnsTrue()
        {
            Assert.IsTrue(JerseyValidation.IsColorValid(new SimpleColor(0.5f, 0.5f, 0.5f)));
        }

        [Test]
        public void JerseyValidation_IsColorValid_OutOfRange_ReturnsFalse()
        {
            Assert.IsFalse(JerseyValidation.IsColorValid(new SimpleColor(-0.1f, 0.5f, 0.5f)),
                "Negative R channel should be invalid.");
            Assert.IsFalse(JerseyValidation.IsColorValid(new SimpleColor(0.5f, 1.1f, 0.5f)),
                "G > 1.0 should be invalid.");
        }

        [Test]
        public void JerseyValidation_IsCrestSizeValid_WithinRange_ReturnsTrue()
        {
            Assert.IsTrue(JerseyValidation.IsCrestSizeValid(0.08f));
        }

        [Test]
        public void JerseyValidation_IsCrestSizeValid_OutOfRange_ReturnsFalse()
        {
            Assert.IsFalse(JerseyValidation.IsCrestSizeValid(0f));
            Assert.IsFalse(JerseyValidation.IsCrestSizeValid(1f));
        }

        // ─── Integration / Full Pipeline Tests ──────────────────────────

        [Test]
        public void FullPipeline_BuilderToValidation_EndToEnd()
        {
            // Build a complete jersey
            var design = new JerseyDesign
            {
                PrimaryColor = new SimpleColor(0.1f, 0.2f, 0.85f),
                SecondaryColor = SimpleColor.White,
                TertiaryColor = SimpleColor.Black,
                Pattern = JerseyPattern.Stripes,
                PatternScale = 1.5f,
                Collar = CollarStyle.Round,
                Sleeves = SleeveStyle.Short
            };

            var config = new JerseyBuilder()
                .WithDesign(design)
                .WithNumber(10)
                .WithName("MESSI")
                .WithCrest("FC Barcelona")
                .Build();

            // Validate
            Assert.IsTrue(JerseyValidation.IsDesignComplete(config),
                "Complete jersey pipeline should produce a valid config.");
            Assert.IsTrue(JerseyValidation.IsDesignValid(config.Design));
            Assert.IsTrue(JerseyValidation.IsNumberValid(config.NumberConfig.Number));
            Assert.IsTrue(JerseyValidation.IsNameValid(config.NameConfig.Name));

            // Format name
            string formatted = NameFormatter.FormatForJersey(config.NameConfig.Name);
            Assert.AreEqual("MESSI", formatted);

            // Suggest text color for the jersey background
            var textColor = JerseyComparer.SuggestTextColor(design.PrimaryColor);
            Assert.AreEqual(1f, textColor.R,
                "White text should be suggested on a dark blue background.");
        }

        [Test]
        public void FullPipeline_SquadAssignment_AllPlayersGetUniqueNumbers()
        {
            var numbers = SquadNumberAssigner.AutoAssign(5);
            Assert.IsTrue(SquadNumberAssigner.ValidateSquad(numbers),
                "Auto-assigned 5-player squad should pass validation.");

            // Build jersey configs for each
            var configs = new List<FullJerseyConfig>();
            var names = new[] { "GK", "DEF", "MID", "FWD", "FWD" };
            for (int i = 0; i < 5; i++)
            {
                var config = new JerseyBuilder()
                    .WithNumber(numbers[i])
                    .WithName(names[i])
                    .WithCrest("Test FC")
                    .Build();
                configs.Add(config);
            }

            // Verify all unique numbers
            var usedNumbers = configs.Select(c => c.NumberConfig.Number).ToList();
            Assert.AreEqual(usedNumbers.Count, usedNumbers.Distinct().Count(),
                "All players in the squad should have unique numbers.");
        }
    }
}
