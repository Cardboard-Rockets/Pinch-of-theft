# Полный исходный код после рефакторинга

## CameraManager.cs

```csharp
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PinchOfTheft.CCTV
{
    public sealed class CameraManager : MonoBehaviour
    {
        [SerializeField] private CCTVCamera[] cameras;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private SphereInteractor interactor;
        [SerializeField] private BoardController board;
        [SerializeField] private DoorController door;
        public bool InputLocked { get => GameContext.Instance != null && GameContext.Instance.InputLock.IsLocked; set { if (GameContext.Instance == null) return; if (value) GameContext.Instance.InputLock.Acquire(this); else GameContext.Instance.InputLock.Release(this); } }
        public event System.Action<CCTVCamera> ActiveCameraChanged;
        private void Awake()
        {
            if (cameras == null || cameras.Length == 0) {
                cameras = GetComponentsInChildren<CCTVCamera>(true);
                if (cameras.Length == 0) cameras = Object.FindObjectsByType<CCTVCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                System.Array.Sort(cameras, (a,b) => a.Group != b.Group ? a.Group.CompareTo(b.Group) : a.Slot.CompareTo(b.Slot));
            }
        }
        private void OnEnable() { if (cameras != null) foreach (var camera in cameras) if (camera != null) camera.AccessibilityChanged += AvailabilityChanged; }
        private void OnDisable() { if (cameras != null) foreach (var camera in cameras) if (camera != null) camera.AccessibilityChanged -= AvailabilityChanged; }
        private void AvailabilityChanged() { if (menu != null) EnsureAvailableCamera(); }
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.15f;
        public IReadOnlyList<CCTVCamera> Cameras => cameras;
        public CCTVCamera ActiveCamera { get; private set; }

        public void Configure(CCTVCamera[] views, CameraMenuUI cameraMenu)
        {
            cameras = views;
            menu = cameraMenu;
        }

        public void UnlockGroup(int group)
        {
            if (cameras == null) return;
            foreach (CCTVCamera camera in cameras)
            {
                if (camera == null || camera.Group != group) continue;
                camera.SetAccessible(true);
                camera.SetVisibleInMenu(true);
            }
        }

        private void Start()
        {
            if (!GameContext.Require(this)) return;
            if (menu == null) menu = GameContext.Instance.Menu;
            if (interactor == null) interactor = GetComponentInChildren<SphereInteractor>(true);
            if (board == null) board = Object.FindAnyObjectByType<BoardController>();
            if (door == null) door = Object.FindAnyObjectByType<DoorController>();
            menu.Initialize(this);
            menu.SetCameraLabel("NO SIGNAL");

            if (cameras == null) return;
            foreach (CCTVCamera camera in cameras)
                if (camera != null) camera.SetActive(false);
            SelectCamera(FindCamera(0, 1) ?? FirstAvailable());
        }

        private CCTVCamera FindCamera(int group, int slot)
        {
            if (cameras == null) return null;
            foreach (CCTVCamera camera in cameras)
                if (camera != null && camera.isAccessible && camera.isActiveAndEnabled &&
                    camera.Group == group && camera.Slot == slot) return camera;
            return null;
        }

        private void Update()
        {
            if (menu == null) return;
            if (InputLocked) { if (menu.IsOpen && !GameContext.Instance.InputLock.HasOtherOwner(menu) && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) menu.SetOpen(false); return; }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                menu.SetOpen(!menu.IsOpen);
                return;
            }
            if (menu.IsOpen || ActiveCamera == null) return;

            if (keyboard != null && keyboard.qKey.wasPressedThisFrame) SwitchCamera(-1);
            else if (keyboard != null && keyboard.eKey.wasPressedThisFrame && (interactor == null || !interactor.HasTarget) && (board == null || !board.IsAimed) && (door == null || !door.IsAimed)) SwitchCamera(1);
            if (Mouse.current != null)
                ActiveCamera.Rotate(Mouse.current.delta.ReadValue() * mouseSensitivity);
        }

        private void SwitchCamera(int direction)
        {
            if (menu.IsOpen || ActiveCamera == null) return;
            int current = ActiveCamera.Slot - 1;
            for (int step = 1; step < 4; step++)
            {
                int index = current + direction * step;
                if (index < 0 || index >= 4) return;
                CCTVCamera candidate = FindCamera(ActiveCamera.Group, index + 1);
                if (candidate == null || candidate == ActiveCamera) continue;
                SelectCamera(candidate);
                return;
            }
        }

        private CCTVCamera FirstAvailable()
        {
            for (int group = 0; group < 3; group++)
                for (int slot = 1; slot <= 4; slot++)
                {
                    CCTVCamera camera = FindCamera(group, slot);
                    if (camera != null) return camera;
                }
            return null;
        }

        private void EnsureAvailableCamera()
        {
            if (ActiveCamera != null && ActiveCamera.isAccessible && ActiveCamera.isActiveAndEnabled) return;
            CCTVCamera replacement = null;
            if (ActiveCamera != null)
            {
                
                int nearestDistance = int.MaxValue;
                for (int slot = 1; slot <= 4; slot++)
                {
                    CCTVCamera candidate = FindCamera(ActiveCamera.Group, slot);
                    int distance = Mathf.Abs(slot - ActiveCamera.Slot);
                    distance = Mathf.Min(distance, 4 - distance);
                    if (candidate == null || distance >= nearestDistance) continue;
                    replacement = candidate;
                    nearestDistance = distance;
                }
            }
            replacement = replacement != null ? replacement : FirstAvailable();
            if (replacement != null)
            {
                bool wasOpen = menu.IsOpen;
                SelectCamera(replacement);
                if (wasOpen) menu.SetOpen(true);
                return;
            }
            if (cameras != null)
                foreach (CCTVCamera camera in cameras)
                    if (camera != null) camera.SetActive(false);
            ActiveCamera = null;
            ActiveCameraChanged?.Invoke(null);
            menu.SetCameraLabel("NO SIGNAL");
        }

        public void SelectCamera(CCTVCamera selected)
        {
            if (selected == null || !selected.isAccessible || !selected.isActiveAndEnabled || cameras == null ||
                System.Array.IndexOf(cameras, selected) < 0) return;
            foreach (CCTVCamera camera in cameras)
                if (camera != null) camera.SetActive(camera == selected);
            ActiveCamera = selected;
            ActiveCameraChanged?.Invoke(selected);
            menu.SetCameraLabel("CAM " + selected.CameraName);
            menu.SetOpen(false);
        }
    }
}









```

## CameraMenuUI.cs

