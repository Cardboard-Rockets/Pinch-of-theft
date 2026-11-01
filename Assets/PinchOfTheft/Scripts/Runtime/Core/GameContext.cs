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
        public static AudioManager Audio => Instance != null ? Instance.GetComponent<AudioManager>() : UnityEngine.Object.FindAnyObjectByType<AudioManager>();
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
