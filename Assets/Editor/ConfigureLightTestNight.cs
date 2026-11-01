using System.IO;
using System.Linq;
using PinchOfTheft.CCTV;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class ConfigureLightTestNight
{
    // Retained for existing editor integrations; always targets the active scene.
    public static void Configure() => ApplyToCurrentScene();

    [MenuItem("Tools/Lighting/Apply Night Lighting To Current Scene")]
    public static void ApplyToCurrentScene()
    {
        Apply(SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/Lighting/Apply Night Lighting To Current Scene", true)]
    public static bool CanApplyToCurrentScene() => !EditorApplication.isPlayingOrWillChangePlaymode && SceneManager.GetActiveScene().IsValid();

    public static void Apply(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) throw new System.InvalidOperationException("No loaded scene is active");
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
            throw new System.InvalidOperationException("Night lighting requires the project's existing URP pipeline");
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene != SceneManager.GetActiveScene())
            throw new System.InvalidOperationException("Apply Night requires the active scene in Edit Mode");
        if (string.IsNullOrEmpty(scene.path))
            throw new System.InvalidOperationException("Save the scene before applying Night Atmosphere");
        var nightSkyShader = Shader.Find("PinchOfTheft/NightSky");
        if (!nightSkyShader) throw new System.InvalidOperationException("Night sky shader has not been imported");
        // Keep the previously saved scene before changing its atmosphere.
        var backup = Path.Combine(".utmp", "NightBackups", AssetDatabase.AssetPathToGUID(scene.path), System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        Directory.CreateDirectory(backup);
        File.Copy(scene.path, Path.Combine(backup, Path.GetFileName(scene.path)));
        var folder = Path.GetDirectoryName(scene.path).Replace('\\', '/') + "/" + Path.GetFileNameWithoutExtension(scene.path) + "_Night";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Path.GetDirectoryName(scene.path).Replace('\\', '/'), Path.GetFileName(folder));
        var volumes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Volume>(true)).ToArray();
        var volume = volumes.Where(v => v.isGlobal && v.gameObject.activeInHierarchy)
            .OrderByDescending(v => v.name == "Night Atmosphere Volume").ThenByDescending(v => v.priority).FirstOrDefault();
        var profilePath = folder + "/NightVolume.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (!profile)
        {
            var source = volume != null ? volume.sharedProfile : null;
            var sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : "";
            if (!string.IsNullOrEmpty(sourcePath))
            {
                if (!AssetDatabase.CopyAsset(sourcePath, profilePath)) throw new IOException("Cannot copy volume profile");
                profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            }
            else
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                if (source != null) foreach (var component in source.components.Where(c => c != null))
                {
                    var copy = Object.Instantiate(component);
                    profile.components.Add(copy); AssetDatabase.AddObjectToAsset(copy, profile);
                }
            }
        }
        var skyPath = folder + "/NightSky.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (!sky) { sky = new Material(nightSkyShader); AssetDatabase.CreateAsset(sky, skyPath); }
        foreach (var asset in new[] { profilePath, skyPath })
            File.Copy(asset, Path.Combine(backup, Path.GetFileName(asset)), true);
        sky.shader = nightSkyShader;
        sky.SetColor("_ZenithColor", new Color(0.035f, 0.045f, 0.065f));
        sky.SetColor("_HorizonColor", new Color(0.075f, 0.09f, 0.115f));
        sky.SetColor("_GroundColor", new Color(0.025f, 0.03f, 0.04f));
        EditorUtility.SetDirty(sky);

        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        // Trilight uses these colours directly; ambientIntensity only scales skybox ambient.
        RenderSettings.ambientSkyColor = new Color(0.105f, 0.125f, 0.165f);
        RenderSettings.ambientEquatorColor = new Color(0.06f, 0.075f, 0.10f);
        RenderSettings.ambientGroundColor = new Color(0.03f, 0.04f, 0.055f);
        RenderSettings.ambientIntensity = 0.38f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 0.22f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.035f, 0.05f, 0.075f);
        RenderSettings.fogDensity = 0.010f;

        var lights = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Light>(true)).ToArray();
        var moon = RenderSettings.sun && RenderSettings.sun.type == LightType.Directional && RenderSettings.sun.gameObject.activeInHierarchy && lights.Contains(RenderSettings.sun) ? RenderSettings.sun :
            lights.FirstOrDefault(light => light.type == LightType.Directional && light.gameObject.activeInHierarchy);
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
            if (light == moon)
            {
                Undo.RecordObject(light, "Apply night lighting");
                Undo.RecordObject(light.transform, "Aim moon above horizon");
                light.transform.rotation = Quaternion.Euler(28f, -35f, 0f);
                light.color = new Color(0.66f, 0.76f, 0.94f);
                light.enabled = true;
                light.useColorTemperature = false;
                light.intensity = 0.035f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.85f;
                RenderSettings.sun = light;
            }
            else if (light.type == LightType.Directional)
            {
                Undo.RecordObject(light, "Disable additional daylight source");
                light.enabled = false;
            }
            // Tiny camera/status LEDs do not warrant six shadow maps each.
            // Retain their colour and intensity, which also convey gameplay state.
            else if (light.type == LightType.Point && light.intensity <= 0.1f)
            {
                Undo.RecordObject(light, "Remove indicator shadow atlas pressure");
                light.shadows = LightShadows.None;
            }
            else if (light.type == LightType.Spot && light.intensity > 1f)
            {
                Undo.RecordObject(light, "Soften practical light shadows");
                light.intensity = Mathf.Min(light.intensity, 8f);
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.85f;
            }
            if (PrefabUtility.IsPartOfPrefabInstance(light)) PrefabUtility.RecordPrefabInstancePropertyModifications(light);
            EditorUtility.SetDirty(light);
        }

        var color = GetOrAdd<ColorAdjustments>(profile);
        color.postExposure.Override(-0.85f);
        color.contrast.Override(5f);
        color.saturation.Override(-16f);
        color.colorFilter.Override(new Color(0.94f, 0.975f, 1f));
        var balance = GetOrAdd<WhiteBalance>(profile);
        balance.temperature.Override(-6f);
        balance.tint.Override(0f);
        var tone = GetOrAdd<Tonemapping>(profile);
        tone.mode.Override(TonemappingMode.Neutral);
        var ranges = GetOrAdd<ShadowsMidtonesHighlights>(profile);
        ranges.shadows.Override(new Vector4(0.985f, 0.995f, 1f, 0f));
        ranges.midtones.Override(new Vector4(1, 1, 1, 0));
        ranges.highlights.Override(new Vector4(1, 1, 1, 0));
        var bloom = GetOrAdd<Bloom>(profile);
        bloom.threshold.Override(1.25f);
        bloom.intensity.Override(0.18f);
        bloom.scatter.Override(0.5f);
        var vignette = GetOrAdd<Vignette>(profile);
        vignette.intensity.Override(0.10f);
        foreach (var component in profile.components) EditorUtility.SetDirty(component);
        EditorUtility.SetDirty(profile);

        if (!volume)
        {
            var go = new GameObject("Night Atmosphere Volume");
            SceneManager.MoveGameObjectToScene(go, scene);
            volume = go.AddComponent<Volume>();
            Undo.RegisterCreatedObjectUndo(go, "Create night atmosphere volume");
        }
        volume.isGlobal = true;
        Undo.RecordObject(volume, "Apply night volume");
        volume.enabled = true;
        volume.weight = 1f;
        volume.priority = Mathf.Max(10f, volumes.Where(v => v != volume).Select(v => v.priority + 1f).DefaultIfEmpty(10f).Max());
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);

        var cameras = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).ToArray();
        foreach (var camera in cameras)
        {
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = Undo.AddComponent<UniversalAdditionalCameraData>(camera.gameObject);
            if (data.renderType == CameraRenderType.Overlay) continue;
            Undo.RecordObject(camera, "Enable night post processing");
            Undo.RecordObject(data, "Enable night post processing");
            camera.allowHDR = true;
            data.renderPostProcessing = true;
            data.volumeLayerMask = data.volumeLayerMask.value | (1 << volume.gameObject.layer);
            if (camera.GetComponent<SurveillanceCameraEffects>() == null)
                Undo.AddComponent<SurveillanceCameraEffects>(camera.gameObject);
            if (PrefabUtility.IsPartOfPrefabInstance(camera)) PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            if (PrefabUtility.IsPartOfPrefabInstance(data)) PrefabUtility.RecordPrefabInstancePropertyModifications(data);
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(data);
        }

        foreach (var probe in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ReflectionProbe>(true)))
            Debug.Log("Existing reflection probe in " + scene.name + ": " + probe.name);
        if (PrefabUtility.IsPartOfPrefabInstance(volume)) PrefabUtility.RecordPrefabInstancePropertyModifications(volume);
        InstallCCTVAnalogFeature.Install();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Night setup saved to {scene.name}. Lights: {lights.Length}; cameras: {cameras.Length}");
    }

    private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet<T>(out var component)) return component;
        component = profile.Add<T>(true);
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }
}
