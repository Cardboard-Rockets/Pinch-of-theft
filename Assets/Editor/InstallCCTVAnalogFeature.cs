using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class InstallCCTVAnalogFeature
{
    [MenuItem("Tools/Lighting/Install CCTV Analog Effect")]
    public static void Install()
    {
        foreach (var path in new[] { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" })
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (!data) throw new System.InvalidOperationException("Renderer missing: " + path);
            if (data.rendererFeatures.OfType<CCTVAnalogRendererFeature>().Any()) continue;
            var feature = ScriptableObject.CreateInstance<CCTVAnalogRendererFeature>();
            feature.name = "CCTV Analog Surveillance";
            AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            Debug.Log("Installed CCTV effect on " + path);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
