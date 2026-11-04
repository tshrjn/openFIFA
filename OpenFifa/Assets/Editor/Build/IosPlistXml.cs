using System.Collections.Generic;
using System.Xml;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Minimal editor for XML property lists (the Info.plist Unity exports). It uses System.Xml only, so it
    /// does not depend on the iOS build module's UnityEditor.iOS.Xcode assembly and can be compiled anywhere.
    /// </summary>
    internal sealed class IosPlistXml
    {
        private readonly XmlDocument _doc;
        private readonly XmlElement _dict;

        private IosPlistXml(XmlDocument doc, XmlElement dict)
        {
            _doc = doc;
            _dict = dict;
        }

        public static IosPlistXml Load(string path)
        {
            // Keep the DOCTYPE but never fetch the external DTD.
            var doc = new XmlDocument { XmlResolver = null, PreserveWhitespace = false };
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = null };
            using (var reader = XmlReader.Create(path, settings))
            {
                doc.Load(reader);
            }
            var dict = doc.DocumentElement?["dict"];
            if (dict == null)
            {
                throw new XmlException($"{path} has no top-level <dict>");
            }
            return new IosPlistXml(doc, dict);
        }

        public void Save(string path)
        {
            var settings = new XmlWriterSettings { Indent = true, IndentChars = "\t", Encoding = new System.Text.UTF8Encoding(false) };
            using (var writer = XmlWriter.Create(path, settings))
            {
                _doc.Save(writer);
            }
        }

        public void SetBool(string key, bool value) => Replace(key, _doc.CreateElement(value ? "true" : "false"));

        public void SetString(string key, string value)
        {
            var e = _doc.CreateElement("string");
            e.InnerText = value;
            Replace(key, e);
        }

        public void SetStringArray(string key, IEnumerable<string> values)
        {
            var array = _doc.CreateElement("array");
            foreach (var v in values)
            {
                var e = _doc.CreateElement("string");
                e.InnerText = v;
                array.AppendChild(e);
            }
            Replace(key, array);
        }

        /// <summary>Text of a scalar value ("true"/"false" for booleans), or null when the key is absent.</summary>
        public string Get(string key)
        {
            var value = ValueOf(key);
            if (value == null)
            {
                return null;
            }
            return value.Name == "true" || value.Name == "false" ? value.Name : value.InnerText;
        }

        private XmlElement KeyNode(string key)
        {
            foreach (XmlNode n in _dict.ChildNodes)
            {
                if (n is XmlElement e && e.Name == "key" && e.InnerText == key)
                {
                    return e;
                }
            }
            return null;
        }

        private XmlElement ValueOf(string key)
        {
            var node = KeyNode(key)?.NextSibling;
            while (node != null && !(node is XmlElement))
            {
                node = node.NextSibling;
            }
            return node as XmlElement;
        }

        private void Replace(string key, XmlElement value)
        {
            var keyNode = KeyNode(key);
            if (keyNode == null)
            {
                keyNode = _doc.CreateElement("key");
                keyNode.InnerText = key;
                _dict.AppendChild(keyNode);
                _dict.AppendChild(value);
                return;
            }
            var old = ValueOf(key);
            if (old != null)
            {
                _dict.ReplaceChild(value, old);
            }
            else
            {
                _dict.InsertAfter(value, keyNode);
            }
        }
    }
}
