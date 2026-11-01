using PinchOfTheft.Core;
using UnityEngine;
namespace PinchOfTheft.Interaction
{
    public sealed class NoPowerPrompt : MonoBehaviour
    {
        [SerializeField] private CodePuzzle puzzle;
        [SerializeField] private CodeLock powerLock;
        [SerializeField] private GameObject popup;
        [SerializeField, Min(0f)] private float maxDistance = 15f;
        private GameContext context;
        private void Start()
        {
            context = GameContext.Instance;
            if (popup != null) popup.SetActive(false);
        }
        private void Update()
        {
            if ((puzzle == null && powerLock == null) || popup == null) return;
            if (powerLock != null ? powerLock.HasPower : puzzle.HasPower)
            {
                popup.SetActive(false);
                enabled = false;
                return;
            }
            var probe = context != null ? context.AimProbe : null;
            bool aimed = probe != null && probe.HasHit && probe.Hit.distance <= maxDistance &&
                probe.Collider.GetComponentInParent<NoPowerPrompt>() == this;
            if (popup.activeSelf != aimed) popup.SetActive(aimed);
        }
        private void OnDisable() { if (popup != null) popup.SetActive(false); }
    }
}
