using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class ConfigureLightTestNight
{
    const string ScenePath = "Assets/Scenes/LightTest_Scene.unity";
    const string ProfilePath = "Assets/Scenes/LightTest_NightVolume.asset";
    const string SkyPath = "Assets/Scenes/LightTest_NightSky.mat";

    [MenuItem("Tools/Lighting/Configure LightTest Night")]
    public static void Configure()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Apply(scene);
    }

    [MenuItem("Tools/Lighting/Apply Night Lighting To Current Scene")]
    public static void ApplyToCurrentScene()
    {
        Apply(SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Lighting/Apply Night Lighting To Current Scene", true)]
    public static bool CanApplyToCurrentScene() => !EditorApplication.isPlayingOrWillChangePlaymode && SceneManager.GetActiveScene().IsValid();

    private static void Apply(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) throw new System.InvalidOperationException("No loaded scene is active");
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

        var lights = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true)).ToArray();
        var moon = lights.FirstOrDefault(light => light.type == LightType.Directional);
        if (!moon)
        {
            var moonObject = new GameObject("Night Directional Light");
            SceneManager.MoveGameObjectToScene(moonObject, scene);
            moonObject.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
            moon = moonObject.AddComponent<Light>();
            moon.type = LightType.Directional;
            Undo.RegisterCreatedObjectUndo(moonObject, "Create night directional light");
            lights = lights.Append(moon).ToArray();
        }
        foreach (var light in lights)
        {
            if (light.type == LightType.Directional)
            {
                Undo.RecordObject(light, "Apply night lighting");
                light.color = new Color(0.58f, 0.70f, 0.84f);
                light.intensity = 0.10f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.68f;
                RenderSettings.sun = light;
            }
            EditorUtility.SetDirty(light);
        }

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (!profile) throw new System.InvalidOperationException("Night volume profile is missing");

        var volume = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Volume>(true))
            .FirstOrDefault(v => v.isGlobal && (v.name == "Night Atmosphere Volume" || v.sharedProfile == profile));
        if (!volume)
        {
            var go = new GameObject("Night Atmosphere Volume");
            SceneManager.MoveGameObjectToScene(go, scene);
            volume = go.AddComponent<Volume>();
            Undo.RegisterCreatedObjectUndo(go, "Create night atmosphere volume");
        }
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);

        var cameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
        foreach (var camera in cameras)
        {
            var data = camera.GetUniversalAdditionalCameraData();
            if (data.renderType == CameraRenderType.Overlay) continue;
            Undo.RecordObject(camera, "Enable night post processing");
            Undo.RecordObject(data, "Enable night post processing");
            camera.allowHDR = true;
            data.renderPostProcessing = true;
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(data);
        }

        foreach (var probe in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ReflectionProbe>(true)))
            Debug.Log("Existing reflection probe in " + scene.name + ": " + probe.name);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Night setup saved to {scene.name}. Lights: {lights.Length}; cameras: {cameras.Length}");
    }

}
