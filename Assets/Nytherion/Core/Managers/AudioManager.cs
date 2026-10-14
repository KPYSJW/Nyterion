using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Nytherion.Core.Data;
using VContainer;
using VContainer.Unity;
using Nytherion.Core.Enums;

namespace Nytherion.Core.Managers
{
    public class AudioManager : BaseManager, IInitializable
    {

        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioClip titleBGM;
        [SerializeField] private AudioClip stageBGM;
        [SerializeField] private AudioClip villageBGM;
        [Header("UI 열기 효과음")]
        [SerializeField] private AudioClip uiOpenSound;
        [InspectorName("창 열기 볼륨")]
        [SerializeField, Range(0f, 1f)] private float uiOpenSoundVolume = 0.7f;
        private AudioSource uiSfxSource;
        private float sfxVolume = 1f;
        [Header("UI 조작 효과음")]
        [SerializeField] private AudioClip uiHoverSound;
        [InspectorName("일반 버튼 마우스 올리기 볼륨")]
        [SerializeField, Range(0f, 1f)] private float uiHoverSoundVolume = 0.3f;
        [SerializeField] private AudioClip uiClickSound;
        [InspectorName("클릭 볼륨")]
        [SerializeField, Range(0f, 1f)] private float uiClickSoundVolume = 1f;
        [SerializeField] private AudioClip uiCloseSound;
        [InspectorName("일반 창 닫기 볼륨")]
        [SerializeField, Range(0f, 1f)] private float uiCloseSoundVolume = 1f;
        [InspectorName("인벤토리·스킬·유물 창 닫기 볼륨")]
        [Tooltip("패널에 개별 닫기 효과음이 연결되어 있으면 이 볼륨을 사용합니다.")]
        [SerializeField, Range(0f, 1f)] private float uiPanelCloseSoundVolume = 1f;
        [SerializeField] private AudioClip uiAdjustSound;
        [InspectorName("슬라이더·토글·유물 회전 볼륨")]
        [SerializeField, Range(0f, 1f)] private float uiAdjustSoundVolume = 0.45f;
        [SerializeField] private AudioClip uiPickupSound;
        [InspectorName("집기 볼륨")]
        [SerializeField, Range(0f, 1f)] private float uiPickupSoundVolume = 1f;
        [SerializeField] private AudioClip uiPlaceSound;
        [InspectorName("놓기·장착·배치 볼륨")]
        [SerializeField, Range(0f, 1f)] private float uiPlaceSoundVolume = 1f;
        [FormerlySerializedAs("uiSuccessSound")]
        [InspectorName("아이템 구매·판매·새로고침 효과음")]
        [SerializeField] private AudioClip uiShopTransactionSound;
        [InspectorName("아이템 구매·판매·새로고침 볼륨")]
        [SerializeField, Range(0f, 1f)] private float uiShopTransactionSoundVolume = 1f;
        [SerializeField] private AudioClip uiErrorSound;
        [InspectorName("실패 볼륨")]
        [SerializeField, Range(0f, 1f)] private float uiErrorSoundVolume = 1f;
        [Header("UI 조작 공통 볼륨")]
        [InspectorName("UI 조작 전체 볼륨")]
        [Tooltip("창 열기를 제외한 각 UI 효과음 볼륨에 곱해집니다. 게임 설정의 효과음 볼륨도 함께 적용됩니다.")]
        [SerializeField, Range(0f, 1f)] private float uiInteractionVolume = 0.65f;
        private UISoundType? pendingUISound;
        private AudioClip pendingUIClip;
        private int openSoundFrame = -1;
        private readonly float[] lastUISoundTimes =
            { -100f, -100f, -100f, -100f, -100f, -100f, -100f, -100f, -100f };
#if UNITY_EDITOR
        public AudioClip LastPlayedUIClip { get; private set; }
        public int LastPlayedUIFrame { get; private set; } = -1;
#endif

