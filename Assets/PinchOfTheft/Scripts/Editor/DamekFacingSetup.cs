using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PinchOfTheft.Editor
{
    public static class DamekFacingSetup
    {
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            var player=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="Damek");
            var rootPosition=player.localPosition;var rootRotation=player.localRotation;
            var marker=player.Find("VisualFacing");
            var children=new[]{player.Find("Body"),player.Find("Wheel")};
            if(children.Any(t=>t==null))throw new System.Exception("Body/Wheel не найдены однозначно");
            var expected=new Quaternion[2];
            for(int i=0;i<children.Length;i++)
            {
                children[i].localRotation=Quaternion.AngleAxis(90f,Vector3.up)*children[i].localRotation;
                expected[i]=children[i].localRotation;
                PrefabUtility.RecordPrefabInstancePropertyModifications(children[i]);
            }
            if(marker!=null)marker.localRotation=Quaternion.identity;
            var agent=player.GetComponent<UnityEngine.AI.NavMeshAgent>();agent.updateRotation=true;
            PrefabUtility.RecordPrefabInstancePropertyModifications(agent);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            player=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="Damek");
            children=new[]{player.Find("Body"),player.Find("Wheel")};
            for(int i=0;i<children.Length;i++)if(Quaternion.Angle(children[i].localRotation,expected[i])>.01f)throw new System.Exception("Поворот модели не сохранился");
            if(player.localPosition!=rootPosition||Quaternion.Angle(player.localRotation,rootRotation)>.01f)throw new System.Exception("Изменён корневой Transform Damek");
            PrefabSetup.Validate();Debug.Log("DAMEK_BODY_AND_WHEEL_RIGHT_90_SAVED_AND_RELOADED");
        }
        public static void Audit()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            var player=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="Damek");
            foreach(var t in player.GetComponentsInChildren<Transform>(true))Debug.Log("FACING_CHILD "+t.name+" parent="+t.parent?.name+" rotation="+t.localEulerAngles+" position="+t.localPosition);
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D/D.A.M.E.K/Damek.fbx"));
            var preview=new PreviewRenderUtility();preview.AddSingleGO(model);
            var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=1000;preview.camera.orthographic=true;preview.camera.orthographicSize=bounds.extents.y*1.2f;
            preview.lights[0].intensity=1.5f;preview.lights[0].transform.rotation=Quaternion.Euler(30,30,0);preview.lights[1].intensity=1f;
            var directions=new[]{Vector3.forward,Vector3.right,Vector3.back,Vector3.left};
            for(int i=0;i<4;i++)
            {
                preview.BeginPreview(new Rect(0,0,512,512),GUIStyle.none);
                preview.camera.transform.position=bounds.center+directions[i]*bounds.size.magnitude*2;
                preview.camera.transform.LookAt(bounds.center);preview.Render(true);
                var texture=preview.EndPreview();var rt=RenderTexture.GetTemporary(512,512,0);Graphics.Blit(texture,rt);
                var prior=RenderTexture.active;RenderTexture.active=rt;var png=new Texture2D(512,512,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,512,512),0,0);png.Apply();
                System.IO.File.WriteAllBytes("Logs/DamekFacing"+i+".png",png.EncodeToPNG());RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(png);
            }
            preview.Cleanup();
        }
    }
}