```csharp
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
            for(int group=0;group<groupContainers.Length;group++)for(int slot=1;slot<=4;slot++)foreach(var camera in manager.Cameras)
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
        }
        private void OnDisable() { if(IsOpen)SetOpen(false); }
        private void OnDestroy()
        {
            if(manager==null)return;manager.ActiveCameraChanged-=Changed;
            foreach(var camera in manager.Cameras)if(camera!=null)camera.AccessibilityChanged-=Refresh;
        }
    }
}

```

## CCTVCamera.cs

```csharp
using UnityEngine;
using UnityEngine.Serialization;

namespace PinchOfTheft.CCTV
{
    [RequireComponent(typeof(Camera), typeof(AudioListener))]
    public sealed class CCTVCamera : MonoBehaviour
    {
        [Header("Номер и доступность камеры")]
        [SerializeField, Range(0, 2), Tooltip("Группа камер: 0, 1 или 2")] private int group;
        [SerializeField, Range(1, 4)] private int slot = 1;
        [SerializeField] private bool isVisibleInMenu = true;
        [FormerlySerializedAs("maxRotationAngle")]
        [SerializeField, Range(0f, 180f)] private float maxYawAngle = 40f;
        [SerializeField, Range(0f, 89f)] private float maxPitchAngle = 30f;
        public bool isAccessible = true;
        public event System.Action AccessibilityChanged;
        public void SetAccessible(bool value) { if (isAccessible == value) return; isAccessible = value; AccessibilityChanged?.Invoke(); }
        public Camera View => view != null ? view : (view = GetComponent<Camera>());
        public void ConfigureGroup(int value, bool accessible) { group = value; SetAccessible(accessible); SetVisibleInMenu(accessible); }
        private void OnEnable() => AccessibilityChanged?.Invoke();
        private void OnDisable() => AccessibilityChanged?.Invoke();
        private Quaternion initialRotation;
        private float yaw;
        private float pitch;
        private Camera view;
        private AudioListener listener;

        public int Group => group;
        public int Slot => slot;
        public string CameraName => $"{group}{slot}";
        public bool IsVisibleInMenu => isVisibleInMenu;
        public void SetVisibleInMenu(bool value) { if (isVisibleInMenu == value) return; isVisibleInMenu = value; AccessibilityChanged?.Invoke(); }

        private void Awake()
        {
            initialRotation = transform.localRotation;
            view = GetComponent<Camera>();
            listener = GetComponent<AudioListener>();
        }

        public void SetActive(bool active)
        {
            if (view == null) view = GetComponent<Camera>();
            if (listener == null) listener = GetComponent<AudioListener>();
            view.enabled = active;
            listener.enabled = active;
        }

        public void Rotate(Vector2 delta)
        {
            yaw = Mathf.Clamp(yaw + delta.x, -maxYawAngle, maxYawAngle);
            pitch = Mathf.Clamp(pitch - delta.y, -maxPitchAngle, maxPitchAngle);
            transform.localRotation = initialRotation * Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}




```

## CCTVGroup.cs

```csharp
using UnityEngine;
namespace PinchOfTheft.CCTV
{
    public sealed class CCTVGroup : MonoBehaviour
    {
        [SerializeField, Range(0,2), Tooltip("Номер группы камер")] private int group;
        [SerializeField, Tooltip("Доступность всей группы")] private bool accessible = true;
        private void OnValidate()
        {
            foreach (var camera in GetComponentsInChildren<CCTVCamera>(true)) camera.ConfigureGroup(group, accessible);
        }
    }
}

```

## AimProbe.cs

```csharp
using UnityEngine;
namespace PinchOfTheft.Core
{
    [DefaultExecutionOrder(-20)]
    public sealed class AimProbe : MonoBehaviour
    {
        [SerializeField, Tooltip("Всё кроме Player и Ignore Raycast")] private LayerMask mask = ~0;
        [SerializeField, Min(0f)] private float maxDistance = 100f;
        public bool HasHit { get; private set; }
        public RaycastHit Hit { get; private set; }
        public Collider Collider => HasHit ? Hit.collider : null;
        public int Mask => mask;
        public bool TryGetRay(out Ray ray)
        {
            ray = default;
            var context = GameContext.Instance;
            if (context == null || context.CameraManager.ActiveCamera == null) return false;
            var camera = context.CameraManager.ActiveCamera.View;
            if (camera == null) return false;
            ray = camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f)); return true;
        }
        private void Update()
        {
            HasHit = false;
            var c = GameContext.Instance;
            if (c == null || c.InputLock.IsLocked || (c.Menu != null && c.Menu.IsOpen) || !TryGetRay(out var ray)) return;
            HasHit = Physics.Raycast(ray, out var hit, maxDistance, mask, QueryTriggerInteraction.Ignore); Hit = hit;
        }
    }
}

```

## Billboard.cs

```csharp
using PinchOfTheft.CCTV;
using UnityEngine;
namespace PinchOfTheft.Core
{
    public sealed class Billboard : MonoBehaviour
    {
        private CameraManager manager;
        private Transform cameraTransform;
        private void OnEnable()
        {
            if (GameContext.Instance == null) return;
            manager = GameContext.Instance.CameraManager;
            manager.ActiveCameraChanged += Changed; Changed(manager.ActiveCamera);
        }
        private void Changed(CCTVCamera camera) => cameraTransform = camera != null ? camera.transform : null;
        private void OnDisable() { if (manager != null) manager.ActiveCameraChanged -= Changed; }
        private void LateUpdate() { if (cameraTransform != null) transform.rotation = cameraTransform.rotation; }
    }
}

```

## CursorController.cs

```csharp
using System.Collections.Generic;
using UnityEngine;
namespace PinchOfTheft.Core
{
    public sealed class CursorController : MonoBehaviour
    {
        private readonly HashSet<object> owners = new();
        private void Start() => Apply();
        public void Unlock(object owner) { if (owner != null) owners.Add(owner); Apply(); }
        public void Lock(object owner) { if (owner != null) owners.Remove(owner); Apply(); }
        private void Apply() { Cursor.lockState = owners.Count == 0 ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = owners.Count > 0; }
        private void OnApplicationFocus(bool focus) { if (focus) Apply(); }
    }
}

```

## GameContext.cs

