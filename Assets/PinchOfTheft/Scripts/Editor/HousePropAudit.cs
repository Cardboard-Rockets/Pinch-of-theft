using System;
using System.Linq;
using PinchOfTheft.Core;
using PinchOfTheft.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace PinchOfTheft.Editor
{
    public static class HousePropAudit
    {
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var names = new[] { "board", "f_board", "box_a", "box_a1", "box_b" };
            foreach (var name in names)
            {
                var found = all.Where(t => Normalize(t.name) == Normalize(name)).ToArray();
                Debug.Log("PROP_AUDIT " + name + " count=" + found.Length);
                foreach (var item in found)
                {
                    var renderer = item.GetComponent<Renderer>();
                    var colliders = item.GetComponentsInChildren<Collider>(true);
                    var bounds = renderer != null ? renderer.bounds : new Bounds(item.position, Vector3.zero);
                    Debug.Log("PROP_ITEM " + Path(item) + " active=" + item.gameObject.activeSelf +
                        " pos=" + item.position.ToString("F3") + " bounds=" + bounds.ToString() +
                        " colliders=" + colliders.Length + " colliderBounds=" + (colliders.Length > 0 ? colliders[0].bounds.ToString() : "none") +
                        " boardController=" + (item.GetComponent<PinchOfTheft.Interaction.BoardController>() != null));
                }
            }
            foreach (var item in all.Where(t => Normalize(t.name).Contains("box") || Normalize(t.name).Contains("board")))
                Debug.Log("PROP_CANDIDATE " + Path(item));
            Debug.Log("PROP_CORE " + (UnityEngine.Object.FindAnyObjectByType<GameContext>() != null) +
                " DAMEK " + (all.Any(t => t.name == "Damek" && t.GetComponent<PlayerMover>() != null)) +
                " NAV " + NavMesh.CalculateTriangulation().vertices.Length);
            PrefabSetup.Validate();
        }

        private static string Normalize(string value) => new string(value.ToLowerInvariant().Where(c => c != ' ' && c != '_').ToArray());
        private static string Path(Transform item) => item.parent == null ? item.name : Path(item.parent) + "/" + item.name;
    }
}
