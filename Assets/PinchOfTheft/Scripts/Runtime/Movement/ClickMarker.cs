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
