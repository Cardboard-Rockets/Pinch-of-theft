using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace PinchOfTheft.Editor
{
    public static class VanModelAudit
    {
        [InitializeOnLoadMethod]
        private static void ScheduleAudit()
        {
            EditorApplication.delayCall += () =>
            {
                if (File.Exists("Logs/Intro-HouseAudit.request") && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    File.Delete("Logs/Intro-HouseAudit.request");
                    AuditHouse();
                }
                if (File.Exists("Logs/Intro-Audit.request") && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    File.Delete("Logs/Intro-Audit.request");
                    Run();
                }
            };
        }
        private static void AuditHouse()
        {
            bool usePreview=SceneManager.GetActiveScene().path!="Assets/Scenes/House.unity";
            var scene = usePreview?EditorSceneManager.OpenPreviewScene("Assets/Scenes/House.unity"):SceneManager.GetActiveScene();
            try
            {
                var report = new StringBuilder();
                report.AppendLine("Scene="+scene.path+" dirty="+scene.isDirty);
                var transforms=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
                if(SceneView.lastActiveSceneView!=null)
                {
                    var camera=SceneView.lastActiveSceneView.camera;
                    report.AppendLine("SCENEVIEW pos="+camera.transform.position.ToString("F5")+" rot="+camera.transform.eulerAngles.ToString("F5")+" fov="+camera.fieldOfView);
                }
                var vanGroup=scene.GetRootGameObjects().Single(g=>g.name=="drdr");
                foreach(var t in vanGroup.GetComponentsInChildren<Transform>(true))
                    report.AppendLine("GROUP "+PathOf(t)+" pos="+t.position.ToString("F5")+" rot="+t.eulerAngles.ToString("F5")+" scale="+t.lossyScale.ToString("F5")+" components="+string.Join(",",t.GetComponents<Component>().Select(c=>c==null?"MISSING":c.GetType().Name)));
                var actor=scene.GetRootGameObjects().Single(g=>g.name=="Damek");
                foreach(var t in actor.GetComponentsInChildren<Transform>(true))
                    report.AppendLine("ACTOR "+PathOf(t)+" pos="+t.position+" scale="+t.lossyScale+" mesh="+(t.GetComponent<MeshFilter>()!=null?t.GetComponent<MeshFilter>().sharedMesh.name:"none"));
                var body=vanGroup.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Van");
                var points=body.sharedMesh.vertices;var indices=body.sharedMesh.triangles;
                var dump=new StringBuilder("ax,ay,az,bx,by,bz,cx,cy,cz\n");
                for(int i=0;i<indices.Length;i+=3)
                    dump.AppendLine(string.Join(",",new[]{indices[i],indices[i+1],indices[i+2]}.SelectMany(n=>{var p=body.transform.TransformPoint(points[n]);return new[]{p.x,p.y,p.z};}).Select(n=>n.ToString("F6",System.Globalization.CultureInfo.InvariantCulture))));
                File.WriteAllText("Logs/Intro/VanBodyTriangles.csv",dump.ToString());
                var laptop=vanGroup.GetComponentsInChildren<Transform>().Single(t=>t.name=="Laptop");
                foreach(var f in laptop.GetComponentsInChildren<MeshFilter>())
                {
                    try
                    {
                        var vertices=f.sharedMesh.vertices;
                        report.AppendLine("LAPTOP_MESH "+PathOf(f.transform));
                        foreach(var v in vertices.Distinct())report.AppendLine("VERTEX "+f.transform.TransformPoint(v).ToString("F6"));
                    }
                    catch(System.Exception e){report.AppendLine(e.Message);}
                }
                foreach(var t in transforms.Where(t=>t.name=="Van" || t.name.StartsWith("Laptop") || t.GetComponent<Light>()!=null))
                {
                    report.AppendLine("DETAIL "+PathOf(t)+" active="+t.gameObject.activeInHierarchy+" pos="+t.position.ToString("F4")+" rotation="+t.eulerAngles.ToString("F3")+" scale="+t.lossyScale.ToString("F5"));
                    if(t.name=="Van")foreach(var child in t.GetComponentsInChildren<Transform>(true))
                    {
                        var renderer=child.GetComponent<Renderer>();
                        report.AppendLine("  VAN "+PathOf(child)+" pos="+child.position.ToString("F4")+" rotation="+child.eulerAngles.ToString("F3")+(renderer!=null?" bounds="+renderer.bounds:""));
                    }
                }
                foreach (var root in scene.GetRootGameObjects())
                {
                    var renderers = root.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) continue;
                    var bounds = renderers[0].bounds;
                    foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    report.AppendLine(root.name + " renderers=" + renderers.Length + " " + bounds);
                    if (root.name == "House")
                        foreach (var r in renderers) report.AppendLine("  " + PathOf(r.transform) + " " + r.bounds);
                }
                File.WriteAllText("Logs/Intro/HouseAudit.txt",report.ToString());
            }
            finally { if(usePreview)EditorSceneManager.ClosePreviewScene(scene); }
        }
        [MenuItem("PinchOfTheft/Intro/Audit Van Model")]
        public static void Run()
        {
            var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/Van/Van.fbx"));
            model.hideFlags = HideFlags.HideAndDontSave;
            var report = new StringBuilder();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                var filter = t.GetComponent<MeshFilter>();
                var renderer = t.GetComponent<Renderer>();
                report.AppendLine($"{PathOf(t)} pos={t.localPosition:F4} rot={t.localEulerAngles:F2} scale={t.localScale:F4}" +
                    (filter != null ? $" mesh={filter.sharedMesh.name} vertices={filter.sharedMesh.vertexCount} bounds={filter.sharedMesh.bounds}" : "") +
                    (renderer != null ? $" worldBounds={renderer.bounds} materials={string.Join(",", renderer.sharedMaterials.Select(m => m == null ? "NULL" : m.name))}" : ""));
            }
            Directory.CreateDirectory("Logs/Intro");
            File.WriteAllText("Logs/Intro/VanModelAudit.txt", report.ToString());
            var preview = EditorSceneManager.NewPreviewScene();
            SceneManager.MoveGameObjectToScene(model, preview);
            var lightObject = new GameObject("Audit light");
            SceneManager.MoveGameObjectToScene(lightObject, preview);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(35, -35, 0);
            var cameraObject = new GameObject("Audit camera");
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.AddComponent<Camera>(); camera.scene = preview;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.22f, .27f, .32f);
            camera.nearClipPlane = .03f;
            var target = new Vector3(1, 1.5f, 0);
            var positions = new[] {new Vector3(-10, 4, 11),new Vector3(12,4,-11),new Vector3(-10,3,-10),new Vector3(1,1.3f,-.5f)};
            for (int i = 0; i < positions.Length; i++)
            {
                camera.transform.position = positions[i];
                camera.transform.LookAt(i == 3 ? new Vector3(1,1.2f,10) : target);
                var rt = new RenderTexture(960,540,24); camera.targetTexture = rt;
                camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
                var texture = new Texture2D(960,540,TextureFormat.RGB24,false);
                texture.ReadPixels(new Rect(0,0,960,540),0,0); texture.Apply();
                File.WriteAllBytes($"Logs/Intro/VanAudit-{i}.png", texture.EncodeToPNG());
                RenderTexture.active = previous; camera.targetTexture = null;
                Object.DestroyImmediate(texture); Object.DestroyImmediate(rt);
            }
            Object.DestroyImmediate(model);
            EditorSceneManager.ClosePreviewScene(preview);
            Debug.Log("VAN_MODEL_AUDIT_PASSED\n" + report);
        }
        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    }
}