```csharp
using System;
using PinchOfTheft.CCTV;
using PinchOfTheft.Movement;
using UnityEngine;
namespace PinchOfTheft.Core
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameContext : MonoBehaviour
    {
        [Header("Сервисы сцены")]
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CursorController cursorController;
        [SerializeField] private InputLock inputLock;
        [SerializeField] private AimProbe aimProbe;
        [SerializeField] private Hud hud;
        public static GameContext Instance { get; private set; }
        public CameraManager CameraManager => cameraManager;
        public CursorController CursorController => cursorController;
        public InputLock InputLock => inputLock;
        public AimProbe AimProbe => aimProbe;
        public Hud Hud => hud;
        public PlayerMover PlayerMover { get; private set; }
        public ClickMarker Marker { get; private set; }
        private CameraMenuUI cachedMenu;
        public CameraMenuUI Menu => cachedMenu != null ? cachedMenu : (cachedMenu = hud != null ? hud.GetComponentInChildren<CameraMenuUI>(true) : null);
        public event Action<PlayerMover> PlayerChanged;
        private void Awake()
        {
            if (Instance != null && Instance != this) { Debug.LogWarning("В сцене уже есть GameContext", this); gameObject.SetActive(false); return; }
            Instance = this;
        }
        public void Register(PlayerMover player)
        {
            PlayerMover = player;
            Marker = player != null ? player.GetComponentInChildren<ClickMarker>(true) : null;
            PlayerChanged?.Invoke(player);
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
        public static bool Require(MonoBehaviour owner)
        {
            if (Instance != null) return true;
            Debug.LogWarning("Добавь Core.prefab (PinchOfTheft/Setup/Add Core To Scene)", owner);
            owner.enabled = false; return false;
        }
        public static void Resolve(MonoBehaviour owner)
        {
            if (!Require(owner)) return;
            var c = Instance;
            foreach (var field in owner.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic))
            {
                if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)) continue;
                if (field.GetValue(owner) is UnityEngine.Object assigned && assigned != null) continue;
                object value = field.FieldType == typeof(CameraManager) ? c.CameraManager :
                    field.FieldType == typeof(CameraMenuUI) ? c.Menu :
                    field.FieldType == typeof(PlayerMover) ? c.PlayerMover :
                    field.FieldType == typeof(ClickMarker) ? c.Marker : null;
                if (value != null) field.SetValue(owner, value);
            }
        }
    }
}

```

## Hud.cs

```csharp
using System.Collections.Generic;
using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.UI;
namespace PinchOfTheft.Core
{
    public sealed class Hud : MonoBehaviour
    {
        [SerializeField, Tooltip("Прицел в центре экрана")] private GameObject crosshair;
        [SerializeField, Tooltip("Номер активной камеры")] private Text cameraLabel;
        private readonly HashSet<object> owners = new();
        private CameraManager manager;
        private void Start() { if (!GameContext.Require(this)) return; manager = GameContext.Instance.CameraManager; manager.ActiveCameraChanged += Changed; Changed(manager.ActiveCamera); }
        private void OnDestroy() { if (manager != null) manager.ActiveCameraChanged -= Changed; }
        private void Changed(CCTVCamera camera) => SetCameraLabel(camera != null ? "CAM " + camera.CameraName : "NO SIGNAL");
        public void SetCameraLabel(string text) { if (cameraLabel != null && cameraLabel.text != text) cameraLabel.text = text; }
        public void Hide(object owner) { owners.Add(owner); Apply(); }
        public void Show(object owner) { owners.Remove(owner); Apply(); }
        private void Apply() { if (crosshair != null) crosshair.SetActive(owners.Count == 0); if (cameraLabel != null) cameraLabel.gameObject.SetActive(owners.Count == 0); }
    }
}

```

## InputLock.cs

```csharp
using System.Collections.Generic;
using UnityEngine;
namespace PinchOfTheft.Core
{
    public sealed class InputLock : MonoBehaviour
    {
        private readonly HashSet<object> owners = new();
        public bool IsLocked => owners.Count > 0;
        public void Acquire(object owner) { if (owner != null) owners.Add(owner); }
        public void Release(object owner) { if (owner != null) owners.Remove(owner); }
        public bool HasOtherOwner(object owner) => owners.Count > (owners.Contains(owner) ? 1 : 0);
    }
}

```

## BoardController.cs