        protected override void Awake()
        {
            base.Awake();

            AudioListener.volume = UserSettings.GetMasterVolume(AudioListener.volume);
            sfxVolume = UserSettings.GetSfxVolume(sfxVolume);

            if (bgmSource != null)
            {
                bgmSource.loop = true;
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
            pendingUISound = null;
            pendingUIClip = null;
            if (uiSfxSource != null) uiSfxSource.Stop();
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

            bgmSource.loop = true;
            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            bgmSource.clip = clip;
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
            if (uiSfxSource != null) uiSfxSource.volume = sfxVolume;
            UserSettings.SetSfxVolume(sfxVolume);
        }

        public void PlayUIOpenSound()
        {
            if (uiOpenSound == null || !isActiveAndEnabled || sfxVolume <= 0f || uiOpenSoundVolume <= 0f) return;
            if (openSoundFrame == Time.frameCount) return;

            PlayUIClip(uiOpenSound, uiOpenSoundVolume, true);
            openSoundFrame = Time.frameCount;
        }

        /// <summary>같은 프레임에는 상점 거래/실패 등 의미 있는 피드백을 우선해 한 번만 재생합니다.</summary>
        public void RequestUISound(UISoundType sound, AudioClip clipOverride = null)
        {
            if (!isActiveAndEnabled || sfxVolume <= 0f) return;
            if (!pendingUISound.HasValue || sound > pendingUISound.Value ||
                (sound == pendingUISound.Value && clipOverride != null))
            {
                pendingUISound = sound;
                pendingUIClip = clipOverride;
            }
        }

        private void LateUpdate()
        {
            if (!pendingUISound.HasValue) return;
            UISoundType sound = pendingUISound.Value;
            AudioClip clipOverride = pendingUIClip;
            pendingUISound = null;
            pendingUIClip = null;
            // 패널 열기와 그 패널을 연 버튼 클릭이 겹치는 것을 방지합니다.
            if (openSoundFrame == Time.frameCount && sound <= UISoundType.Open) return;
            float interval = sound == UISoundType.Hover ? 0.07f : sound == UISoundType.Adjust ? 0.08f : 0f;
            int index = (int)sound;
            if (Time.unscaledTime - lastUISoundTimes[index] < interval) return;
            AudioClip clip = clipOverride != null ? clipOverride : GetUISound(sound);
            float volume = GetUISoundVolume(sound, clipOverride != null);
            if (clip == null || volume <= 0f || sfxVolume <= 0f) return;
            lastUISoundTimes[index] = Time.unscaledTime;
            PlayUIClip(clip, volume, sound == UISoundType.Open || sound == UISoundType.Close);
        }

        private AudioClip GetUISound(UISoundType sound)
        {
            switch (sound)
            {
                case UISoundType.Hover: return uiHoverSound;
                case UISoundType.Click: return uiClickSound;
                case UISoundType.Adjust: return uiAdjustSound;
                case UISoundType.Close: return uiCloseSound;
                // 책 열기 소리는 PlayUIOpenSound로만 요청한다. 일반 창 전환은 클릭음을 사용한다.
                case UISoundType.Open: return uiClickSound;
                case UISoundType.Pickup: return uiPickupSound;
                case UISoundType.Place: return uiPlaceSound;
                case UISoundType.ShopTransaction: return uiShopTransactionSound;
                case UISoundType.Error: return uiErrorSound;
                default: return null;
            }
        }

        private float GetUISoundVolume(UISoundType sound, bool hasClipOverride)
        {
            if (sound == UISoundType.Open)
                return Mathf.Clamp01(uiInteractionVolume) * Mathf.Clamp01(uiClickSoundVolume);

            float volume;
            switch (sound)
            {
                case UISoundType.Hover: volume = uiHoverSoundVolume; break;
                case UISoundType.Click: volume = uiClickSoundVolume; break;
                case UISoundType.Adjust: volume = uiAdjustSoundVolume; break;
                case UISoundType.Close:
                    volume = hasClipOverride ? uiPanelCloseSoundVolume : uiCloseSoundVolume;
                    break;
                case UISoundType.Pickup: volume = uiPickupSoundVolume; break;
                case UISoundType.Place: volume = uiPlaceSoundVolume; break;
                case UISoundType.ShopTransaction: volume = uiShopTransactionSoundVolume; break;
                case UISoundType.Error: volume = uiErrorSoundVolume; break;
                default: return 0f;
            }
            return Mathf.Clamp01(uiInteractionVolume) * Mathf.Clamp01(volume);
        }

        private void PlayUIClip(AudioClip clip, float volume, bool interrupt = false)
        {
            if (uiSfxSource == null)
            {
                // BGM과 분리된 2D 소스로 재생해 UI 전환 중에도 효과음을 유지한다.
                uiSfxSource = gameObject.AddComponent<AudioSource>();
                uiSfxSource.playOnAwake = false;
                uiSfxSource.loop = false;
                uiSfxSource.spatialBlend = 0f;
                uiSfxSource.dopplerLevel = 0f;
                uiSfxSource.ignoreListenerPause = true;
            }

            uiSfxSource.volume = sfxVolume;
            // 긴 열기 효과음이 닫기 소리와 겹치지 않도록 패널 전환 시 이전 UI 소리를 끊습니다.
            if (interrupt) uiSfxSource.Stop();
#if UNITY_EDITOR
            LastPlayedUIClip = clip;
            LastPlayedUIFrame = Time.frameCount;
#endif
            uiSfxSource.PlayOneShot(clip, volume);
        }

        public float GetSFXVolume()
        {
            return sfxVolume;
        }
    }
}
