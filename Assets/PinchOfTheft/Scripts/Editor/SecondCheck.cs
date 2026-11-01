using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
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
    public static class SecondCheck
    {
        private static T F<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
        private static void R(bool b,string m){if(!b)throw new Exception(m);}
        static SecondCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("SecondCheck",false))EditorApplication.update+=Tick;};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("SecondCheck",true);EditorApplication.EnterPlaymode();}
        private static void Tick()
        {
            if(Time.frameCount<5)return;EditorApplication.update-=Tick;
            try
            {
                var ctx=GameContext.Instance;var source=Object.FindAnyObjectByType<CodePuzzle>();var codeLock=Object.FindAnyObjectByType<CodeLock>();var key=Object.FindAnyObjectByType<KeypadUI>();var interaction=codeLock.GetComponent<InteractableSphere>();int generated=0;F<UnityEvent>(source,"onCodeGenerated").AddListener(()=>generated++);
                R(!interaction.IsAvailable,"Lock initially available");codeLock.Open();R(!key.IsOpen,"Lock opened without code");source.OnShieldActivated();R(source.HasCode&&!source.CodeVisible&&!interaction.IsAvailable,"Lock available before TV");source.ActivateTV();R(!interaction.IsAvailable,"Lock must wait for elec");Object.FindObjectsByType<InteractableSphere>().Single(t=>t.name.Equals("elec",StringComparison.OrdinalIgnoreCase)).Complete();R(interaction.IsAvailable&&generated==1,"Code and elec availability");source.OnShieldActivated();R(generated==1,"Code regenerated");
                codeLock.Open();R(key.IsOpen&&ctx.InputLock.IsLocked,"Lock UI failed");string wrong=source.Code=="0000"?"1111":"0000";foreach(char c in wrong)key.AddDigit(c-'0');key.Submit();R(!codeLock.IsSolved&&F<Text>(key,"display").text=="ERROR","Wrong lock code");key.Close();key.Open();foreach(char c in source.Code)key.AddDigit(c-'0');key.Submit();R(!source.CanOpen&&!codeLock.IsSolved,"First and second solved states mixed");codeLock.Open();R(key.IsOpen,"Lock unusable after first solve");foreach(char c in source.Code)key.AddDigit(c-'0');key.Submit();R(codeLock.IsSolved&&!key.IsOpen&&!ctx.InputLock.IsLocked&&!interaction.IsAvailable,"Second solve failed");
                var doorEvent=F<UnityEvent>(codeLock,"onSolved");var door=(DoorOpener)doorEvent.GetPersistentTarget(0);R(door.IsOpen&&!F<GameObject>(door,"closedDoor").activeSelf&&F<GameObject>(door,"openedDoor").activeSelf,"Boxx swap failed");
                Debug.Log("SECOND_LOCK_PLAY_PASSED: code event once, TV gate, ERROR, shared panel independent contexts, both door swaps");
                var flows=Object.FindObjectsByType<CurrentFlow>();var first=flows.Single(f=>f.name=="wire1");var second=flows.Single(f=>f.name=="wire2");R(F<bool>(first,"powered")&&F<bool>(second,"powered"),"elec power shared event");Object.FindObjectsByType<InteractableSphere>().Single(t=>t.name.Equals("elec",StringComparison.OrdinalIgnoreCase)).Complete();R(F<bool>(first,"powered")&&F<bool>(second,"powered"),"Shared elec event lost");second.ActivateFuse();R(!F<bool>(first,"fuseActivated"),"Fuse states shared");second.Begin();
                Debug.Log("SECOND_CIRCUIT_STATE_PASSED running="+second.IsRunning+" order="+second.WireOrder+" offset="+second.FuseOffset);
                if(!second.IsRunning){var path=F<List<Vector3>>(second,"path");for(int i=0;i<path.Count;i++)Debug.Log("SECOND_WAYPOINT "+i+" "+path[i].ToString("F5"));}
                R(Object.FindObjectsByType<AudioListener>().Count(l=>l.isActiveAndEnabled)==1,"Listeners");Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){SessionState.SetBool("SecondCheck",false);EditorApplication.Exit(code);}
    }
}

