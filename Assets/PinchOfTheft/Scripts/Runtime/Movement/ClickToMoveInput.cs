using PinchOfTheft.CCTV;
using PinchOfTheft.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
namespace PinchOfTheft.Movement
{
    public sealed class ClickToMoveInput : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private PlayerMover mover;
        [SerializeField] private ClickMarker marker;
        [SerializeField] private LayerMask raycastMask=~0;
        [SerializeField] private float maxDistance=100f;
        [SerializeField] private float sampleRadius=1.5f;
        private AimProbe probe;
        private void Start()
        {
            GameContext.Resolve(this);
            if(GameContext.Instance==null)return;
            probe=GameContext.Instance.AimProbe;GameContext.Instance.PlayerChanged+=PlayerChanged;
            PlayerChanged(GameContext.Instance.PlayerMover);
        }
        private void PlayerChanged(PlayerMover player){mover=player;marker=GameContext.Instance.Marker;}
        private void OnDestroy(){if(GameContext.Instance!=null)GameContext.Instance.PlayerChanged-=PlayerChanged;}
        private void Update()
        {
            var mouse=Mouse.current;
            if(mouse==null || !mouse.rightButton.wasPressedThisFrame || cameraManager==null || cameraManager.InputLocked || menu==null || menu.IsOpen || mover==null || marker==null || probe==null)return;
            RaycastHit hit;
            if(raycastMask.value==probe.Mask){if(!probe.HasHit || probe.Hit.distance>maxDistance)return;hit=probe.Hit;}
            else if(!probe.TryGetRay(out var ray) || !Physics.Raycast(ray,out hit,maxDistance,raycastMask,QueryTriggerInteraction.Ignore))return;
            if(!mover.TryMoveToWithClearance(hit.point,sampleRadius,out var destination))return;
            marker.Show(destination);
        }
    }
}