```csharp
using PinchOfTheft.CCTV;
using PinchOfTheft.Movement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    [DefaultExecutionOrder(-10)]
    public sealed class BoardController : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private PlayerMover mover;
        [SerializeField] private ClickMarker marker;
        [SerializeField] private GameObject standingBoard;
        [SerializeField] private GameObject fallenBoard;
        [SerializeField] private Transform approachPoint;
        [SerializeField] private GameObject popup;
        [SerializeField] private string popupText = "Уронить";
        [SerializeField] private LayerMask mask = ~0;
        [SerializeField] private float maxDistance = 15f;
        [SerializeField] private float arriveTolerance = 0.4f;
        [SerializeField] private float sampleRadius = 1.5f;
        [SerializeField, Min(0f)] private float contactTolerance = 0.15f;
        [SerializeField] private UnityEvent onFallen = new();
        private bool pending;
        private bool fallen;
        private Vector3 destination;
        private int commandFrame;
        private float commandTime;
        public bool IsAimed { get; private set; }
        private Collider[] cachedColliders;
        private PinchOfTheft.Core.AimProbe probe;
        private PinchOfTheft.Core.Billboard billboard;
        private Text popupLabel;
        private string actionText;
        private void Start()
        {
            PinchOfTheft.Core.GameContext.Resolve(this);
            actionText = "E  " + popupText;
            if (PinchOfTheft.Core.GameContext.Instance != null)
            {
                probe = PinchOfTheft.Core.GameContext.Instance.AimProbe;
                PinchOfTheft.Core.GameContext.Instance.PlayerChanged += PlayerChanged;
            }
            cachedColliders = standingBoard != null ? standingBoard.GetComponentsInChildren<Collider>() : System.Array.Empty<Collider>();
            if (popup != null)
            {
                popupLabel = popup.GetComponentInChildren<Text>(true);
                billboard = popup.GetComponent<PinchOfTheft.Core.Billboard>();
                if (billboard == null) billboard = popup.AddComponent<PinchOfTheft.Core.Billboard>();
            }
        }

        private void Update()
        {
            IsAimed = false;
            if (cameraManager == null || mover == null || standingBoard == null ||
                fallenBoard == null || approachPoint == null)
            {
                SetPopup(false);
                return;
            }
            if (cameraManager.InputLocked || fallen)
            {
                pending = false;
                SetPopup(false);
                return;
            }
            var active = cameraManager.ActiveCamera;
            if (active != null && menu != null && !menu.IsOpen)
            {
                if (probe != null && probe.HasHit && probe.Hit.distance <= maxDistance)
                {
                    IsAimed = probe.Collider.GetComponentInParent<InteractableSphere>() == null &&
                        probe.Collider.transform.IsChildOf(standingBoard.transform);
                }
            }
            SetPopup(IsAimed);
            if (IsAimed && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                BeginApproach();
            if (!pending || Time.frameCount == commandFrame || Time.time - commandTime < 0.25f) return;
            if (Vector3.Distance(mover.Destination, destination) > 0.2f)
            {
                pending = false;
                return;
            }
            if (!mover.PathPending && IsTouchingBoard())
            {
                fallen = true;
                pending = false;
                IsAimed = false;
                standingBoard.SetActive(false);
                fallenBoard.SetActive(true);
                SetPopup(false);
                if (marker != null) marker.Hide();
                onFallen.Invoke();
                enabled = false;
            }
            else if (!mover.PathPending && !mover.IsMoving) pending = false;
        }
        private void PlayerChanged(PlayerMover player)
        {
            mover = player;
            marker = PinchOfTheft.Core.GameContext.Instance.Marker;
            pending = false;
        }
        private void OnDestroy()
        {
            if (PinchOfTheft.Core.GameContext.Instance != null) PinchOfTheft.Core.GameContext.Instance.PlayerChanged -= PlayerChanged;
        }

        private void BeginApproach()
        {
            if (!NavMesh.SamplePosition(approachPoint.position, out var hit, sampleRadius, NavMesh.AllAreas)) return;
            destination = hit.position;
            mover.MoveTo(destination);
            if (marker != null) marker.Show(destination);
            pending = true;
            commandFrame = Time.frameCount;
            commandTime = Time.time;
        }

        private bool IsTouchingBoard()
        {
            var capsule = mover.GetComponent<CapsuleCollider>();
            if (capsule == null) return false;
            Bounds body = capsule.bounds;
            float radius = Mathf.Min(body.extents.x, body.extents.z);
            float halfSegment = Mathf.Max(0f, body.extents.y - radius);
            // Проверяем вдоль капсулы, поскольку её центр расположен выше пола.
            foreach (var collider in cachedColliders)
            {
                if (!collider.enabled || collider.isTrigger) continue;
                for (int i = 0; i <= 4; i++)
                {
                    Vector3 point = body.center + Vector3.up * Mathf.Lerp(-halfSegment, halfSegment, i / 4f);
                    if (Vector3.Distance(point, collider.ClosestPoint(point)) <= radius + contactTolerance)
                        return true;
                }
            }
            return false;
        }

        private void SetPopup(bool visible)
        {
            if (popup == null) return;
            popup.SetActive(visible);
            if (!visible) return;
            if (billboard != null) billboard.enabled = true;
            var text = popupLabel;
            if (text != null && text.text != actionText) text.text = actionText;
        }
    }
}




```

## CurrentFlow.cs

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

namespace PinchOfTheft.Interaction
{
    public sealed class CurrentFlow : MonoBehaviour
    {
        [Header("Объекты цепи")]
        [SerializeField, Tooltip("Начало цепи; можно оставить пустым для поиска по структуре")] private Transform startPoint;
        [SerializeField] private Transform finishPoint;
        [SerializeField] private Transform currentObject;
        [SerializeField] private Transform[] wires;
        [Header("Движение и события")]
        [SerializeField, Min(0f), Tooltip("Скорость в метрах в секунду")] private float speed = 2f;
        [SerializeField, Min(0f)] private float startDelay;
        [SerializeField] private bool hideUntilStart;
        [SerializeField] private bool hideOnFinish;
        [SerializeField, Min(0f)] private float junctionTolerance = 0.1f;
        [SerializeField] private string finishMessage = "Ток дошёл до конца цепи";
        [SerializeField] private UnityEvent onStarted = new();
        [SerializeField] private UnityEvent onFinished = new();
        private readonly List<Vector3> path = new();
        private readonly List<Vector3> junctions = new();
        private int nextPoint;
        private float delayLeft;
        private bool warned;
        public bool IsRunning { get; private set; }
        public bool IsFinished { get; private set; }
        public string WireOrder => string.Join(" → ", GetWires().Select(w => w != null ? w.name : "null"));

