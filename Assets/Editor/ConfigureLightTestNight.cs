using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ConfigureLightTestNight
{
    const string ScenePath = "Assets/Scenes/LightTest_Scene.unity";
    const string ProfilePath = "Assets/Scenes/LightTest_NightVolume.asset";
    const string SkyPath = "Assets/Scenes/LightTest_NightSky.mat";

    [MenuItem("Tools/Lighting/Configure LightTest Night")]
    public static void Configure()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
        if (!sky)
        {
            sky = new Material(Shader.Find("Skybox/Procedural"));
            AssetDatabase.CreateAsset(sky, SkyPath);
        }
        sky.SetFloat("_SunDisk", 0);
        sky.SetFloat("_AtmosphereThickness", 0.25f);
        sky.SetColor("_SkyTint", new Color(0.24f, 0.31f, 0.42f));
        sky.SetColor("_GroundColor", new Color(0.025f, 0.033f, 0.045f));
        sky.SetFloat("_Exposure", 0.17f);
        EditorUtility.SetDirty(sky);

        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.095f, 0.125f, 0.17f);
        RenderSettings.ambientEquatorColor = new Color(0.055f, 0.073f, 0.10f);
        RenderSettings.ambientGroundColor = new Color(0.025f, 0.032f, 0.045f);
        RenderSettings.ambientIntensity = 0.38f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 0.22f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.025f, 0.034f, 0.050f);
        RenderSettings.fogDensity = 0.008f;

        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var light in lights)
        {
            if (light.type == LightType.Directional)
            {
                light.color = new Color(0.58f, 0.70f, 0.84f);
                light.intensity = 0.10f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.68f;
                RenderSettings.sun = light;
            }
            else if (light.type == LightType.Spot && light.intensity > 50f)
            {
                light.color = new Color(0.90f, 0.92f, 1f);
                light.intensity = 55f;
            }
            EditorUtility.SetDirty(light);
        }

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (!profile) throw new System.InvalidOperationException("Night volume profile is missing");

        var volume = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(v => v.gameObject.scene == scene && v.isGlobal);
        if (!volume)
        {
            var go = new GameObject("Night Atmosphere Volume");
            volume = go.AddComponent<Volume>();
        }
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);

        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera.gameObject.scene != scene) continue;
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(data);
        }

        foreach (var probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (probe.gameObject.scene == scene) Debug.Log("Existing reflection probe: " + probe.name);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Night setup saved. Lights: {lights.Length}; cameras: {Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
    }

}
