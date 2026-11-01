using System;
using System.Linq;
using System.Reflection;
using PinchOfTheft.CCTV;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    public static class HousePuzzleSetup
    {
        private static void Set(Object obj,string field,object value)
        {
            var s=new SerializedObject(obj);var p=s.FindProperty(field);
            if(value is int i)p.intValue=i;else if(value is float f)p.floatValue=f;else if(value is bool b)p.boolValue=b;else if(value is string t)p.stringValue=t;else p.objectReferenceValue=value as Object;
            s.ApplyModifiedPropertiesWithoutUndo();
        }
        private static UnityEvent Event(Object obj,string field)=>(UnityEvent)obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
        private static void Wire(Object obj,string field,UnityAction action)
        {
            var e=Event(obj,field);
            for(int i=0;i<e.GetPersistentEventCount();i++)if(e.GetPersistentTarget(i)==action.Target as Object && e.GetPersistentMethodName(i)==action.Method.Name)return;
            UnityEventTools.AddPersistentListener(e,action);EditorUtility.SetDirty(obj);
        }
        private static Text Text(string name,Transform parent,string value,Vector2 size,Vector2 pos,int fontSize=32)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=fontSize;text.color=Color.white;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.text=value;text.rectTransform.sizeDelta=size;text.rectTransform.anchoredPosition=pos;return text;
        }
        private static Button Button(string title,Transform parent,Vector2 pos)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(165,70);rect.anchoredPosition=pos;
            var image=go.GetComponent<Image>();image.color=new Color(0.12f,0.19f,0.25f);var button=go.GetComponent<Button>();button.targetGraphic=image;
            Text("Label",go.transform,title,rect.sizeDelta,Vector2.zero,22);return button;
        }
        private static InteractableSphere Interact(Transform target,CameraManager manager,bool singleUse,bool available,float duration)
        {
            if(target.GetComponentsInChildren<Collider>(true).Length==0)target.gameObject.AddComponent<BoxCollider>();
            target.gameObject.layer=LayerMask.NameToLayer("Interactable");
            var interaction=target.GetComponent<InteractableSphere>()??target.gameObject.AddComponent<InteractableSphere>();
            var prompt=target.GetComponentInChildren<KeyPrompt>(true);
            if(prompt==null)
            {
                var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PinchOfTheft/Prefabs/Interaction/KeyPrompt.prefab"),target);
                var scale=target.lossyScale;root.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);root.transform.rotation=Quaternion.identity;
                var bounds=target.GetComponentsInChildren<Collider>(true)[0].bounds;root.transform.position=bounds.center+Vector3.up*(bounds.extents.y+0.3f);
                prompt=root.GetComponentInChildren<KeyPrompt>(true);
            }
            Set(interaction,"cameraManager",manager);Set(interaction,"prompt",prompt);Set(interaction,"unlockGroup",-1);Set(interaction,"singleUse",singleUse);Set(interaction,"startAvailable",available);Set(interaction,"holdDuration",duration);return interaction;
        }
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var snapshots=all.ToDictionary(t=>t,t=>(t.name,t.parent,t.localPosition,t.localRotation,t.localScale));
            var context=Object.FindAnyObjectByType<GameContext>();if(context==null)throw new Exception("Core missing");
            Transform Unique(string name){var a=all.Where(t=>t.name.Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();if(a.Length!=1)throw new Exception("Candidates "+name+": "+a.Length);return a[0];}
            var unlock=Unique("onlock tv|panel");var tv=Unique("tv");var panel=Unique("keypad");
            var doorRoot=Unique("Door_code");var door=doorRoot.Find("door");if(door==null)throw new Exception("Door_code/door missing");
            var puzzle=Object.FindAnyObjectByType<CodePuzzle>()??new GameObject("CodePuzzle").AddComponent<CodePuzzle>();
            var keypad=Object.FindAnyObjectByType<KeypadUI>(FindObjectsInactive.Include)??puzzle.gameObject.AddComponent<KeypadUI>();
            var manager=Object.FindAnyObjectByType<CameraManager>();var menu=Object.FindAnyObjectByType<CameraMenuUI>();
            Set(keypad,"cameraManager",manager);Set(keypad,"menu",menu);Set(keypad,"puzzle",puzzle);Set(keypad,"maskInput",false);Set(keypad,"allowKeyboardInput",true);Set(keypad,"errorShowTime",1f);
            var canvas=Object.FindObjectsByType<Canvas>().Single(c=>c.renderMode==RenderMode.ScreenSpaceOverlay);
            var ui=canvas.transform.Find("KeypadPanel");
            if(ui==null)
            {
                var go=new GameObject("KeypadPanel",typeof(RectTransform),typeof(Image));go.transform.SetParent(canvas.transform,false);go.GetComponent<RectTransform>().sizeDelta=new Vector2(600,620);go.GetComponent<Image>().color=new Color(0.025f,0.035f,0.05f,0.98f);ui=go.transform;
                Text("Display",ui,"____",new Vector2(500,80),new Vector2(0,240),40);
                int[] digits={1,2,3,4,5,6,7,8,9,-1,0,-2};
                for(int i=0;i<digits.Length;i++){int digit=digits[i];var b=Button(digit==-1?"Стереть":digit==-2?"Подтвердить":digit.ToString(),ui,new Vector2((i%3-1)*180,130-i/3*100));if(digit>=0)UnityEventTools.AddIntPersistentListener(b.onClick,keypad.AddDigit,digit);else if(digit==-1)UnityEventTools.AddPersistentListener(b.onClick,keypad.Erase);else UnityEventTools.AddPersistentListener(b.onClick,keypad.Submit);}
                UnityEventTools.AddPersistentListener(Button("X",ui,new Vector2(210,300)).onClick,keypad.Close);
            }
            Set(keypad,"panel",ui.gameObject);Set(keypad,"display",ui.Find("Display").GetComponent<Text>());ui.gameObject.SetActive(false);
            var panelInteraction=Interact(panel,manager,false,false,0.5f);var unlockInteraction=Interact(unlock,manager,true,true,2f);
            Set(puzzle,"panelInteractable",panelInteraction);Set(puzzle,"keypad",keypad);Set(puzzle,"codeLength",4);Set(puzzle,"tvIdleText","");Set(puzzle,"tvSolvedText","OK");Set(puzzle,"successMessage","Код принят: доступ разрешён");
            Wire(unlockInteraction,"onCompleted",puzzle.OnShieldActivated);Wire(panelInteraction,"onCompleted",keypad.Open);
            var screen=tv.Find("CodeScreen");
            if(screen==null)
            {
                var renderer=tv.GetComponentsInChildren<Renderer>(true).Single();var bounds=renderer.bounds;
                var go=new GameObject("CodeScreen",typeof(RectTransform),typeof(Canvas),typeof(Image));go.transform.SetParent(tv,false);screen=go.transform;
                go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<Image>().color=new Color(0.005f,0.012f,0.015f);go.GetComponent<Image>().raycastTarget=false;
                // У этой модели широкая лицевая плоскость лежит в YZ, наружу по +X.
                screen.position=new Vector3(bounds.max.x+0.015f,bounds.center.y,bounds.center.z);screen.rotation=Quaternion.LookRotation(Vector3.left,Vector3.up);
                screen.localScale=Vector3.one*(0.005f/tv.lossyScale.x);
                var size=new Vector2(bounds.size.z*0.82f/0.005f,bounds.size.y*0.75f/0.005f);go.GetComponent<RectTransform>().sizeDelta=size;
                var label=Text("Code",screen,"",size-Vector2.one*20,Vector2.zero,110);label.color=new Color(0.5f,1f,0.7f);label.gameObject.AddComponent<Shadow>().effectColor=Color.black;
            }
            Set(puzzle,"tvText",screen.Find("Code").GetComponent<Text>());
            var opener=door.GetComponent<DoorOpener>()??door.gameObject.AddComponent<DoorOpener>();
            var openedDoor=doorRoot.Find("open_door");
            if(openedDoor==null)throw new Exception("Door_code/open_door missing");
            Set(opener,"closedDoor",door.gameObject);Set(opener,"openedDoor",openedDoor.gameObject);
            door.gameObject.SetActive(true);openedDoor.gameObject.SetActive(false);
            if(door.GetComponent<Collider>()==null)door.gameObject.AddComponent<BoxCollider>();
            var modifier=door.GetComponent<NavMeshModifier>()??door.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
            var eventSettings = new SerializedObject(puzzle);
            var calls = eventSettings.FindProperty("onSolved").FindPropertyRelative("m_PersistentCalls.m_Calls");
            for (int i = calls.arraySize - 1; i >= 0; i--)
            {
                var call = calls.GetArrayElementAtIndex(i);
                if (call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue.Contains("PinchOfTheft.Interaction.DoorOpener") &&
                    call.FindPropertyRelative("m_Target").objectReferenceValue != opener) calls.DeleteArrayElementAtIndex(i);
            }
            eventSettings.ApplyModifiedPropertiesWithoutUndo();
            Wire(puzzle,"onSolved",opener.Open);
            foreach(var pair in snapshots){var t=pair.Key;var s=pair.Value;if(t==null||t.name!=s.Item1||t.parent!=s.Item2||t.localPosition!=s.Item3||t.localRotation!=s.Item4||t.localScale!=s.Item5)throw new Exception("Original transform changed: "+s.Item1);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);PrefabSetup.Validate();
            Debug.Log("HOUSE_PUZZLE_SETUP_PASSED originals="+snapshots.Count+" door=Door_code/door screen=tv/Cube front+X");
        }
        public static void Verify()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            int Count()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Transform>(true).Sum(t=>t.GetComponents<Component>().Length));
            int count=Count();Run();if(Count()!=count)throw new Exception("Puzzle setup duplicated components");
            foreach(var interaction in Object.FindObjectsByType<InteractableSphere>())
            {
                if(interaction.name!="onlock tv|panel"&&interaction.name!="keypad")continue;
                var center=interaction.GetComponent<Collider>().bounds.center;
                foreach(var camera in Object.FindObjectsByType<CCTVCamera>())
                {
                    var delta=center-camera.transform.position;
                    var local=Quaternion.Inverse(camera.transform.rotation)*delta.normalized;
                    var settings=new SerializedObject(camera);
                    bool limits=Mathf.Abs(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg)<=settings.FindProperty("maxYawAngle").floatValue && Mathf.Abs(Mathf.Asin(local.y)*Mathf.Rad2Deg)<=settings.FindProperty("maxPitchAngle").floatValue;
                    int mask=~((1<<LayerMask.NameToLayer("Player"))|(1<<2));
                    bool clear=Physics.Raycast(camera.transform.position,delta.normalized,out var hit,delta.magnitude+0.1f,mask,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponentInParent<InteractableSphere>()==interaction;
                    if(clear&&limits&&delta.magnitude<=15)Debug.Log("PUZZLE_AIMABLE: "+interaction.name+" CAM "+camera.CameraName+" distance="+delta.magnitude+" accessible="+camera.isAccessible);
                }
            }
            var door=Object.FindAnyObjectByType<DoorOpener>();
            var opened=(GameObject)new SerializedObject(door).FindProperty("openedDoor").objectReferenceValue;
            Debug.Log("DOOR_SWAP_CONFIGURED: "+opened.name);
            Debug.Log("HOUSE_PUZZLE_IDEMPOTENCE_PASSED");
        }
    }
}

