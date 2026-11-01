using System;
using System.Linq;
using PinchOfTheft.Core;
using PinchOfTheft.CCTV;
using PinchOfTheft.Interaction;
using PinchOfTheft.Movement;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    public static class DamekSetup
    {
        private static void Set(Object o,string n,object v){var s=new SerializedObject(o);var p=s.FindProperty(n);if(v is float f)p.floatValue=f;else if(v is int i)p.intValue=i;else p.objectReferenceValue=v as Object;s.ApplyModifiedPropertiesWithoutUndo();}
        private static void Ignore(GameObject o){var m=o.GetComponent<NavMeshModifier>();if(m==null)m=o.AddComponent<NavMeshModifier>();m.ignoreFromBuild=true;EditorUtility.SetDirty(m);}
        public static void Verify()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            int Count()=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Sum(r=>r.GetComponentsInChildren<Transform>(true).Sum(t=>t.GetComponents<Component>().Length));
            int before=Count();Run();if(Count()!=before)throw new Exception("Damek setup duplicated components");Debug.Log("DAMEK_IDEMPOTENCE_PASSED");
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/test_Scene.unity");
            var reference=Object.FindObjectsByType<PlayerMover>().Single();var refAgent=reference.GetComponent<NavMeshAgent>();var refClick=Object.FindAnyObjectByType<ClickToMoveInput>();var cs=new SerializedObject(refClick);
            float sample=cs.FindProperty("sampleRadius").floatValue,maxDistance=cs.FindProperty("maxDistance").floatValue;
            var settings=new[]{refAgent.speed,refAgent.angularSpeed,refAgent.acceleration,refAgent.stoppingDistance};int priority=refAgent.avoidancePriority;var avoidance=refAgent.obstacleAvoidanceType;bool braking=refAgent.autoBraking;
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");var roots=scene.GetRootGameObjects();var all=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();var snapshots=all.ToDictionary(t=>t,t=>(t.name,t.parent,t.localPosition,t.localRotation,t.localScale));
            var context=Object.FindAnyObjectByType<GameContext>();if(context==null)throw new Exception("Core missing");
            Transform Unique(string n){var a=all.Where(t=>t.name.Replace(" ","").Equals(n,StringComparison.OrdinalIgnoreCase)).ToArray();if(a.Length!=1)throw new Exception(n+" candidates="+string.Join(",",a.Select(HouseCircuitAudit.Path)));return a[0];}
            var damek=Unique("Damek");var road=Unique("houseroad3");var house=Unique("House");
            var renderers=damek.GetComponentsInChildren<Renderer>(true).Where(r=>r.GetComponentInParent<ClickMarker>(true)==null).ToArray();if(renderers.Length==0)throw new Exception("Damek renderer missing");Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            Physics.SyncTransforms();bool groundFound=Physics.Raycast(new Vector3(bounds.center.x,bounds.min.y+0.03f,bounds.center.z),Vector3.down,out var floorHit,3f,~(1<<LayerMask.NameToLayer("Player")),QueryTriggerInteraction.Ignore);float floor=groundFound?floorHit.point.y:bounds.min.y;
            var collider=damek.GetComponent<CapsuleCollider>();if(collider==null)collider=damek.gameObject.AddComponent<CapsuleCollider>();var scale=damek.lossyScale;float radius=Mathf.Max(bounds.extents.x,bounds.extents.z),height=Mathf.Max(bounds.size.y,radius*2f);
            collider.center=damek.InverseTransformPoint(bounds.center);collider.direction=1;collider.radius=radius/Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));collider.height=height/Mathf.Abs(scale.y);collider.isTrigger=false;collider.enabled=true;
            var body=damek.GetComponent<Rigidbody>();if(body==null)body=damek.gameObject.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            var controller=damek.GetComponent<CharacterController>();if(controller!=null){controller.enabled=false;Debug.Log("DAMEK_DISABLED CharacterController");}
            damek.gameObject.layer=LayerMask.NameToLayer("Player");
            var agent=damek.GetComponent<NavMeshAgent>();if(agent==null)agent=damek.gameObject.AddComponent<NavMeshAgent>();agent.agentTypeID=0;agent.radius=radius;agent.height=height;agent.baseOffset=(damek.position.y-floor)/Mathf.Abs(scale.y);agent.speed=settings[0];agent.angularSpeed=settings[1];agent.acceleration=settings[2];agent.stoppingDistance=settings[3];agent.avoidancePriority=priority;agent.obstacleAvoidanceType=avoidance;agent.autoBraking=braking;
            var mover=damek.GetComponent<PlayerMover>();if(mover==null)mover=damek.gameObject.AddComponent<PlayerMover>();if(damek.GetComponent<PlayerCollisionGuard>()==null)damek.gameObject.AddComponent<PlayerCollisionGuard>();Ignore(damek.gameObject);
            var marker=damek.GetComponentInChildren<ClickMarker>(true);
            if(marker==null){var prefab=PrefabUtility.LoadPrefabContents("Assets/PinchOfTheft/Prefabs/Player/Player.prefab");var source=prefab.GetComponentInChildren<ClickMarker>(true);var go=Object.Instantiate(source.gameObject,damek,false);go.name="ClickMarker";marker=go.GetComponent<ClickMarker>();PrefabUtility.UnloadPrefabContents(prefab);}
            var markerTemplate=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PinchOfTheft/Prefabs/Player/Player.prefab").GetComponentInChildren<ClickMarker>(true);var markerScale=markerTemplate.transform.lossyScale;marker.transform.localScale=new Vector3(markerScale.x/scale.x,markerScale.y/scale.y,markerScale.z/scale.z);Set(marker,"mover",mover);marker.gameObject.SetActive(false);Ignore(marker.gameObject);
            int added=0;
            foreach(var root in new[]{house,all.Single(t=>t.name=="OpenCan")})foreach(var mesh in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(mesh.sharedMesh==null||mesh.GetComponent<Collider>()!=null||mesh.GetComponentInParent<Canvas>()!=null||mesh.name.Trim().Equals("open_door",StringComparison.OrdinalIgnoreCase))continue;
                var c=mesh.gameObject.AddComponent<MeshCollider>();c.sharedMesh=mesh.sharedMesh;c.convex=false;added++;
            }
            foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))Ignore(canvas.gameObject);
            foreach(var flow in Object.FindObjectsByType<CurrentFlow>(FindObjectsInactive.Include,FindObjectsSortMode.None)){Ignore(flow.gameObject);var s=new SerializedObject(flow);foreach(string f in new[]{"startPoint","finishPoint","currentObject","fuse"}){var t=(Transform)s.FindProperty(f).objectReferenceValue;if(t!=null)Ignore(t.gameObject);}}
            foreach(var camera in Object.FindObjectsByType<CCTVCamera>(FindObjectsInactive.Include,FindObjectsSortMode.None))Ignore(camera.transform.parent!=null?camera.transform.parent.gameObject:camera.gameObject);
            foreach(var opener in Object.FindObjectsByType<DoorOpener>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var s=new SerializedObject(opener);var closed=(GameObject)s.FindProperty("closedDoor").objectReferenceValue;var opened=(GameObject)s.FindProperty("openedDoor").objectReferenceValue;Ignore(closed);Ignore(opened);
                var obstacle=closed.GetComponent<NavMeshObstacle>();if(obstacle==null)obstacle=closed.AddComponent<NavMeshObstacle>();var mesh=closed.GetComponent<MeshFilter>().sharedMesh;obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=mesh.bounds.center;obstacle.size=mesh.bounds.size;obstacle.carving=true;obstacle.carveOnlyStationary=false;
                var e=(UnityEvent)typeof(DoorOpener).GetField("onOpened",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(opener);bool wired=false;for(int i=e.GetPersistentEventCount()-1;i>=0;i--)if(e.GetPersistentTarget(i)==obstacle.gameObject&&e.GetPersistentMethodName(i)=="SetActive"){if(wired)UnityEventTools.RemovePersistentListener(e,i);else wired=true;}if(!wired)UnityEventTools.AddBoolPersistentListener(e,obstacle.gameObject.SetActive,false);EditorUtility.SetDirty(opener);
            }
            var click=Object.FindObjectsByType<ClickToMoveInput>().Single();Set(click,"mover",mover);Set(click,"marker",marker);Set(click,"sampleRadius",sample);Set(click,"maxDistance",maxDistance);Set(click,"raycastMask",~((1<<LayerMask.NameToLayer("Player"))|(1<<2)));
            var surface=Object.FindAnyObjectByType<NavMeshSurface>();if(surface==null)surface=new GameObject("NavMesh").AddComponent<NavMeshSurface>();surface.agentTypeID=0;surface.collectObjects=CollectObjects.All;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.layerMask=~((1<<LayerMask.NameToLayer("Player"))|(1<<LayerMask.NameToLayer("UI")));surface.overrideVoxelSize=true;surface.voxelSize=radius/4f;
            float roadLevel=road.GetComponent<Renderer>().bounds.max.y;float difference=Mathf.Abs(roadLevel-floor);float step=Mathf.Max(0.4f,difference);
            var project=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);var build=project.FindProperty("m_Settings").GetArrayElementAtIndex(0);build.FindPropertyRelative("agentRadius").floatValue=radius;build.FindPropertyRelative("agentHeight").floatValue=height;build.FindPropertyRelative("agentSlope").floatValue=45f;build.FindPropertyRelative("agentClimb").floatValue=step;project.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();surface.BuildNavMesh();const string navPath="Assets/Scenes/HouseNavMesh.asset";var previous=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);if(previous==null)AssetDatabase.CreateAsset(surface.navMeshData,navPath);else{var built=surface.navMeshData;surface.RemoveData();EditorUtility.CopySerialized(built,previous);surface.navMeshData=previous;surface.AddData();Object.DestroyImmediate(built);EditorUtility.SetDirty(previous);}
            if(NavMesh.SamplePosition(new Vector3(damek.position.x,floor,damek.position.z),out var initialNav,1f,NavMesh.AllAreas))agent.baseOffset=(damek.position.y-initialNav.position.y)/Mathf.Abs(scale.y);
            foreach(var x in snapshots){var t=x.Key;var v=x.Value;if(t.GetComponent<ClickMarker>()==null&&(t.name!=v.Item1||t.parent!=v.Item2||t.localPosition!=v.Item3||t.localRotation!=v.Item4||t.localScale!=v.Item5))throw new Exception("Transform changed "+v.Item1);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("DAMEK_SETUP radius="+radius+" height="+height+" colliderCenter="+collider.center+" localRadius="+collider.radius+" localHeight="+collider.height+" baseOffset="+agent.baseOffset+" floor="+floor+" roadLevel="+roadLevel+" difference="+difference+" step="+step+" collidersAdded="+added+" triangles="+NavMesh.CalculateTriangulation().indices.Length/3);
            Debug.Log("DAMEK_SAMPLE "+NavMesh.SamplePosition(new Vector3(damek.position.x,floor,damek.position.z),out var hit,1f,NavMesh.AllAreas)+" point="+hit.position);
            PrefabSetup.Validate();
        }
    }
}





