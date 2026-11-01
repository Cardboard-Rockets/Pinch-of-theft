using PinchOfTheft.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PinchOfTheft.Interaction
{
    [DefaultExecutionOrder(-5)]
    public sealed class LockedHint : MonoBehaviour
    {
        [SerializeField] private InteractableSphere target;
        [SerializeField] private GameObject label;
        [SerializeField] private string text = "Нет энергии";
        private GameContext context;

        private void Start()
        {
            context = GameContext.Instance;
            if (target == null) target = GetComponent<InteractableSphere>();
            if (label != null)
            {
                var caption = label.GetComponentInChildren<Text>(true);
                if (caption != null) caption.text = text;
            }
            SetVisible(false);
        }

        private void Update()
        {
            if (target != null && target.HasCompleted)
            {
                SetVisible(false);
                enabled = false;
                return;
            }
            var probe = context != null ? context.AimProbe : null;
            bool visible = target != null && !target.IsAvailable && context != null &&
                !context.InputLock.IsLocked && probe != null && probe.HasHit &&
                probe.Collider.transform.IsChildOf(target.transform);
            SetVisible(visible);
        }

        private void SetVisible(bool value)
        {
            if (label != null && label.activeSelf != value) label.SetActive(value);
        }
    }
}
