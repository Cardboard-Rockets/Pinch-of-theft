using UnityEngine;
using UnityEngine.Events;

namespace PinchOfTheft.Interaction
{
    public sealed class DoorOpener : MonoBehaviour
    {
        [SerializeField, Tooltip("Объект закрытой двери")] private GameObject closedDoor;
        [SerializeField, Tooltip("Объект открытой двери")] private GameObject openedDoor;
        [SerializeField] private UnityEvent onOpened = new();

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (closedDoor == null) closedDoor = gameObject;
            enabled = false;
        }

        public void Open()
        {
            if (IsOpen || closedDoor == null || openedDoor == null) return;
            IsOpen = true;
            openedDoor.SetActive(true);
            closedDoor.SetActive(false);
            enabled = false;
            onOpened.Invoke();
        }
    }
}
