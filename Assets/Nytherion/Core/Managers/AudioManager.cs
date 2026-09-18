using UnityEngine;
using UnityEngine.SceneManagement;
using Nytherion.Core.Data;
using VContainer;
using VContainer.Unity;

namespace Nytherion.Core.Managers
{
    public class AudioManager : BaseManager, IInitializable
    {

        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioClip titleBGM;
        [SerializeField] private AudioClip stageBGM;
        [SerializeField] private AudioClip villageBGM;
        private float sfxVolume = 1f;

        protected override void Awake()
        {
            base.Awake();

            AudioListener.volume = UserSettings.GetMasterVolume(AudioListener.volume);
            sfxVolume = UserSettings.GetSfxVolume(sfxVolume);

            if (bgmSource != null)
            {
                bgmSource.volume = UserSettings.GetBgmVolume(bgmSource.volume);
            }
        }

        public override void Initialize()
        {

        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UserSettings.Save();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                UserSettings.Save();
            }
        }

        private void OnApplicationQuit()
        {
            UserSettings.Save();
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
        public override void PopulateSaveData(SaveData saveData)
        {
            // 저장할 데이터 설정
        }
        public override void LoadFromSaveData(SaveData saveData)
        {
            // 저장된 데이터 로드
        }
        public void PlayBGM(AudioClip clip)
        {
            if (clip == null || bgmSource == null) return;

            if (bgmSource.clip == clip) return;

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.Play();
        }

        public void SetBGMVolume(float volume)
        {
            float clampedVolume = Mathf.Clamp01(volume);
            if (bgmSource != null)
            {
                bgmSource.volume = clampedVolume;
            }

            UserSettings.SetBgmVolume(clampedVolume);
        }

        public float GetBGMVolume()
        {
            return bgmSource != null
                ? bgmSource.volume
                : UserSettings.GetBgmVolume();
        }

        public void SetMasterVolume(float volume)
        {
            float clampedVolume = Mathf.Clamp01(volume);
            AudioListener.volume = clampedVolume;
            UserSettings.SetMasterVolume(clampedVolume);
        }

        public float GetMasterVolume()
        {
            return AudioListener.volume;
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            UserSettings.SetSfxVolume(sfxVolume);
        }

        public float GetSFXVolume()
        {
            return sfxVolume;
        }
    }
}
