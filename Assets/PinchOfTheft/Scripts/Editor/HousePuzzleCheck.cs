using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class HousePuzzleCheck
    {
        private static int phase,frame;
        private static double began;
        private static float openedAt;
        private static CodePuzzle puzzle;
        private static KeypadUI keypad;
        private static DoorOpener door;
        private static Keyboard keyboard;
        private static string code;
        private static Quaternion closedRotation;
        private static Vector3 closedPosition; private static Vector3 openedPosition;
        private static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
        private static void Require(bool value,string text){if(!value)throw new Exception(text);}
        static HousePuzzleCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("HousePuzzleCheck",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("HousePuzzleCheck",true);EditorApplication.EnterPlaymode();}
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-began>45)throw new Exception("Puzzle test timeout phase="+phase);
                if(Time.frameCount<5||Time.frameCount<frame+3)return;
                var context=GameContext.Instance;
                if(phase==0)
                {
                    puzzle=Object.FindAnyObjectByType<CodePuzzle>();keypad=Object.FindAnyObjectByType<KeypadUI>();door=Object.FindAnyObjectByType<DoorOpener>();closedRotation=door.transform.rotation;closedPosition=door.transform.position;openedPosition=Field<GameObject>(door,"openedDoor").transform.position;
                    var panel=Field<InteractableSphere>(puzzle,"panelInteractable");Require(!panel.IsAvailable,"Panel initially available");Require(Field<Text>(puzzle,"tvText").text=="","TV initially on");Require(!door.IsOpen,"Door initially open");
                    panel.Complete();Require(!keypad.IsOpen,"Unavailable panel opened");
                    var unlock=Object.FindObjectsByType<InteractableSphere>().Single(t=>t.name=="onlock tv|panel");unlock.Complete();code=Field<Text>(puzzle,"tvText").text;
                    Require(code.Length==4&&code.All(char.IsDigit)&&panel.IsAvailable,"TV code/panel activation failed");puzzle.OnShieldActivated();Require(Field<Text>(puzzle,"tvText").text==code&&!unlock.IsAvailable,"Code regenerated/unlock reusable");
                    panel.Complete();Require(keypad.IsOpen&&context.InputLock.IsLocked,"Panel opening lock");Require(!Field<GameObject>(context.Hud,"crosshair").activeSelf&&!Field<Text>(context.Hud,"cameraLabel").gameObject.activeSelf,"HUD not hidden");
                    keypad.Submit();Require(Field<Text>(keypad,"display").text=="____","Short submit not ignored");
                    string wrong=code=="0000"?"1111":"0000";foreach(char c in wrong)keypad.AddDigit(c-'0');keypad.Submit();Require(Field<Text>(keypad,"display").text=="ERROR"&&!door.IsOpen,"Wrong code behavior");
                    keypad.Close();Require(!context.InputLock.IsLocked,"Close leaked lock");keypad.Open();Require(Field<Text>(keypad,"display").text=="____","Error not cleared on close");
                    var ui=Field<GameObject>(keypad,"panel");ui.transform.Find("1").GetComponent<Button>().onClick.Invoke();ui.transform.Find("2").GetComponent<Button>().onClick.Invoke();ui.transform.Find("Стереть").GetComponent<Button>().onClick.Invoke();Require(Field<Text>(keypad,"display").text=="1___","UI buttons");
                    ui.transform.Find("X").GetComponent<Button>().onClick.Invoke();Require(!keypad.IsOpen&&!context.InputLock.IsLocked,"X close");keypad.Open();
                    InputSystem.settings=Object.Instantiate(InputSystem.settings);InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;keyboard=InputSystem.AddDevice<Keyboard>();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Numpad2));frame=Time.frameCount;phase=1;
                }
                else if(phase==1){Require(Field<Text>(keypad,"display").text=="2___","Numpad input");InputSystem.QueueStateEvent(keyboard,new KeyboardState());frame=Time.frameCount;phase=2;}
                else if(phase==2){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Backspace));frame=Time.frameCount;phase=3;}
                else if(phase==3){Require(Field<Text>(keypad,"display").text=="____","Backspace input");InputSystem.QueueStateEvent(keyboard,new KeyboardState());frame=Time.frameCount;phase=4;}
                else if(phase==4){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));frame=Time.frameCount;phase=5;}
                else if(phase==5)
                {
                    Require(!keypad.IsOpen&&!context.InputLock.IsLocked,"Escape close");keypad.Open();foreach(char c in code)keypad.AddDigit(c-'0');InputSystem.QueueStateEvent(keyboard,new KeyboardState());frame=Time.frameCount;phase=6;
                }
                else if(phase==6){InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));frame=Time.frameCount;phase=7;}
                else if(phase==7)
                {
                    Require(door.IsOpen&&!keypad.IsOpen&&!context.InputLock.IsLocked,"Solve did not open door/close UI");Require(Field<Text>(puzzle,"tvText").text=="OK"&&!Field<InteractableSphere>(puzzle,"panelInteractable").IsAvailable,"Solved TV/panel");Require(!Field<GameObject>(door,"closedDoor").activeSelf&&Field<GameObject>(door,"openedDoor").activeSelf,"Door objects not swapped");Require(Field<GameObject>(context.Hud,"crosshair").activeSelf,"HUD not restored");
                    door.Open();Require(!puzzle.CheckCode(code),"Repeated solve accepted");openedAt=Time.time;phase=8;
                }
                else if(phase==8&&Time.time-openedAt>1.2f)
                {
                    Require(!door.enabled&&Quaternion.Angle(closedRotation,door.transform.rotation)<0.01f,"Door rotated during swap");Require(Object.FindObjectsByType<AudioListener>().Count(l=>l.isActiveAndEnabled)==1,"AudioListeners");

                    Require(door.transform.position==closedPosition&&Field<GameObject>(door,"openedDoor").transform.position==openedPosition,"Door objects moved");
                    Debug.Log("HOUSE_PUZZLE_PLAY_PASSED code="+code+" idle,activation API,repeat guard,wrong ERROR,buttons,numpad,Backspace,Esc,Enter,locks,HUD,door object swap, transforms preserved");Finish(0);
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("HousePuzzleCheck",false);EditorApplication.Exit(code);}
    }
}

