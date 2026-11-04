using System.Collections.Generic;
using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("Kits")]
    public class KitDefinitionTests
    {
        private static KitDefinitionData ValidKit()
        {
            return new KitDefinitionData
            {
                Id = "test-home",
                TeamId = "test",
                Variant = "Home",
                PrimaryColor = "#1F4FC9",
                SecondaryColor = "#FFFFFF",
                TertiaryColor = "#E8B92E",
                ShortsColor = "#FFFFFF",
                SocksColor = "#1F4FC9",
                Pattern = "Stripes",
                PatternScale = 1f,
                Collar = "VNeck",
                FontStyle = "Block",
                NameLayout = "Arched",
                NumberColor = "#FFFFFF",
                NumberOutlineColor = "#000000",
                NumberOutlineWidth = 0f
            };
        }

        [Test]
        public void Validate_ValidKit_ReturnsNoErrors()
        {
            var errors = KitDefinitionValidator.Validate(ValidKit());
            Assert.IsEmpty(errors, string.Join(" | ", errors));
        }

        [Test]
        public void Validate_NullKit_ReturnsError()
        {
            Assert.IsNotEmpty(KitDefinitionValidator.Validate(null));
        }

        [Test]
        public void Validate_MissingIdAndTeam_ReportsBoth()
        {
            var kit = ValidKit();
            kit.Id = "";
            kit.TeamId = " ";
            var errors = KitDefinitionValidator.Validate(kit);
            Assert.That(errors.Exists(e => e.Contains("no Id")), string.Join(" | ", errors));
            Assert.That(errors.Exists(e => e.Contains("TeamId")), string.Join(" | ", errors));
        }

        [TestCase("Fourth")]
        [TestCase("")]
        [TestCase("1")]
        public void Validate_UnknownVariant_IsInvalid(string variant)
        {
            var kit = ValidKit();
            kit.Variant = variant;
            Assert.IsFalse(KitDefinitionValidator.IsValid(kit));
        }

        [TestCase("blue")]
        [TestCase("#12345")]
        [TestCase("#GGGGGG")]
        public void Validate_BadPrimaryColor_IsInvalid(string color)
        {
            var kit = ValidKit();
            kit.PrimaryColor = color;
            Assert.IsFalse(KitDefinitionValidator.IsValid(kit));
        }

        [Test]
        public void Validate_UnknownPattern_IsInvalid()
        {
            var kit = ValidKit();
            kit.Pattern = "Tartan";
            Assert.IsFalse(KitDefinitionValidator.IsValid(kit));
        }

        [Test]
        public void Validate_PatternNamesAreCaseInsensitive_IsValid()
        {
            var kit = ValidKit();
            kit.Pattern = "sash";
            kit.Collar = "polo";
            kit.FontStyle = "condensed";
            Assert.IsTrue(KitDefinitionValidator.IsValid(kit));
        }

        [TestCase(0.05f)]
        [TestCase(11f)]
        public void Validate_PatternScaleOutOfRange_IsInvalid(float scale)
        {
            var kit = ValidKit();
            kit.PatternScale = scale;
            Assert.IsFalse(KitDefinitionValidator.IsValid(kit));
        }

        [Test]
        public void Validate_NumberSameAsShirtWithoutOutline_IsInvalid()
        {
            var kit = ValidKit();
            kit.NumberColor = "#1F4FCA";
            Assert.IsFalse(KitDefinitionValidator.IsValid(kit), "Blue number on blue shirt must be rejected.");
        }

        [Test]
        public void Validate_NumberSameAsShirtWithContrastingOutline_IsValid()
        {
            var kit = ValidKit();
            kit.NumberColor = "#1F4FCA";
            kit.NumberOutlineColor = "#FFFFFF";
            kit.NumberOutlineWidth = 0.3f;
            Assert.IsTrue(KitDefinitionValidator.IsValid(kit), string.Join(" | ", KitDefinitionValidator.Validate(kit)));
        }

        [Test]
        public void Validate_OutlineTooWide_IsInvalid()
        {
            var kit = ValidKit();
            kit.NumberOutlineWidth = 1.5f;
            Assert.IsFalse(KitDefinitionValidator.IsValid(kit));
        }

        [Test]
        public void Validate_LicensedNormalWithoutAlbedo_IsInvalid()
        {
            var kit = ValidKit();
            kit.Licensed.NormalPath = "Kits/Licensed/x_normal";
            Assert.IsFalse(KitDefinitionValidator.IsValid(kit));

            kit.Licensed.AlbedoPath = "Kits/Licensed/x_albedo";
            Assert.IsTrue(KitDefinitionValidator.IsValid(kit));
            Assert.IsTrue(kit.Licensed.HasAlbedo);
        }

        [TestCase("#FF0000", 1f, 0f, 0f, 1f)]
        [TestCase("00FF00", 0f, 1f, 0f, 1f)]
        [TestCase("#00f", 0f, 0f, 1f, 1f)]
        [TestCase("#FFFFFF80", 1f, 1f, 1f, 0.5f)]
        public void TryParseHex_ValidInput_ParsesChannels(string hex, float r, float g, float b, float a)
        {
            SimpleColor c;
            Assert.IsTrue(KitColorUtility.TryParseHex(hex, out c));
            Assert.AreEqual(r, c.R, 0.01f);
            Assert.AreEqual(g, c.G, 0.01f);
            Assert.AreEqual(b, c.B, 0.01f);
            Assert.AreEqual(a, c.A, 0.01f);
        }

        [Test]
        public void ToHex_RoundTrip_PreservesColor()
        {
            var color = new SimpleColor(0.2f, 0.4f, 0.8f);
            SimpleColor parsed;
            Assert.IsTrue(KitColorUtility.TryParseHex(KitColorUtility.ToHex(color), out parsed));
            Assert.AreEqual(color.R, parsed.R, 0.005f);
            Assert.AreEqual(color.G, parsed.G, 0.005f);
            Assert.AreEqual(color.B, parsed.B, 0.005f);
        }

        [Test]
        public void ToJerseyDesign_StripesKit_MapsFields()
        {
            var design = KitDefinitionConverter.ToJerseyDesign(ValidKit());
            Assert.AreEqual(JerseyPattern.Stripes, design.Pattern);
            Assert.AreEqual(CollarStyle.VNeck, design.Collar);
            Assert.AreEqual(0x1F / 255f, design.PrimaryColor.R, 0.005f);
            Assert.IsTrue(JerseyValidation.IsDesignValid(design));
        }

        [Test]
        public void JerseyPattern_SashValue_MatchesShaderConstant()
        {
            // OpenFifaKit.shader: KIT_PATTERN_SASH 7, KIT_PATTERN_CHEVRON 6.
            Assert.AreEqual(7, (int)JerseyPattern.Sash);
            Assert.AreEqual(6, (int)JerseyPattern.Chevron);
            Assert.AreEqual(1, (int)KitMappingMode.MeshUV);
        }
    }
}
