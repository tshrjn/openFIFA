using System;
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
            var patched = AddExternalDisplayScene(xml, out var error);
            if (error != null)
            {
                Debug.LogError($"[ExternalDisplay] {error} in {plistPath}; Info.plist left unchanged, external monitors will mirror the iPad");
                return;
            }
            if (patched != xml)
            {
                File.WriteAllText(plistPath, patched);
                Debug.Log($"[ExternalDisplay] declared {ExternalRole} -> {DelegateClass} in {plistPath}");
            }
        }

        public const string ApplicationRole = "UIWindowSceneSessionRoleApplication";

        /// <summary>
        /// Returns the plist with an external-display scene configuration added next to the existing
        /// application scene configuration (UIApplicationSceneManifest/UISceneConfigurations). Idempotent.
        /// Refuses (returns the input unchanged and sets <paramref name="error"/>) when the plist has no
        /// application-role scene configuration: adding a scene manifest to an app-delegate-based plist would
        /// leave the main window without a scene and show a black screen.
        /// </summary>
        public static string AddExternalDisplayScene(string plistXml, out string error)
        {
            error = null;
            var doc = new XmlDocument { XmlResolver = null, PreserveWhitespace = false };
            using (var reader = XmlReader.Create(new StringReader(plistXml),
                       new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }))
            {
                doc.Load(reader);
            }

            var root = doc.SelectSingleNode("/plist/dict") as XmlElement;
            if (root == null)
            {
                error = "Info.plist has no top-level dict";
                return plistXml;
            }

            var manifest = FindValue(root, "UIApplicationSceneManifest") as XmlElement;
            var configs = manifest != null ? FindValue(manifest, "UISceneConfigurations") as XmlElement : null;
            if (configs == null || configs.Name != "dict" || !(FindValue(configs, ApplicationRole) is XmlElement appRole) ||
                appRole.Name != "array" || appRole.SelectSingleNode("dict") == null)
            {
                error = $"no {ApplicationRole} scene configuration (the Unity iOS shell is expected to be scene-based)";
                return plistXml;
            }
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
            var output = sb.ToString().Replace("encoding=\"utf-16\"", "encoding=\"UTF-8\"");

            // DtdProcessing.Ignore drops the plist DOCTYPE; put Apple's back so the file stays canonical.
            int doctype = plistXml.IndexOf("<!DOCTYPE", StringComparison.Ordinal);
            if (doctype >= 0 && output.IndexOf("<!DOCTYPE", StringComparison.Ordinal) < 0)
            {
                int end = plistXml.IndexOf('>', doctype);
                int declEnd = output.StartsWith("<?xml", StringComparison.Ordinal) ? output.IndexOf("?>", StringComparison.Ordinal) + 2 : 0;
                if (end > doctype)
                    output = output.Insert(declEnd, (declEnd > 0 ? "\n" : "") + plistXml.Substring(doctype, end - doctype + 1));
            }
            return output;
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
