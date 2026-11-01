# House: доска и коробка

Настроены существующие board → f_Board и подтверждённая пользователем Box_A1 → Box_B. Трансформы существующих объектов сохранены.

| Объект | Настройка |
|---|---|
| board | Существующий BoxCollider; NavMeshModifier Ignore From Build; carving obstacle; BoardInteraction с BoardController, E «Уронить» |
| f_Board | Существующий Collider оставлен без изменений; выключен на старте |
| Box_A1 | Существующий MeshCollider; Ignore From Build; carving obstacle |
| Box_B | Существующий MeshCollider сохранён; Ignore From Build; carving obstacle, локальный размер 0.02 × 0.02 × 0.02, мировой примерно 0.70 × 0.70 × 0.70 м; выключен на старте |
| BoxController | Второй BoardController, E «Отодвинуть» |

Точка доски: (7.676, 1.155, -23.607). Точка коробки: (2.428, 1.155, -31.613). Подход к коробке требует открытых дверей. Пользователь разрешил рассчитывать его для этого состояния. В Play Mode после открытия дверей полный путь подтверждён; точки внутри расширенных bounds Box_B отброшены.

HouseNavMesh.asset перепечён. CameraManager кэширует все BoardController один раз и подавляет E, если любой из них IsAimed. Q/E вне взаимодействия не изменены.

Исправлена ошибка definite assignment в редакторском тесте и переход теста между фазами. Для подхода к дверям и объектам добавлена короткая выдержка перед отменой после остановки агента, чтобы обновился физический коллайдер.

Проверено автоматизированно в headless Play Mode:
- board: попап, E, подход, смена на f_Board после прибытия (0.399 м);
- Box_A1: попап, E, подход, смена на Box_B после прибытия (0.400 м);
- Box_B: полный путь из 6 углов огибает bounds коробки;
- обычная и кодовая двери: разрешение, E, подход, открытие;
- компиляция без ошибок; Validate House: Core=1, cameras=13, listeners=1, Player=True, NavMesh=True, MissingScript=0, MissingReference=0.

Не проверены вручную визуальный вид попапов, все ракурсы пользовательских камер, отмена подхода реальным ПКМ и полное прохождение остальных головоломок. f_Board имеет существующий Collider; отдельная проверка перекрытия прохода после падения не выполнена.

Изменены: House.unity, HouseNavMesh.asset, CameraManager.cs, BoardController.cs, DoorController.cs. Добавлены редакторские HousePropAudit, HousePropSetup, HousePropPlayCheck, HouseBoxReachabilityCheck и этот отчёт. Публичные UnityEvent API сохранены.

