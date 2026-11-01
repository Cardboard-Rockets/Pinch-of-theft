using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PinchOfTheft.Interaction;
using PinchOfTheft.Core;

namespace PinchOfTheft.Editor
{
    public static class AnimationAudioSetup
    {
        private const string DoorControllerPath = "Assets/Animations/Door/Door_Controller.controller";
        private const string BoardControllerPath = "Assets/Animations/Board/Boadr_Controller.controller";
        private const string MotorPath = "Assets/Sounds/enginesound.mp3";

        [MenuItem("PinchOfTheft/Setup/Apply Animation And Audio")]
        public static void ApplyAll()
        {
            ApplyScene("Assets/Scenes/House.unity");
            ApplyScene("Assets/Scenes/test_Scene.unity");
            ApplyIntro();
            AssetDatabase.SaveAssets();
            Debug.Log("ANIMATION_AUDIO_SETUP_COMPLETE");
        }

        private static void ApplyScene(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var doorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DoorControllerPath);
            var boardController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(BoardControllerPath);
            foreach (var opener in UnityEngine.Object.FindObjectsByType<DoorOpener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var closed = GetObject(opener, "closedDoor");
                if (closed == null) continue;
                var animationHost = closed.transform.Find("Movable") != null ? closed.transform.Find("Movable").gameObject : closed;
                var animator = animationHost.GetComponent<Animator>();
                if (animator == null) animator = animationHost.AddComponent<Animator>();
                animator.runtimeAnimatorController = doorController;
                SetObject(opener, "doorAnimator", animator);
                EditorUtility.SetDirty(closed);
                EditorUtility.SetDirty(opener);
            }
            foreach (var board in UnityEngine.Object.FindObjectsByType<BoardController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var standing = GetObject(board, "standingBoard");
                if (standing == null) continue;
                var animator = standing.GetComponent<Animator>() ?? standing.AddComponent<Animator>();
                animator.runtimeAnimatorController = boardController;
                EditorUtility.SetDirty(standing);
                EditorUtility.SetDirty(board);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ApplyIntro()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Intro_Van.unity", OpenSceneMode.Single);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MotorPath);
            foreach (var sequence in UnityEngine.Object.FindObjectsByType<VanIntroSequence>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(sequence);
                var property = serialized.FindProperty("engine");
                var source = property != null ? property.objectReferenceValue as AudioSource : null;
                if (source != null)
                {
                    source.clip = clip;
                    source.loop = true;
                    source.playOnAwake = false;
                    source.volume = .23f;
                    EditorUtility.SetDirty(source);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static GameObject GetObject(UnityEngine.Object target, string propertyName)
        {
            var property = new SerializedObject(target).FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as GameObject : null;
        }

        private static void SetObject(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null) return;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [InitializeOnLoadMethod]
        private static void RunInBatchMode()
        {
            if (!Application.isBatchMode || Array.IndexOf(Environment.GetCommandLineArgs(), "-executeMethod") < 0) return;
            if (SessionState.GetBool("AnimationAudioSetupDone", false)) return;
            SessionState.SetBool("AnimationAudioSetupDone", true);
            EditorApplication.delayCall += ApplyAll;
        }
    }
}
