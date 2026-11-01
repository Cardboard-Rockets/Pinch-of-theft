using UnityEngine;

namespace PinchOfTheft.Core
{
    /// <summary>Centralized gameplay audio with per-sound inspector volumes.</summary>
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Клипы")]
        [SerializeField] private AudioClip robotCallClip;
        [SerializeField] private AudioClip otherInteractionClip;
        [SerializeField] private AudioClip holdInteractionClip;
        [SerializeField] private AudioClip confirmationClip;
        [SerializeField] private AudioClip nightVisionClip;
        [SerializeField] private AudioClip motorClip;

        [Header("Громкость")]
        [SerializeField, Range(0f, 1f)] private float robotCallVolume = .8f;
        [SerializeField, Range(0f, 1f)] private float otherInteractionVolume = .8f;
        [SerializeField, Range(0f, 1f)] private float holdInteractionVolume = .7f;
        [SerializeField, Range(0f, 1f)] private float confirmationVolume = .85f;
        [SerializeField, Range(0f, 1f)] private float nightVisionVolume = .7f;
        [SerializeField, Range(0f, 1f)] private float motorVolume = .12f;
        [SerializeField, Range(0f, 1f)] private float introMotorVolume = .23f;

        private AudioSource oneShot;
        private AudioSource hold;
        private AudioSource motor;

        public AudioClip MotorClip => motorClip;
        public float IntroMotorVolume => introMotorVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            oneShot = CreateSource("One Shots", false);
            hold = CreateSource("Hold Interaction", true);
            motor = CreateSource("Robot Motor", true);
            if (motorClip != null) { motor.clip = motorClip; motor.volume = 0f; }
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.name = sourceName;
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        public void PlayRobotCall() => PlayOneShot(robotCallClip, robotCallVolume);
        public void PlayOtherInteraction() => PlayOneShot(otherInteractionClip, otherInteractionVolume);
        public void PlayConfirmation() => PlayOneShot(confirmationClip, confirmationVolume);
        public void PlayNightVision() => PlayOneShot(nightVisionClip, nightVisionVolume);

        public void BeginHold(float duration)
        {
            if (duration < 1.5f || holdInteractionClip == null || hold == null) return;
            hold.clip = holdInteractionClip;
            hold.volume = holdInteractionVolume;
            if (!hold.isPlaying) hold.Play();
        }

        public void CancelHold()
        {
            if (hold != null && hold.isPlaying) hold.Stop();
        }

        public void CompleteHold(float duration)
        {
            CancelHold();
            if (duration >= 1.5f) PlayConfirmation();
            else PlayOtherInteraction();
        }

        public void SetRobotMoving(bool moving)
        {
            if (motor == null || motorClip == null) return;
            motor.clip = motorClip;
            motor.volume = moving ? motorVolume : 0f;
            if (moving) { if (!motor.isPlaying) motor.Play(); }
            else if (motor.isPlaying) motor.Stop();
        }

        private void PlayOneShot(AudioClip clip, float volume)
        {
            if (oneShot != null && clip != null) oneShot.PlayOneShot(clip, volume);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
