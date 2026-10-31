using System.Linq;
using PinchOfTheft.CCTV;
using PinchOfTheft.Movement;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction.Editor
{
    public static class PuzzleSceneSetup
    {
        private static void Set(Object target, string field, Object value)
        { var s = new SerializedObject(target); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Number(Object target, string field, float value)
        { var s = new SerializedObject(target); var p = s.FindProperty(field); if (p.propertyType == SerializedPropertyType.Integer) p.intValue = (int)value; else if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value != 0; else p.floatValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        private static Text Label(string name, Transform parent, string text, Vector2 size, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.sizeDelta = size; rt.anchoredPosition = position;
            var label = go.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 32; label.text = text; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false; return label;
        }
        private static GameObject WorldCanvas(string name, Transform parent, Vector3 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas)); go.transform.SetParent(parent, false);
            go.transform.position = pos; go.transform.localScale = Vector3.one * 0.005f;
            go.GetComponent<RectTransform>().sizeDelta = size; go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace; return go;
        }
        private static Image Image(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = size; var image = go.GetComponent<Image>(); image.raycastTarget = false; return image;
        }
        private static InteractableSphere Interaction(GameObject go, CameraManager manager, int layer)
        {
            go.layer = layer; if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
            var interaction = go.AddComponent<InteractableSphere>(); Set(interaction, "cameraManager", manager); Number(interaction, "unlockGroup", -1);
            var bounds = go.GetComponent<Renderer>() != null ? go.GetComponent<Renderer>().bounds : new Bounds(go.transform.position, Vector3.one);
            var canvas = WorldCanvas("KeyPrompt", go.transform, bounds.center + Vector3.up * (bounds.extents.y + 0.4f), new Vector2(80,80));
            var alpha = canvas.AddComponent<CanvasGroup>(); alpha.blocksRaycasts = false; alpha.alpha = 0;
            Image("Background", canvas.transform, new Vector2(80,80)).color = Color.black;
            var fill = Image("Fill", canvas.transform, new Vector2(80,80)); fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"); fill.type = UnityEngine.UI.Image.Type.Filled; fill.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical; fill.fillOrigin = 0; fill.fillAmount = 0;
            Label("E", canvas.transform, "E", new Vector2(80,80), Vector2.zero).color = Color.cyan;
            var prompt = canvas.AddComponent<KeyPrompt>(); Set(prompt,"cameraManager",manager); Set(prompt,"canvasGroup",alpha); Set(prompt,"fill",fill); Set(interaction,"prompt",prompt); return interaction;
        }
        private static void Button(Transform parent, string title, Vector2 pos, UnityEngine.Events.UnityAction action)
        {
            var image = Image(title,parent,new Vector2(160,70)); image.rectTransform.anchoredPosition = pos; image.color = new Color(0.15f,0.22f,0.3f); image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
            Label("Text",image.transform,title,new Vector2(160,70),Vector2.zero).fontSize = 23;
            // Persistent callbacks are configured separately for scene buttons.
        }
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            string[][] names = {new[]{"board","доска"},new[]{"panel","панель"},new[]{"tv","тв"},new[]{"shield","щит"}};
            var objects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(t=>t.gameObject).ToArray();
            var found = names.Select(n=>objects.Where(o=>n.Contains(o.name.ToLowerInvariant())).ToArray()).ToArray();
            if (found.Any(a=>a.Length!=1)) throw new System.Exception("Ambiguous objects: " + string.Join(";",found.Select(a=>string.Join(",",a.Select(o=>o.name)))));
            var boardObject=found[0][0]; var panelObject=found[1][0]; var tv=found[2][0]; var shield=found[3][0];
            var manager=Object.FindAnyObjectByType<CameraManager>(); var menu=Object.FindAnyObjectByType<CameraMenuUI>(); var mover=Object.FindAnyObjectByType<PlayerMover>(); var marker=Object.FindAnyObjectByType<ClickMarker>(FindObjectsInactive.Include);
            int layer=LayerMask.NameToLayer("Interactable"); int mask=~((1<<LayerMask.NameToLayer("Player"))|(1<<2));
            var interactor=Object.FindAnyObjectByType<SphereInteractor>(); Number(interactor,"interactMask",mask);
            var puzzle=new GameObject("CodePuzzle").AddComponent<CodePuzzle>();
            var keypad=puzzle.gameObject.AddComponent<KeypadUI>(); Set(keypad,"cameraManager",manager); Set(keypad,"menu",menu); Set(keypad,"puzzle",puzzle); Set(puzzle,"keypad",keypad);
            var canvas=Object.FindObjectsByType<Canvas>().First(c=>c.renderMode==RenderMode.ScreenSpaceOverlay);
            var ui=new GameObject("KeypadPanel",typeof(RectTransform),typeof(Image)); ui.transform.SetParent(canvas.transform,false); ui.GetComponent<RectTransform>().sizeDelta=new Vector2(600,620); ui.GetComponent<Image>().color=new Color(0.03f,0.04f,0.06f,0.98f);
            Set(keypad,"panel",ui); Set(keypad,"display",Label("Display",ui.transform,"____",new Vector2(500,80),new Vector2(0,240)));
            var menuSettings=new SerializedObject(menu); Set(keypad,"crosshair",menuSettings.FindProperty("crosshair").objectReferenceValue); var hud=menuSettings.FindProperty("cameraLabel").objectReferenceValue as Text; if(hud!=null)Set(keypad,"cameraLabel",hud.gameObject);
            int[] digits={1,2,3,4,5,6,7,8,9,-1,0,-2};
            for(int i=0;i<12;i++)
            {
                int digit=digits[i]; string title=digit==-1?"Стереть":digit==-2?"Подтвердить":digit.ToString();
                Button(ui.transform,title,new Vector2((i%3-1)*180,130-(i/3)*100),()=>{});
                var button=ui.transform.Find(title).GetComponent<UnityEngine.UI.Button>();
                if(digit>=0)UnityEventTools.AddIntPersistentListener(button.onClick,keypad.AddDigit,digit);
                else if(digit==-1)UnityEventTools.AddPersistentListener(button.onClick,keypad.Erase);
                else UnityEventTools.AddPersistentListener(button.onClick,keypad.Submit);
            }
            Button(ui.transform,"X",new Vector2(210,300),()=>{}); UnityEventTools.AddPersistentListener(ui.transform.Find("X").GetComponent<UnityEngine.UI.Button>().onClick,keypad.Close); ui.SetActive(false);
            var panel=Interaction(panelObject,manager,layer); Number(panel,"singleUse",0); Number(panel,"startAvailable",0); Number(panel,"holdDuration",0.5f); Set(puzzle,"panelInteractable",panel);
            var shieldInteraction=Interaction(shield,manager,layer); Number(shieldInteraction,"holdDuration",2);
            var eventField=typeof(InteractableSphere).GetField("onCompleted",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            UnityEventTools.AddPersistentListener((UnityEngine.Events.UnityEvent)eventField.GetValue(panel),keypad.Open);
            UnityEventTools.AddPersistentListener((UnityEngine.Events.UnityEvent)eventField.GetValue(shieldInteraction),puzzle.OnShieldActivated);
            var tvBounds=tv.GetComponent<Renderer>()!=null?tv.GetComponent<Renderer>().bounds:new Bounds(tv.transform.position,Vector3.one);
            var screen=WorldCanvas("TV Screen",tv.transform,tvBounds.center-tv.transform.forward*(tvBounds.extents.z+0.02f),new Vector2(200,120)); screen.transform.rotation=tv.transform.rotation;
            Image("Background",screen.transform,new Vector2(200,120)).color=new Color(0.01f,0.02f,0.02f); var tvText=Label("Code",screen.transform,"",new Vector2(200,120),Vector2.zero); tvText.fontSize=48; tvText.color=Color.green; Set(puzzle,"tvText",tvText);
            var board=new GameObject("BoardController").AddComponent<BoardController>(); Set(board,"cameraManager",manager); Set(board,"menu",menu); Set(board,"mover",mover); Set(board,"marker",marker); Set(board,"standingBoard",boardObject); Set(manager,"board",board); Number(board,"mask",mask);
            if(boardObject.GetComponent<Collider>()==null)boardObject.AddComponent<BoxCollider>();
            var fallen=Object.Instantiate(boardObject,boardObject.transform.parent); fallen.name="FallenBoard"; var bounds=boardObject.GetComponent<Renderer>().bounds; fallen.transform.Rotate(Vector3.right,90,Space.World); fallen.transform.position=bounds.center+boardObject.transform.forward*(bounds.extents.y+0.2f); var fallenBounds=fallen.GetComponent<Renderer>().bounds; fallen.transform.position+=Vector3.up*(0.06f-fallenBounds.min.y); var obstacle=fallen.AddComponent<NavMeshObstacle>(); obstacle.carving=true; obstacle.shape=NavMeshObstacleShape.Box; obstacle.size=fallen.GetComponent<MeshFilter>().sharedMesh.bounds.size; fallen.SetActive(false); Set(board,"fallenBoard",fallen);
            var point=new GameObject("ApproachPoint").transform; point.SetParent(boardObject.transform); var wanted=bounds.center-boardObject.transform.forward*(bounds.extents.z+1.2f); if(NavMesh.SamplePosition(wanted,out var hit,3,NavMesh.AllAreas))wanted=hit.position; point.position=wanted; Set(board,"approachPoint",point);
            var popup=WorldCanvas("BoardPopup",board.transform,bounds.center+Vector3.up*(bounds.extents.y+0.4f),new Vector2(240,70)); Image("Background",popup.transform,new Vector2(240,70)).color=Color.black; Label("Action",popup.transform,"E  Уронить",new Vector2(240,70),Vector2.zero); popup.SetActive(false); Set(board,"popup",popup);
            EditorUtility.SetDirty(panel); EditorUtility.SetDirty(shieldInteraction); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); Debug.Log("PUZZLE_SETUP_COMPLETE");
        }
    }
}
