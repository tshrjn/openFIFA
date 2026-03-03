using UnityEditor;
using UnityEngine;
using System.IO;

namespace OpenFifa.Editor
{
    public static class TMPImportHelper
    {
        private static bool _importDone;

        public static void ImportEssentials()
        {
            string packagePath = Path.GetFullPath("Packages/com.unity.ugui");
            string unitypackage = packagePath + "/Package Resources/TMP Essential Resources.unitypackage";

            if (!File.Exists(unitypackage))
            {
                Debug.LogError($"[TMPImportHelper] Package not found at: {unitypackage}");
                EditorApplication.Exit(1);
                return;
            }

            _importDone = false;
            AssetDatabase.importPackageCompleted += OnImportComplete;
            AssetDatabase.importPackageFailed += OnImportFailed;

            Debug.Log($"[TMPImportHelper] Importing TMP Essentials from: {unitypackage}");
            AssetDatabase.ImportPackage(unitypackage, false);

            // Wait synchronously for the import to complete
            EditorApplication.update += WaitForImport;
        }

        private static void WaitForImport()
        {
            if (!_importDone) return;

            EditorApplication.update -= WaitForImport;
            AssetDatabase.importPackageCompleted -= OnImportComplete;
            AssetDatabase.importPackageFailed -= OnImportFailed;
            AssetDatabase.Refresh();

            bool success = File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            Debug.Log($"[TMPImportHelper] Import done. TMP Settings exists: {success}");
            EditorApplication.Exit(success ? 0 : 1);
        }

        private static void OnImportComplete(string packageName)
        {
            Debug.Log($"[TMPImportHelper] Import completed: {packageName}");
            _importDone = true;
        }

        private static void OnImportFailed(string packageName, string errorMessage)
        {
            Debug.LogError($"[TMPImportHelper] Import failed: {packageName} - {errorMessage}");
            _importDone = true;
        }
    }
}
