using UnityEngine;
using UnityEngine.Events;

namespace PinchOfTheft.Interaction
{
    public sealed class DoorOpener : MonoBehaviour
    {
        [SerializeField, Tooltip("Объект закрытой двери")] private GameObject closedDoor;
        [SerializeField, Tooltip("Объект открытой двери")] private GameObject openedDoor;
        [SerializeField] private UnityEvent onOpened = new();
        [SerializeField] private Animator doorAnimator;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (closedDoor == null) closedDoor = gameObject;
            if (doorAnimator == null)
            {
                var movable = closedDoor.transform.Find("Movable");
                doorAnimator = movable != null ? movable.GetComponent<Animator>() : closedDoor.GetComponent<Animator>();
            }
            enabled = false;
        }

        public void Open()
        {
            if (IsOpen || closedDoor == null || openedDoor == null) return;
            IsOpen = true;
            if (doorAnimator != null && doorAnimator.runtimeAnimatorController != null)
            {
                doorAnimator.Play("door_open", 0, 0f);
                foreach (var collider in closedDoor.GetComponentsInChildren<Collider>()) collider.enabled = false;
                openedDoor.SetActive(false);
            }
            else
            {
                openedDoor.SetActive(true);
                closedDoor.SetActive(false);
            }
            enabled = false;
            onOpened.Invoke();
        }
    }
}
