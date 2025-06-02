using UnityEngine;

namespace Nytherion.Core
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioSource bgmSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // Make sure we're working with a root object
            transform.SetParent(null);
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetBGMVolume(float volume)
        {
            if (bgmSource == null) return;
            bgmSource.volume = volume;
        }

        public void PlayBGM(AudioClip clip)
        {
            if (clip == null || bgmSource == null) return;

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        public float GetBGMVolume()
        {
            if (bgmSource == null) return 0f;
            return bgmSource.volume;
        }

    }
}
