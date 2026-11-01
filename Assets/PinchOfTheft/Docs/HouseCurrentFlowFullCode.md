# Полный актуальный CurrentFlow

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;

namespace PinchOfTheft.Interaction
{
    public sealed class CurrentFlow : MonoBehaviour
    {
        [Header("Объекты цепи")]
        [SerializeField, Tooltip("Начало цепи; можно оставить пустым для поиска по структуре")] private Transform startPoint;
        [SerializeField] private Transform finishPoint;
        [SerializeField] private Transform currentObject;
        [SerializeField] private Transform[] wires;
        [SerializeField, Tooltip("Корень проводов: геометрия собирается в глубину иерархии")] private Transform wireRoot;
        [Header("Питание и предохранитель")]
        [SerializeField, Tooltip("Пусто — цепь без предохранителя")] private Transform fuse;
        [SerializeField, Tooltip("Начальное состояние предохранителя")] private bool fuseActivated;
        public enum FuseFailMode { Vanish, ReturnToStart }
        [SerializeField, Tooltip("Поведение тока при отказе")] private FuseFailMode failMode = FuseFailMode.ReturnToStart;
        [SerializeField, Tooltip("Для запуска необходимо питание")] private bool requirePower = true;
        [SerializeField, Tooltip("Начальное состояние питания")] private bool powered;
        [SerializeField, Tooltip("Подпись до подачи питания")] private string noPowerMessage = "Нет энергии";
        [SerializeField, Tooltip("Сообщение при отказе предохранителя")] private string fuseFailMessage = "Предохранитель не активирован: ток потерян";
        [SerializeField, Tooltip("Мировая подсказка возле Start")] private GameObject noPowerLabel;
        [SerializeField, Tooltip("Взаимодействие Start")] private InteractableSphere startInteractable;
        [SerializeField, Tooltip("Событие отказа")] private UnityEvent onFuseFailed = new();
        [SerializeField, Tooltip("Событие подачи питания")] private UnityEvent onPowered = new();
        [Header("Движение и события")]
        [SerializeField, Min(0f), Tooltip("Скорость в метрах в секунду")] private float speed = 2f;
        [SerializeField, Min(0f)] private float startDelay;
        [SerializeField] private bool hideUntilStart;
        [SerializeField] private bool hideOnFinish;
        [SerializeField, Min(0f)] private float junctionTolerance = 0.1f;
        [SerializeField] private string finishMessage = "Ток дошёл до конца цепи";
        [SerializeField] private UnityEvent onStarted = new();
        [SerializeField] private UnityEvent onFinished = new();
        private readonly List<Vector3> path = new();
        private readonly List<Vector3> junctions = new();
        private int nextPoint;
        private float delayLeft;
        private bool warned;
        private bool fuseWarned;
        private float traveled;
        private float fuseDistance = float.PositiveInfinity;
        private float fuseOffset;
        private Vector3 fusePosition;
        public float FuseDistance => fuseDistance;
        public float FuseOffset => fuseOffset;
        public bool IsRunning { get; private set; }
        public bool IsFinished { get; private set; }
        public string WireOrder => string.Join(" → ", GetWires().Select(w => w != null ? w.name : "null"));

