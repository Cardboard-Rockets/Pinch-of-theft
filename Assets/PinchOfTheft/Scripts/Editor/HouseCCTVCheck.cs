using System;
using System.Linq;
using PinchOfTheft.CCTV;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class HouseCCTVCheck
    {
        private static int stage, frame;
        private static double began;
        private static float heldAt;
        private static Keyboard keyboard;
        private static InteractableSphere first, second;
        static HouseCCTVCheck()
        {
            EditorApplication.playModeStateChanged += s => { if(s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("HouseCheck", false)) { began = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; } };
        }
        public static void Run() { EditorSceneManager.OpenScene("Assets/Scenes/House.unity"); SessionState.SetBool("HouseCheck", true); EditorApplication.EnterPlaymode(); }
        private static void Require(bool value, string message) { if(!value) throw new Exception(message); }
        private static void Press(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); frame = Time.frameCount; }
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-began > 45) throw new Exception("House test timeout stage="+stage);
                if(Time.frameCount < 5 || Time.frameCount-frame < 3) return;
                var context=GameContext.Instance; var manager=context.CameraManager;
                if(stage==0)
                {
                    Require(manager.ActiveCamera.CameraName=="01", "Startup camera");
                    Require(context.Hud.GetComponentsInChildren<Text>(true).Any(t=>t.text=="CAM 01" && t.gameObject.activeInHierarchy), "Camera label");
                    Require(Object.FindObjectsByType<AudioListener>().Count(a=>a.isActiveAndEnabled)==1,"Listeners");
                    Require(Object.FindObjectsByType<Camera>().Count(a=>a.isActiveAndEnabled)==1,"Active cameras");
                    Require(manager.Cameras.Count==13,"Camera count");
                    manager.SelectCamera(manager.Cameras.Single(c=>c.CameraName=="31"));Require(manager.ActiveCamera.CameraName=="31","31 not initially accessible");
                    manager.SelectCamera(manager.Cameras.Single(c=>c.CameraName=="01"));
                    foreach(var c in manager.Cameras.Where(c=>c.Group>0 && c.Group<3)){manager.SelectCamera(c);Require(manager.ActiveCamera.CameraName=="01","Locked camera selected");}
                    context.Menu.SetOpen(true);Require(manager.Cameras.Count(c=>c.IsVisibleInMenu)==4,"Initial menu cameras");context.Menu.SetOpen(false);
                    var active=manager.ActiveCamera;var original=active.transform.localRotation;
                    active.Rotate(new Vector2(1000,1000));var s=new SerializedObject(active);
                    Require(Quaternion.Angle(active.transform.localRotation,original*Quaternion.Euler(-s.FindProperty("maxPitchAngle").floatValue,s.FindProperty("maxYawAngle").floatValue,0))<0.1f,"Rotation clamp");
                    active.Rotate(new Vector2(-s.FindProperty("maxYawAngle").floatValue,-s.FindProperty("maxPitchAngle").floatValue));
                    InputSystem.settings=Object.Instantiate(InputSystem.settings); InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus; Application.runInBackground=true; keyboard=InputSystem.AddDevice<Keyboard>();Press(Key.E);stage=1;
                }
                else if(stage==1){Require(manager.ActiveCamera.CameraName=="02","E did not select 02");Press();stage=2;}
                else if(stage==2){Press(Key.Q);stage=3;}
                else if(stage==3){Require(manager.ActiveCamera.CameraName=="01","Q did not select 01");Press();stage=4;}
                else if(stage==4){context.Menu.SetOpen(true);Press(Key.E);stage=5;}
                else if(stage==5)
                {
                    Require(manager.ActiveCamera.CameraName=="01","E switched in menu");context.Menu.SetOpen(false);Press();
                    var targets=Object.FindObjectsByType<InteractableSphere>();first=targets.Single(x=>x.name.Replace(" ","").Replace("Onlock","Unlock").Equals("Unlockcam11",StringComparison.OrdinalIgnoreCase));second=targets.Single(x=>x.name.Replace(" ","").Replace("Onlock","Unlock").Equals("Unlockcam21",StringComparison.OrdinalIgnoreCase));
                    first.Complete();first.Complete();Require(!first.IsAvailable && manager.Cameras.Where(c=>c.Group==1).All(c=>c.isAccessible && c.IsVisibleInMenu),"First unlock");
                    manager.SelectCamera(manager.Cameras.Single(c=>c.CameraName=="13"));stage=6;
                }
                else if(stage==6)
                {
                    Require(Object.FindAnyObjectByType<SphereInteractor>().HasTarget,"Second unlocker not under crosshair on 13");
                    Require(second.GetComponentInChildren<CanvasGroup>(true).alpha==1,"Prompt highlight");Press(Key.E);heldAt=Time.time;stage=7;
                }
                else if(stage==7)
                {
                    Require(manager.ActiveCamera.CameraName=="13","E not suppressed");
                    if(Time.time-heldAt<0.5f)return;Press();stage=8;
                }
                else if(stage==8)
                {
                    Require(second.IsAvailable,"Premature unlock");
                    Require(second.GetComponentsInChildren<Image>(true).Where(i=>i.type==Image.Type.Filled).All(i=>i.fillAmount==0),"Hold not reset");
                    Press(Key.E);heldAt=Time.time;stage=9;
                }
                else if(stage==9)
                {
                    if(Time.time-heldAt<2.3f)return;
                    Require(!second.IsAvailable && manager.Cameras.Where(c=>c.Group==2).All(c=>c.isAccessible && c.IsVisibleInMenu),"Second hold unlock");
                    Require(manager.ActiveCamera.CameraName=="13","E switched during completion");
                    Require(!second.GetComponentInChildren<KeyPrompt>(true).gameObject.activeSelf,"Prompt not hidden");Press();stage=10;
                }
                else if(stage==10){Press(Key.E);stage=11;}
                else if(stage==11)
                {
                    Require(manager.ActiveCamera.CameraName=="14","E did not resume after unlock");Press();
                    context.Menu.SetOpen(true);Require(manager.Cameras.Count(c=>c.IsVisibleInMenu)==13,"Unlocked menu");
                    var buttons=context.Menu.GetComponentsInChildren<Button>().Select(b=>b.GetComponentInChildren<Text>()).Where(t=>t!=null).Select(t=>t.text).ToArray();
                    Require(buttons.Contains("25") && buttons.Contains("31"),"Extended camera buttons missing");context.Menu.SetOpen(false);
                    manager.SelectCamera(manager.Cameras.Single(c=>c.CameraName=="24"));stage=12;
                }
                else if(stage==12){Press(Key.E);stage=13;}
                else if(stage==13){Require(manager.ActiveCamera.CameraName=="25","24 to 25 failed");Press();stage=14;}
                else if(stage==14){Press(Key.E);stage=15;}
                else if(stage==15){Require(manager.ActiveCamera.CameraName=="25","25 wrapped");Press();stage=16;}
                else if(stage==16){Press(Key.Q);stage=17;}
                else if(stage==17){Require(manager.ActiveCamera.CameraName=="24","25 to 24 failed");Debug.Log("HOUSE_PLAY_PASSED: start01,label,rotation clamp,Q/E,menu lock,first unlock API,second real E hold/reset/suppression,31 initially available,24-25 noncyclic switching,13 menu buttons,one camera/listener");Finish(0);}
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("HouseCheck",false);EditorApplication.Exit(code);}
    }
}