        private void Awake()
        {
            ResolveStructure();
            wires = GetWires();
            enabled = false;
            if (currentObject == null) return;
            foreach (var collider in currentObject.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in currentObject.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.useGravity = false; }
            if (hideUntilStart) currentObject.gameObject.SetActive(false);
        }
        private void ResolveStructure()
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                string name = child.name.Replace(" ", "").ToLowerInvariant();
                if (startPoint == null && (name == "start" || name == "старт")) startPoint = child;
                if (finishPoint == null && (name == "finish" || name == "финиш")) finishPoint = child;
                if (currentObject == null && (name == "current" || name == "electriccurrent" || name == "electrocurrent" || name == "электроток")) currentObject = child;
            }
        }

        private Transform[] GetWires()
        {
            if (wires != null && wires.Length > 0) return wires;
            Transform root = startPoint != null && startPoint.parent != null ? startPoint.parent : transform;
            return root.GetComponentsInChildren<Transform>(true)
                .Where(t => t != root && t != startPoint && t != finishPoint && t != currentObject &&
                    t.name.Replace(" ", "").StartsWith("wire", System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => WireNumber(t.name)).ToArray();
        }

        private static int WireNumber(string name)
        {
            Match match = Regex.Match(name.Trim(), @"\d+$");
            return match.Success && int.TryParse(match.Value, out int number) ? number : 1;
        }

        public void Begin()
        {
            if (IsRunning || IsFinished) return;
            if (!BuildPath(out string error)) { Warn(error); return; }
            string outside = OutsideSegments();
            if (outside.Length > 0) { Warn("Путь выходит за цепь, отрезки: " + outside); return; }
            currentObject.position = path[0];
            if (!hideUntilStart) currentObject.gameObject.SetActive(true);
            delayLeft = startDelay;
            nextPoint = 1;
            IsRunning = true;
            enabled = true;
            onStarted.Invoke();
        }

        private void Warn(string text)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning(text, this);
        }

        private bool ChainActive()
        {
            return startPoint != null && finishPoint != null && currentObject != null &&
                startPoint.gameObject.activeInHierarchy && finishPoint.gameObject.activeInHierarchy &&
                WiresActive();
        }
        private bool WiresActive()
        {
            if (wires == null) return false;
            foreach (var wire in wires) if (wire == null || !wire.gameObject.activeInHierarchy) return false;
            return true;
        }

        private bool BuildPath(out string error)
        {
            path.Clear(); junctions.Clear(); wires = GetWires();
            error = "Назначьте Start, Finish, объект тока и провода";
            if (startPoint == null || finishPoint == null || currentObject == null || wires.Length == 0) return false;
            if (!ChainActive()) { error = "Объекты цепи должны быть активны"; return false; }
            var entries = new Vector3[wires.Length];
            var exits = new Vector3[wires.Length];
            Vector3 previous = Center(startPoint);
            for (int i = 0; i < wires.Length; i++)
            {
                if (!Axis(wires[i], out var a, out var b)) { error = "Нет геометрии у " + wires[i].name; return false; }
                bool aFirst = (a - previous).sqrMagnitude <= (b - previous).sqrMagnitude;
                entries[i] = aFirst ? a : b; exits[i] = aFirst ? b : a;
                previous = exits[i];
            }
            Add(Center(startPoint)); Add(entries[0]);
            for (int i = 1; i < wires.Length; i++)
            {
                Closest(entries[i-1], exits[i-1], entries[i], exits[i], out var a, out var b);
                if (Vector3.Distance(a,b) > junctionTolerance)
                { error = "Разрыв цепи: " + wires[i-1].name + " / " + wires[i].name; path.Clear(); return false; }
                Vector3 joint = (a+b)*0.5f;
                Add(joint); junctions.Add(joint);
            }
            Add(exits[exits.Length-1]); Add(Center(finishPoint));
            return path.Count > 0;
        }

        private void Add(Vector3 point)
        {
            if (path.Count == 0 || Vector3.Distance(path[path.Count-1],point) >= 0.001f) path.Add(point);
        }

        private static Vector3 Center(Transform obj)
        {
            var renderer = obj.GetComponent<Renderer>(); if (renderer != null) return renderer.bounds.center;
            var collider = obj.GetComponent<Collider>(); return collider != null ? collider.bounds.center : obj.position;
        }

        private static bool Axis(Transform obj, out Vector3 a, out Vector3 b)
        {
            var filter = obj.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Bounds bounds = filter.sharedMesh.bounds;
                Vector3 size = bounds.size;
                Vector3 extent = size.x >= size.y && size.x >= size.z ? Vector3.right * bounds.extents.x :
                    size.y >= size.z ? Vector3.up * bounds.extents.y : Vector3.forward * bounds.extents.z;
                a = obj.TransformPoint(bounds.center - extent); b = obj.TransformPoint(bounds.center + extent); return true;
            }
            var renderer = obj.GetComponent<Renderer>();
            a = b = obj.position;
            if (renderer == null) return false;
            Bounds world = renderer.bounds;
            Vector3 delta = world.size.x >= world.size.y && world.size.x >= world.size.z ? Vector3.right * world.extents.x :
                world.size.y >= world.size.z ? Vector3.up * world.extents.y : Vector3.forward * world.extents.z;
            a = world.center-delta; b = world.center+delta; return true;
        }

        private static void Closest(Vector3 p, Vector3 q, Vector3 r, Vector3 s, out Vector3 a, out Vector3 b)
        {
            Vector3 u=q-p, v=s-r, w=p-r;
            float uu=Vector3.Dot(u,u), vv=Vector3.Dot(v,v), uv=Vector3.Dot(u,v);
            float uw=Vector3.Dot(u,w), vw=Vector3.Dot(v,w);
            float t=0f, h=0f;
            if (uu <= 0.000001f) h = vv > 0.000001f ? Mathf.Clamp01(vw/vv) : 0;
            else if (vv <= 0.000001f) t = Mathf.Clamp01(-uw/uu);
            else
            {
                float denominator=uu*vv-uv*uv;
                t=denominator>0.000001f ? Mathf.Clamp01((uv*vw-uw*vv)/denominator) : 0;
                h=(uv*t+vw)/vv;
                if (h<0) {h=0;t=Mathf.Clamp01(-uw/uu);}
                else if (h>1) {h=1;t=Mathf.Clamp01((uv-uw)/uu);}
            }
            a=p+u*t; b=r+v*h;
        }

        private void Update()
        {
            if (!IsRunning || !ChainActive()) return;
            float time = Time.deltaTime;
            if (delayLeft > 0)
            {
                float waited = Mathf.Min(time,delayLeft); delayLeft-=waited; time-=waited;
                if (delayLeft > 0) return;
            }
            currentObject.gameObject.SetActive(true);
            float distance = speed*time;
            // Остаток шага переносится через стык без срезания угла.
            while (nextPoint < path.Count)
            {
                float length=Vector3.Distance(currentObject.position,path[nextPoint]);
                currentObject.position=Vector3.MoveTowards(currentObject.position,path[nextPoint],distance);
                if (distance < length) return;
                distance-=length; nextPoint++;
            }
            IsRunning=false; IsFinished=true;
            enabled = false;
            Debug.Log(finishMessage,this); onFinished.Invoke();
            if (hideOnFinish) currentObject.gameObject.SetActive(false);
        }

        private static bool Contains(Transform obj, Vector3 point)
        {
            var colliders=obj.GetComponentsInChildren<Collider>();
            bool tested=false;
            foreach(var collider in colliders)
            {
                if (!collider.enabled || (collider is MeshCollider mesh && !mesh.convex)) continue;
                tested=true;
                if (Vector3.Distance(collider.ClosestPoint(point),point)<=0.01f) return true;
            }
            if (tested) return false;
            return obj.GetComponentsInChildren<Renderer>().Any(r => r.bounds.Contains(point));
        }

        private string OutsideSegments()
        {
            var objects = wires.Concat(new[]{startPoint,finishPoint}).ToArray();
            var bad=new List<int>();
            for(int i=1;i<path.Count;i++)
                for(int sample=0;sample<20;sample++)
                    if (!objects.Any(obj=>Contains(obj,Vector3.Lerp(path[i-1],path[i],sample/19f))))
                    {bad.Add(i);break;}
            return string.Join(", ",bad);
        }

        [ContextMenu("Validate path")]
        public void ValidatePath()
        {
            if (!BuildPath(out string error)) { Debug.LogWarning("Validate path: " + error,this); return; }
            string outside=OutsideSegments();
            Debug.Log(outside.Length==0 ? "Validate path: ОК. " + WireOrder : "Validate path: вне цепи отрезки " + outside,this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color=Color.cyan;
            for(int i=1;i<path.Count;i++)Gizmos.DrawLine(path[i-1],path[i]);
            Gizmos.color=Color.yellow;
            foreach(var point in junctions)Gizmos.DrawSphere(point,0.04f);
        }
    }
}

