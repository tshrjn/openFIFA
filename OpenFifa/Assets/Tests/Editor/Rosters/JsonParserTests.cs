using NUnit.Framework;
using OpenFifa.Core;

namespace OpenFifa.Tests.Editor
{
    [TestFixture]
    [Category("FID_DATA")]
    public class JsonParserTests
    {
        [Test]
        public void Parse_NestedDocument_ReadsAllKinds()
        {
            var n = JsonParser.Parse("{\"a\": [1, -2.5, 3e2], \"b\": {\"c\": true, \"d\": null}, \"e\": \"x\\u00e9\\n\"}");
            Assert.AreEqual(3, n.Get("a").Count);
            Assert.AreEqual(-2.5, n.Get("a")[1].NumberValue, 1e-9);
            Assert.AreEqual(300.0, n.Get("a")[2].NumberValue, 1e-9);
            Assert.IsTrue(n.Get("b").GetBool("c", false));
            Assert.AreEqual(JsonNodeKind.Null, n.Get("b").Get("d").Kind);
            Assert.AreEqual("xé\n", n.GetString("e"));
        }

        [Test]
        public void Parse_KeepsMemberOrder()
        {
            var n = JsonParser.Parse("{\"z\":1,\"a\":2,\"m\":3}");
            Assert.AreEqual("z", n.Members[0].Key);
            Assert.AreEqual("m", n.Members[2].Key);
        }

        [Test]
        public void Parse_DuplicateKey_Throws()
        {
            Assert.Throws<JsonParseException>(() => JsonParser.Parse("{\"a\":1,\"a\":2}"));
        }

        [Test]
        public void Parse_TrailingComma_ThrowsWithPosition()
        {
            var ex = Assert.Throws<JsonParseException>(() => JsonParser.Parse("[1,\n 2,\n]"));
            Assert.AreEqual(3, ex.Line);
        }

        [Test]
        public void Serialize_RoundTrip_Equal()
        {
            const string text = "{\"a\":[1,2.5,\"q\\\"\"],\"b\":{\"c\":false,\"d\":null}}";
            Assert.AreEqual(text, JsonWriter.Serialize(JsonParser.Parse(text)));
        }
    }
}
