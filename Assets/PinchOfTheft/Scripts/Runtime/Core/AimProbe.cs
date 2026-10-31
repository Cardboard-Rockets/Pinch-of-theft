using UnityEngine;
namespace PinchOfTheft.Core
{
    [DefaultExecutionOrder(-20)]
    public sealed class AimProbe : MonoBehaviour
    {
        [SerializeField, Tooltip("Всё кроме Player и Ignore Raycast")] private LayerMask mask = ~0;
        [SerializeField, Min(0f)] private float maxDistance = 100f;
        public bool HasHit { get; private set; }
        public RaycastHit Hit { get; private set; }
        public Collider Collider => HasHit ? Hit.collider : null;
        public int Mask => mask;
        public bool TryGetRay(out Ray ray)
        {
            ray = default;
            var context = GameContext.Instance;
            if (context == null || context.CameraManager.ActiveCamera == null) return false;
            var camera = context.CameraManager.ActiveCamera.View;
            if (camera == null) return false;
            ray = camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f)); return true;
        }
        private void Update()
        {
            HasHit = false;
            var c = GameContext.Instance;
            if (c == null || c.InputLock.IsLocked || (c.Menu != null && c.Menu.IsOpen) || !TryGetRay(out var ray)) return;
            HasHit = Physics.Raycast(ray, out var hit, maxDistance, mask, QueryTriggerInteraction.Ignore); Hit = hit;
        }
    }
}