```

## DoorController.cs

```csharp
using PinchOfTheft.CCTV;
using PinchOfTheft.Movement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    [DefaultExecutionOrder(-10)]
    public sealed class DoorController : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private PlayerMover mover;
        [SerializeField] private ClickMarker marker;
        [SerializeField] private GameObject closedDoor;
        [SerializeField] private GameObject openDoor;
        [SerializeField] private Transform approachPoint;
        [SerializeField] private GameObject popup;
        [SerializeField] private string popupText = "Открыть";
        [SerializeField] private LayerMask mask = ~0;
        [SerializeField] private float maxDistance = 15f;
        [SerializeField] private float arriveTolerance = 0.4f;
        [SerializeField] private float sampleRadius = 1.5f;
        [SerializeField, Min(0f)] private float contactTolerance = 0.15f;
        [SerializeField] private UnityEvent onOpened = new();
        private bool pending;
        private bool opened;
        private Vector3 destination;
        private int commandFrame;
        private float commandTime;
        public bool IsAimed { get; private set; }
        private Collider[] cachedColliders;
        private PinchOfTheft.Core.AimProbe probe;
        private PinchOfTheft.Core.Billboard billboard;
        private Text popupLabel;
        private string actionText;
        private void Start()
        {
            PinchOfTheft.Core.GameContext.Resolve(this);
            actionText = "E  " + popupText;
            if (PinchOfTheft.Core.GameContext.Instance != null)
            {
                probe = PinchOfTheft.Core.GameContext.Instance.AimProbe;
                PinchOfTheft.Core.GameContext.Instance.PlayerChanged += PlayerChanged;
            }
            cachedColliders = closedDoor != null ? closedDoor.GetComponentsInChildren<Collider>() : System.Array.Empty<Collider>();
            if (popup != null)
            {
                popupLabel = popup.GetComponentInChildren<Text>(true);
                billboard = popup.GetComponent<PinchOfTheft.Core.Billboard>();
                if (billboard == null) billboard = popup.AddComponent<PinchOfTheft.Core.Billboard>();
            }
        }
        public bool IsUnlocked { get; private set; }
        public void Unlock() => IsUnlocked = true;

        private void Update()
        {
            IsAimed = false;
            if (cameraManager == null || mover == null || closedDoor == null ||
                openDoor == null || approachPoint == null)
            {
                SetPopup(false);
                return;
            }
            if (cameraManager.InputLocked || opened)
            {
                pending = false;
                SetPopup(false);
                return;
            }
            var active = cameraManager.ActiveCamera;
            if (active != null && menu != null && !menu.IsOpen)
            {
                if (probe != null && probe.HasHit && probe.Hit.distance <= maxDistance)
                {
                    IsAimed = probe.Collider.GetComponentInParent<InteractableSphere>() == null &&
                        probe.Collider.transform.IsChildOf(closedDoor.transform);
                }
            }
            SetPopup(IsAimed);
            if (IsUnlocked && IsAimed && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                BeginApproach();
            if (!pending || Time.frameCount == commandFrame || Time.time - commandTime < 0.25f) return;
            if (Vector3.Distance(mover.Destination, destination) > 0.2f)
            {
                pending = false;
                return;
            }
            if (!mover.PathPending && IsTouchingDoor())
            {
                opened = true;
                pending = false;
                IsAimed = false;
                closedDoor.SetActive(false);
                openDoor.SetActive(true);
                SetPopup(false);
                if (marker != null) marker.Hide();
                onOpened.Invoke();
                enabled = false;
            }
            else if (!mover.PathPending && !mover.IsMoving) pending = false;
        }
        private void PlayerChanged(PlayerMover player)
        {
            mover = player;
            marker = PinchOfTheft.Core.GameContext.Instance.Marker;
            pending = false;
        }
        private void OnDestroy()
        {
            if (PinchOfTheft.Core.GameContext.Instance != null) PinchOfTheft.Core.GameContext.Instance.PlayerChanged -= PlayerChanged;
        }

        private void BeginApproach()
        {
            if (!IsUnlocked || opened) return;
            if (!NavMesh.SamplePosition(approachPoint.position, out var hit, sampleRadius, NavMesh.AllAreas)) return;
            destination = hit.position;
            mover.MoveTo(destination);
            if (marker != null) marker.Show(destination);
            pending = true;
            commandFrame = Time.frameCount;
            commandTime = Time.time;
        }

        private bool IsTouchingDoor()
        {
            var capsule = mover.GetComponent<CapsuleCollider>();
            if (capsule == null) return false;
            Bounds body = capsule.bounds;
            float radius = Mathf.Min(body.extents.x, body.extents.z);
            float halfSegment = Mathf.Max(0f, body.extents.y - radius);
            // Проверяем вдоль капсулы, поскольку её центр расположен выше пола.
            foreach (var collider in cachedColliders)
            {
                if (!collider.enabled || collider.isTrigger) continue;
                for (int i = 0; i <= 4; i++)
                {
                    Vector3 point = body.center + Vector3.up * Mathf.Lerp(-halfSegment, halfSegment, i / 4f);
                    if (Vector3.Distance(point, collider.ClosestPoint(point)) <= radius + contactTolerance)
                        return true;
                }
            }
            return false;
        }

        private void SetPopup(bool visible)
        {
            if (popup == null) return;
            popup.SetActive(visible);
            if (!visible) return;
            if (billboard != null) billboard.enabled = true;
            var text = popupLabel;
            if (text != null) { var value = IsUnlocked ? actionText : "Закрыто"; if (text.text != value) text.text = value; }
        }
    }
}





```

## InteractableSphere.cs

```csharp
using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.Events;

namespace PinchOfTheft.Interaction
{
    public sealed class InteractableSphere : MonoBehaviour
    {
        [Header("Действие при удержании E")]
        [SerializeField, Tooltip("Группа камер; -1 отключает разблокировку")] private int unlockGroup = -1;
        [SerializeField] private KeyPrompt prompt;
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private UnityEvent onCompleted = new();
        [SerializeField, Min(0f), Tooltip("Секунды удержания; 0 использует длительность Interactor")] private float holdDuration;
        [SerializeField] private bool singleUse = true;
        [SerializeField] private bool startAvailable = true;
        public bool IsAvailable { get; private set; } = true;
        public float HoldDuration => holdDuration;
        private void Awake()
        {
            PinchOfTheft.Core.GameContext.Resolve(this);
            if (prompt == null) prompt = GetComponentInChildren<KeyPrompt>(true);
            SetAvailable(startAvailable);
        }
        private void Reset() => prompt = GetComponentInChildren<KeyPrompt>(true);
        private void OnValidate() { if (prompt == null) prompt = GetComponentInChildren<KeyPrompt>(true); }
        public void SetAvailable(bool value)
        {
            IsAvailable = value;
            enabled = value;
            if (prompt == null) return;
            if (!value) prompt.Hide();
            else { prompt.gameObject.SetActive(true); prompt.SetProgress(0); prompt.SetHighlighted(false); }
        }
        public void SetVisual(float progress, bool highlighted)
        {
            if (prompt == null || !IsAvailable) return;
            prompt.SetProgress(progress); prompt.SetHighlighted(highlighted);
        }
        public void Complete()
        {
            if (!IsAvailable) return;
            if (unlockGroup >= 0 && cameraManager != null) cameraManager.UnlockGroup(unlockGroup);
            if (singleUse) SetAvailable(false);
            onCompleted.Invoke();
        }
    }
}

