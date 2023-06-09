using UnityEngine.Audio;
using UnityEngine;
using System;

namespace Code
{
    [Serializable]
    public class Sound
    {
        public int ID;
        public AudioClip clip;
        [Range(0f,1f)]
        public float volume;
        [Range(0f,3f)]
        public float pitch;
        public bool loop;

        [HideInInspector]
        public AudioSource source;
    }
}
