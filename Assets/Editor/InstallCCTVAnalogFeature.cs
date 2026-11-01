using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class InstallCCTVAnalogFeature
{
    [MenuItem("Tools/Lighting/Install CCTV Analog Effect")]
    public static void Install()
    {
        var shader = Shader.Find("Hidden/PinchOfTheft/CCTVAnalog");
        if (shader == null) throw new System.InvalidOperationException("CCTV shader has not been imported");
        var pipelines = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid)));
        foreach (var pipeline in pipelines)
        foreach (var data in pipeline.rendererDataList)
        {
            if (data == null) continue;
            var feature = data.rendererFeatures.OfType<CCTVAnalogRendererFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<CCTVAnalogRendererFeature>();
                feature.name = "CCTV Analog Surveillance";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
            }
            feature.SetActive(true);
            var serialized = new SerializedObject(feature);
            serialized.FindProperty("effectShader").objectReferenceValue = shader;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            feature.Create();
            data.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
    }
}