```

## KeyPrompt.cs

```csharp
using PinchOfTheft.CCTV;
using PinchOfTheft.Core;
using UnityEngine;
using UnityEngine.UI;
namespace PinchOfTheft.Interaction
{
    public sealed class KeyPrompt : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image fill;
        private Billboard billboard;
        private void Awake(){billboard=GetComponent<Billboard>();if(billboard==null)billboard=gameObject.AddComponent<Billboard>();SetHighlighted(false);}
        public void SetHighlighted(bool value){if(canvasGroup!=null)canvasGroup.alpha=value?1:0;if(billboard!=null)billboard.enabled=value;enabled=value;}
        public void SetProgress(float value){if(fill!=null)fill.fillAmount=Mathf.Clamp01(value);}
        public void Hide(){SetHighlighted(false);gameObject.SetActive(false);}
    }
}

```

## SphereInteractor.cs

```csharp
using PinchOfTheft.CCTV;
using PinchOfTheft.Core;
using UnityEngine;
using UnityEngine.InputSystem;
namespace PinchOfTheft.Interaction
{
    [DefaultExecutionOrder(-10)]
    public sealed class SphereInteractor : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private LayerMask interactMask;
        [SerializeField] private float maxDistance=15f;
        [SerializeField] private float holdDuration=2f;
        private InteractableSphere target;
        private CCTVCamera previousCamera;
        private float progress;
        private bool waitForRelease;
        private AimProbe probe;
        public bool HasTarget => target != null && target.IsAvailable;
        private void Start() { GameContext.Resolve(this); if(GameContext.Instance!=null)probe=GameContext.Instance.AimProbe; }
        private void Update()
        {
            if(cameraManager==null || probe==null || cameraManager.InputLocked) { ResetTarget();return; }
            var keyboard=Keyboard.current;
            if(keyboard==null){ResetTarget();return;}
            if(!keyboard.eKey.isPressed)waitForRelease=false;
            var active=cameraManager.ActiveCamera;
            bool changed=active!=previousCamera;previousCamera=active;
            InteractableSphere next=null;
            if(menu!=null && !menu.IsOpen && !keyboard.tabKey.wasPressedThisFrame && active!=null && probe.HasHit && probe.Hit.distance<=maxDistance)
            {next=probe.Collider.GetComponentInParent<InteractableSphere>();if(next!=null && !next.IsAvailable)next=null;}
            if(changed || next!=target)ResetTarget();target=next;
            if(!HasTarget)return;
            if(changed || !keyboard.eKey.isPressed || waitForRelease)progress=0;
            else progress+=Time.deltaTime/Mathf.Max(0.01f,target.HoldDuration>0?target.HoldDuration:holdDuration);
            target.SetVisual(progress,true);
            if(progress<1)return;
            target.Complete();waitForRelease=true;ResetTarget();
        }
        private void ResetTarget(){if(target!=null)target.SetVisual(0,false);target=null;progress=0;}
        private void OnDisable()=>ResetTarget();
    }
}

```

## ClickMarker.cs

```csharp
using UnityEngine;

namespace PinchOfTheft.Movement
{
    public sealed class ClickMarker : MonoBehaviour
    {
        [SerializeField] private PlayerMover mover;
        private int shownFrame;
        private void Start() { PinchOfTheft.Core.GameContext.Resolve(this); transform.SetParent(null, true); }

        public void Show(Vector3 pos)
        {
            transform.position = pos + Vector3.up * 0.03f;
            shownFrame = Time.frameCount;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void Update()
        {
            // Даём агенту начать расчёт пути после клика.
            if (Time.frameCount > shownFrame && (mover == null || !mover.IsMoving)) Hide();
        }
    }
}

```

## ClickToMoveInput.cs

```csharp
using PinchOfTheft.CCTV;
using PinchOfTheft.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
namespace PinchOfTheft.Movement
{
    public sealed class ClickToMoveInput : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private PlayerMover mover;
        [SerializeField] private ClickMarker marker;
        [SerializeField] private LayerMask raycastMask=~0;
        [SerializeField] private float maxDistance=100f;
        [SerializeField] private float sampleRadius=1.5f;
        private AimProbe probe;
        private void Start()
        {
            GameContext.Resolve(this);
            if(GameContext.Instance==null)return;
            probe=GameContext.Instance.AimProbe;GameContext.Instance.PlayerChanged+=PlayerChanged;
            PlayerChanged(GameContext.Instance.PlayerMover);
        }
        private void PlayerChanged(PlayerMover player){mover=player;marker=GameContext.Instance.Marker;}
        private void OnDestroy(){if(GameContext.Instance!=null)GameContext.Instance.PlayerChanged-=PlayerChanged;}
        private void Update()
        {
            var mouse=Mouse.current;
            if(mouse==null || !mouse.rightButton.wasPressedThisFrame || cameraManager==null || cameraManager.InputLocked || menu==null || menu.IsOpen || mover==null || marker==null || probe==null)return;
            RaycastHit hit;
            if(raycastMask.value==probe.Mask){if(!probe.HasHit || probe.Hit.distance>maxDistance)return;hit=probe.Hit;}
            else if(!probe.TryGetRay(out var ray) || !Physics.Raycast(ray,out hit,maxDistance,raycastMask,QueryTriggerInteraction.Ignore))return;
            if(!NavMesh.SamplePosition(hit.point,out var navHit,sampleRadius,NavMesh.AllAreas))return;
            mover.MoveTo(navHit.position);marker.Show(navHit.position);
        }
    }
}

```

## PlayerMover.cs

```csharp
using UnityEngine;
using UnityEngine.AI;

