using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace PinchOfTheft.Movement.Editor
{
    [InitializeOnLoad]
    public static class MovementPlayCheck
    {
        private static double started;
        private static PlayerMover mover;
        private static ClickMarker marker;
        private static int stage;
        static MovementPlayCheck()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("MovementCheck", false)) return;
                started = EditorApplication.timeSinceStartup;
                EditorApplication.update += Tick;
            };
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            SessionState.SetBool("MovementCheck", true);
            EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 30) Finish(false);
            if (Time.frameCount < 3) return;
            if (stage == 0)
            {
                mover = Object.FindAnyObjectByType<PlayerMover>();
                marker = Object.FindAnyObjectByType<ClickMarker>(FindObjectsInactive.Include);
                var agent = mover.GetComponent<NavMeshAgent>();
                if (!agent.isOnNavMesh) { Finish(false); return; }
                if (!NavMesh.SamplePosition(agent.nextPosition + Vector3.forward * 6, out var hit, 2, NavMesh.AllAreas)) { Finish(false); return; }
                mover.MoveTo(hit.position); marker.Show(hit.position); stage = 1;
            }
            else if (stage == 1 && mover.IsMoving) stage = 2;
            else if (stage == 2 && !mover.IsMoving && !marker.gameObject.activeSelf) Finish(true);
        }
        private static void Finish(bool passed)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool("MovementCheck", false);
            Debug.Log(passed ? "MOVEMENT_PLAY_PASSED" : "MOVEMENT_PLAY_FAILED");
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
