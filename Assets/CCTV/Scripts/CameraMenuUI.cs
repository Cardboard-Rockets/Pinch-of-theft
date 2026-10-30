using UnityEngine;
using UnityEngine.UI;

namespace PinchOfTheft.CCTV
{
    public sealed class CameraMenuUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Transform[] groupContainers;
        [SerializeField] private Button buttonTemplate;
        [SerializeField] private Color activeCameraColor = new Color(0.15f, 0.7f, 0.5f);
        [SerializeField] private GameObject crosshair;
        [SerializeField] private Text cameraLabel;
        private readonly System.Collections.Generic.Dictionary<Button, CCTVCamera> buttons = new();
        private CameraManager manager;
        public bool IsOpen => panel.activeSelf;

        public void Configure(GameObject menuPanel, Transform[] containers, Button template, GameObject aim = null, Text label = null)
        {
            panel = menuPanel;
            crosshair = aim;
            cameraLabel = label;
            groupContainers = containers;
            buttonTemplate = template;
        }

        public void SetCameraLabel(string text)
        {
            if (cameraLabel != null) cameraLabel.text = text;
        }

        public void Initialize(CameraManager cameraManager)
        {
            manager = cameraManager;
            panel.SetActive(false);
            buttonTemplate.gameObject.SetActive(false);
        }

        public void SetOpen(bool open)
        {
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
            if (crosshair != null) crosshair.SetActive(!open);
            if (cameraLabel != null) cameraLabel.gameObject.SetActive(!open);
            if (open == IsOpen) return;
            if (open) RebuildButtons();
            panel.SetActive(open);
        }

        private void LateUpdate()
        {
            if (manager != null && manager.InputLocked) return;
            if (crosshair != null)
                crosshair.SetActive(!IsOpen && Cursor.lockState == CursorLockMode.Locked);
            if (!IsOpen) return;
            foreach (var entry in buttons)
            {
                bool available = entry.Value != null && entry.Value.isAccessible && entry.Value.isActiveAndEnabled;
                entry.Key.interactable = available;
                entry.Key.image.color = !available ? Color.gray :
                    entry.Value == manager.ActiveCamera ? activeCameraColor : buttonTemplate.image.color;
            }
        }

        private void RebuildButtons()
        {
            buttons.Clear();
            foreach (Transform container in groupContainers)
                foreach (Transform child in container)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            for (int group = 0; group < groupContainers.Length; group++)
                for (int slot = 1; slot <= 4; slot++)
                    foreach (CCTVCamera camera in manager.Cameras)
                    {
                        if (camera == null || !camera.IsVisibleInMenu || !camera.isActiveAndEnabled ||
                            camera.Group != group || camera.Slot != slot) continue;
                        CCTVCamera selected = camera;
                        Button button = Instantiate(buttonTemplate, groupContainers[group]);
                        buttons.Add(button, camera);
                        button.interactable = camera.isAccessible;
                        button.name = "Select_" + camera.CameraName;
                        button.GetComponentInChildren<Text>().text = camera.CameraName;
                        if (camera == manager.ActiveCamera)
                            button.image.color = activeCameraColor;
                        button.onClick.AddListener(() => manager.SelectCamera(selected));
                        button.gameObject.SetActive(true);
                    }
        }

        private void OnDisable()
        {
            if (panel != null && IsOpen) SetOpen(false);
        }
    }
}



