using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.Core;
using PinchOfTheft.CCTV;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    public static class HousePowerSetup
    {
        private static void Set(Object obj,string field,object value){var s=new SerializedObject(obj);var p=s.FindProperty(field);if(value is bool b)p.boolValue=b;else if(value is int i)p.intValue=i;else if(value is float f)p.floatValue=f;else p.objectReferenceValue=value as Object;s.ApplyModifiedPropertiesWithoutUndo();}
        private static void Popup(Transform target,CodePuzzle puzzle)
        {
            var prompt=target.GetComponent<NoPowerPrompt>()??target.gameObject.AddComponent<NoPowerPrompt>();
            var popup=target.Find("NoPowerPopup");
            if(popup==null)
            {
                var go=new GameObject("NoPowerPopup",typeof(RectTransform),typeof(Canvas),typeof(Image),typeof(Billboard));popup=go.transform;popup.SetParent(target,false);
                var scale=target.lossyScale;popup.localScale=new Vector3(0.005f/scale.x,0.005f/scale.y,0.005f/scale.z);popup.rotation=Quaternion.identity;
                var bounds=target.GetComponentsInChildren<Collider>(true)[0].bounds;popup.position=bounds.center+Vector3.up*(bounds.extents.y+0.3f);
                go.GetComponent<RectTransform>().sizeDelta=new Vector2(240,70);go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<Image>().color=new Color(0.04f,0.04f,0.04f,0.95f);go.GetComponent<Image>().raycastTarget=false;
                var text=new GameObject("Text",typeof(RectTransform),typeof(Text));text.transform.SetParent(popup,false);var label=text.GetComponent<Text>();label.rectTransform.sizeDelta=new Vector2(230,60);label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=30;label.alignment=TextAnchor.MiddleCenter;label.color=Color.white;label.raycastTarget=false;label.text="Нет энергии";
            }
            Set(prompt,"puzzle",puzzle);Set(prompt,"popup",popup.gameObject);popup.gameObject.SetActive(false);
        }
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");var puzzle=Object.FindAnyObjectByType<CodePuzzle>();if(puzzle==null||Object.FindAnyObjectByType<GameContext>()==null)throw new Exception("Puzzle/Core missing");
            var targets=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.Equals("on_tv_panel",StringComparison.OrdinalIgnoreCase)).ToArray();if(targets.Length!=1)throw new Exception("on_tv_panel candidates="+targets.Length);
            var target=targets[0];if(target.GetComponentsInChildren<Collider>(true).Length==0)target.gameObject.AddComponent<BoxCollider>();target.gameObject.layer=LayerMask.NameToLayer("Interactable");
            var interaction=target.GetComponent<InteractableSphere>()??target.gameObject.AddComponent<InteractableSphere>();var key=target.GetComponentInChildren<KeyPrompt>(true);
            if(key==null)
            {
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PinchOfTheft/Prefabs/Interaction/KeyPrompt.prefab"),target);var scale=target.lossyScale;root.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);root.transform.rotation=Quaternion.identity;var bounds=target.GetComponentsInChildren<Collider>(true)[0].bounds;root.transform.position=bounds.center+Vector3.up*(bounds.extents.y+0.3f);key=root.GetComponentInChildren<KeyPrompt>(true);
            }
            Set(interaction,"cameraManager",Object.FindAnyObjectByType<CameraManager>());Set(interaction,"prompt",key);Set(interaction,"unlockGroup",-1);Set(interaction,"singleUse",true);Set(interaction,"startAvailable",false);Set(interaction,"holdDuration",0.5f);
            var events=(UnityEvent)typeof(InteractableSphere).GetField("onCompleted",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(interaction);
            bool wired=false;for(int i=0;i<events.GetPersistentEventCount();i++)if(events.GetPersistentTarget(i)==puzzle&&events.GetPersistentMethodName(i)=="ActivateTV")wired=true;if(!wired)UnityEventTools.AddPersistentListener(events,puzzle.ActivateTV);EditorUtility.SetDirty(interaction);
            Set(puzzle,"separateTvActivation",true);Set(puzzle,"tvInteractable",interaction);
            var panel=(InteractableSphere)new SerializedObject(puzzle).FindProperty("panelInteractable").objectReferenceValue;
            Popup(target,puzzle);Popup(panel.transform,puzzle);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);PrefabSetup.Validate();Debug.Log("HOUSE_POWER_SETUP_PASSED");
        }
    }
}
