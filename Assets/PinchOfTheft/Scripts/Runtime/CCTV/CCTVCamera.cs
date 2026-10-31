using UnityEngine;
using UnityEngine.Serialization;

namespace PinchOfTheft.CCTV
{
    [RequireComponent(typeof(Camera), typeof(AudioListener))]
    public sealed class CCTVCamera : MonoBehaviour
    {
        [Header("Номер и доступность камеры")]
        [SerializeField, Range(0, 9), Tooltip("Первая цифра номера камеры")] private int group;
        [SerializeField, Range(1, 9)] private int slot = 1;
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



