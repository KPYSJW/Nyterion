using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nytherion.Core
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioClip titleBGM;
        [SerializeField] private AudioClip stageBGM;
        [SerializeField] private AudioClip villageBGM;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            transform.SetParent(null);
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AudioClip newClip = GetBGMForScene(scene.name);
            PlayBGM(newClip);
        }

        private AudioClip GetBGMForScene(string sceneName)
        {
            switch (sceneName)
            {
                case "Title":
                    return titleBGM;
                case "Stage_1_1":
                case "Stage_1_2":
                    return stageBGM;
                case "Village":
                    return villageBGM;
                default:
                    return null;
            }
        }

        public void PlayBGM(AudioClip clip)
        {
            if (clip == null || bgmSource == null) return;

            if (bgmSource.clip == clip) return; // 동일한 BGM이면 재생하지 않음

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        public void SetBGMVolume(float volume)
        {
            if (bgmSource == null) return;
            bgmSource.volume = volume;
        }

        public float GetBGMVolume()
        {
            if (bgmSource == null) return 0f;
            return bgmSource.volume;
        }
    }
}