namespace PinchOfTheft.Movement
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PlayerMover : MonoBehaviour
    {
        private NavMeshAgent agent;
        private bool warned;
        private void OnEnable()
        {
            if (PinchOfTheft.Core.GameContext.Instance != null) PinchOfTheft.Core.GameContext.Instance.Register(this);
        }
        private void OnDisable()
        {
            var context = PinchOfTheft.Core.GameContext.Instance;
            if (context != null && context.PlayerMover == this) context.Register(null);
        }
        public Vector3 Destination => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh ? agent.destination : transform.position;
        public bool PathPending => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.pathPending;
        public bool IsMoving => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh &&
            (agent.pathPending || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance));

        private void Awake() => agent = GetComponent<NavMeshAgent>();

        public void MoveTo(Vector3 point)
        {
            if (agent == null || !agent.isActiveAndEnabled) return;
            if (!agent.isOnNavMesh)
            {
                if (!warned)
                {
                    Debug.LogWarning("Player не на NavMesh, запеки NavMesh", this);
                    warned = true;
                }
                return;
            }
            agent.SetDestination(point);
        }
    }
}


```

## CodePuzzle.cs

```csharp
using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    public sealed class CodePuzzle : MonoBehaviour
    {
        [Header("Части головоломки")]
        [SerializeField, Tooltip("Панель становится доступной после щитка")] private InteractableSphere panelInteractable;
        [SerializeField] private KeypadUI keypad;
        [SerializeField] private Text tvText;
        [Header("Код и тексты")]
        [SerializeField, Range(1, 8), Tooltip("Число цифр в пароле")] private int codeLength = 4;
        [SerializeField] private bool useFixedCode;
        [SerializeField] private string fixedCode = "1234";
        [SerializeField] private string tvIdleText = "";
        [SerializeField] private string tvSolvedText = "OK";
        [SerializeField] private string successMessage = "Код принят: доступ разрешён";
        [SerializeField] private bool logWrongAttempts;
        [SerializeField] private string wrongMessage = "Неверный код";
        [SerializeField] private UnityEvent onSolved = new();
        [SerializeField] private UnityEvent onWrong = new();
        private string code;
        private bool solved;
        public int CodeLength => Mathf.Clamp(codeLength, 1, 8);
        public bool CanOpen => code != null && !solved;
        private void Start()
        {
            if (panelInteractable != null) panelInteractable.SetAvailable(false);
            if (tvText != null) tvText.text = tvIdleText;
        }
        public void OnShieldActivated()
        {
            if (code != null) return;
            bool valid = fixedCode != null && fixedCode.Length == CodeLength;
            if (valid) foreach (char c in fixedCode) if (c < '0' || c > '9') valid = false;
            code = useFixedCode && valid ? fixedCode : "";
            if (code.Length == 0) for (int i = 0; i < CodeLength; i++) code += Random.Range(0, 10).ToString();
            if (tvText != null) tvText.text = code;
            if (panelInteractable != null) panelInteractable.SetAvailable(true);
        }
        public bool CheckCode(string input)
        {
            if (!CanOpen) return false;
            if (input != code)
            {
                onWrong.Invoke();
                if (logWrongAttempts) Debug.Log(wrongMessage);
                if (keypad != null) keypad.ShowError();
                return false;
            }
            solved = true;
            Debug.Log(successMessage);
            onSolved.Invoke();
            if (tvText != null) tvText.text = tvSolvedText;
            if (keypad != null) keypad.Close();
            if (panelInteractable != null) panelInteractable.SetAvailable(false);
            return true;
        }
    }
}

```

## KeypadUI.cs

```csharp
using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    public sealed class KeypadUI : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private CodePuzzle puzzle;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text display;
        [SerializeField] private GameObject crosshair;
        [SerializeField] private GameObject cameraLabel;
        [SerializeField] private bool maskInput;
        [SerializeField] private bool allowKeyboardInput = true;
        [SerializeField, Min(0f)] private float errorShowTime = 1f;
        private string input = "";
        private float errorUntil;
        public bool IsOpen { get; private set; }
        public void Open()
        {
            if (PinchOfTheft.Core.GameContext.Instance == null) return;
            if (cameraManager == null || menu == null || menu.IsOpen || puzzle == null || !puzzle.CanOpen || panel == null) return;
            IsOpen = true;
            var context = PinchOfTheft.Core.GameContext.Instance;
            context.InputLock.Acquire(this);
            context.CursorController.Unlock(this);
            context.Hud.Hide(this);
            input = ""; errorUntil = 0; panel.SetActive(true); Refresh();
        }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (panel != null) panel.SetActive(false);
            var context = PinchOfTheft.Core.GameContext.Instance;
            if (context != null)
            {
                context.InputLock.Release(this);
                context.CursorController.Lock(this);
                context.Hud.Show(this);
            }
        }
        public void AddDigit(int digit)
        {
            if (!IsOpen || puzzle == null || digit < 0 || digit > 9 || Time.unscaledTime < errorUntil || input.Length >= puzzle.CodeLength) return;
            input += digit.ToString(); Refresh();
        }
        public void Erase()
        {
            if (!IsOpen || input.Length == 0) return;
            input = input.Substring(0, input.Length - 1); Refresh();
        }
        public void Submit()
        {
            if (IsOpen && puzzle != null && input.Length == puzzle.CodeLength) puzzle.CheckCode(input);
        }
        public void ShowError() { input = ""; errorUntil = Time.unscaledTime + errorShowTime; Refresh(); }
        private void Refresh()
        {
            if (display == null || puzzle == null) return;
            display.text = Time.unscaledTime < errorUntil ? "ERROR" :
                (maskInput ? new string('*', input.Length) : input).PadRight(puzzle.CodeLength, '_');
        }
        private void Update()
        {
            if (!IsOpen) return;
            if (panel == null || !panel.activeInHierarchy) { Close(); return; }
            if (errorUntil > 0 && Time.unscaledTime >= errorUntil) { errorUntil = 0; Refresh(); }
            var keyboard = Keyboard.current;
            if (!allowKeyboardInput || keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
            for (int digit = 0; digit <= 9; digit++)
            {
                Key main = digit == 0 ? Key.Digit0 : (Key)((int)Key.Digit1 + digit - 1);
                Key num = digit == 0 ? Key.Numpad0 : (Key)((int)Key.Numpad1 + digit - 1);
                if (keyboard[main].wasPressedThisFrame || keyboard[num].wasPressedThisFrame) AddDigit(digit);
            }
            if (keyboard.backspaceKey.wasPressedThisFrame) Erase();
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Submit();
        }
        private void OnDisable() { if (IsOpen) Close(); }
        private void Start() => PinchOfTheft.Core.GameContext.Resolve(this);
    }
}

```

