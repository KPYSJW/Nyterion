using UnityEngine;
using UnityEngine.SceneManagement;
using Nytherion.Core.Data;
using Zenject;

namespace Nytherion.Core.Managers
{
    public class AudioManager : BaseManager, IInitializable
    {

        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioClip titleBGM;
        [SerializeField] private AudioClip stageBGM;
        [SerializeField] private AudioClip villageBGM;


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