        private void Awake()
        {
            ResolveStructure();
            wires = GetWires();
            if (startInteractable == null && startPoint != null) startInteractable = startPoint.GetComponent<InteractableSphere>();
            ApplyPower();
            enabled = false;
            if (currentObject == null) return;
            foreach (var collider in currentObject.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var body in currentObject.GetComponentsInChildren<Rigidbody>(true))
            { body.isKinematic = true; body.useGravity = false; }
            if (hideUntilStart) currentObject.gameObject.SetActive(false);
        }
        private void ResolveStructure()
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                string name = child.name.Replace(" ", "").ToLowerInvariant();
                if (startPoint == null && (name == "start" || name == "старт")) startPoint = child;
                if (finishPoint == null && (name == "finish" || name == "финиш")) finishPoint = child;
                if (currentObject == null && (name == "current" || name == "electriccurrent" || name == "electrocurrent" || name == "электроток")) currentObject = child;
            }
        }

        private Transform[] GetWires()
        {
            if (wires != null && wires.Length > 0) return wires;
            if (wireRoot != null) return wireRoot.GetComponentsInChildren<Transform>(true)
                .Where(t => t != startPoint && t != finishPoint && t != currentObject &&
                    (t.GetComponent<MeshFilter>() != null || t.GetComponent<Renderer>() != null)).ToArray();
            Transform root = startPoint != null && startPoint.parent != null ? startPoint.parent : transform;
            return root.GetComponentsInChildren<Transform>(true)
                .Where(t => t != root && t != startPoint && t != finishPoint && t != currentObject &&
                    t.name.Replace(" ", "").StartsWith("wire", System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => WireNumber(t.name)).ToArray();
        }

        private static int WireNumber(string name)
        {
            Match match = Regex.Match(name.Trim(), @"\d+$");
            return match.Success && int.TryParse(match.Value, out int number) ? number : 1;
        }

        public void Begin()
        {
            if (requirePower && !powered) return;
            if (IsRunning || IsFinished) return;
            if (!BuildPath(out string error)) { Warn(error); ApplyPower(); return; }
            string outside = OutsideSegments();
            if (outside.Length > 0) { Warn("Путь выходит за цепь, отрезки: " + outside); ApplyPower(); return; }
            currentObject.position = path[0];
            if (!hideUntilStart) currentObject.gameObject.SetActive(true);
            delayLeft = startDelay;
            nextPoint = 1;
            traveled = 0f;
            LocateFuse();
            IsRunning = true;
            enabled = true;
            onStarted.Invoke();
        }
        public void SetPowered(bool value)
        {
            bool changed = powered != value;
            powered = value;
            ApplyPower();
            if (changed && value) onPowered.Invoke();
        }
        public void ActivateFuse() => fuseActivated = true;
        private void ApplyPower()
        {
            bool available = !requirePower || powered;
            if (noPowerLabel != null)
            {
                var text = noPowerLabel.GetComponentInChildren<UnityEngine.UI.Text>(true);
                if (text != null) text.text = noPowerMessage;
                noPowerLabel.SetActive(!available);
            }
            if (startInteractable != null && !IsRunning) startInteractable.SetAvailable(available && !IsFinished);
        }
        private void LocateFuse()
        {
            fuseDistance = float.PositiveInfinity; fuseOffset = float.PositiveInfinity;
            if (fuse == null) return;
            Vector3 center = Center(fuse); float cumulative = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                Vector3 segment = path[i] - path[i-1]; float length = segment.magnitude;
                float t = length > 0f ? Mathf.Clamp01(Vector3.Dot(center-path[i-1],segment)/(length*length)) : 0f;
                Vector3 point = path[i-1] + segment*t;
                float distance = Vector3.Distance(center,point);
                if (distance < fuseOffset) { fuseOffset=distance; fuseDistance=cumulative+length*t; fusePosition=point; }
                cumulative += length;
            }
            if (fuseOffset > 1f)
            {
                fuseDistance = float.PositiveInfinity;
                if (!fuseWarned) { fuseWarned=true; Debug.LogWarning("fu1 далеко от пути",this); }
            }
        }
        private void FuseFailed()
        {
            IsRunning=false; enabled=false;
            if (failMode == FuseFailMode.ReturnToStart) currentObject.position=path[0];
            currentObject.gameObject.SetActive(false);
            if (startInteractable != null) startInteractable.SetAvailable(!requirePower || powered);
            Debug.Log(fuseFailMessage,this); onFuseFailed.Invoke();
        }
        private void Finished()
        {
            IsRunning=false; IsFinished=true; enabled=false;
            if (startInteractable != null) startInteractable.SetAvailable(false);
            Debug.Log(finishMessage,this); onFinished.Invoke();
            if (hideOnFinish) currentObject.gameObject.SetActive(false);
        }

        private void Warn(string text)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning(text, this);
        }

        private bool ChainActive()
        {
            return startPoint != null && finishPoint != null && currentObject != null &&
                startPoint.gameObject.activeInHierarchy && finishPoint.gameObject.activeInHierarchy &&
                WiresActive();
        }
        private bool WiresActive()
        {
            if (wires == null) return false;
            foreach (var wire in wires) if (wire == null || !wire.gameObject.activeInHierarchy) return false;
            return true;
        }

        private bool BuildPath(out string error)
        {
            wires = GetWires();
            if (BuildOrdered(out error)) return true;
            if (wires == null || wires.Length < 2 || startPoint == null) return false;
            var remaining = new List<Transform>(wires);
            var ordered = new List<Transform>();
            Vector3 previous = Center(startPoint), previousA=previous, previousB=previous;
            while (remaining.Count > 0)
            {
                int best = -1; float bestGap = float.PositiveInfinity, bestEnd = float.PositiveInfinity;
                for (int i = 0; i < remaining.Count; i++)
                {
                    if (remaining[i] == null || !Axis(remaining[i],out var a,out var b)) continue;
                    Closest(previousA,previousB,a,b,out var p,out var q);
                    float gap=Vector3.Distance(p,q);
                    float end=Mathf.Min(Vector3.Distance(previous,a),Vector3.Distance(previous,b));
                    if (gap > bestGap + 0.0001f || (Mathf.Abs(gap-bestGap)<=0.0001f && end>=bestEnd)) continue;
                    best=i; bestGap=gap; bestEnd=end;
                }
                if (best < 0) return false;
                var next=remaining[best]; ordered.Add(next); remaining.RemoveAt(best);
                Axis(next,out previousA,out previousB);
                previous=Vector3.Distance(previous,previousA)<=Vector3.Distance(previous,previousB)?previousB:previousA;
            }
            wires=ordered.ToArray();
            return BuildOrdered(out error);
        }
        private bool BuildOrdered(out string error)
        {
            path.Clear(); junctions.Clear();
            error = "Назначьте Start, Finish, объект тока и провода";
            if (startPoint == null || finishPoint == null || currentObject == null || wires.Length == 0) return false;
            if (!ChainActive()) { error = "Объекты цепи должны быть активны"; return false; }
            var entries = new Vector3[wires.Length];
            var exits = new Vector3[wires.Length];
            Vector3 previous = Center(startPoint);
            for (int i = 0; i < wires.Length; i++)
            {
                if (!Axis(wires[i], out var a, out var b)) { error = "Нет геометрии у " + wires[i].name; return false; }
                bool aFirst = (a - previous).sqrMagnitude <= (b - previous).sqrMagnitude;
                entries[i] = aFirst ? a : b; exits[i] = aFirst ? b : a;
                previous = exits[i];
            }
            Add(Center(startPoint)); Add(entries[0]);
            for (int i = 1; i < wires.Length; i++)
            {
                Closest(entries[i-1], exits[i-1], entries[i], exits[i], out var a, out var b);
                if (Vector3.Distance(a,b) > junctionTolerance)
                { error = "Разрыв цепи: " + wires[i-1].name + " / " + wires[i].name; path.Clear(); return false; }
                Vector3 joint = (a+b)*0.5f;
                Add(joint); junctions.Add(joint);
            }
            Add(exits[exits.Length-1]); Add(Center(finishPoint));
            return path.Count > 0;
        }

        private void Add(Vector3 point)
        {
            if (path.Count == 0 || Vector3.Distance(path[path.Count-1],point) >= 0.001f) path.Add(point);
        }

        private static Vector3 Center(Transform obj)
        {
            var renderer = obj.GetComponent<Renderer>(); if (renderer != null) return renderer.bounds.center;
            var collider = obj.GetComponent<Collider>(); return collider != null ? collider.bounds.center : obj.position;
        }

        private static bool Axis(Transform obj, out Vector3 a, out Vector3 b)
        {
            var filter = obj.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                Bounds bounds = filter.sharedMesh.bounds;
                Vector3 size = new Vector3(obj.TransformVector(Vector3.right*bounds.size.x).magnitude,
                    obj.TransformVector(Vector3.up*bounds.size.y).magnitude,obj.TransformVector(Vector3.forward*bounds.size.z).magnitude);
                Vector3 extent = size.x >= size.y && size.x >= size.z ? Vector3.right * bounds.extents.x :
                    size.y >= size.z ? Vector3.up * bounds.extents.y : Vector3.forward * bounds.extents.z;
                a = obj.TransformPoint(bounds.center - extent); b = obj.TransformPoint(bounds.center + extent); return true;
            }
            var renderer = obj.GetComponent<Renderer>();
            a = b = obj.position;
            if (renderer == null) return false;
            Bounds world = renderer.bounds;
            Vector3 delta = world.size.x >= world.size.y && world.size.x >= world.size.z ? Vector3.right * world.extents.x :
                world.size.y >= world.size.z ? Vector3.up * world.extents.y : Vector3.forward * world.extents.z;
            a = world.center-delta; b = world.center+delta; return true;
        }

        private static void Closest(Vector3 p, Vector3 q, Vector3 r, Vector3 s, out Vector3 a, out Vector3 b)
        {
            Vector3 u=q-p, v=s-r, w=p-r;
            float uu=Vector3.Dot(u,u), vv=Vector3.Dot(v,v), uv=Vector3.Dot(u,v);
            float uw=Vector3.Dot(u,w), vw=Vector3.Dot(v,w);
            float t=0f, h=0f;
            if (uu <= 0.000001f) h = vv > 0.000001f ? Mathf.Clamp01(vw/vv) : 0;
            else if (vv <= 0.000001f) t = Mathf.Clamp01(-uw/uu);
            else
            {
                float denominator=uu*vv-uv*uv;
                t=denominator>0.000001f ? Mathf.Clamp01((uv*vw-uw*vv)/denominator) : 0;
                h=(uv*t+vw)/vv;
                if (h<0) {h=0;t=Mathf.Clamp01(-uw/uu);}
                else if (h>1) {h=1;t=Mathf.Clamp01((uv-uw)/uu);}
            }
            a=p+u*t; b=r+v*h;
        }

        private void Update()
        {
            if (!IsRunning || !ChainActive()) return;
            float time = Time.deltaTime;
            if (delayLeft > 0)
            {
                float waited = Mathf.Min(time,delayLeft); delayLeft-=waited; time-=waited;
                if (delayLeft > 0) return;
            }
            currentObject.gameObject.SetActive(true);
            float distance = speed*time;
            bool fail = fuse != null && !fuseActivated && traveled + distance >= fuseDistance;
            if (fail) distance = Mathf.Max(0f,fuseDistance-traveled);
            // Остаток шага переносится через стык без срезания угла.
            while (nextPoint < path.Count)
            {
                float length=Vector3.Distance(currentObject.position,path[nextPoint]);
                float moved = Mathf.Min(distance,length);
                currentObject.position=Vector3.MoveTowards(currentObject.position,path[nextPoint],moved);
                traveled += moved;
                if (distance < length)
                {
                    if (fail) FuseFailed();
                    return;
                }
                distance-=length; nextPoint++;
                if (fail && traveled >= fuseDistance-0.0001f) { FuseFailed(); return; }
            }
            Finished();
        }

        private static bool Contains(Transform obj, Vector3 point)
        {
            var colliders=obj.GetComponentsInChildren<Collider>();
            bool tested=false;
            foreach(var collider in colliders)
            {
                if (!collider.enabled || (collider is MeshCollider mesh && !mesh.convex)) continue;
                tested=true;
                if (Vector3.Distance(collider.ClosestPoint(point),point)<=0.01f) return true;
            }
            if (tested) return false;
            return obj.GetComponentsInChildren<Renderer>().Any(r => r.bounds.Contains(point));
        }

        private string OutsideSegments()
        {
            var objects = wires.Concat(new[]{startPoint,finishPoint}).ToArray();
            var bad=new List<int>();
            for(int i=1;i<path.Count;i++)
                for(int sample=0;sample<20;sample++)
                    if (!objects.Any(obj=>Contains(obj,Vector3.Lerp(path[i-1],path[i],sample/19f))))
                    {bad.Add(i);break;}
            return string.Join(", ",bad);
        }

        [ContextMenu("Validate path")]
        public void ValidatePath()
        {
            if (!BuildPath(out string error)) { Debug.LogWarning("Validate path: " + error,this); return; }
            string outside=OutsideSegments();
            LocateFuse();
            Debug.Log(outside.Length==0 ? "Validate path: ОК. " + WireOrder : "Validate path: вне цепи отрезки " + outside,this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color=Color.cyan;
            for(int i=1;i<path.Count;i++)Gizmos.DrawLine(path[i-1],path[i]);
            Gizmos.color=Color.yellow;
            foreach(var point in junctions)Gizmos.DrawSphere(point,0.04f);
            if (fuse != null && !float.IsInfinity(fuseDistance)) { Gizmos.color=Color.red; Gizmos.DrawSphere(fusePosition,0.07f); }
        }
    }
}

```

