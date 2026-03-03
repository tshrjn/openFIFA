using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SetupURPPipeline
{
    public static void Execute()
    {
        // Create URP pipeline asset
        var pipelineAsset = UniversalRenderPipelineAsset.Create();

        // Configure it
        // Use SerializedObject to set properties
        var so = new SerializedObject(pipelineAsset);

        // Enable HDR
        var hdrProp = so.FindProperty("m_SupportsHDR");
        if (hdrProp != null) hdrProp.boolValue = true;

        // Shadow distance
        var shadowDistProp = so.FindProperty("m_ShadowDistance");
        if (shadowDistProp != null) shadowDistProp.floatValue = 80f;

        // Main light shadows
        var mainLightShadows = so.FindProperty("m_MainLightRenderingMode");
        if (mainLightShadows != null) mainLightShadows.intValue = 1; // Per pixel

        // Additional lights
        var additionalLights = so.FindProperty("m_AdditionalLightsRenderingMode");
        if (additionalLights != null) additionalLights.intValue = 1; // Per pixel

        // Additional light shadows
        var addLightShadows = so.FindProperty("m_AdditionalLightsShadowsEnabled");
        if (addLightShadows != null) addLightShadows.boolValue = true;

        // Max additional lights per object
        var maxLights = so.FindProperty("m_AdditionalLightsPerObjectLimit");
        if (maxLights != null) maxLights.intValue = 8;

        // Shadow resolution
        var shadowRes = so.FindProperty("m_MainLightShadowmapResolution");
        if (shadowRes != null) shadowRes.intValue = 2048;

        // MSAA
        var msaa = so.FindProperty("m_MSAA");
        if (msaa != null) msaa.intValue = 2; // 2x MSAA

        so.ApplyModifiedPropertiesWithoutUndo();

        // Save the asset
        string assetPath = "Assets/Settings/URPPipelineAsset.asset";

        // Ensure directory exists
        if (!AssetDatabase.IsValidFolder("Assets/Settings"))
            AssetDatabase.CreateFolder("Assets", "Settings");

        AssetDatabase.CreateAsset(pipelineAsset, assetPath);

        // Also save the renderer that was created
        var renderers = pipelineAsset.rendererDataList;
        if (renderers != null && renderers.Length > 0 && renderers[0] != null)
        {
            AssetDatabase.CreateAsset(renderers[0], "Assets/Settings/URPRendererData.asset");
        }

        AssetDatabase.SaveAssets();

        // Assign to Graphics Settings
        GraphicsSettings.defaultRenderPipeline = pipelineAsset;

        // Assign to all Quality levels
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.renderPipeline = pipelineAsset;
        }

        Debug.Log($"[SetupURP] Created and assigned URP pipeline asset at {assetPath}");
        Debug.Log($"[SetupURP] HDR: {pipelineAsset.supportsHDR}, Shadows: {pipelineAsset.shadowDistance}m");
    }
}
