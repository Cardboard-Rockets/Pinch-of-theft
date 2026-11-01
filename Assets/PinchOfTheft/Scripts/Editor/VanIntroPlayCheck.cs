using System;
using System.IO;
using System.Linq;
using PinchOfTheft.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class VanIntroPlayCheck
    {
        private const string SessionKey = "VanIntroPlayCheck";
        private static VanIntroSequence sequence;
        private static Keyboard keyboard;
        private static Keyboard previousKeyboard;
        private static int stage, completed;
        private static double started;
        private static Quaternion wheelStart;
        private static Vector3 windshieldPose, parkedPosition, initialVehiclePosition;
        private static Quaternion parkedWheel;
        private static bool originalBackground;

        static VanIntroPlayCheck()
        {
            EditorApplication.playModeStateChanged += Changed;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists("Logs/Intro-Play.request") || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete("Logs/Intro-Play.request"); Run();
            };
        }

        [MenuItem("PinchOfTheft/Intro/Check Opening In Play Mode")]
        public static void Run()
        {
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save the current scene before the Play Mode check.");
            EditorSceneManager.OpenScene(VanIntroSetup.ScenePath);
            SessionState.SetBool(SessionKey,true);
            EditorApplication.EnterPlaymode();
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(SessionKey,false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                stage=0;completed=0;started=EditorApplication.timeSinceStartup;
                originalBackground=Application.runInBackground;Application.runInBackground=true;
                EditorApplication.update += Tick;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(SessionKey,false);EditorApplication.update -= Tick;
            }
        }

        private static void Require(bool value,string message) { if(!value)throw new Exception(message); }

        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-started>45)throw new Exception("Play check timed out at stage "+stage);
                if(!EditorApplication.isPlaying || Time.frameCount<3)return;
                if(stage==0)
                {
                    sequence=Object.FindAnyObjectByType<VanIntroSequence>();Require(sequence!=null,"No sequence.");
                    sequence.Completed+=()=>completed++;
                    previousKeyboard=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>("VanIntroCheckKeyboard");keyboard.MakeCurrent();
                    sequence.Replay();wheelStart=sequence.Wheels[0].transform.localRotation;initialVehiclePosition=sequence.Vehicle.position;
                    Time.timeScale=6;stage=1;
                }
                else if(stage==1 && sequence.Elapsed>.7f)
                {
                    Require(Vector3.Distance(sequence.Vehicle.position,initialVehiclePosition)>1,"Van did not travel.");
                    Require(Quaternion.Angle(wheelStart,sequence.Wheels[0].transform.localRotation)>1,"Wheels did not rotate.");
                    stage=2;
                }
                else if(stage==2 && sequence.IsComplete)
                {
                    Require(completed==1,"Full playback completion event count differs from one.");
                    Require(sequence.CurrentShot==sequence.FinalShotLabel,"Did not reach the final shot.");
                    Require(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled)==1,"Camera conflict.");
                    Require(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l=>l.enabled)==1,"Listener conflict.");
                    windshieldPose=sequence.View.transform.localPosition;
                    parkedPosition=sequence.Vehicle.position;parkedWheel=sequence.Wheels[0].transform.localRotation;
                    Require(sequence.CurrentSpeed==0,"Van did not stop.");
                    var parkingTarget=GameObject.Find("ParkingTarget_FromHouse");
                    if(parkingTarget!=null)Require(Vector3.Distance(parkedPosition,parkingTarget.transform.position)<.001f,"Parking differs from the source House scene.");
                    Require(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).All(t=>t.name!="Cabin_Interior"),"Unwanted cabin geometry.");
                    stage=3;
                }
                else if(stage==3 && sequence.Elapsed>sequence.Duration+2)
                {
                    Require(Vector3.Distance(windshieldPose,sequence.View.transform.localPosition)<.001f,"Windshield camera moved after end.");
                    Require(completed==1,"Completion event repeated while holding final shot.");
                    Require(Vector3.Distance(parkedPosition,sequence.Vehicle.position)<.001f,"Parked van drifted.");
                    Require(Quaternion.Angle(parkedWheel,sequence.Wheels[0].transform.localRotation)<.001f,"Parked wheels kept rotating.");
                    Require(SceneManager.GetActiveScene().path==VanIntroSetup.ScenePath,"Unexpected scene transition.");
                    // Replay using the actual input path, then hold Space to test skipping.
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R));stage=4;
                }
                else if(stage==4 && sequence.Elapsed<1)
                {
                    Require(sequence.TransitionFadeAlpha<.001f,"Replay did not reset the transition fade.");
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Space));stage=5;
                }
                else if(stage==5 && sequence.IsComplete)
                {
                    Require(completed==2,"Skip did not complete once.");
                    Require(Vector3.Distance(windshieldPose,sequence.View.transform.localPosition)<.001f,"Skip final pose differs.");
                    Require(Vector3.Distance(parkedPosition,sequence.Vehicle.position)<.001f,"Skip parking position differs.");
                    Require(sequence.CurrentShot==sequence.FinalShotLabel,"Skip did not select final shot.");
                    if(sequence.IsFirstPersonRoute)Require(sequence.OriginalScreenDistance<.07f&&sequence.TransitionFadeAlpha<.001f,"Skip did not reach the original laptop screen.");
                    Finish(true,"Night arrival; first-person route; exit and entry fades; seated window view; parked hold; replay and skip match original-screen close-up; one camera/listener; no scene transition.");
                }
            }
            catch(Exception e){Finish(false,e.ToString());}
        }

        private static void Finish(bool passed,string detail)
        {
            EditorApplication.update-=Tick;
            if(keyboard!=null && keyboard.added)InputSystem.RemoveDevice(keyboard);
            if(previousKeyboard!=null && previousKeyboard.added)previousKeyboard.MakeCurrent();
            Time.timeScale=1;Application.runInBackground=originalBackground;
            Directory.CreateDirectory("Logs/Intro");
            File.WriteAllText("Logs/Intro/PlayCheck.txt",(passed?"INTRO_PLAY_CHECK_PASSED":"INTRO_PLAY_CHECK_FAILED")+"\n"+detail);
            Debug.Log((passed?"INTRO_PLAY_CHECK_PASSED: ":"INTRO_PLAY_CHECK_FAILED: ")+detail);
            EditorApplication.ExitPlaymode();
        }
    }
}
