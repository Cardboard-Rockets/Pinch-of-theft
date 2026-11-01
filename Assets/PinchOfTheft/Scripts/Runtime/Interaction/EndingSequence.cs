using PinchOfTheft.CCTV;
using PinchOfTheft.Core;
using PinchOfTheft.Movement;
using UnityEngine;
using UnityEngine.AI;

namespace PinchOfTheft.Interaction
{
    public sealed class EndingSequence : MonoBehaviour
    {
        [SerializeField] private CurrentFlow firstCircuit;
        [SerializeField] private CurrentFlow secondCircuit;
        [SerializeField] private DoorOpener laptopDoor;
        [SerializeField] private InteractableSphere firstLaptop;
        [SerializeField] private GameObject secretDoor;
        [SerializeField] private BoardController entry;
        [SerializeField] private Transform destination;
        [SerializeField] private CCTVCamera roomCamera;
        [SerializeField] private BoardController finalLaptop;
        private bool passageUnlocked;
        private bool entered;
        private bool finished;

        private void Start() => CheckCircuits();

        public void CheckCircuits()
        {
            if (firstCircuit == null || secondCircuit == null || !firstCircuit.IsFinished || !secondCircuit.IsFinished) return;
            if (laptopDoor != null) laptopDoor.Open();
            if (firstLaptop != null) firstLaptop.SetAvailable(true);
        }

        public void UnlockPassage()
        {
            if (passageUnlocked || laptopDoor == null || !laptopDoor.IsOpen) return;
            passageUnlocked = true;
            if (secretDoor != null) secretDoor.SetActive(false);
            if (entry != null) entry.SetAvailable(true);
        }

        public void EnterRoom()
        {
            var context = GameContext.Instance;
            if (entered || !passageUnlocked || context == null || context.PlayerMover == null || destination == null) return;
            var player = context.PlayerMover;
            var agent = player.GetComponent<NavMeshAgent>();
            if (agent == null || !agent.isOnNavMesh || !NavMesh.SamplePosition(destination.position, out var hit, 1f, agent.areaMask)) return;
            var guard = player.GetComponent<PlayerCollisionGuard>();
            bool guarded = guard != null && guard.enabled;
            if (guarded) guard.enabled = false;
            bool warped = agent.Warp(hit.position + Vector3.up * (agent.baseOffset * Mathf.Abs(player.transform.lossyScale.y)));
            Physics.SyncTransforms();
            if (guarded) guard.enabled = true;
            if (!warped) return;
            agent.ResetPath();
            entered = true;
            if (context.Marker != null) context.Marker.Hide();
            if (roomCamera != null)
            {
                roomCamera.SetAccessible(true);
                context.CameraManager.SelectCamera(roomCamera);
            }
            if (finalLaptop != null) finalLaptop.SetAvailable(true);
        }

        public void Finish()
        {
            if (!entered || finished) return;
            finished = true;
            Debug.Log("концовка 1", this);
        }
    }
}
