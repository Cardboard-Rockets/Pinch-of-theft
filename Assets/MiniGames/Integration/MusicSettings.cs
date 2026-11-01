using UnityEngine;
namespace MiniGames.Integration
{
    public sealed class MusicSettings : ScriptableObject
    {
        public AudioClip mainTrack;
        public AudioClip miniGameTrack;
        [Range(0f, 1f)] public float mainVolume = 0.5f;
        [Range(0f, 1f)] public float miniGameVolume = 0.5f;
        [Min(0f)] public float repeatDelay = 5f;
    }
}
