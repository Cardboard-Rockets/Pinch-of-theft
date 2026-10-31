using System;
using System.IO;
using System.Linq;
using PinchOfTheft.CCTV;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class HouseUnlockRepair
    {
        private const string Request="Temp/HouseUnlockRepair.request";
        private static string Normalize(string s)=>s.ToLowerInvariant().Replace(" ","").Replace("_","").Replace("onlock","unlock");
        static HouseUnlockRepair(){EditorApplication.delayCall+=()=>{if(File.Exists(Request)&&!EditorApplication.isPlaying){File.Delete(Request);Repair();}};}
        public static void FixPromptVisuals()
        {
            const string path = "Assets/PinchOfTheft/Prefabs/Interaction/KeyPrompt.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            var prefabPrompt = prefab.GetComponentInChildren<KeyPrompt>(true);
            prefab.transform.localScale = Vector3.one;
            prefabPrompt.transform.localPosition = Vector3.zero;
            prefabPrompt.transform.localRotation = Quaternion.identity;
            prefabPrompt.transform.localScale = Vector3.one * 0.00625f;
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            PrefabUtility.UnloadPrefabContents(prefab);
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            foreach (var target in Object.FindObjectsByType<InteractableSphere>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (Normalize(target.name) != "unlockcam11" && Normalize(target.name) != "unlockcam21") continue;
                var prompt = target.GetComponentInChildren<KeyPrompt>(true);
                var wrapper = prompt.transform.parent;
                var scale = wrapper.parent.lossyScale;
                wrapper.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
                wrapper.rotation = Quaternion.identity;
                var bounds = target.GetComponent<Collider>().bounds;
                wrapper.position = bounds.center + Vector3.up * (bounds.extents.y + 0.3f);
                prompt.transform.localPosition = Vector3.zero;
                prompt.transform.localRotation = Quaternion.identity;
                prompt.transform.localScale = Vector3.one * 0.00625f;
                EditorUtility.SetDirty(prompt.transform);
                EditorUtility.SetDirty(wrapper);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Repair();
        }
        [MenuItem("PinchOfTheft/Setup/Lock House Groups 1 And 2")]
        public static void LockTestGroups()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            int count = 0;
            foreach (var camera in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<CCTVCamera>(true)))
            {
                if (camera.Group != 1 && camera.Group != 2) continue;
                var settings = new SerializedObject(camera);
                settings.FindProperty("isAccessible").boolValue = false;
                settings.FindProperty("isVisibleInMenu").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                camera.SetActive(false);
                EditorUtility.SetDirty(camera.GetComponent<Camera>());
                EditorUtility.SetDirty(camera.GetComponent<AudioListener>());
                count++;
            }
            if (count == 0) throw new Exception("No group 1/2 cameras found in House");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("HOUSE_GROUPS_LOCKED: " + count);
        }
        [MenuItem("PinchOfTheft/Setup/Repair House Unlockers")]
        public static void Repair()
        {
            try
            {
                if (Application.isBatchMode) EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
                var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if(!scene.name.Equals("House",StringComparison.OrdinalIgnoreCase))throw new Exception("Open House first");
                var manager=Object.FindAnyObjectByType<CameraManager>();
                var targets=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>Normalize(t.name)=="unlockcam11"||Normalize(t.name)=="unlockcam21").ToArray();
                if(targets.Length!=2||manager==null)throw new Exception("Unlock targets/Core not unique");
                foreach(var target in targets)
                {
                    var interactable=target.GetComponent<InteractableSphere>();
                    if(interactable==null)interactable=target.gameObject.AddComponent<InteractableSphere>();
                    var prompt=target.GetComponentInChildren<KeyPrompt>(true);
                    if(prompt==null)throw new Exception("Missing KeyPrompt: "+target.name);
                    var s=new SerializedObject(interactable);s.FindProperty("cameraManager").objectReferenceValue=manager;s.FindProperty("prompt").objectReferenceValue=prompt;s.FindProperty("unlockGroup").intValue=Normalize(target.name)=="unlockcam11"?1:2;s.FindProperty("holdDuration").floatValue=2;s.FindProperty("singleUse").boolValue=true;s.FindProperty("startAvailable").boolValue=true;s.ApplyModifiedPropertiesWithoutUndo();interactable.enabled=true;
                    foreach(var collider in target.GetComponentsInChildren<Collider>(true)){collider.enabled=true;collider.isTrigger=false;EditorUtility.SetDirty(collider);}
                    target.gameObject.layer=LayerMask.NameToLayer("Interactable");
                }
                var first=targets.Single(t=>Normalize(t.name)=="unlockcam11");
                var camera=Object.FindObjectsByType<CCTVCamera>().Single(c=>c.CameraName=="03");
                var direction=Quaternion.Inverse(camera.transform.rotation)*(first.GetComponent<Collider>().bounds.center-camera.transform.position).normalized;
                var settings=new SerializedObject(camera);
                float yaw=Mathf.Abs(Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg)+3f;
                float pitch=Mathf.Abs(Mathf.Asin(direction.y)*Mathf.Rad2Deg)+3f;
                settings.FindProperty("maxYawAngle").floatValue=Mathf.Max(settings.FindProperty("maxYawAngle").floatValue,yaw);
                settings.FindProperty("maxPitchAngle").floatValue=Mathf.Max(settings.FindProperty("maxPitchAngle").floatValue,pitch);
                settings.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                File.WriteAllText("Logs/Unlock-Repair.txt","REPAIRED: both existing scripts/references/colliders; CAM03 yaw="+settings.FindProperty("maxYawAngle").floatValue+" pitch="+settings.FindProperty("maxPitchAngle").floatValue);
                SessionState.SetBool("UnlockRepairTest",true);EditorApplication.playModeStateChanged+=StartTest;EditorApplication.EnterPlaymode();
            }
            catch(Exception e){File.WriteAllText("Logs/Unlock-Repair.txt",e.ToString());Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);}
        }
        private static Keyboard keyboard;
        private static InteractableSphere target;
        private static int stage,frame;
        private static float began;
        private static void StartTest(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode)return;
            EditorApplication.update+=Test;
        }
        [InitializeOnLoadMethod]
        private static void Resume(){if(SessionState.GetBool("UnlockRepairTest",false))EditorApplication.playModeStateChanged+=StartTest;}
        private static void Test()
        {
            try
            {
                if(Time.frameCount<5||Time.frameCount<=frame+2)return;
                var context=GameContext.Instance;var manager=context.CameraManager;
                if(stage==0||stage==2)
                {
                    if(stage==0){InputSystem.settings=Object.Instantiate(InputSystem.settings);InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;keyboard=InputSystem.AddDevice<Keyboard>();}
                    string number=stage==0?"03":"13",name=stage==0?"unlockcam11":"unlockcam21";
                    target=Object.FindObjectsByType<InteractableSphere>().Single(t=>Normalize(t.name)==name);
                    var camera=manager.Cameras.Single(c=>c.CameraName==number);manager.SelectCamera(camera);
                    var v=Quaternion.Inverse(camera.transform.rotation)*(target.GetComponent<Collider>().bounds.center-camera.transform.position).normalized;
                    camera.Rotate(new Vector2(Mathf.Atan2(v.x,v.z)*Mathf.Rad2Deg,Mathf.Asin(v.y)*Mathf.Rad2Deg));
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());frame=Time.frameCount;stage++;
                }
                else if(stage==1||stage==3)
                {
                    if(!Object.FindAnyObjectByType<SphereInteractor>().HasTarget)throw new Exception("No aimed target: "+target.name);
                    var prompt = target.GetComponentInChildren<KeyPrompt>(true);
                    if (prompt.GetComponent<CanvasGroup>().alpha < 0.99f) throw new Exception("Prompt transparent");
                    var corners = new Vector3[4];
                    ((RectTransform)prompt.transform).GetWorldCorners(corners);
                    float width = Vector3.Distance(corners[0], corners[3]);
                    if (width < 0.25f || width > 1.5f) throw new Exception("Prompt world width=" + width);
                    var view = manager.ActiveCamera.View;
                    float pixels = Vector3.Distance(view.WorldToScreenPoint(corners[0]), view.WorldToScreenPoint(corners[3]));
                    if (pixels < 8f) throw new Exception("Prompt too small: " + pixels + "px");
                    File.AppendAllText("Logs/Unlock-Repair.txt", "\nPROMPT_VISIBLE " + target.name + " width=" + width + " pixels=" + pixels);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));began=Time.time;frame=Time.frameCount;stage+=3;
                }
                else if(stage==4||stage==6)
                {
                    if(Time.time-began<2.4f)return;
                    if(target.IsAvailable)throw new Exception("Hold failed: "+target.name);
                    int group=stage==4?1:2;
                    if(!manager.Cameras.Where(c=>c.Group==group).All(c=>c.isAccessible&&c.IsVisibleInMenu))throw new Exception("Group not unlocked");
                    File.AppendAllText("Logs/Unlock-Repair.txt","\nPLAY_HOLD_PASSED group="+group);
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());frame=Time.frameCount;
                    if(stage==4)stage=2;else Finish();
                }
            }
            catch(Exception e){File.AppendAllText("Logs/Unlock-Repair.txt","\n"+e);Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code=0){EditorApplication.update-=Test;SessionState.SetBool("UnlockRepairTest",false);if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.ExitPlaymode();}
    }
}
