# Полный код настройки и проверок Damek
## Assets/PinchOfTheft/Scripts/Runtime/Movement/PlayerMover.cs
```csharp
using UnityEngine;
using UnityEngine.AI;

namespace PinchOfTheft.Movement
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PlayerMover : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Дополнительный отступ цели ПКМ от препятствий, в метрах")] private float destinationClearance = 0.12f;
        [SerializeField, Min(0.25f), Tooltip("Радиус поиска свободной цели возле места клика")] private float destinationSearchRadius = 3f;
        [SerializeField, Tooltip("Препятствия, возле которых нужен отступ цели")] private LayerMask destinationObstacleMask = ~0;
        private readonly Collider[] overlaps = new Collider[64];
        private NavMeshPath destinationPath;
        private NavMeshAgent agent;
        private bool warned;
        private void OnEnable()
        {
            if (PinchOfTheft.Core.GameContext.Instance != null) PinchOfTheft.Core.GameContext.Instance.Register(this);
        }
        private void OnDisable()
        {
            var context = PinchOfTheft.Core.GameContext.Instance;
            if (context != null && context.PlayerMover == this) context.Register(null);
        }
        public Vector3 Destination => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh ? agent.destination : transform.position;
        public bool PathPending => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.pathPending;
        public bool IsMoving => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh &&
            (agent.pathPending || (agent.hasPath && agent.remainingDistance > agent.stoppingDistance));

        private void Awake() => agent = GetComponent<NavMeshAgent>();
        private void Start()
        {
            if (agent != null && !agent.isOnNavMesh) WarnOffMesh();
        }
        private void WarnOffMesh()
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning(gameObject.name + " не на NavMesh, запеки NavMesh", this);
        }

        public bool TryMoveToWithClearance(Vector3 point, float sampleRadius, out Vector3 destination)
        {
            destination = point;
            if (agent == null || !agent.isActiveAndEnabled) return false;
            if (!agent.isOnNavMesh) { WarnOffMesh(); return false; }
            if (destinationPath == null) destinationPath = new NavMeshPath();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            float bestScore = float.PositiveInfinity;
            bool found = false;
            int rings = Mathf.CeilToInt(destinationSearchRadius / 0.25f);
            for (int ring = 0; ring <= rings; ring++)
            {
                int samples = ring == 0 ? 1 : 24;
                for (int i = 0; i < samples; i++)
                {
                    float angle = i * Mathf.PI * 2f / samples;
                    Vector3 candidate = point + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * 0.25f);
                    if (!NavMesh.SamplePosition(candidate, out var hit, sampleRadius, filter)) continue;
                    if (!HasDestinationClearance(hit.position)) continue;
                    if (!agent.CalculatePath(hit.position, destinationPath) || destinationPath.status != NavMeshPathStatus.PathComplete) continue;
                    if (ring == 0)
                    {
                        destination = hit.position;
                        return agent.SetDestination(destination);
                    }
                    var corners = destinationPath.corners;
                    float length = 0f;
                    for (int j = 1; j < corners.Length; j++) length += Vector3.Distance(corners[j - 1], corners[j]);
                    float score = Vector3.Distance(point, hit.position) + length * 0.01f;
                    if (score >= bestScore) continue;
                    bestScore = score;
                    destination = hit.position;
                    found = true;
                }
                if (found && ring * 0.25f > bestScore + sampleRadius) break;
            }
            return found && agent.SetDestination(destination);
        }

        private bool HasDestinationClearance(Vector3 point)
        {
            float radius = agent.radius + destinationClearance;
            // Нижняя точка приподнята над полом: проверяем препятствия, а не землю.
            Vector3 bottom = point + Vector3.up * (radius + 0.05f);
            Vector3 top = point + Vector3.up * Mathf.Max(radius + 0.05f, agent.height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps,
                destinationObstacleMask, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var obstacle = overlaps[i];
                if (obstacle.transform.IsChildOf(transform) ||
                    Physics.GetIgnoreLayerCollision(gameObject.layer, obstacle.gameObject.layer)) continue;
                return false;
            }
            return true;
        }

        public void MoveTo(Vector3 point)
        {
            if (agent == null || !agent.isActiveAndEnabled) return;
            if (!agent.isOnNavMesh)
            {
                WarnOffMesh();
                return;
            }
            agent.SetDestination(point);
        }
    }
}


```
## Assets/PinchOfTheft/Scripts/Editor/DamekAudit.cs
```csharp
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace PinchOfTheft.Editor
{
    public static class DamekAudit
    {
        public static void Run()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            foreach(var t in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>new[]{"damek","houseroad3","house"}.Contains(t.name.Replace(" ","").ToLowerInvariant())))
            {
                Debug.Log("DAMEK_AUDIT "+t.name+" position="+t.position+" rotation="+t.eulerAngles+" scale="+t.lossyScale+" up="+t.up);
                foreach(var r in t.GetComponentsInChildren<Renderer>(true))Debug.Log("DAMEK_GEOMETRY "+t.name+"/"+r.name+" bounds="+r.bounds+" colliders="+string.Join(",",r.GetComponents<Collider>().Select(c=>c.GetType().Name)));
                Debug.Log("DAMEK_COMPONENTS "+string.Join(",",t.GetComponents<Component>().Select(c=>c==null?"MISSING":c.GetType().Name)));
            }
        }
    }
}

```
## Assets/PinchOfTheft/Scripts/Editor/DamekSetup.cs
```csharp
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






```
## Assets/PinchOfTheft/Scripts/Editor/DamekCheck.cs
```csharp
using System;
using System.Linq;
using PinchOfTheft.Core;
using PinchOfTheft.CCTV;
using PinchOfTheft.Movement;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class DamekCheck
    {
        private static int phase;
        private static double began;
        private static PlayerMover mover;
        private static Vector3 inside,road,goal;
        private static int stoppedFrame=-1;
        private static void R(bool v,string m){if(!v)throw new Exception(m);}
        static DamekCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("DamekCheck",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetVector3("DamekOriginal",Object.FindObjectsByType<PlayerMover>().Single(p=>p.name=="Damek").transform.position);SessionState.SetBool("DamekCheck",true);EditorApplication.EnterPlaymode();}
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup-began>100)throw new Exception("Damek timeout phase="+phase+" position="+(mover!=null?mover.transform.position:Vector3.zero));
                if(Time.frameCount<5)return;
                var context=GameContext.Instance;
                if(phase==0)
                {
                    mover=context.PlayerMover;R(mover!=null&&mover.name=="Damek"&&context.Marker!=null,"Registration failed");var agent=mover.GetComponent<NavMeshAgent>();R(agent.isOnNavMesh,"Damek off NavMesh");R(Vector3.Distance(mover.transform.position,SessionState.GetVector3("DamekOriginal",Vector3.zero))<0.01f,"Damek moved at start "+mover.transform.position);
                    R(NavMesh.SamplePosition(mover.transform.position-Vector3.up*(agent.baseOffset*Mathf.Abs(mover.transform.lossyScale.y)),out var initialHit,1,NavMesh.AllAreas),"No initial nav sample");inside=initialHit.position;
                    var roadObject=Object.FindObjectsByType<Transform>().Single(t=>t.name.Equals("houseroad3",StringComparison.OrdinalIgnoreCase));var bounds=roadObject.GetComponent<Renderer>().bounds;
                    R(NavMesh.SamplePosition(new Vector3(bounds.center.x,bounds.max.y,bounds.center.z),out var hit,2,NavMesh.AllAreas),"No road NavMesh");road=hit.position;
                    var path=new NavMeshPath();bool connected=NavMesh.CalculatePath(inside,road,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;Debug.Log("DAMEK_CONNECTIVITY connected="+connected+" inside="+inside+" road="+road+" corners="+string.Join(";",path.corners.Select(v=>v.ToString())));
                    foreach(var camera in Object.FindObjectsByType<CCTVCamera>().Where(c=>c.Group==0)){var center=mover.GetComponent<CapsuleCollider>().bounds.center;var p=camera.View.WorldToViewportPoint(center);bool hitTarget=Physics.Raycast(camera.transform.position,(center-camera.transform.position).normalized,out var rayHit,Vector3.Distance(center,camera.transform.position)+0.1f,~(1<<2),QueryTriggerInteraction.Ignore)&&rayHit.collider.transform.IsChildOf(mover.transform);Debug.Log("DAMEK_VISIBLE CAM"+camera.CameraName+" inFrustum="+(p.z>0&&p.x>0&&p.x<1&&p.y>0&&p.y<1)+" clear="+hitTarget);}
                    if(!connected){Debug.Log("DAMEK_ROAD_CENTER_BLOCKED");float best=float.PositiveInfinity;Vector3 original=road;for(int x=0;x<=10;x++)for(int z=0;z<=10;z++){var sample=new Vector3(Mathf.Lerp(bounds.min.x+0.4f,bounds.max.x-0.4f,x/10f),bounds.max.y,Mathf.Lerp(bounds.min.z+0.4f,bounds.max.z-0.4f,z/10f));if(!NavMesh.SamplePosition(sample,out var near,0.4f,NavMesh.AllAreas))continue;var test=new NavMeshPath();if(!NavMesh.CalculatePath(inside,near.position,NavMesh.AllAreas,test)||test.status!=NavMeshPathStatus.PathComplete)continue;float score=(near.position-original).sqrMagnitude;if(score>=best)continue;best=score;road=near.position;connected=true;}Debug.Log("DAMEK_ENTRANCE_ROAD connected="+connected+" point="+road);}R(connected,"Road and house disconnected");R(mover.TryMoveToWithClearance(road,1.5f,out goal),"Road destination rejected");context.Marker.Show(goal);phase=1;Debug.Log("DAMEK_START_PASSED");
                }
                else if(phase==1||phase==2)
                {
                    if(mover.IsMoving){stoppedFrame=-1;return;}
                    if(stoppedFrame<0){stoppedFrame=Time.frameCount;return;}if(Time.frameCount<=stoppedFrame+2)return;
                    var delta=mover.transform.position-goal;delta.y=0;R(delta.magnitude<0.3f,"Agent stopped before goal phase="+phase+" distance="+delta.magnitude+" position="+mover.transform.position+" goal="+goal);R(!context.Marker.gameObject.activeSelf,"Marker remained");
                    if(phase==1){Debug.Log("DAMEK_ROAD_ARRIVAL_PASSED");R(mover.TryMoveToWithClearance(inside,1.5f,out goal),"Inside destination rejected");context.Marker.Show(goal);phase=2;stoppedFrame=-1;}
                    else{R(Object.FindObjectsByType<AudioListener>().Count(l=>l.isActiveAndEnabled)==1,"Listeners");Debug.Log("DAMEK_HOUSE_RETURN_PASSED");Finish(0);}
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("DamekCheck",false);EditorApplication.Exit(code);}
    }
}




```
## Assets/PinchOfTheft/Scripts/Editor/DamekInputCheck.cs
```csharp
using System;
using System.Linq;
using PinchOfTheft.Core;
using PinchOfTheft.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class DamekInputCheck
    {
        private static Mouse mouse;
        private static int phase,frame;
        private static Vector3 inside,destination;
        private static double began;
        static DamekInputCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("DamekInput",false)){began=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("DamekInput",true);EditorApplication.EnterPlaymode();}
        private static void R(bool v,string m){if(!v)throw new Exception(m);}
        private static void Aim(Vector3 point){var camera=GameContext.Instance.CameraManager.ActiveCamera.transform;camera.position=point+Vector3.up*0.9f;camera.LookAt(point);Physics.SyncTransforms();}
        private static void Click(){InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));frame=Time.frameCount;}
        private static void Release(){InputSystem.QueueStateEvent(mouse,new MouseState());frame=Time.frameCount;}
        private static void Tick()
        {
            if(Time.frameCount<5||Time.frameCount<frame+3)return;
            try
            {
                if(EditorApplication.timeSinceStartup-began>45)throw new Exception("Damek input timeout phase="+phase);
                var c=GameContext.Instance;var mover=c.PlayerMover;
                if(phase==0){InputSystem.settings=Object.Instantiate(InputSystem.settings);InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;mouse=InputSystem.AddDevice<Mouse>();R(NavMesh.SamplePosition(mover.transform.position,out var hit,1,NavMesh.AllAreas),"No start nav point");inside=hit.position;Aim(new Vector3(11.25f,1.15f,-25.2f));frame=Time.frameCount;phase=1;}
                else if(phase==1){Click();phase=2;}
                else if(phase==2){R(mover.IsMoving&&c.Marker.gameObject.activeSelf,"RMB did not move/show marker");destination=mover.Destination;Debug.Log("DAMEK_RMB_START_PASSED");Release();Aim(inside);phase=3;}
                else if(phase==3){Click();phase=4;}
                else if(phase==4){R(Vector3.Distance(destination,mover.Destination)>1f,"RMB redirect failed");destination=mover.Destination;Debug.Log("DAMEK_RMB_REDIRECT_PASSED");Release();c.Menu.SetOpen(true);Aim(new Vector3(11.25f,1.15f,-25.2f));phase=5;}
                else if(phase==5){Click();phase=6;}
                else if(phase==6){R(Vector3.Distance(destination,mover.Destination)<0.01f,"RMB not blocked by menu");c.Menu.SetOpen(false);Release();phase=7;}
                else if(phase==7&&!mover.IsMoving){R(!c.Marker.gameObject.activeSelf,"Marker did not hide");Debug.Log("DAMEK_RMB_MENU_AND_MARKER_PASSED");Finish(0);}
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("DamekInput",false);EditorApplication.Exit(code);}
    }
}

```
## Assets/PinchOfTheft/Scripts/Editor/DamekFinalAudit.cs
```csharp
using System;
using System.Linq;
using PinchOfTheft.Core;
using PinchOfTheft.CCTV;
using PinchOfTheft.Movement;
using PinchOfTheft.Interaction;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    public static class DamekFinalAudit
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/House.unity");
            foreach(var p in Object.FindObjectsByType<PlayerMover>(FindObjectsInactive.Include,FindObjectsSortMode.None))Debug.Log("PLAYER_OBJECT "+HouseCircuitAudit.Path(p.transform));
            foreach(var d in Object.FindObjectsByType<DoorOpener>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                var s=new SerializedObject(d);var closed=(GameObject)s.FindProperty("closedDoor").objectReferenceValue;var b=closed.GetComponent<Renderer>().bounds;var o=closed.GetComponent<NavMeshObstacle>();
                Debug.Log("DAMEK_DOOR "+HouseCircuitAudit.Path(closed.transform)+" worldSize="+b.size+" carving="+(o!=null&&o.carving)+" ignore="+closed.GetComponent<NavMeshModifier>().ignoreFromBuild);
            }
            var mover=Object.FindObjectsByType<PlayerMover>().Single(p=>p.name=="Damek");var a=mover.GetComponent<NavMeshAgent>();Debug.Log("DAMEK_AGENT speed="+a.speed+" angular="+a.angularSpeed+" accel="+a.acceleration+" stop="+a.stoppingDistance+" priority="+a.avoidancePriority+" braking="+a.autoBraking+" avoidance="+a.obstacleAvoidanceType);
            Debug.Log("DAMEK_MARKER size="+mover.GetComponentInChildren<ClickMarker>(true).GetComponent<Renderer>().bounds.size);
            PrefabSetup.Validate();Debug.Log("DAMEK_FINAL_VALIDATION_PASSED");
        }
    }
}

```
## Assets/PinchOfTheft/Scripts/Editor/DamekDoorCheck.cs
```csharp
using System;
using System.Linq;
using PinchOfTheft.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;
namespace PinchOfTheft.Editor
{
    [InitializeOnLoad]
    public static class DamekDoorCheck
    {
        private static int stage,frame;
        private static Vector3 a,b;
        private static DoorOpener door;
        static DamekDoorCheck(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("DamekDoor",false))EditorApplication.update+=Tick;};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/House.unity");SessionState.SetBool("DamekDoor",true);EditorApplication.EnterPlaymode();}
        private static void Tick()
        {
            if(Time.frameCount<10||Time.frameCount<frame+10)return;
            try
            {
                if(stage==0)
                {
                    door=Object.FindObjectsByType<DoorOpener>().Single(d=>d.transform.parent.name.Equals("Door_code",StringComparison.OrdinalIgnoreCase));var bounds=door.GetComponent<Collider>().bounds;var normal=bounds.size.x<bounds.size.z?Vector3.right:Vector3.forward;
                    bool Sample(Vector3 p,out Vector3 point){point=p;if(!Physics.Raycast(p,Vector3.down,out var hit,3,~(1<<LayerMask.NameToLayer("Player")),QueryTriggerInteraction.Ignore))return false;if(!NavMesh.SamplePosition(hit.point,out var nav,0.25f,NavMesh.AllAreas))return false;point=nav.position;return true;}
                    if(!Sample(bounds.center+normal*0.65f,out a)||!Sample(bounds.center-normal*0.65f,out b))throw new Exception("Cannot sample both sides of code door");
                    if(!NavMesh.Raycast(a,b,out _,NavMesh.AllAreas))throw new Exception("Closed door carve not blocking");Debug.Log("DAMEK_CLOSED_DOOR_CARVE_PASSED a="+a+" b="+b);door.Open();frame=Time.frameCount;stage=1;
                }
                else
                {
                    if(NavMesh.Raycast(a,b,out _,NavMesh.AllAreas))throw new Exception("Open door still blocks NavMesh");var path=new NavMeshPath();if(!NavMesh.CalculatePath(a,b,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("No path through open door");Debug.Log("DAMEK_OPEN_DOOR_NAV_PATH_PASSED");Finish(0);
                }
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        private static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool("DamekDoor",false);EditorApplication.Exit(code);}
    }
}

```

