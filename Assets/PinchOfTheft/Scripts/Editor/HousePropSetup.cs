using System;
using System.Collections.Generic;
using System.Linq;
using PinchOfTheft.Core;
using PinchOfTheft.Interaction;
using PinchOfTheft.Movement;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PinchOfTheft.Editor
{
    public static class HousePropSetup
    {
        private const string ScenePath = "Assets/Scenes/House.unity";
        private const string NavPath = "Assets/Scenes/HouseNavMesh.asset";

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var snapshots = all.ToDictionary(t => t, t => (t.localPosition, t.localRotation, t.localScale));
            var context = Object.FindAnyObjectByType<GameContext>();
            var damek = all.SingleOrDefault(t => t.name == "Damek")?.GetComponent<PlayerMover>();
            if (context == null || damek == null) throw new Exception("House: Core или Damek с PlayerMover не найден");

            var board = Unique(all, "board");
            var fallen = Unique(all, "f_board");
            var box = Unique(all, "box_a1");
            var moved = Unique(all, "box_b");
            if (board == null || fallen == null || box == null || moved == null)
                throw new Exception("House: неоднозначная пара board/f_board или подтверждённая Box_A1/Box_B");
            if (!board.gameObject.activeSelf || !box.gameObject.activeSelf)
                Debug.LogWarning("Стоящая доска или коробка уже выключена; состояние не менялось");
            if (fallen.gameObject.activeSelf) { fallen.gameObject.SetActive(false); Debug.Log("SETUP: f_Board выключена на старте"); }
            if (moved.gameObject.activeSelf) { moved.gameObject.SetActive(false); Debug.Log("SETUP: Box_B выключена на старте"); }

            ConfigureStanding(board);
            ConfigureStanding(box);
            ConfigureMoved(moved);

            var surface = Object.FindObjectsByType<NavMeshSurface>().Single();
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            var built = surface.navMeshData;
            var asset = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavPath);
            if (asset == null) throw new Exception("HouseNavMesh.asset не найден");
            surface.RemoveData();
            EditorUtility.CopySerialized(built, asset);
            surface.navMeshData = asset;
            surface.AddData();
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(asset);

            ConfigureController("BoardInteraction", board, fallen, "Уронить", context, damek);
            ConfigureController("BoxController", box, moved, "Отодвинуть", context, damek);

            foreach (var pair in snapshots)
            {
                var t = pair.Key;
                if (t == null) continue;
                var value = pair.Value;
                if (t.localPosition != value.Item1 || t.localRotation != value.Item2 || t.localScale != value.Item3)
                    throw new Exception("Изменён трансформ существующего объекта: " + Path(t));
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            PrefabSetup.Validate();
            Debug.Log("HOUSE_PROP_SETUP_PASSED");
        }

        private static Transform Unique(Transform[] all, string name)
        {
            var found = all.Where(t => Normalize(t.name) == Normalize(name)).ToArray();
            Debug.Log("PROP_FIND " + name + " count=" + found.Length + " " + string.Join(", ", found.Select(Path)));
            return found.Length == 1 ? found[0] : null;
        }

        private static string Normalize(string name) => new string(name.ToLowerInvariant().Where(c => c != ' ' && c != '_').ToArray());
        private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

        private static void Set(Object component, string field, Object value)
        {
            var data = new SerializedObject(component);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object component, string field, float value)
        {
            var data = new SerializedObject(component);
            data.FindProperty(field).floatValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object component, string field, bool value)
        {
            var data = new SerializedObject(component);
            data.FindProperty(field).boolValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object component, string field, string value)
        {
            var data = new SerializedObject(component);
            data.FindProperty(field).stringValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Collider EnsureCollider(Transform t)
        {
            var collider = t.GetComponentsInChildren<Collider>(true).FirstOrDefault(c => !c.isTrigger);
            if (collider != null) return collider;
            var box = t.GetComponent<BoxCollider>();
            if (box == null) box = t.gameObject.AddComponent<BoxCollider>();
            var mesh = t.GetComponent<MeshFilter>();
            if (mesh != null && mesh.sharedMesh != null) { box.center = mesh.sharedMesh.bounds.center; box.size = mesh.sharedMesh.bounds.size; }
            return box;
        }

        private static void ConfigureStanding(Transform t)
        {
            var collider = EnsureCollider(t);
            var modifier = t.GetComponent<NavMeshModifier>();
            if (modifier == null) modifier = t.gameObject.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            ConfigureObstacle(t, collider);
            Debug.Log("PROP_STANDING " + Path(t) + " collider=" + collider.GetType().Name +
                " world=" + collider.bounds.ToString() + " obstacle=" + t.GetComponent<NavMeshObstacle>().size.ToString("F3"));
        }

        private static void ConfigureMoved(Transform t)
        {
            var collider = EnsureCollider(t);
            if (collider.isTrigger || t.gameObject.layer == LayerMask.NameToLayer("Player") || t.gameObject.layer == 2)
                throw new Exception("Box_B: Collider или слой пропускает персонажа/луч");
            var renderer = t.GetComponent<Renderer>();
            if (renderer != null && collider is BoxCollider box)
            {
                Bounds meshBounds = t.GetComponent<MeshFilter>().sharedMesh.bounds;
                if ((box.center - meshBounds.center).sqrMagnitude > 0.001f || (box.size - meshBounds.size).sqrMagnitude > 0.001f)
                { box.center = meshBounds.center; box.size = meshBounds.size; }
            }
            var modifier = t.GetComponent<NavMeshModifier>();
            if (modifier == null) modifier = t.gameObject.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            ConfigureObstacle(t, collider);
            Debug.Log("PROP_MOVED " + Path(t) + " collider=" + collider.GetType().Name +
                " center=" + t.GetComponent<NavMeshObstacle>().center.ToString("F3") +
                " size=" + t.GetComponent<NavMeshObstacle>().size.ToString("F3"));
        }

        private static void ConfigureObstacle(Transform t, Collider collider)
        {
            var obstacle = t.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = t.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            if (collider is BoxCollider box) { obstacle.center = box.center; obstacle.size = box.size; }
            else if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
            { obstacle.center = meshCollider.sharedMesh.bounds.center; obstacle.size = meshCollider.sharedMesh.bounds.size; }
            else
            {
                var bounds = collider.bounds;
                obstacle.center = t.InverseTransformPoint(bounds.center);
                obstacle.size = new Vector3(bounds.size.x / Mathf.Abs(t.lossyScale.x),
                    bounds.size.y / Mathf.Abs(t.lossyScale.y), bounds.size.z / Mathf.Abs(t.lossyScale.z));
            }
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
        }

        private static void ConfigureController(string name, Transform standing, Transform alternate,
            string action, GameContext context, PlayerMover mover)
        {
            var service = standing.parent != null ? standing.parent.Find(name) :
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == name);
            var approach = service != null ? service.Find("ApproachPoint") : null;
            Vector3 selectedPoint = default;
            if (approach == null && !ChooseApproach(standing.GetComponent<Renderer>().bounds,
                    alternate.GetComponent<Renderer>().bounds, mover, out selectedPoint))
            {
                if (standing.name != "Box_A1" || !ChooseAfterDoorsOpen(standing.GetComponent<Renderer>().bounds,
                        alternate.GetComponent<Renderer>().bounds, mover, out selectedPoint))
                {
                    if (service != null && service.GetComponent<BoardController>() != null &&
                        new SerializedObject(service.GetComponent<BoardController>()).FindProperty("standingBoard").objectReferenceValue == null)
                        Object.DestroyImmediate(service.gameObject);
                    Debug.LogWarning("Нет достижимой точки подхода к " + Path(standing));
                    return;
                }
            }
            if (service == null)
            {
                service = new GameObject(name).transform;
                if (standing.parent != null) service.SetParent(standing.parent, false);
            }
            var controller = service.GetComponent<BoardController>();
            if (controller == null) controller = service.gameObject.AddComponent<BoardController>();
            if (approach == null)
            {
                approach = new GameObject("ApproachPoint").transform;
                approach.SetParent(service, false);
                approach.position = selectedPoint;
            }
            var popup = service.Find("Popup");
            if (popup == null) popup = CreatePopup(service, standing.GetComponent<Renderer>().bounds, action);
            popup.gameObject.SetActive(false);
            Set(controller, "cameraManager", context.CameraManager);
            Set(controller, "menu", context.Menu);
            Set(controller, "mover", mover);
            Set(controller, "marker", context.Marker);
            Set(controller, "standingBoard", standing.gameObject);
            Set(controller, "fallenBoard", alternate.gameObject);
            Set(controller, "approachPoint", approach);
            Set(controller, "popup", popup.gameObject);
            Set(controller, "popupText", action);
            Set(controller, "maxDistance", 15f);
            Set(controller, "arriveTolerance", 0.4f);
            Set(controller, "sampleRadius", 1.5f);
            Set(controller, "requireContact", false);
            var data = new SerializedObject(controller);
            data.FindProperty("mask").intValue = ~((1 << LayerMask.NameToLayer("Player")) | (1 << 2));
            data.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("PROP_CONTROLLER " + Path(service) + " action=" + action +
                " approach=" + approach.position.ToString("F3") +
                " distance=" + Vector3.Distance(approach.position, standing.GetComponent<Renderer>().bounds.center).ToString("F3"));
        }

        private static bool ChooseApproach(Bounds standing, Bounds alternate, PlayerMover mover, out Vector3 point, bool requireCompletePath = true)
        {
            point = default;
            float radius = mover.GetComponent<NavMeshAgent>().radius;
            Bounds forbidden = alternate;
            forbidden.Expand((radius + 0.2f) * 2f);
            float best = float.PositiveInfinity;
            var path = new NavMeshPath();
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 0.25f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float edge = Mathf.Abs(direction.x) * standing.extents.x + Mathf.Abs(direction.z) * standing.extents.z;
                Vector3 candidate = new Vector3(standing.center.x, standing.min.y, standing.center.z) + direction * (edge + radius + 0.5f);
                if (!NavMesh.SamplePosition(candidate, out var hit, 1f, NavMesh.AllAreas))
                { Debug.Log("APPROACH_REJECT angle=" + (i * 45) + " no NavMesh " + candidate.ToString("F3")); continue; }
                if (Mathf.Abs(hit.position.y - standing.min.y) > 0.5f)
                { Debug.Log("APPROACH_REJECT angle=" + (i * 45) + " wrong height " + hit.position.ToString("F3")); continue; }
                if (forbidden.Contains(hit.position))
                { Debug.Log("APPROACH_REJECT angle=" + (i * 45) + " inside alternate " + hit.position.ToString("F3")); continue; }
                if (!NavMesh.CalculatePath(mover.transform.position, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    if (requireCompletePath)
                    { Debug.Log("APPROACH_REJECT angle=" + (i * 45) + " path=" + path.status + " " + hit.position.ToString("F3")); continue; }
                    Debug.Log("APPROACH_DEFERRED angle=" + (i * 45) + " editor carve path=" + path.status);
                }
                float distance = Vector3.Distance(mover.transform.position, hit.position);
                Debug.Log("APPROACH_CANDIDATE angle=" + (i * 45) + " point=" + hit.position.ToString("F3") + " distance=" + distance.ToString("F3"));
                if (distance >= best) continue;
                best = distance;
                point = hit.position;
            }
            return best < float.PositiveInfinity;
        }

        private static bool ChooseAfterDoorsOpen(Bounds standing, Bounds alternate, PlayerMover mover, out Vector3 point)
        {
            var doorObstacles = Object.FindObjectsByType<DoorController>()
                .Select(door => new SerializedObject(door).FindProperty("closedDoor").objectReferenceValue as GameObject)
                .Where(closed => closed != null).Select(closed => closed.GetComponent<NavMeshObstacle>())
                .Where(obstacle => obstacle != null && obstacle.enabled).ToArray();
            foreach (var obstacle in doorObstacles) obstacle.enabled = false;
            try
            {
                // В редакторе carving обновляется позже; полный путь проверен в Play Mode с открытыми дверями.
                bool found = ChooseApproach(standing, alternate, mover, out point, false);
                Debug.Log("BOX_AFTER_DOORS_OPEN candidate=" + found + " doors=" + doorObstacles.Length);
                return found;
            }
            finally { foreach (var obstacle in doorObstacles) obstacle.enabled = true; }
        }

        private static Transform CreatePopup(Transform service, Bounds bounds, string action)
        {
            var root = new GameObject("Popup", typeof(RectTransform), typeof(Canvas), typeof(Billboard));
            root.transform.SetParent(service, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260, 60);
            Vector3 world = bounds.center + Vector3.right * (bounds.extents.x + 0.25f);
            world.y = Mathf.Min(bounds.max.y + 0.2f, bounds.center.y + 0.4f);
            rect.position = world;
            var scale = service.lossyScale;
            rect.localScale = new Vector3(0.002f / scale.x, 0.002f / scale.y, 0.002f / scale.z);
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            var bg = background.GetComponent<RectTransform>();
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;
            var image = background.GetComponent<Image>(); image.color = new Color(0.03f, 0.04f, 0.06f, 0.85f); image.raycastTarget = false;
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(root.transform, false);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 0); labelRect.offsetMax = new Vector2(-10, 0);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white;
            text.text = "E  " + action; text.raycastTarget = false;
            return root.transform;
        }
    }
}
