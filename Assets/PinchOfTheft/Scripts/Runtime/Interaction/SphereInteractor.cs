using PinchOfTheft.CCTV;
using PinchOfTheft.Core;
using UnityEngine;
using UnityEngine.InputSystem;
namespace PinchOfTheft.Interaction
{
    [DefaultExecutionOrder(-10)]
    public sealed class SphereInteractor : MonoBehaviour
    {
        [SerializeField] private CameraManager cameraManager;
        [SerializeField] private CameraMenuUI menu;
        [SerializeField] private LayerMask interactMask;
        [SerializeField] private float maxDistance=15f;
        [SerializeField] private float holdDuration=2f;
        private InteractableSphere target;
        private CCTVCamera previousCamera;
        private float progress;
        private bool waitForRelease;
        private bool holdSoundStarted;
        private AimProbe probe;
        public bool HasTarget => target != null && target.IsAvailable;
        private void Start() { GameContext.Resolve(this); if(GameContext.Instance!=null)probe=GameContext.Instance.AimProbe; }
        private void Update()
        {
            if(cameraManager==null || probe==null || cameraManager.InputLocked) { ResetTarget();return; }
            var keyboard=Keyboard.current;
            if(keyboard==null){ResetTarget();return;}
            if(!keyboard.eKey.isPressed)waitForRelease=false;
            var active=cameraManager.ActiveCamera;
            bool changed=active!=previousCamera;previousCamera=active;
            InteractableSphere next=null;
            if(menu!=null && !menu.IsOpen && !keyboard.tabKey.wasPressedThisFrame && active!=null && probe.HasHit && probe.Hit.distance<=maxDistance)
            {next=probe.Collider.GetComponentInParent<InteractableSphere>();if(next!=null && !next.IsAvailable)next=null;}
            if(changed || next!=target)ResetTarget();target=next;
            if(!HasTarget)return;
            if(changed || !keyboard.eKey.isPressed || waitForRelease)
            {
                if (holdSoundStarted) { GameContext.Audio?.CancelHold(); holdSoundStarted = false; }
                progress=0;
            }
            else
            {
                float duration = target.HoldDuration > 0 ? target.HoldDuration : holdDuration;
                if (!holdSoundStarted) { GameContext.Audio?.BeginHold(duration); holdSoundStarted = true; }
                progress+=Time.deltaTime/Mathf.Max(0.01f,duration);
            }
            target.SetVisual(progress,true);
            if(progress<1)return;
            float completedDuration = target.HoldDuration > 0 ? target.HoldDuration : holdDuration;
            target.Complete(); GameContext.Audio?.CompleteHold(completedDuration); holdSoundStarted = false; waitForRelease=true; ResetTarget();
        }
        private void ResetTarget(){if (holdSoundStarted) { GameContext.Audio?.CancelHold(); holdSoundStarted = false; } if(target!=null)target.SetVisual(0,false);target=null;progress=0;}
        private void OnDisable()=>ResetTarget();
    }
}
