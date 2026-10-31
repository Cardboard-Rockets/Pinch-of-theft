using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.CCTV;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace PinchOfTheft.Interaction.Editor
{
    [InitializeOnLoad]
    public static class PuzzlePlayCheck
    {
        static PuzzlePlayCheck() { EditorApplication.playModeStateChanged += s => { if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PuzzleCheck",false)) EditorApplication.delayCall+=Check; }; }
        public static void Run() { EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity"); SessionState.SetBool("PuzzleCheck",true); EditorApplication.EnterPlaymode(); }
        private static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
        private static void Check()
        {
            try
            {
                var puzzle=UnityEngine.Object.FindAnyObjectByType<CodePuzzle>(); var keypad=UnityEngine.Object.FindAnyObjectByType<KeypadUI>(); var manager=UnityEngine.Object.FindAnyObjectByType<CameraManager>();
                var panel=(InteractableSphere)Field(puzzle,"panelInteractable"); var tv=(Text)Field(puzzle,"tvText");
                Require(!panel.IsAvailable && tv.text=="" && !puzzle.CanOpen);
                var shield=GameObject.Find("shield").GetComponent<InteractableSphere>(); shield.Complete();
                Require(panel.IsAvailable && !shield.IsAvailable && tv.text.Length==4); string code=tv.text;
                puzzle.OnShieldActivated(); Require(tv.text==code);
                panel.Complete(); Require(keypad.IsOpen && manager.InputLocked);
                string wrong=code=="0000"?"1111":"0000"; Require(!puzzle.CheckCode(wrong)); Require(((Text)Field(keypad,"display")).text=="ERROR");
                keypad.Close(); Require(!manager.InputLocked); keypad.Open();
                foreach(char c in code)keypad.AddDigit(c-'0'); keypad.Submit();
                Require(!keypad.IsOpen && !manager.InputLocked && !panel.IsAvailable && tv.text=="OK"); keypad.Open(); Require(!keypad.IsOpen);
                Require(UnityEngine.Object.FindObjectsByType<AudioListener>().Count(a=>a.isActiveAndEnabled)==1);
                var board=UnityEngine.Object.FindAnyObjectByType<BoardController>(); Require(!((GameObject)Field(board,"fallenBoard")).activeSelf);
                Debug.Log("PUZZLE_PLAY_PASSED"); SessionState.SetBool("PuzzleCheck",false); EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e); SessionState.SetBool("PuzzleCheck",false);EditorApplication.Exit(1);}
        }
        private static void Require(bool condition){if(!condition)throw new Exception("Puzzle check failed");}
    }
}
