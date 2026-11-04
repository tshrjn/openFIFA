using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OpenFifa.Editor
{
    /// <summary>
    /// Declares an external-display scene in the exported iOS Info.plist so iPadOS hands a connected
    /// monitor to the game (Plugins/iOS/OpenFootballExternalDisplay.mm) instead of mirroring the iPad.
    /// Runs after every iOS export, including IosBuild's CI exports.
    /// </summary>
    public sealed class ExternalDisplayPostprocess : IPostprocessBuildWithReport
    {
        public const string ExternalRole = "UIWindowSceneSessionRoleExternalDisplayNonInteractive";
        public const string ConfigurationName = "External Display";
        public const string DelegateClass = "OFExternalDisplayScene";

        public int callbackOrder => 200;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            var plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
            if (!File.Exists(plistPath))
            {
                Debug.LogWarning($"[ExternalDisplay] no Info.plist at {plistPath}; external monitors will mirror the iPad");
                return;
            }
            var xml = File.ReadAllText(plistPath);
            var patched = AddExternalDisplayScene(xml);
            if (patched != xml)
            {
                File.WriteAllText(plistPath, patched);
                Debug.Log($"[ExternalDisplay] declared {ExternalRole} -> {DelegateClass} in {plistPath}");
            }
        }

        /// <summary>
        /// Returns the plist with an external-display scene configuration added under
        /// UIApplicationSceneManifest/UISceneConfigurations. Idempotent; creates missing dictionaries.
        /// </summary>
        public static string AddExternalDisplayScene(string plistXml)
        {
            var doc = new XmlDocument { XmlResolver = null, PreserveWhitespace = false };
            using (var reader = XmlReader.Create(new StringReader(plistXml),
                       new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }))
            {
                doc.Load(reader);
            }

            var root = doc.SelectSingleNode("/plist/dict") as XmlElement;
            if (root == null) return plistXml;

            var manifest = GetOrAddDict(doc, root, "UIApplicationSceneManifest");
            var configs = GetOrAddDict(doc, manifest, "UISceneConfigurations");
            if (FindValue(configs, ExternalRole) != null) return plistXml;

            var entry = doc.CreateElement("dict");
            AddString(doc, entry, "UISceneConfigurationName", ConfigurationName);
            AddString(doc, entry, "UISceneDelegateClassName", DelegateClass);
            var array = doc.CreateElement("array");
            array.AppendChild(entry);
            AddKey(doc, configs, ExternalRole);
            configs.AppendChild(array);

            var sb = new System.Text.StringBuilder();
            using (var writer = XmlWriter.Create(sb, new XmlWriterSettings
                   {
                       Indent = true,
                       IndentChars = "\t",
                       Encoding = new System.Text.UTF8Encoding(false),
                   }))
            {
                doc.Save(writer);
            }
            return sb.ToString().Replace("encoding=\"utf-16\"", "encoding=\"UTF-8\"");
        }

        private static XmlElement GetOrAddDict(XmlDocument doc, XmlElement parent, string key)
        {
            if (FindValue(parent, key) is XmlElement existing && existing.Name == "dict") return existing;
            var dict = doc.CreateElement("dict");
            AddKey(doc, parent, key);
            parent.AppendChild(dict);
            return dict;
        }

        private static XmlNode FindValue(XmlElement dict, string key)
        {
            for (var node = dict.FirstChild; node != null; node = node.NextSibling)
            {
                if (node.Name == "key" && node.InnerText == key)
                {
                    var value = node.NextSibling;
                    while (value != null && value.NodeType != XmlNodeType.Element) value = value.NextSibling;
                    return value;
                }
            }
            return null;
        }

        private static void AddKey(XmlDocument doc, XmlElement dict, string key)
        {
            var k = doc.CreateElement("key");
            k.InnerText = key;
            dict.AppendChild(k);
        }

        private static void AddString(XmlDocument doc, XmlElement dict, string key, string value)
        {
            AddKey(doc, dict, key);
            var s = doc.CreateElement("string");
            s.InnerText = value;
            dict.AppendChild(s);
        }
    }
}
