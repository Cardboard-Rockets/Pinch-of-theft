using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace PinchOfTheft.Core
{
    /// <summary>Night arrival with a first-person exit, a cut to the rear seat, and a push into the original laptop screen.</summary>
    public sealed class VanIntroSequence : MonoBehaviour
    {
        [Serializable]
        public struct Shot
        {
            public string label;
            [Min(.01f)] public float duration;
            public Vector3 fromPosition, toPosition;
            public Quaternion fromRotation, toRotation;
            [Range(15, 100)] public float fromFov, toFov;
            public bool walking;
        }

        [Serializable]
        public struct Wheel
        {
            public Transform transform;
            public Quaternion restRotation;
            public Vector3 localAxis;
            [Min(.01f)] public float radius;
        }

        [Header("Staging")]
        [SerializeField] private Transform vehicle;
        [SerializeField] private Camera view;
        [SerializeField] private Shot[] shots;
        [SerializeField] private Wheel[] wheels;
        [SerializeField] private CanvasGroup windowTransition;
        [SerializeField] private AudioSource engine;
        [SerializeField] private AudioLowPassFilter cabinFilter;
        [SerializeField] private Renderer exteriorBody;
        [SerializeField] private bool keepOriginalBodyInside;
        [SerializeField] private float[] transitionCuts = { 15f };
        [SerializeField] private bool firstPersonRoute;
        [SerializeField] private Vector3 originalLaptopScreenPoint;
        [Header("Travel")]
        [SerializeField, Min(0)] private float speed = 7f;
        [SerializeField, Min(0)] private float brakeStartTime = 13.5f;
        [SerializeField, Min(.1f)] private float brakeDuration = 3.5f;
        [SerializeField] private Vector3 vehicleStart;
        [FormerlySerializedAs("cabinCutTime")]
        [SerializeField, Min(0)] private float windshieldCutTime = 15f;
        [SerializeField, Min(.01f)] private float windowWipeDuration = .22f;
        private float runningTime;
        private float skipHeld;
        public float Duration { get { float sum = 0; if (shots != null) foreach (var s in shots) sum += s.duration; return sum; } }
        public float Elapsed => runningTime;
        public bool IsComplete => runningTime >= Duration;
        public Camera View => view;
        public Transform Vehicle => vehicle;
        public Wheel[] Wheels => wheels;
        public float StopTime => brakeStartTime + brakeDuration;
        public float CurrentSpeed => SpeedAtTime(runningTime);
        public string CurrentShot { get; private set; }
        public string FinalShotLabel => shots != null && shots.Length > 0 ? shots[shots.Length-1].label : "";
        public float TransitionFadeAlpha => windowTransition != null ? windowTransition.alpha : 0;
        public bool IsFirstPersonRoute => firstPersonRoute;
        public float OriginalScreenDistance => view != null ? Vector3.Distance(view.transform.localPosition, originalLaptopScreenPoint) : float.PositiveInfinity;
        public event Action Completed;
        private bool completionSent;

        public void Configure(Transform van, Camera camera, Shot[] montage, Wheel[] wheelData,
            CanvasGroup wipe, AudioSource motor, AudioLowPassFilter lowPass, Renderer body)
        {
            vehicle = van; view = camera; shots = montage; wheels = wheelData;
            vehicleStart = van.localPosition; windowTransition = wipe;
            engine = motor; cabinFilter = lowPass;
            exteriorBody = body;
            Sample(0);
        }

        public void ConfigureFirstPersonRoute(float[] cuts, Vector3 screenPoint)
        {
            keepOriginalBodyInside = true;
            firstPersonRoute = true;
            transitionCuts = cuts;
            originalLaptopScreenPoint = screenPoint;
            windowWipeDuration = .35f;
            Sample(0);
        }

        private void Start()
        {
            if (vehicle == null || view == null || shots == null || shots.Length == 0)
            { Debug.LogError("Van intro: vehicle, camera and shots are required.", this); enabled = false; return; }
            Replay();
        }

        public void Replay()
        {
            runningTime = 0; skipHeld = 0; completionSent = false;
            Sample(0);
            if (engine != null && !engine.isPlaying) engine.Play();
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            runningTime += delta;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Replay();
            if (!IsComplete && keyboard != null && keyboard.spaceKey.isPressed)
            {
                skipHeld += delta;
                if (skipHeld >= .75f) runningTime = Duration;
            }
            else skipHeld = 0;
            Sample(runningTime);
            if (IsComplete && !completionSent) { completionSent = true; Completed?.Invoke(); }
        }

        /// <summary>Deterministic staging for editor previews and verification. Does not fire events.</summary>
        public float SpeedAtTime(float seconds) => speed * (1 - Mathf.Clamp01((seconds - brakeStartTime) / brakeDuration));

        public float DistanceAtTime(float seconds)
        {
            seconds = Mathf.Max(0, seconds);
            float braking = Mathf.Clamp(seconds - brakeStartTime, 0, brakeDuration);
            return speed * Mathf.Min(seconds, brakeStartTime) + speed * braking - .5f * speed / brakeDuration * braking * braking;
        }

        public void Sample(float seconds)
        {
            if (vehicle == null || view == null || shots == null || shots.Length == 0) return;
            seconds = Mathf.Max(0, seconds);
            float distance = DistanceAtTime(seconds);
            vehicle.localPosition = vehicleStart + vehicle.localRotation * Vector3.forward * distance;
            if (wheels != null) foreach (var wheel in wheels)
                if (wheel.transform != null && wheel.radius > 0)
                    wheel.transform.localRotation = wheel.restRotation * Quaternion.AngleAxis(
                        -distance / wheel.radius * Mathf.Rad2Deg % 360f, wheel.localAxis.normalized);
            float shotTime = Mathf.Min(seconds, Duration);
            int index = 0;
            while (index < shots.Length - 1 && shotTime >= shots[index].duration)
            { shotTime -= shots[index].duration; index++; }
            var shot = shots[index];
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(shotTime / Mathf.Max(.01f, shot.duration)));
            CurrentShot = shot.label;
            bool windshieldView = seconds >= windshieldCutTime;
            if (exteriorBody != null) exteriorBody.enabled = keepOriginalBodyInside || !windshieldView;
            view.transform.localPosition = Vector3.Lerp(shot.fromPosition, shot.toPosition, t);
            if(shot.walking)
                view.transform.localPosition += Vector3.up * (Mathf.Sin(t * Mathf.PI * 6) * Mathf.Sin(t * Mathf.PI) * .009f);
            view.transform.localRotation = Quaternion.Slerp(shot.fromRotation, shot.toRotation, t);
            // A small settling motion sells the brakes without moving the parked vehicle.
            if (windshieldView)
            {
                float settle = seconds - StopTime;
                if (settle >= 0) view.transform.localRotation *= Quaternion.Euler(Mathf.Sin(settle * 12) * Mathf.Exp(-settle * 5) * .45f, 0, 0);
            }
            view.fieldOfView = Mathf.Lerp(shot.fromFov, shot.toFov, t);
            // Fade completely hides the exit and rear-entry cuts; the original body stays intact.
            if (windowTransition != null)
            {
                float alpha = 0;
                if (transitionCuts != null) foreach(float cut in transitionCuts)
                    alpha = Mathf.Max(alpha, Mathf.Clamp01(1 - Mathf.Abs(seconds - cut) / windowWipeDuration));
                windowTransition.alpha = alpha;
            }
            float cabin = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(windshieldCutTime - .2f, windshieldCutTime + .5f, seconds));
            if (cabinFilter != null) cabinFilter.cutoffFrequency = Mathf.Lerp(8500, 1700, cabin);
            if (engine != null)
            {
                float moving = SpeedAtTime(seconds) / Mathf.Max(.01f, speed);
                engine.volume = Mathf.Lerp(.23f, .14f, cabin) * Mathf.Lerp(.35f, 1, moving);
                engine.pitch = Mathf.Lerp(.65f, 1, moving);
            }
        }

        private void OnDisable()
        {
            if (engine != null) engine.Stop();
            if (windowTransition != null) windowTransition.alpha = 0;

        }
    }
}
