using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.Core;
using PinchOfTheft.CCTV;
using PinchOfTheft.Interaction;
using PinchOfTheft.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class RefactorCheck
    {
        private static double began;
        private static int stage;
        private static int stoppedFrame = -1;
        private static PlayerMover player;
        static RefactorCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("RefactorCheck",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void TestScene(){Run("Assets/Scenes/test_Scene.unity");}
        public static void Sandbox(){Run("Assets/Scenes/Sandbox_Empty.unity");}
        private static void Run(string path){EditorSceneManager.OpenScene(path);SessionState.SetBool("RefactorCheck",true);EditorApplication.EnterPlaymode();}
        private static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-began>35)throw new Exception("Refactor check timeout");
                if(Time.frameCount<4)return;
                var c=GameContext.Instance;
                if(stage==0)
                {
                    Require(c!=null && c.CameraManager.ActiveCamera!=null,"No context/active camera");
                    Require(c.PlayerMover!=null && c.Marker!=null,"Player/marker registration failed");
                    var original=c.CameraManager.ActiveCamera;c.Menu.SetOpen(true);Require(c.InputLock.IsLocked,"Menu lock failed");
                    object extra=new object();c.InputLock.Acquire(extra);c.Menu.SetOpen(false);Require(c.InputLock.IsLocked,"Nested lock released prematurely");c.InputLock.Release(extra);Require(!c.InputLock.IsLocked,"Lock leaked");
                    var puzzle=Object.FindAnyObjectByType<CodePuzzle>();if(puzzle!=null)
                    {
                        puzzle.OnShieldActivated();var field=typeof(CodePuzzle).GetField("code",BindingFlags.Instance|BindingFlags.NonPublic);var code=(string)field.GetValue(puzzle);
                        var keypad=Object.FindAnyObjectByType<KeypadUI>();keypad.Open();Require(c.InputLock.IsLocked && keypad.IsOpen,"Keypad lock failed");puzzle.CheckCode(code);Require(!c.InputLock.IsLocked && !keypad.IsOpen,"Keypad lock leaked");
                    }
                    Require(Object.FindObjectsByType<AudioListener>().Count(x=>x.isActiveAndEnabled)==1,"Multiple listeners");
                    player=c.PlayerMover;var agent=player.GetComponent<NavMeshAgent>();Require(agent.isOnNavMesh,"Player off NavMesh");
                    if(!NavMesh.SamplePosition(agent.nextPosition+Vector3.forward*2,out var hit,4,NavMesh.AllAreas))throw new Exception("No destination");
                    player.MoveTo(hit.position);c.Marker.Show(hit.position);stage=1;
                }
                else if(stage==1 && !player.IsMoving && Time.frameCount>10)
                {
                    if (stoppedFrame < 0) { stoppedFrame = Time.frameCount; return; }
                    if (Time.frameCount <= stoppedFrame + 1) return;
                    Require(!c.Marker.gameObject.activeSelf,"Marker did not hide");
                    Debug.Log("REFACTOR_PLAY_PASSED "+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);Finish(0);
                }
            }
            catch(Exception exception){Debug.LogException(exception);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("RefactorCheck",false);EditorApplication.Exit(code);}
        public static void FixPlayerTemplate()
        {
            var path="Assets/PinchOfTheft/Prefabs/Player/Player.prefab";var root=PrefabUtility.LoadPrefabContents(path);var mover=root.GetComponentInChildren<PlayerMover>(true);var marker=root.GetComponentInChildren<ClickMarker>(true);marker.transform.SetParent(mover.transform,true);PrefabUtility.SaveAsPrefabAsset(root,path);PrefabUtility.UnloadPrefabContents(root);
        }
        public static void ValidateAll()
        {
            foreach(var path in new[]{"Assets/Scenes/test_Scene.unity","Assets/Scenes/Sandbox_Empty.unity"})
            {
                EditorSceneManager.OpenScene(path);
                int before=Object.FindObjectsByType<GameContext>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
                PrefabSetup.AddCore();PrefabSetup.AddCore();
                Require(Object.FindObjectsByType<GameContext>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==before,"Add Core duplicated context");
                PrefabSetup.Validate();
            }
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/PinchOfTheft/Prefabs"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(path);
                int missing=0;
                foreach(var t in root.GetComponentsInChildren<Transform>(true))missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                Require(missing==0,"Missing prefab script "+path);
                foreach(var component in root.GetComponentsInChildren<Component>(true))
                {
                    if(component==null)continue;var s=new SerializedObject(component);var p=s.GetIterator();
                    while(p.Next(true))
                    {
                        if(p.propertyType!=SerializedPropertyType.ObjectReference || p.name=="m_Script")continue;
                        var value=p.objectReferenceValue;
                        Require(value!=null || p.objectReferenceEntityIdValue==default,"Missing prefab reference: "+path+" / "+p.propertyPath);
                        if (value != null && EditorUtility.IsPersistent(value)) continue;
                        if(value is Component c)Require(c.transform.IsChildOf(root.transform),"External reference: "+path+" / "+p.propertyPath);
                        if(value is GameObject g)Require(g.transform.IsChildOf(root.transform),"External reference: "+path+" / "+p.propertyPath);
                    }
                }
                PrefabUtility.UnloadPrefabContents(root);
                Debug.Log("PREFAB_VALIDATED: "+path);
            }
            Debug.Log("FINAL_VALIDATION_PASSED: scenes, prefabs, idempotence");
        }
        public static void ExtendCircuitTemplate()
        {
            const string path="Assets/PinchOfTheft/Prefabs/Puzzle/CurrentCircuit.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            var flow=root.GetComponentInChildren<CurrentFlow>(true);
            var s=new SerializedObject(flow);
            var start=(Transform)s.FindProperty("startPoint").objectReferenceValue;
            var finish=(Transform)s.FindProperty("finishPoint").objectReferenceValue;
            var wires=flow.GetComponentsInChildren<Transform>(true).Where(t=>t.name.ToLowerInvariant().StartsWith("wire")).ToArray();
            if(wires.Length==2)
            {
                var last=wires[1];var bounds=last.GetComponent<MeshFilter>().sharedMesh.bounds;
                var axis=bounds.size.x>=bounds.size.y && bounds.size.x>=bounds.size.z?Vector3.right*bounds.extents.x:bounds.size.y>=bounds.size.z?Vector3.up*bounds.extents.y:Vector3.forward*bounds.extents.z;
                Vector3 a=last.TransformPoint(bounds.center-axis),b=last.TransformPoint(bounds.center+axis);
                Vector3 direction=Vector3.Distance(a,finish.position)<Vector3.Distance(b,finish.position)?a-b:b-a;
                var third=Object.Instantiate(last,last.parent);third.name="Wire3";third.position=last.position+direction;finish.position+=direction;
                flow.ValidatePath();PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
