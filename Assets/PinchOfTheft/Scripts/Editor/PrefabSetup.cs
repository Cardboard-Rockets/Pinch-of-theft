using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PinchOfTheft.Core;
using PinchOfTheft.CCTV;
using PinchOfTheft.Interaction;
using PinchOfTheft.Movement;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    public static class PrefabSetup
    {
        private const string Root="Assets/PinchOfTheft/Prefabs/";
        private static readonly Dictionary<Object,Object> map=new();
        private static T Find<T>() where T:Object=>Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        private static Object Get(Object obj,string field){var s=new SerializedObject(obj);return s.FindProperty(field).objectReferenceValue;}
        private static void Set(Object obj,string field,Object value){var s=new SerializedObject(obj);s.FindProperty(field).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        private static void Match(GameObject original,GameObject copy)
        {
            map[original]=copy;map[original.transform]=copy.transform;
            var a=original.GetComponents<Component>();var b=copy.GetComponents<Component>();for(int i=0;i<a.Length;i++)if(a[i]!=null && i<b.Length)map[a[i]]=b[i];
            for(int i=0;i<original.transform.childCount && i<copy.transform.childCount;i++)Match(original.transform.GetChild(i).gameObject,copy.transform.GetChild(i).gameObject);
        }
        private static GameObject Clone(GameObject source,Transform parent)
        {var clone=Object.Instantiate(source,parent,true);clone.name=source.name;Match(source,clone);return clone;}
        private static void Remap(GameObject root,bool clearExternal)
        {
            foreach(var component in root.GetComponentsInChildren<Component>(true))
            {
                if(component==null)continue;var serialized=new SerializedObject(component);var p=serialized.GetIterator();
                while(p.Next(true))
                {
                    if(p.propertyType!=SerializedPropertyType.ObjectReference || p.name=="m_Script")continue;
                    Object value=p.objectReferenceValue;
                    if(value==null)continue;
                    if(map.TryGetValue(value,out var target) && target!=null)p.objectReferenceValue=target;
                    else if(clearExternal && (value is Component c && !c.transform.IsChildOf(root.transform) || value is GameObject g && !g.transform.IsChildOf(root.transform)) && !EditorUtility.IsPersistent(value))p.objectReferenceValue=null;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        private static GameObject Save(string path,params GameObject[] sources)
        {
            map.Clear();var root=new GameObject(Path.GetFileNameWithoutExtension(path));foreach(var source in sources)if(source!=null)Clone(source,root.transform);Remap(root,true);
            PrefabUtility.SaveAsPrefabAsset(root,Root+path);Object.DestroyImmediate(root);return AssetDatabase.LoadAssetAtPath<GameObject>(Root+path);
        }
        public static void TemplatesOnly()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            var oldManager=Find<CameraManager>();var oldMenu=Find<CameraMenuUI>();
            var canvasSource=oldMenu.GetComponentInParent<Canvas>();
            var player=Find<PlayerMover>();var marker=Find<ClickMarker>();
            Save("Player/Player.prefab",player.gameObject,marker.gameObject);
            Save("CCTV/CCTV_Group.prefab",Find<CCTVCamera>().transform.parent.gameObject);
            var shield=GameObject.Find("shield");
            Save("Interaction/KeyPrompt.prefab",shield.GetComponentInChildren<KeyPrompt>(true).gameObject);
            Save("Interaction/Interactable_Hold.prefab",shield);
            var keypad=Find<KeypadUI>();var keypadPanel=(GameObject)Get(keypad,"panel");
            Save("Puzzle/CodePuzzle.prefab",Find<CodePuzzle>().gameObject,shield,GameObject.Find("panel"),GameObject.Find("TV"),keypadPanel);
            var board=Find<BoardController>();Save("Props/FallingBoard.prefab",board.gameObject,(GameObject)Get(board,"standingBoard"),(GameObject)Get(board,"fallenBoard"));
            Save("Puzzle/CurrentCircuit.prefab",Find<CurrentFlow>().gameObject);
            map.Clear();var core=new GameObject("Core");
            var context=core.AddComponent<GameContext>();var locks=core.AddComponent<InputLock>();var cursor=core.AddComponent<CursorController>();var probe=core.AddComponent<AimProbe>();
            var manager=core.AddComponent<CameraManager>();EditorUtility.CopySerialized(oldManager,manager);map[oldManager]=manager;
            var canvas=Clone(canvasSource.gameObject,core.transform);var menu=canvas.GetComponentInChildren<CameraMenuUI>(true);
            var hud=canvas.AddComponent<Hud>();Set(hud,"crosshair",map[Get(oldMenu,"crosshair")]);Set(hud,"cameraLabel",map[Get(oldMenu,"cameraLabel")]);
            var interactor=Clone(Find<SphereInteractor>().gameObject,core.transform).GetComponent<SphereInteractor>();
            Clone(Find<ClickToMoveInput>().gameObject,core.transform);
            var events=Find<EventSystem>();if(events!=null)Clone(events.gameObject,core.transform);
            Set(context,"cameraManager",manager);Set(context,"inputLock",locks);Set(context,"cursorController",cursor);Set(context,"aimProbe",probe);Set(context,"hud",hud);
            Set(manager,"menu",menu);Set(manager,"interactor",interactor);Set(manager,"board",null);Set(manager,"door",null);
            var ms=new SerializedObject(manager);ms.FindProperty("cameras").arraySize=0;ms.ApplyModifiedPropertiesWithoutUndo();
            var ps=new SerializedObject(probe);ps.FindProperty("mask").intValue=~((1<<LayerMask.NameToLayer("Player"))|(1<<2));ps.ApplyModifiedPropertiesWithoutUndo();
            if(map.TryGetValue(keypadPanel,out var duplicatedPanel) && duplicatedPanel!=null)Object.DestroyImmediate(duplicatedPanel);
            Remap(core,true);PrefabUtility.SaveAsPrefabAsset(core,Root+"Core/Core.prefab");Object.DestroyImmediate(core);
            var group=PrefabUtility.LoadPrefabContents(Root+"CCTV/CCTV_Group.prefab");
            group.AddComponent<CCTVGroup>();int slot=1;
            foreach(var c in group.GetComponentsInChildren<CCTVCamera>(true))
            {
                c.ConfigureGroup(0,true);var cs=new SerializedObject(c);cs.FindProperty("slot").intValue=slot;cs.ApplyModifiedPropertiesWithoutUndo();
                c.transform.localRotation=Quaternion.Euler(0,(slot-1)*90,0);c.transform.localPosition=c.transform.localRotation*Vector3.forward*0.5f;
                c.GetComponent<Camera>().enabled=slot==1;c.GetComponent<AudioListener>().enabled=slot==1;slot++;
            }
            PrefabUtility.SaveAsPrefabAsset(group,Root+"CCTV/CCTV_Group.prefab");PrefabUtility.UnloadPrefabContents(group);
            var hold=PrefabUtility.LoadPrefabContents(Root+"Interaction/Interactable_Hold.prefab");
            foreach(var interact in hold.GetComponentsInChildren<InteractableSphere>(true))
            {var s=new SerializedObject(interact);s.FindProperty("onCompleted").FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize=0;s.ApplyModifiedPropertiesWithoutUndo();}
            PrefabUtility.SaveAsPrefabAsset(hold,Root+"Interaction/Interactable_Hold.prefab");PrefabUtility.UnloadPrefabContents(hold);
            var puzzleRoot=PrefabUtility.LoadPrefabContents(Root+"Puzzle/CodePuzzle.prefab");
            var overlay=new GameObject("Keypad Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));overlay.transform.SetParent(puzzleRoot.transform);
            overlay.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            puzzleRoot.transform.Find("KeypadPanel").SetParent(overlay.transform,false);
            PrefabUtility.SaveAsPrefabAsset(puzzleRoot,Root+"Puzzle/CodePuzzle.prefab");PrefabUtility.UnloadPrefabContents(puzzleRoot);
            AssetDatabase.SaveAssets();Debug.Log("TEMPLATES_ONLY_COMPLETE: test_Scene not saved");
        }
        public static void SandboxOnly() { BuildSandbox(); }
        public static void AttachServicesOnly()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            var manager=Find<CameraManager>();
            var root=manager.gameObject;
            var context=root.GetComponent<GameContext>();if(context==null)context=root.AddComponent<GameContext>();
            var locks=root.GetComponent<InputLock>();if(locks==null)locks=root.AddComponent<InputLock>();
            var cursor=root.GetComponent<CursorController>();if(cursor==null)cursor=root.AddComponent<CursorController>();
            var probe=root.GetComponent<AimProbe>();if(probe==null)probe=root.AddComponent<AimProbe>();
            var menu=Find<CameraMenuUI>();var canvas=menu.GetComponentInParent<Canvas>();
            var hud=canvas.GetComponent<Hud>();if(hud==null)hud=canvas.gameObject.AddComponent<Hud>();
            Set(hud,"crosshair",Get(menu,"crosshair"));Set(hud,"cameraLabel",Get(menu,"cameraLabel"));
            Set(context,"cameraManager",manager);Set(context,"inputLock",locks);Set(context,"cursorController",cursor);Set(context,"aimProbe",probe);Set(context,"hud",hud);
            var ps=new SerializedObject(probe);ps.FindProperty("mask").intValue=~((1<<LayerMask.NameToLayer("Player"))|(1<<2));ps.ApplyModifiedPropertiesWithoutUndo();
            var marker=Find<ClickMarker>();var player=Find<PlayerMover>();if(marker!=null && player!=null)marker.transform.SetParent(player.transform,true);
            EditorSceneManager.SaveScene(scene);Validate();Debug.Log("SERVICES_ATTACHED: original objects retained");
        }
        public static void Build()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            var oldManager=Find<CameraManager>();var oldMenu=Find<CameraMenuUI>();var oldCanvas=oldMenu.GetComponent<Canvas>();if(oldCanvas==null)oldCanvas=oldMenu.GetComponentInParent<Canvas>();
            var oldInteractor=Find<SphereInteractor>();var oldClick=Find<ClickToMoveInput>();var oldEvents=Find<EventSystem>();var player=Find<PlayerMover>();var marker=Find<ClickMarker>();
            if(marker!=null && player!=null)marker.transform.SetParent(player.transform,true);
            map.Clear();var core=new GameObject("Core");var context=core.AddComponent<GameContext>();var locks=core.AddComponent<InputLock>();var cursor=core.AddComponent<CursorController>();var probe=core.AddComponent<AimProbe>();
            var manager=core.AddComponent<CameraManager>();EditorUtility.CopySerialized(oldManager,manager);map[oldManager]=manager;
            var canvas=Clone(oldCanvas.gameObject,core.transform);var menu=canvas.GetComponentInChildren<CameraMenuUI>(true);
            var hud=canvas.AddComponent<Hud>();Set(hud,"crosshair",map[Get(oldMenu,"crosshair")]);Set(hud,"cameraLabel",map[Get(oldMenu,"cameraLabel")]);
            var interactor=Clone(oldInteractor.gameObject,core.transform).GetComponent<SphereInteractor>();var click=Clone(oldClick.gameObject,core.transform).GetComponent<ClickToMoveInput>();
            if(oldEvents!=null)Clone(oldEvents.gameObject,core.transform);else new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)).transform.SetParent(core.transform);
            Set(context,"cameraManager",manager);Set(context,"inputLock",locks);Set(context,"cursorController",cursor);Set(context,"aimProbe",probe);Set(context,"hud",hud);
            Set(manager,"menu",menu);Set(manager,"interactor",interactor);Set(manager,"board",null);Set(manager,"door",null);var managerSettings=new SerializedObject(manager);managerSettings.FindProperty("cameras").arraySize=0;managerSettings.ApplyModifiedPropertiesWithoutUndo();
            var probeSettings=new SerializedObject(probe);probeSettings.FindProperty("mask").intValue=~((1<<LayerMask.NameToLayer("Player"))|(1<<2));probeSettings.ApplyModifiedPropertiesWithoutUndo();
            var keypad=Find<KeypadUI>();var keypadPanel=(GameObject)Get(keypad,"panel");
            if(map.TryGetValue(keypadPanel,out var duplicate)&&duplicate!=null)Object.DestroyImmediate(duplicate);
            Remap(core,true);PrefabUtility.SaveAsPrefabAsset(core,Root+"Core/Core.prefab");
            var sourceMap=new Dictionary<Object,Object>(map);map.Clear();var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Core/Core.prefab"));Match(core,instance);
            var finalMap=new Dictionary<Object,Object>();foreach(var pair in sourceMap)if(pair.Value!=null && map.TryGetValue(pair.Value,out var replacement))finalMap[pair.Key]=replacement;
            map.Clear();foreach(var pair in finalMap)map[pair.Key]=pair.Value;
            keypadPanel.transform.SetParent(instance.GetComponentInChildren<Canvas>(true).transform,true);
            foreach(var root in scene.GetRootGameObjects())if(root!=core)Remap(root,false);
            Object.DestroyImmediate(core);Object.DestroyImmediate(oldManager);Object.DestroyImmediate(oldCanvas.gameObject);Object.DestroyImmediate(oldInteractor.gameObject);Object.DestroyImmediate(oldClick.gameObject);if(oldEvents!=null)Object.DestroyImmediate(oldEvents.gameObject);
            Save("Player/Player.prefab",player.gameObject);
            var group=Find<CCTVCamera>().transform.parent;var groupPrefab=Save("CCTV/CCTV_Group.prefab",group.gameObject);
            var groupContents=PrefabUtility.LoadPrefabContents(Root+"CCTV/CCTV_Group.prefab");
            var groupNode=groupContents.AddComponent<CCTVGroup>();var gs=new SerializedObject(groupNode);gs.FindProperty("group").intValue=0;gs.FindProperty("accessible").boolValue=true;gs.ApplyModifiedPropertiesWithoutUndo();
            int slot=1;foreach(var camera in groupContents.GetComponentsInChildren<CCTVCamera>(true)){camera.ConfigureGroup(0,true);camera.transform.localPosition=Quaternion.Euler(0,(slot-1)*90,0)*Vector3.forward*0.5f;camera.transform.localRotation=Quaternion.Euler(0,(slot-1)*90,0);camera.GetComponent<Camera>().enabled=slot==1;camera.GetComponent<AudioListener>().enabled=slot==1;slot++;}
            PrefabUtility.SaveAsPrefabAsset(groupContents,Root+"CCTV/CCTV_Group.prefab");PrefabUtility.UnloadPrefabContents(groupContents);
            var shield=GameObject.Find("shield");Save("Interaction/KeyPrompt.prefab",shield.GetComponentInChildren<KeyPrompt>(true).gameObject);
            var hold=Save("Interaction/Interactable_Hold.prefab",shield);
            var holdRoot=PrefabUtility.LoadPrefabContents(Root+"Interaction/Interactable_Hold.prefab");foreach(var interact in holdRoot.GetComponentsInChildren<InteractableSphere>(true)){var s=new SerializedObject(interact);s.FindProperty("onCompleted").FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize=0;s.ApplyModifiedPropertiesWithoutUndo();}PrefabUtility.SaveAsPrefabAsset(holdRoot,Root+"Interaction/Interactable_Hold.prefab");PrefabUtility.UnloadPrefabContents(holdRoot);
            var puzzle=Find<CodePuzzle>();Save("Puzzle/CodePuzzle.prefab",puzzle.gameObject,shield,GameObject.Find("panel"),GameObject.Find("TV"),keypadPanel);
            var puzzleRoot=PrefabUtility.LoadPrefabContents(Root+"Puzzle/CodePuzzle.prefab");var panelRoot=puzzleRoot.transform.Find("KeypadPanel");if(panelRoot!=null){var overlay=new GameObject("Keypad Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));overlay.transform.SetParent(puzzleRoot.transform);overlay.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;panelRoot.SetParent(overlay.transform,false);}PrefabUtility.SaveAsPrefabAsset(puzzleRoot,Root+"Puzzle/CodePuzzle.prefab");PrefabUtility.UnloadPrefabContents(puzzleRoot);
            var board=Find<BoardController>();Save("Props/FallingBoard.prefab",board.gameObject,(GameObject)Get(board,"standingBoard"),(GameObject)Get(board,"fallenBoard"));
            var flow=Find<CurrentFlow>();Save("Puzzle/CurrentCircuit.prefab",flow.gameObject);
            EditorSceneManager.SaveScene(scene);Validate();BuildSandbox();EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");Debug.Log("PREFABS_MIGRATED");
        }
        private static void BuildSandbox()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            foreach(var path in new[]{"Core/Core.prefab","Player/Player.prefab","CCTV/CCTV_Group.prefab","Interaction/Interactable_Hold.prefab","Puzzle/CodePuzzle.prefab","Props/FallingBoard.prefab","Puzzle/CurrentCircuit.prefab"})PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+path));
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Ground";ground.transform.localScale=Vector3.one*5;
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(50,-30,0);
            var surface=new GameObject("NavMesh").AddComponent<NavMeshSurface>();surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.layerMask=~(1<<LayerMask.NameToLayer("Player"));surface.BuildNavMesh();AssetDatabase.CreateAsset(surface.navMeshData,"Assets/Scenes/SandboxNavMesh.asset");
            var player=Find<PlayerMover>();if(NavMesh.SamplePosition(Vector3.zero,out var hit,10,NavMesh.AllAreas))player.transform.position=hit.position+Vector3.up;
            var cameras=Object.FindObjectsByType<CCTVCamera>();foreach(var c in cameras){c.GetComponent<Camera>().enabled=c.Slot==1;c.GetComponent<AudioListener>().enabled=c.Slot==1;}
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/Sandbox_Empty.unity");Validate();
        }
        [MenuItem("PinchOfTheft/Setup/Add Core To Scene")]
        public static void AddCore()
        {
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=tags.FindProperty("layers");foreach(var name in new[]{"Player","Interactable"})if(LayerMask.NameToLayer(name)<0)for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)){layers.GetArrayElementAtIndex(i).stringValue=name;break;}tags.ApplyModifiedPropertiesWithoutUndo();
            if(Find<GameContext>()!=null){Debug.Log("Core уже добавлен");return;}PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Core/Core.prefab"));
        }
        [MenuItem("PinchOfTheft/Validate Scene")]
        public static void Validate()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();int missing=0,refs=0;
            foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                foreach(var component in t.GetComponents<Component>())if(component!=null){var s=new SerializedObject(component);var p=s.GetIterator();while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference && p.objectReferenceValue==null && p.objectReferenceEntityIdValue!=default)refs++;}
            }
            var contexts=Object.FindObjectsByType<GameContext>(FindObjectsInactive.Include,FindObjectsSortMode.None);var cameras=Object.FindObjectsByType<CCTVCamera>(FindObjectsInactive.Include,FindObjectsSortMode.None);int listeners=Object.FindObjectsByType<AudioListener>().Count(a=>a.isActiveAndEnabled);
            bool nav=NavMesh.CalculateTriangulation().vertices.Length>0;
            Debug.Log($"VALIDATE {scene.name}: Core={contexts.Length}, cameras={cameras.Length}, listeners={listeners}, Player={Find<PlayerMover>()!=null}, NavMesh={nav}, layers={LayerMask.NameToLayer("Player")>=0 && LayerMask.NameToLayer("Interactable")>=0}, MissingScript={missing}, MissingReference={refs}");
            if(missing+refs>0 || contexts.Length!=1 || cameras.Length==0 || listeners!=1)throw new Exception("Scene validation failed");
        }
    }
}