## Assets/PinchOfTheft/Scripts/Runtime/Interaction/BoardController.cs

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
        [SerializeField] private bool requireContact = true;
        [SerializeField] private UnityEvent onFallen = new();
        private bool pending;
        private bool fallen;
        private Vector3 destination;
        private int commandFrame;
        private float commandTime;
        private float stoppedSince = -1f;
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
            if (cameraManager == null || mover == null || standingBoard == null || fallenBoard == null || approachPoint == null)
                Debug.LogWarning("BoardController: назначь standingBoard, fallenBoard, approachPoint и PlayerMover", this);
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
            Vector3 remaining = mover.transform.position - approachPoint.position;
            remaining.y = 0f;
            if (!mover.PathPending && (requireContact ? IsTouchingBoard() : remaining.sqrMagnitude <= arriveTolerance * arriveTolerance))
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
            else if (!mover.PathPending && !mover.IsMoving)
            {
                if (stoppedSince < 0f) stoppedSince = Time.time;
                else if (Time.time - stoppedSince > 0.3f) pending = false;
            }
            else stoppedSince = -1f;
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
            stoppedSince = -1f;
        }

        private bool IsTouchingBoard()
        {
            var capsule = mover.GetComponent<CapsuleCollider>();
            if (capsule == null) return false;
            Bounds body = capsule.bounds;
            float radius = Mathf.Min(body.extents.x, body.extents.z);
            float halfSegment = Mathf.Max(0f, body.extents.y - radius);
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

## Assets/PinchOfTheft/Scripts/Runtime/CCTV/CameraManager.cs

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
        private BoardController[] sceneBoards;
        private DoorController[] sceneDoors;
        private bool IsBoardAimed()
        {
            if (sceneBoards != null) foreach (var candidate in sceneBoards)
                if (candidate != null && candidate.isActiveAndEnabled && candidate.IsAimed) return true;
            return false;
        }
        private bool IsDoorAimed()
        {
            if (sceneDoors != null) foreach (var candidate in sceneDoors)
                if (candidate != null && candidate.isActiveAndEnabled && candidate.IsAimed) return true;
            return false;
        }
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
            sceneBoards = Object.FindObjectsByType<BoardController>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            if (door == null) door = Object.FindAnyObjectByType<DoorController>();
            sceneDoors = Object.FindObjectsByType<DoorController>(FindObjectsInactive.Include,FindObjectsSortMode.None);
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
            else if (keyboard != null && keyboard.eKey.wasPressedThisFrame && (interactor == null || !interactor.HasTarget) && !IsBoardAimed() && !IsDoorAimed()) SwitchCamera(1);
            if (Mouse.current != null)
                ActiveCamera.Rotate(Mouse.current.delta.ReadValue() * mouseSensitivity);
        }

        private void SwitchCamera(int direction)
        {
            if (menu.IsOpen || ActiveCamera == null) return;
            CCTVCamera next = null;
            int nearest = int.MaxValue;
            foreach (var candidate in cameras)
            {
                if (candidate == null || !candidate.isAccessible || !candidate.isActiveAndEnabled || candidate.Group != ActiveCamera.Group) continue;
                int distance = (candidate.Slot - ActiveCamera.Slot) * direction;
                if (distance <= 0 || distance >= nearest) continue;
                next = candidate;
                nearest = distance;
            }
            if (next != null) SelectCamera(next);
        }

        private CCTVCamera FirstAvailable()
        {
            for (int group = 0; group <= 9; group++)
                for (int slot = 1; slot <= 9; slot++)
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
                int lastSlot = 4;
                foreach (var camera in cameras)
                    if (camera != null && camera.Group == ActiveCamera.Group) lastSlot = Mathf.Max(lastSlot, camera.Slot);
                for (int slot = 1; slot <= lastSlot; slot++)
                {
                    CCTVCamera candidate = FindCamera(ActiveCamera.Group, slot);
                    int distance = Mathf.Abs(slot - ActiveCamera.Slot);
                    distance = Mathf.Min(distance, lastSlot - distance);
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

## Assets/PinchOfTheft/Scripts/Runtime/Interaction/DoorController.cs

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
        [SerializeField] private DoorOpener opener;
        [SerializeField] private bool chooseReachableSide;
        private bool pending;
        private bool opened;
        private Vector3 destination;
        private int commandFrame;
        private float commandTime;
        private float stoppedSince = -1f;
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
                if (opener != null) opener.Open();
                else { closedDoor.SetActive(false); openDoor.SetActive(true); }
                SetPopup(false);
                if (marker != null) marker.Hide();
                onOpened.Invoke();
                enabled = false;
            }
            else if (!mover.PathPending && !mover.IsMoving)
            {
                // После остановки агента коллайдер обновляется на следующем шаге физики.
                if (stoppedSince < 0f) stoppedSince = Time.time;
                else if (Time.time - stoppedSince > 0.3f) pending = false;
            }
            else stoppedSince = -1f;
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
            if (chooseReachableSide)
            {
                if (cachedColliders == null || cachedColliders.Length == 0) return;
                var agent = mover.GetComponent<NavMeshAgent>();
                if (agent == null || !agent.isOnNavMesh) return;
                Bounds bounds = cachedColliders[0].bounds;
                Vector3 normal = bounds.size.x < bounds.size.z ? Vector3.right : Vector3.forward;
                float best = float.PositiveInfinity;
                var path = new NavMeshPath();
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 candidate = bounds.center + normal * (agent.radius + 0.12f) * side;
                    candidate.y = approachPoint.position.y;
                    if (!NavMesh.SamplePosition(candidate, out var sample, sampleRadius, agent.areaMask) ||
                        !agent.CalculatePath(sample.position,path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    var corners = path.corners; float length = 0f;
                    for(int i=1;i<corners.Length;i++) length += Vector3.Distance(corners[i-1],corners[i]);
                    if(length>=best)continue;
                    best=length; destination=sample.position;
                }
                if(float.IsInfinity(best))return;
            }
            else
            {
                if (!NavMesh.SamplePosition(approachPoint.position, out var hit, sampleRadius, NavMesh.AllAreas)) return;
                destination = hit.position;
            }
            mover.MoveTo(destination);
            if (marker != null) marker.Show(destination);
            pending = true;
            commandFrame = Time.frameCount;
            commandTime = Time.time;
            stoppedSince = -1f;
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
