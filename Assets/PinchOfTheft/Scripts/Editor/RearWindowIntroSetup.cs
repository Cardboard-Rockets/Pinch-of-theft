using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PinchOfTheft.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace PinchOfTheft.Editor
{
    public static class RearWindowIntroSetup
    {
        private const string Output="Assets/PinchOfTheft/Intro";
        private static readonly Dictionary<Mesh,Mesh> Meshes=new();
        private static int meshIndex;

        [InitializeOnLoadMethod]
        private static void Schedule()
        {
            EditorApplication.delayCall+=Process;
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=Process;};
        }
        private static void Process()
        {
            const string request="Logs/Intro-RearWindow.request";
            if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete(request);
            try { Build();CaptureAndCheck();File.WriteAllText("Logs/Intro/RearWindow.result","REAR_WINDOW_BUILD_PASSED"); }
            catch(Exception e){Debug.LogException(e);File.WriteAllText("Logs/Intro/RearWindow.result",e.ToString());}
        }

        [MenuItem("PinchOfTheft/Intro/Build House Parking And Laptop Route")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save the current scene first.");
            Directory.CreateDirectory(Output+"/SiteMeshes");Directory.CreateDirectory("Logs/Intro");AssetDatabase.Refresh();
            if(File.Exists(VanIntroSetup.ScenePath))AssetDatabase.CopyAsset(VanIntroSetup.ScenePath,AssetDatabase.GenerateUniqueAssetPath(Output+"/BeforeRearWindow.unity"));
            var previousScene=SceneManager.GetActiveScene();
            bool sourceIsPreview=previousScene.path!="Assets/Scenes/House.unity";
            var source=sourceIsPreview?EditorSceneManager.OpenPreviewScene("Assets/Scenes/House.unity"):SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);Meshes.Clear();meshIndex=0;
            string temporary=AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/Intro_RearWindowBuild.unity");
            try
            {
                new GameObject("Site_RearWindowOpening");
                var sourceMobile=source.GetRootGameObjects().Single(g=>g.name=="drdr");
                var sourceBody=sourceMobile.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Van");
                Vector3 parked=sourceBody.transform.position;
                Quaternion travelRotation=Quaternion.Euler(0,90,0);
                Quaternion inverse=Quaternion.Inverse(travelRotation);
                Vector3 Local(Vector3 world)=>inverse*(world-parked);
                var parkingTarget=new GameObject("ParkingTarget_FromHouse").transform;
                parkingTarget.SetPositionAndRotation(parked,travelRotation);
                var vehicle=new GameObject("Van_TravelRoot").transform;
                vehicle.SetPositionAndRotation(parked-Vector3.right*106.75f,travelRotation);
                var site=new GameObject("Site_HouseAndRoad").transform;
                foreach(var root in source.GetRootGameObjects())
                {
                    if(root==sourceMobile)continue;
                    var parent=new GameObject(root.name).transform;parent.SetParent(site,false);
                    foreach(var filter in root.GetComponentsInChildren<MeshFilter>(false))
                    {
                        var renderer=filter.GetComponent<MeshRenderer>();
                        if(renderer==null||filter.sharedMesh==null||renderer.bounds.center.z>15)continue;
                        CopyMesh(filter,parent,filter.transform.position,filter.transform.rotation);
                    }
                }
                var wheels=new List<VanIntroSequence.Wheel>();Renderer body=null;
                foreach(var filter in sourceMobile.GetComponentsInChildren<MeshFilter>(false))
                {
                    var obj=CopyMesh(filter,vehicle,Local(filter.transform.position),inverse*filter.transform.rotation);
                    if(filter==sourceBody)body=obj.GetComponent<Renderer>();
                    if(filter.name.StartsWith("Cylinder",StringComparison.Ordinal))
                    {
                        var bounds=filter.GetComponent<Renderer>().bounds;
                        wheels.Add(new VanIntroSequence.Wheel{transform=obj.transform,restRotation=obj.transform.localRotation,localAxis=Vector3.forward,radius=(bounds.extents.x+bounds.extents.y)*.5f});
                    }
                }
                if(body==null||wheels.Count!=4)throw new Exception("Source van body/wheels are missing.");
                BuildOriginalRearDoorExterior(sourceBody,body,vehicle,Local);
                foreach(var light in sourceMobile.GetComponentsInChildren<Light>(false))
                {
                    var obj=new GameObject(light.name);obj.transform.SetParent(vehicle,false);
                    obj.transform.localPosition=Local(light.transform.position);obj.transform.localRotation=inverse*light.transform.rotation;
                    var copy=obj.AddComponent<Light>();EditorUtility.CopySerialized(light,copy);copy.shadows=LightShadows.None;
                    if(copy.type==LightType.Spot){copy.intensity=20;copy.range=45;}
                }
                BuildLaptopSupport(source,sourceBody,vehicle,Local);
                SetupNight();
                var bounce=new GameObject("Soft cabin bounce").transform;bounce.SetParent(vehicle,false);bounce.localPosition=new Vector3(.1f,1.2f,-.45f);
                var cabinLight=bounce.gameObject.AddComponent<Light>();cabinLight.type=LightType.Point;cabinLight.color=new Color(.49f,.62f,.56f);cabinLight.range=4;cabinLight.intensity=.30f;
                var rim=new GameObject("Moon bounce on tyres").transform;rim.SetParent(vehicle,false);rim.localPosition=new Vector3(-2.8f,1.4f,.4f);
                var rimLight=rim.gameObject.AddComponent<Light>();rimLight.type=LightType.Point;rimLight.range=6;rimLight.color=new Color(.25f,.36f,.70f);rimLight.intensity=.8f;
                var rearFill=new GameObject("Moon bounce on rear doors").transform;rearFill.SetParent(vehicle,false);rearFill.localPosition=new Vector3(.3f,1.4f,-4.7f);
                var rearLight=rearFill.gameObject.AddComponent<Light>();rearLight.type=LightType.Point;rearLight.range=5;rearLight.color=new Color(.4f,.5f,.7f);rearLight.intensity=1.5f;

                var camera=new GameObject("Intro_Camera").AddComponent<Camera>();camera.transform.SetParent(vehicle,false);
                camera.tag="MainCamera";camera.nearClipPlane=.015f;camera.farClipPlane=180;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.002f,.004f,.009f);camera.gameObject.AddComponent<AudioListener>();
                var wipe=Canvas("Window_Transition",camera,Color.black,null);
                // Source screen corners on the laptop already placed inside drdr.
                Vector3 bl=new Vector3(-.342898f,1.388903f,-6.834481f),br=new Vector3(-.341230f,1.390920f,-6.387667f);
                Vector3 tl=new Vector3(-.166649f,1.610102f,-6.835241f),tr=new Vector3(-.164195f,1.613106f,-6.389411f);
                Vector3 screenCentre=(bl+br+tl+tr)*.25f,screenUp=(tl-bl).normalized;
                Vector3 screenNormal=Vector3.Cross(br-bl,tl-bl).normalized;
                var front=wheels.Where(w=>w.transform.localPosition.x<0).OrderByDescending(w=>w.transform.localPosition.z).First().transform.localPosition;
                var rear=wheels.Where(w=>w.transform.localPosition.x<0).OrderBy(w=>w.transform.localPosition.z).First().transform.localPosition;
                Vector3 first=front+new Vector3(-1.45f,-.10f,1);
                Vector3 third=front+new Vector3(-1.65f,.02f,-.30f);
                Vector3 window=new Vector3(-.55f,.90f,.35f);
                Vector3 frontView=Local(new Vector3(1.45f,2.02f,-6.35f));
                Vector3 rearA=Local(new Vector3(-.70f,1.85f,-6.95f));
                Vector3 rearB=Local(new Vector3(-.70f,1.83f,-7.00f));
                Vector3 houseTarget=Local(new Vector3(.66f,3.6f,-22.47f));
                Vector3 laptopWide=Local(new Vector3(-.85f,1.83f,-6.92f));
                Vector3 laptopClose=Local(screenCentre+screenNormal*.065f);
                Vector3 screen=Local(screenCentre);
                Vector3 outside=new Vector3(-1.40f,.66f,.90f);
                Vector3 backCorner=new Vector3(-1.40f,.66f,-2.90f);
                Vector3 backDoor=new Vector3(-.05f,.66f,-3.55f);
                Vector3 doorLook=new Vector3(.3f,.35f,-1.83f);
                var shots=new[]{
                    Shot("01 / front wheel",4,first,first+new Vector3(.08f,.05f,-.14f),front,front,48,46),
                    Shot("02 / rear wheel",3,rear+new Vector3(-1.35f,.02f,.5f),rear+new Vector3(-1.25f,.04f,.28f),rear,rear,45,43),
                    Shot("03 / front wheel wide",3,third,third+new Vector3(-.15f,.15f,.16f),front+Vector3.up*.15f,front+Vector3.up*.25f,53,53),
                    Shot("04 / rise to van window",5,third+new Vector3(-.15f,.15f,.16f),window,front+Vector3.up*.25f,new Vector3(.3f,.9f,.35f),53,57),
                    Shot("05 / curbside braking",3,frontView,frontView,frontView+Vector3.forward*10,frontView+Vector3.forward*10,62,62),
                    Shot("06 / straight ahead exit fade",1,frontView,frontView,frontView+Vector3.forward*10,frontView+Vector3.forward*10,62,62),
                    Shot("06b / outside first person",1,outside,outside,outside+Vector3.back*10,outside+Vector3.back*10,62,62),
                    Shot("06c / first person walk to rear",4,outside,backCorner,outside+Vector3.back*10,backCorner+Vector3.back*10,62,62,true),
                    Shot("06d / first person around corner",2,backCorner,backDoor,backCorner+Vector3.back*10,doorLook,62,62,true),
                    Shot("06e / rear door entry fade",1,backDoor,backDoor,doorLook,doorLook,62,62),
                    Shot("07 / seated beside window",2,rearA,rearB,houseTarget,houseTarget,62,58),
                    Shot("08 / seated house view",3,rearB,rearB,houseTarget,houseTarget,58,58),
                    Shot("09 / seated turn to laptop",3,rearB,laptopWide,houseTarget,screen,58,54),
                    Shot("10 / original laptop screen",4,laptopWide,laptopClose,screen,screen,54,50)
                };
                for(int i=5;i<shots.Length;i++)if(Vector3.Distance(shots[i].fromPosition,shots[i-1].toPosition)<.001f)shots[i].fromRotation=shots[i-1].toRotation;
                shots[shots.Length-1].toRotation=Quaternion.LookRotation(-(inverse*screenNormal),inverse*screenUp);
                var motor=vehicle.gameObject.AddComponent<AudioSource>();motor.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Output+"/VanMotor_Placeholder.wav");motor.loop=true;motor.playOnAwake=true;
                var lowPass=vehicle.gameObject.AddComponent<AudioLowPassFilter>();
                var sequence=new GameObject("Van_IntroSequence").AddComponent<VanIntroSequence>();
                sequence.Configure(vehicle,camera,shots,wheels.ToArray(),wipe,motor,lowPass,body);
                sequence.ConfigureFirstPersonRoute(new[]{15f,19f,27f},screen);
                EditorSceneManager.SaveScene(scene,temporary);AssetDatabase.SaveAssets();
            }
            finally
            {
                if(previousScene.IsValid()&&previousScene.isLoaded)SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene,true);
                if(sourceIsPreview)EditorSceneManager.ClosePreviewScene(source);
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            File.Copy(temporary,VanIntroSetup.ScenePath,true);AssetDatabase.ImportAsset(VanIntroSetup.ScenePath,ImportAssetOptions.ForceSynchronousImport);AssetDatabase.DeleteAsset(temporary);
            EditorSceneManager.OpenScene(VanIntroSetup.ScenePath);
        }

        private static void BuildOriginalRearDoorExterior(MeshFilter source,Renderer originalBody,Transform van,Func<Vector3,Vector3> local)
        {
            // The authored rear faces point inward. Preserve their positions and UVs,
            // adding only their outward-facing side with the very same material.
            var mesh=source.sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;var triangles=mesh.triangles;
            var exteriorVertices=new List<Vector3>();var exteriorNormals=new List<Vector3>();var exteriorUv=new List<Vector2>();var exteriorIndices=new List<int>();
            var inverse=Quaternion.Inverse(van.rotation);
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 a=local(source.transform.TransformPoint(vertices[triangles[i]])),b=local(source.transform.TransformPoint(vertices[triangles[i+1]])),c=local(source.transform.TransformPoint(vertices[triangles[i+2]]));
                var face=Vector3.Cross(b-a,c-a).normalized;
                if(Mathf.Max(a.z,Mathf.Max(b.z,c.z))>=-1.65f||face.z<=.5f)continue;
                foreach(int index in new[]{triangles[i],triangles[i+2],triangles[i+1]})
                {
                    exteriorIndices.Add(exteriorVertices.Count);
                    exteriorVertices.Add(local(source.transform.TransformPoint(vertices[index]))+Vector3.back*.0005f);
                    exteriorNormals.Add(-(inverse*source.transform.TransformDirection(normals[index])));
                    exteriorUv.Add(uv[index]);
                }
            }
            if(exteriorVertices.Count==0)throw new Exception("No original inward rear faces found.");
            var exterior=new Mesh{name="Original rear door exterior"};exterior.SetVertices(exteriorVertices);exterior.SetNormals(exteriorNormals);exterior.SetUVs(0,exteriorUv);exterior.SetTriangles(exteriorIndices,0);exterior.RecalculateBounds();
            string path=Output+"/SiteMeshes/OriginalRearDoorExterior.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null)AssetDatabase.CreateAsset(exterior,path);
            else{EditorUtility.CopySerialized(exterior,existing);Object.DestroyImmediate(exterior);exterior=existing;}
            var obj=new GameObject("Van_OriginalRearDoorExterior",typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(van,false);
            obj.GetComponent<MeshFilter>().sharedMesh=exterior;obj.GetComponent<MeshRenderer>().sharedMaterials=originalBody.sharedMaterials;
        }

        private static void BuildLaptopSupport(Scene source,MeshFilter body,Transform van,Func<Vector3,Vector3> local)
        {
            var root=new GameObject("Laptop_Worktable").transform;root.SetParent(van,false);
            var originalTable=source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).First(r=>r.name.StartsWith("Table3",StringComparison.Ordinal));
            var top=originalTable.sharedMaterial;
            var metal=top;
            const float contact=1.334581f,thickness=.055f;
            IntroVanAccessGeometry.Cube("Laptop_Tabletop",root,local(new Vector3(-.54f,contact-thickness*.5f,-6.585f)),new Vector3(.72f,thickness,.72f),top);
            foreach(float x in new[]{-.79f,-.29f})foreach(float z in new[]{-6.835f,-6.335f})
            {
                float floor=IntroVanAccessGeometry.SurfaceBelow(body,new Vector3(x,0,z),contact-thickness-.005f);
                float height=contact-thickness-floor;
                IntroVanAccessGeometry.Cube("Worktable leg",root,local(new Vector3(x,floor+height*.5f,z)),new Vector3(.045f,Mathf.Max(.005f,height),.045f),metal);
                IntroVanAccessGeometry.Cube("Worktable foot",root,local(new Vector3(x,floor+.008f,z)),new Vector3(.075f,.016f,.075f),metal);
            }
        }

        private static GameObject CopyMesh(MeshFilter source,Transform parent,Vector3 position,Quaternion rotation)
        {
            var obj=new GameObject(source.name);obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localRotation=rotation;obj.transform.localScale=source.transform.lossyScale;
            if(!Meshes.TryGetValue(source.sharedMesh,out var mesh))
            {
                string path=AssetDatabase.GetAssetPath(source.sharedMesh);
                if(path.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".asset",StringComparison.OrdinalIgnoreCase))mesh=source.sharedMesh;
                else
                {
                    mesh=Object.Instantiate(source.sharedMesh);
                    string target=Output+"/SiteMeshes/Site_"+(meshIndex++).ToString("000")+".asset";
                    var existing=AssetDatabase.LoadAssetAtPath<Mesh>(target);
                    if(existing!=null){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}
                    else AssetDatabase.CreateAsset(mesh,target);
                }
                Meshes[source.sharedMesh]=mesh;
            }
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterials=source.GetComponent<MeshRenderer>().sharedMaterials;
            return obj;
        }
        private static VanIntroSequence.Shot Shot(string name,float seconds,Vector3 a,Vector3 b,Vector3 ta,Vector3 tb,float fa,float fb,bool walk=false)
            =>new VanIntroSequence.Shot{label=name,duration=seconds,fromPosition=a,toPosition=b,fromRotation=Quaternion.LookRotation(ta-a),toRotation=Quaternion.LookRotation(tb-b),fromFov=fa,toFov=fb,walking=walk};

        private static void SetupNight()
        {
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.018f,.028f,.060f);RenderSettings.ambientEquatorColor=new Color(.009f,.014f,.027f);RenderSettings.ambientGroundColor=new Color(.003f,.004f,.008f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogDensity=.006f;RenderSettings.fogColor=new Color(.002f,.004f,.009f);
            var moon=new GameObject("Moonlight").AddComponent<Light>();moon.type=LightType.Directional;moon.color=new Color(.40f,.53f,1);moon.intensity=.22f;moon.shadows=LightShadows.Soft;moon.transform.rotation=Quaternion.Euler(30,-35,0);RenderSettings.sun=moon;
            var fill=new GameObject("Night sky fill").AddComponent<Light>();fill.type=LightType.Directional;fill.color=new Color(.36f,.45f,.65f);fill.intensity=.07f;fill.shadows=LightShadows.None;fill.transform.rotation=Quaternion.Euler(90,0,0);
            var porch=new GameObject("Porch light").AddComponent<Light>();porch.type=LightType.Point;porch.color=new Color(1,.6f,.3f);porch.intensity=4;porch.range=13;porch.transform.position=new Vector3(5.2f,3.8f,-20);
            var wash=new GameObject("Moon on facade").AddComponent<Light>();wash.type=LightType.Spot;wash.color=new Color(.35f,.46f,.8f);wash.intensity=14;wash.range=50;wash.spotAngle=95;wash.transform.position=new Vector3(14,12,-3);wash.transform.LookAt(new Vector3(4,5,-24));
        }
        private static CanvasGroup Canvas(string name,Camera camera,Color color,Texture texture)
        {
            var obj=new GameObject(name,typeof(Canvas),typeof(CanvasGroup));var canvas=obj.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.025f;canvas.sortingOrder=texture==null?100:110;
            var group=obj.GetComponent<CanvasGroup>();group.alpha=0;group.interactable=false;group.blocksRaycasts=false;
            var graphic=new GameObject("Image",typeof(RectTransform),typeof(RawImage));graphic.transform.SetParent(obj.transform,false);
            var image=graphic.GetComponent<RawImage>();image.color=color;image.texture=texture;image.raycastTarget=false;
            var rect=graphic.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            return group;
        }
        [MenuItem("PinchOfTheft/Intro/Render And Check Rear Window Route")]
        public static void CaptureAndCheck()
        {
            if(SceneManager.GetActiveScene().path!=VanIntroSetup.ScenePath)throw new InvalidOperationException("Open Intro_Van first.");
            var sequence=Object.FindAnyObjectByType<VanIntroSequence>();var target=GameObject.Find("ParkingTarget_FromHouse").transform;
            var road=GameObject.Find("Site_HouseAndRoad").transform.Find("road1");
            File.WriteAllText("Logs/Intro/SiteRoad.txt",string.Join("\n",road.GetComponentsInChildren<Renderer>().Select(r=>r.name+" "+r.bounds+" material="+string.Join(",",r.sharedMaterials.Select(m=>m.name)))));
            if(Mathf.Abs(sequence.Duration-39)>.001f)throw new Exception("Wrong route duration.");
            var times=new[]{0f,.1f,16f,18.5f,19f,19.5f,21f,23.5f,25f,26.5f,27f,27.5f,29f,31f,33.5f,35f,37f,39f};
            foreach(float t in times){sequence.Sample(t);Capture(sequence.View,"Logs/Intro/POV-"+t.ToString("00.0",CultureInfo.InvariantCulture)+".png");}
            foreach(float cut in new[]{19f,27f}){sequence.Sample(cut);if(sequence.TransitionFadeAlpha<.999f)throw new Exception("Exit/entry cut is not hidden by the fade.");}
            sequence.Sample(18.05f);var stoppedRotation=sequence.View.transform.localRotation;
            foreach(float t in new[]{18.25f,18.5f,18.9f}){sequence.Sample(t);if(Quaternion.Angle(stoppedRotation,sequence.View.transform.localRotation)>.01f)throw new Exception("Head turns before the exit fade.");}
            sequence.Sample(21);if(sequence.View.transform.localPosition.x>-1.3f)throw new Exception("POV did not exit the vehicle.");
            sequence.Sample(26);if(sequence.View.transform.localPosition.z>-3)throw new Exception("POV did not reach the rear door.");
            sequence.Sample(30);if(sequence.View.transform.localPosition.x<.9f)throw new Exception("Seated view is not close to the house-side window.");
            var body=sequence.Vehicle.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Van");
            if(!AssetDatabase.GetAssetPath(body.sharedMesh).EndsWith("Van.fbx",StringComparison.OrdinalIgnoreCase))throw new Exception("Van body is not the original FBX mesh.");
            var rearDoor=sequence.Vehicle.Find("Van_OriginalRearDoorExterior").GetComponent<MeshRenderer>();
            if(rearDoor.sharedMaterial!=body.GetComponent<Renderer>().sharedMaterial)throw new Exception("Rear door material differs from the original van.");
            var originalUv=new HashSet<Vector2>(body.sharedMesh.uv);
            if(rearDoor.GetComponent<MeshFilter>().sharedMesh.uv.Any(uv=>!originalUv.Contains(uv)))throw new Exception("Rear door UVs differ from the original van.");
            var unexpected=sequence.Vehicle.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Hinge")||t.name=="Character_ExitAndRearEntry"||t.name=="Laptop_TransitionScreen").ToArray();
            if(unexpected.Length>0)throw new Exception("Extra door, character or screen geometry remains.");
            foreach(var r in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)))
                foreach(var material in r.sharedMaterials)
                    if(material!=null&&AssetDatabase.GetAssetPath(material).StartsWith(Output,StringComparison.Ordinal))throw new Exception("Generated material remains: "+material.name);
            var laptop=sequence.Vehicle.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Cube").GetComponent<Renderer>();
            var tabletop=GameObject.Find("Laptop_Tabletop").GetComponent<Renderer>();
            if(Mathf.Abs(laptop.bounds.min.y-tabletop.bounds.max.y)>.002f)throw new Exception("Laptop is not in contact with the tabletop.");
            sequence.Sample(39);var wheel=sequence.Wheels[0].transform.localRotation;
            if(Vector3.Distance(sequence.Vehicle.position,target.position)>.001f)throw new Exception("Parking differs from House drdr.");
            sequence.Sample(60);
            if(Vector3.Distance(sequence.Vehicle.position,target.position)>.001f||Quaternion.Angle(wheel,sequence.Wheels[0].transform.localRotation)>.001f)throw new Exception("Vehicle/wheels drift while parked.");
            if(sequence.OriginalScreenDistance>.07f)throw new Exception("Camera did not enter the original screen.");
            if(sequence.TransitionFadeAlpha>.001f)throw new Exception("Final laptop image is obscured by a fade.");
            if(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled)!=1)throw new Exception("Camera conflict.");
            sequence.Sample(0);
            File.WriteAllText("Logs/Intro/RearWindowCheck.txt","REAR_WINDOW_STATIC_PASSED\n39s; first person exit and outside walk; fade to seated view beside window; original van mesh, laptop, textures and materials; worktable contact; original screen close-up; parked hold.");
            Debug.Log("REAR_WINDOW_STATIC_PASSED");
        }
        private static void Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(1280,720,24);var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);}
        }
    }
}
