using UnityEngine;
using UnityEngine.Serialization;

namespace PinchOfTheft.CCTV
{
    [RequireComponent(typeof(Camera), typeof(AudioListener))]
    public sealed class CCTVCamera : MonoBehaviour
    {
        [SerializeField, Range(0, 2)] private int group;
        [SerializeField, Range(1, 4)] private int slot = 1;
        [SerializeField] private bool isVisibleInMenu = true;
        [FormerlySerializedAs("maxRotationAngle")]
        [SerializeField, Range(0f, 180f)] private float maxYawAngle = 40f;
        [SerializeField, Range(0f, 89f)] private float maxPitchAngle = 30f;
        public bool isAccessible = true;
        public void SetAccessible(bool value) => isAccessible = value;
        private Quaternion initialRotation;
        private float yaw;
        private float pitch;
        private Camera view;
        private AudioListener listener;

        public int Group => group;
        public int Slot => slot;
        public string CameraName => $"{group}{slot}";
        public bool IsVisibleInMenu => isVisibleInMenu;
        public void SetVisibleInMenu(bool value) => isVisibleInMenu = value;

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


