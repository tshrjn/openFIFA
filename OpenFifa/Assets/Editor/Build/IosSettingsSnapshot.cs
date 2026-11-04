using UnityEditor;
using UnityEditor.Build;
using UnityEngine.Rendering;

namespace OpenFifa.Editor
{
    /// <summary>
    /// The iOS player settings <see cref="IosBuild"/> overrides, captured before an export and put back
    /// afterwards so a build never leaves ProjectSettings/ProjectSettings.asset modified.
    /// </summary>
    internal sealed class IosSettingsSnapshot
    {
        private string _bundleId;
        private string _bundleVersion;
        private string _productName;
        private string _buildNumber;
        private iOSTargetDevice _targetDevice;
        private UIOrientation _orientation;
        private bool _landscapeLeft;
        private bool _landscapeRight;
        private bool _portrait;
        private bool _portraitUpsideDown;
        private bool _requiresFullScreen;
        private string _targetOs;
        private ScriptingImplementation _backend;
        private int _architecture;
        private Il2CppCompilerConfiguration _il2cppConfig;
        private Il2CppCodeGeneration _il2cppCodeGen;
        private bool _defaultGraphics;
        private GraphicsDeviceType[] _graphics;
        private iOSSdkVersion _sdk;
        private AppleMobileArchitectureSimulator _simArch;
        private bool _autoSigning;
        private ProvisioningProfileType _profileType;
        private string _teamId;
        private XcodeBuildConfig _xcodeConfig;

        public static IosSettingsSnapshot Capture()
        {
            var ios = NamedBuildTarget.iOS;
            return new IosSettingsSnapshot
            {
                _bundleId = PlayerSettings.GetApplicationIdentifier(ios),
                _bundleVersion = PlayerSettings.bundleVersion,
                _productName = PlayerSettings.productName,
                _buildNumber = PlayerSettings.iOS.buildNumber,
                _targetDevice = PlayerSettings.iOS.targetDevice,
                _orientation = PlayerSettings.defaultInterfaceOrientation,
                _landscapeLeft = PlayerSettings.allowedAutorotateToLandscapeLeft,
                _landscapeRight = PlayerSettings.allowedAutorotateToLandscapeRight,
                _portrait = PlayerSettings.allowedAutorotateToPortrait,
                _portraitUpsideDown = PlayerSettings.allowedAutorotateToPortraitUpsideDown,
                _requiresFullScreen = PlayerSettings.iOS.requiresFullScreen,
                _targetOs = PlayerSettings.iOS.targetOSVersionString,
                _backend = PlayerSettings.GetScriptingBackend(ios),
                _architecture = PlayerSettings.GetArchitecture(ios),
                _il2cppConfig = PlayerSettings.GetIl2CppCompilerConfiguration(ios),
                _il2cppCodeGen = PlayerSettings.GetIl2CppCodeGeneration(ios),
                _defaultGraphics = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.iOS),
                _graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS),
                _sdk = PlayerSettings.iOS.sdkVersion,
                _simArch = PlayerSettings.iOS.simulatorSdkArchitecture,
                _autoSigning = PlayerSettings.iOS.appleEnableAutomaticSigning,
                _profileType = PlayerSettings.iOS.iOSManualProvisioningProfileType,
                _teamId = PlayerSettings.iOS.appleDeveloperTeamID,
                _xcodeConfig = EditorUserBuildSettings.iOSXcodeBuildConfig,
            };
        }

        public void Restore()
        {
            var ios = NamedBuildTarget.iOS;
            PlayerSettings.SetApplicationIdentifier(ios, _bundleId);
            PlayerSettings.bundleVersion = _bundleVersion;
            PlayerSettings.productName = _productName;
            PlayerSettings.iOS.buildNumber = _buildNumber;
            PlayerSettings.iOS.targetDevice = _targetDevice;
            PlayerSettings.defaultInterfaceOrientation = _orientation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = _landscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeRight = _landscapeRight;
            PlayerSettings.allowedAutorotateToPortrait = _portrait;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = _portraitUpsideDown;
            PlayerSettings.iOS.requiresFullScreen = _requiresFullScreen;
            PlayerSettings.iOS.targetOSVersionString = _targetOs;
            PlayerSettings.SetScriptingBackend(ios, _backend);
            PlayerSettings.SetArchitecture(ios, _architecture);
            PlayerSettings.SetIl2CppCompilerConfiguration(ios, _il2cppConfig);
            PlayerSettings.SetIl2CppCodeGeneration(ios, _il2cppCodeGen);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, _defaultGraphics);
            if (!_defaultGraphics && _graphics != null && _graphics.Length > 0)
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, _graphics);
            }
            PlayerSettings.iOS.sdkVersion = _sdk;
            PlayerSettings.iOS.simulatorSdkArchitecture = _simArch;
            PlayerSettings.iOS.appleEnableAutomaticSigning = _autoSigning;
            PlayerSettings.iOS.iOSManualProvisioningProfileType = _profileType;
            PlayerSettings.iOS.appleDeveloperTeamID = _teamId;
            EditorUserBuildSettings.iOSXcodeBuildConfig = _xcodeConfig;
        }
    }
}
