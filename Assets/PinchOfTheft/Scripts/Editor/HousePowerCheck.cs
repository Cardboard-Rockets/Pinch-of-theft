using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class HousePowerCheck
    {
        private static int phase,frame;
        private static double began;
        private static CodePuzzle puzzle;
        private static InteractableSphere panel,tv;
        private static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);
        private static void Require(bool value,string text){if(!value)throw new Exception(text);}
        static HousePowerCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("PowerCheck",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("PowerCheck",true);EditorApplication.EnterPlaymode();}
        private static void Aim(InteractableSphere target)
        {
            var camera=GameContext.Instance.CameraManager.ActiveCamera;
            var bounds=target.GetComponent<Collider>().bounds;
            // В тестовой сессии ставим камеру перед объектом, изменения в сцену не сохраняются.
            camera.transform.position=bounds.center+Vector3.up*0.1f+Vector3.forward*1.5f;camera.transform.LookAt(bounds.center);Physics.SyncTransforms();frame=Time.frameCount;
        }
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-began>30)throw new Exception("Power check timeout");
                if(Time.frameCount<5||Time.frameCount<frame+3)return;
                var context=GameContext.Instance;
                if(phase==0)
                {
                    puzzle=Object.FindAnyObjectByType<CodePuzzle>();panel=Field<InteractableSphere>(puzzle,"panelInteractable");tv=Field<InteractableSphere>(puzzle,"tvInteractable");
                    Require(!puzzle.HasPower&&!panel.IsAvailable&&!tv.IsAvailable,"Initially powered");tv.Complete();Require(Field<Text>(puzzle,"tvText").text=="","TV activated without power");Aim(tv);phase=1;
                }
                else if(phase==1)
                {
                    Require(Field<GameObject>(tv.GetComponent<NoPowerPrompt>(),"popup").activeSelf,"TV no-power popup absent");Aim(panel);phase=2;
                }
                else if(phase==2)
                {
                    Require(Field<GameObject>(panel.GetComponent<NoPowerPrompt>(),"popup").activeSelf,"Panel no-power popup absent");
                    var unlock=Object.FindObjectsByType<InteractableSphere>().Single(t=>{var e=Field<UnityEvent>(t,"onCompleted");for(int i=0;i<e.GetPersistentEventCount();i++)if(e.GetPersistentTarget(i)==puzzle&&e.GetPersistentMethodName(i)=="OnShieldActivated")return true;return false;});
                    unlock.Complete();Require(puzzle.HasPower&&panel.IsAvailable&&tv.IsAvailable,"Energy did not enable controls");Require(Field<Text>(puzzle,"tvText").text=="","Energy turned TV on too early");
                    panel.Complete();var keypad=Field<KeypadUI>(puzzle,"keypad");Require(keypad.IsOpen&&context.InputLock.IsLocked,"Panel cannot open before TV");keypad.Close();frame=Time.frameCount;phase=3;
                }
                else if(phase==3)
                {
                    Require(!Field<GameObject>(tv.GetComponent<NoPowerPrompt>(),"popup").activeSelf&&!Field<GameObject>(panel.GetComponent<NoPowerPrompt>(),"popup").activeSelf,"No-power popup remains");
                    tv.Complete();var code=Field<Text>(puzzle,"tvText").text;Require(code.Length==4&&code.All(char.IsDigit)&&!tv.IsAvailable,"TV button did not show code");puzzle.ActivateTV();Require(Field<Text>(puzzle,"tvText").text==code,"Code regenerated");
                    panel.Complete();var keypad=Field<KeypadUI>(puzzle,"keypad");foreach(char c in code)keypad.AddDigit(c-'0');keypad.Submit();var door=Object.FindAnyObjectByType<DoorOpener>(FindObjectsInactive.Include);
                    Require(door.IsOpen&&!Field<GameObject>(door,"closedDoor").activeSelf&&Field<GameObject>(door,"openedDoor").activeSelf,"Door swap failed");Require(!panel.IsAvailable&&!keypad.IsOpen&&!context.InputLock.IsLocked,"Solved panel remains active");
                    Debug.Log("POWER_CHAIN_PLAY_PASSED: no-energy hover on both objects, energy unlock, panel before TV, separate TV/code, door swap");Finish(0);
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("PowerCheck",false);EditorApplication.Exit(code);}
    }
}
