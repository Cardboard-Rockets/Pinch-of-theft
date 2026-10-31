using System.Collections.Generic;
using PinchOfTheft.Core;
using UnityEngine;
using UnityEngine.UI;
namespace PinchOfTheft.CCTV
{
    public sealed class CameraMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform[] groupContainers;
        [SerializeField] private Button buttonTemplate;
        [SerializeField] private Color activeCameraColor = new(0.15f,0.7f,0.5f);
        [SerializeField] private GameObject crosshair;
        [SerializeField] private Text cameraLabel;
        private readonly List<Button> pool = new();
        private CameraManager manager;
        public bool IsOpen => panel != null && panel.activeSelf;
        public void Configure(GameObject p, Transform[] containers, Button template, GameObject aim = null, Text label = null)
        { panel=p;groupContainers=containers;buttonTemplate=template;crosshair=aim;cameraLabel=label; }
        public void SetCameraLabel(string text) { if (GameContext.Instance != null) GameContext.Instance.Hud.SetCameraLabel(text); }
        public void Initialize(CameraManager cameraManager)
        {
            manager = cameraManager; panel.SetActive(false); buttonTemplate.gameObject.SetActive(false);
            int groups = groupContainers.Length;
            foreach (var camera in manager.Cameras) if (camera != null) groups = Mathf.Max(groups, camera.Group + 1);
            if (groups > groupContainers.Length && groupContainers.Length > 0)
            {
                int oldCount = groupContainers.Length;
                var template = groupContainers[oldCount - 1];
                System.Array.Resize(ref groupContainers, groups);
                for (int group = oldCount; group < groups; group++)
                {
                    groupContainers[group] = Instantiate(template, template.parent);
                    groupContainers[group].name = "Group " + group + "x";
                }
            }
            manager.ActiveCameraChanged += Changed;
            foreach(var camera in manager.Cameras) if(camera!=null) camera.AccessibilityChanged += Refresh;
        }
        private void Changed(CCTVCamera camera) { if(IsOpen) RebuildButtons(); }
        private void Refresh() { if(IsOpen) RebuildButtons(); }
        public void SetOpen(bool open)
        {
            var context=GameContext.Instance; if(context==null || panel==null)return;
            if(open && context.InputLock.HasOtherOwner(this))return;
            if(open) { context.InputLock.Acquire(this);context.CursorController.Unlock(this);context.Hud.Hide(this); }
            else {context.InputLock.Release(this);context.CursorController.Lock(this);context.Hud.Show(this);}
            if(open==IsOpen)return;
            if(open)RebuildButtons(); panel.SetActive(open);
        }
        private void RebuildButtons()
        {
            int used=0;
            for(int group=0;group<groupContainers.Length;group++)for(int slot=1;slot<=9;slot++)foreach(var camera in manager.Cameras)
            {
                if(camera==null || !camera.IsVisibleInMenu || !camera.isActiveAndEnabled || camera.Group!=group || camera.Slot!=slot)continue;
                if(used==pool.Count)pool.Add(Instantiate(buttonTemplate,groupContainers[group]));
                var button=pool[used++];button.transform.SetParent(groupContainers[group],false);
                button.GetComponentInChildren<Text>(true).text=camera.CameraName;
                button.interactable=camera.isAccessible;
                button.image.color=!camera.isAccessible?Color.gray:camera==manager.ActiveCamera?activeCameraColor:buttonTemplate.image.color;
                button.onClick.RemoveAllListeners();var selected=camera;button.onClick.AddListener(()=>manager.SelectCamera(selected));button.gameObject.SetActive(true);
            }
            for(int i=used;i<pool.Count;i++)pool[i].gameObject.SetActive(false);
            foreach (var container in groupContainers)
            {
                var grid = container.GetComponent<GridLayoutGroup>();
                var layout = container.GetComponent<LayoutElement>();
                if (grid == null || layout == null || grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount) continue;
                int count = 0;
                foreach (var button in pool) if (button.gameObject.activeSelf && button.transform.parent == container) count++;
                int rows = Mathf.Max(1, Mathf.CeilToInt((float)count / grid.constraintCount));
                layout.preferredHeight = Mathf.Max(100f, rows * grid.cellSize.y + (rows - 1) * grid.spacing.y);
            }
            if (groupContainers.Length > 0 && groupContainers[0].parent is RectTransform content)
            {
                var vertical = content.GetComponent<VerticalLayoutGroup>();
                if (vertical != null)
                {
                    float height = vertical.padding.vertical + vertical.spacing * (groupContainers.Length - 1);
                    foreach (var container in groupContainers) height += LayoutUtility.GetPreferredHeight((RectTransform)container);
                    if (height > content.rect.height) content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                }
            }
        }
        private void OnDisable() { if(IsOpen)SetOpen(false); }
        private void OnDestroy()
        {
            if(manager==null)return;manager.ActiveCameraChanged-=Changed;
            foreach(var camera in manager.Cameras)if(camera!=null)camera.AccessibilityChanged-=Refresh;
        }
    }
}
