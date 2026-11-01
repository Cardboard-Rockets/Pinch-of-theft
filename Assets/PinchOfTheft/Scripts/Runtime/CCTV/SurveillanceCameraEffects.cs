using UnityEngine;
using UnityEngine.InputSystem;

namespace PinchOfTheft.CCTV
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class SurveillanceCameraEffects : MonoBehaviour
    {
        private Camera view;
        private Light infraredLight;
        private CCTVCamera managedCamera;
        public bool NightVisionEnabled { get; private set; }

        private void Awake() { view = GetComponent<Camera>(); managedCamera = GetComponent<CCTVCamera>(); }
        private void Update()
        {
            // Existing CCTV controls own N when a CameraManager is present.
            if (view.isActiveAndEnabled && view.targetTexture == null && Keyboard.current != null &&
                Keyboard.current.nKey.wasPressedThisFrame &&
                (managedCamera == null || Object.FindAnyObjectByType<CameraManager>() == null)) ToggleNightVision();
        }
        private void LateUpdate() => RefreshLight();
        private void OnDisable() { if (infraredLight != null) infraredLight.enabled = false; }
        private void OnDestroy() { if (infraredLight != null) Destroy(infraredLight.gameObject); }

        public void ToggleNightVision()
        {
            if (view == null) view = GetComponent<Camera>();
            NightVisionEnabled = !NightVisionEnabled;
            if (NightVisionEnabled && infraredLight == null)
            {
                var emitter = new GameObject("Camera IR illuminator") { hideFlags = HideFlags.DontSave };
                emitter.transform.SetParent(transform, false);
                infraredLight = emitter.AddComponent<Light>();
                infraredLight.type = LightType.Spot;
                infraredLight.color = Color.white;
                infraredLight.intensity = 12f;
                infraredLight.range = 18f;
                infraredLight.spotAngle = Mathf.Clamp(view.fieldOfView * 1.25f, 70f, 140f);
                infraredLight.innerSpotAngle = infraredLight.spotAngle * 0.55f;
                infraredLight.cullingMask = view.cullingMask;
                infraredLight.shadows = LightShadows.Soft;
                infraredLight.shadowNormalBias = 0.2f;
                infraredLight.bounceIntensity = 0f;
            }
            RefreshLight();
        }

        public void RefreshLight()
        {
            if (infraredLight != null)
                infraredLight.enabled = NightVisionEnabled && isActiveAndEnabled && view != null && view.isActiveAndEnabled &&
                    (managedCamera == null || managedCamera.isActiveAndEnabled);
        }
    }
}
