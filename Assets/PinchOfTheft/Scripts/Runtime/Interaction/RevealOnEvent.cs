using UnityEngine;

namespace PinchOfTheft.Interaction
{
    public sealed class RevealOnEvent : MonoBehaviour
    {
        [SerializeField] private GameObject target;
        [SerializeField] private bool hideUntilBoxMoved;
        private void Awake()
        {
            if (hideUntilBoxMoved && target != null) target.SetActive(false);
        }
        public void Show()
        {
            if (target != null && !target.activeSelf) target.SetActive(true);
        }
    }
}
