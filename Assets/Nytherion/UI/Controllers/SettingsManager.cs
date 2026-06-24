using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.Core.Managers;
using TMPro;
using VContainer;

namespace Nytherion.UI.Controllers
{
    public class SettingsManager : MonoBehaviour
    {
        [SerializeField] private GameSceneUIRefs gameSceneuiRefs;
        private AudioManager audioManager;
        private Slider masterSlider;
        private Slider bgmSlider;
        private Slider sfxSlider;
        private Toggle fullscreenToggle;
        private TMP_Dropdown resolutionDropdown;

        [Header("Volume Input Fields")]
        [SerializeField] private TMP_InputField masterInputField;
        [SerializeField] private TMP_InputField bgmInputField;
        [SerializeField] private TMP_InputField sfxInputField;

        [Header("Volume Control Buttons")]
        [SerializeField] private Button masterUpButton;
        [SerializeField] private Button masterDownButton;
        [SerializeField] private Button bgmUpButton;
        [SerializeField] private Button bgmDownButton;
        [SerializeField] private Button sfxUpButton;
        [SerializeField] private Button sfxDownButton;
        
        private List<Resolution> customResolutions = new List<Resolution>
        {
            new Resolution { width = 1280, height = 720 },
            new Resolution { width = 1600, height = 900 },
            new Resolution { width = 1920, height = 1080 },
            new Resolution { width = 2560, height = 1440 }
        };

        [Inject]
        public void Construct(AudioManager audioManager)
        {
            this.audioManager = audioManager;
        }

        private void Awake()
        {
            if (gameSceneuiRefs != null)
            {
                this.masterSlider = gameSceneuiRefs.MasterSlider;
                this.bgmSlider = gameSceneuiRefs.BgmSlider;
                this.sfxSlider = gameSceneuiRefs.SfxSlider;
                this.fullscreenToggle = gameSceneuiRefs.FullscreenToggle;
                this.resolutionDropdown = gameSceneuiRefs.ResolutionDropdown;
            }
        }

        private void Start()
        {
            // 볼륨 슬라이더, 입력 필드, Up/Down 버튼 제어 연동 초기화
            InitializeVolumeController(masterSlider, masterInputField, masterUpButton, masterDownButton, SetMasterVolume);
            InitializeVolumeController(bgmSlider, bgmInputField, bgmUpButton, bgmDownButton, SetBGMVolume);
            InitializeVolumeController(sfxSlider, sfxInputField, sfxUpButton, sfxDownButton, SetSFXVolume);

            if (fullscreenToggle != null)
            {
                fullscreenToggle.isOn = Screen.fullScreen;
                fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
            }

            PopulateResolutions();
            
            if (resolutionDropdown != null)
            {
                resolutionDropdown.onValueChanged.AddListener(SetResolution);
            }

            // 초기 BGM 볼륨값으로 세팅
            if (audioManager != null && bgmSlider != null)
            {
                bgmSlider.value = audioManager.GetBGMVolume();
            }
        }

        /// <summary>
        /// 슬라이더, 인풋필드, Up/Down 버튼을 하나로 연동하는 헬퍼 메서드
        /// </summary>
        private void InitializeVolumeController(
            Slider slider, 
            TMP_InputField inputField, 
            Button upButton, 
            Button downButton, 
            System.Action<float> onVolumeChanged)
        {
            if (slider == null) return;

            // 1. 슬라이더 값 변경 리스너 등록
            slider.onValueChanged.AddListener(delegate (float value)
            {
                onVolumeChanged?.Invoke(value);
                if (inputField != null)
                {
                    // 0.0 ~ 1.0 값을 0 ~ 100 값으로 변환하여 텍스트에 표시
                    inputField.text = Mathf.RoundToInt(value * 100f).ToString();
                }
            });

            // 초기 슬라이더 값에 맞게 텍스트박스 초기화
            if (inputField != null)
            {
                inputField.text = Mathf.RoundToInt(slider.value * 100f).ToString();
            }

            // 2. 인풋필드 편집 종료 리스너 등록 (안전장치 포함)
            if (inputField != null)
            {
                inputField.onEndEdit.AddListener(delegate (string text)
                {
                    int parsedValue;
                    // 숫자이고, 0 ~ 100 사이 범위에 있는 정수인지 체크
                    if (int.TryParse(text, out parsedValue) && parsedValue >= 0 && parsedValue <= 100)
                    {
                        slider.value = parsedValue / 100f;
                    }
                    else
                    {
                        // 문자이거나 범위 밖의 값이면 이전 정상 볼륨 값으로 원복 (안전장치)
                        inputField.text = Mathf.RoundToInt(slider.value * 100f).ToString();
                    }
                });
            }

            // 3. Up 버튼 리스너 등록 (+1 증가)
            if (upButton != null)
            {
                upButton.onClick.AddListener(delegate ()
                {
                    int currentVal = Mathf.RoundToInt(slider.value * 100f);
                    if (currentVal < 100)
                    {
                        slider.value = (currentVal + 1) / 100f;
                    }
                });
            }

            // 4. Down 버튼 리스너 등록 (-1 감소)
            if (downButton != null)
            {
                downButton.onClick.AddListener(delegate ()
                {
                    int currentVal = Mathf.RoundToInt(slider.value * 100f);
                    if (currentVal > 0)
                    {
                        slider.value = (currentVal - 1) / 100f;
                    }
                });
            }
        }

        private void PopulateResolutions()
        {
            List<string> options = new List<string>();
            int currentIndex = 0;

            for (int i = 0; i < customResolutions.Count; i++)
            {
                string label = customResolutions[i].width + "x" + customResolutions[i].height;
                options.Add(label);

                if (Screen.width == customResolutions[i].width && Screen.height == customResolutions[i].height)
                {
                    currentIndex = i;
                }
            }

            if (resolutionDropdown != null)
            {
                resolutionDropdown.ClearOptions();
                resolutionDropdown.AddOptions(options);
                resolutionDropdown.value = currentIndex;
                resolutionDropdown.RefreshShownValue();
            }
        }

        private void SetResolution(int index)
        {
            Resolution res = customResolutions[index];
            Screen.SetResolution(res.width, res.height, Screen.fullScreen);
            Debug.Log($"해상도 변경: {res.width}x{res.height}");
        }

        private void SetFullscreen(bool isFullscreen)
        {
            Screen.fullScreen = isFullscreen;
            Debug.Log("전체화면: " + isFullscreen);
        }

        private void SetMasterVolume(float value)
        {
            AudioListener.volume = value;
        }

        private void SetBGMVolume(float value)
        {
            if (audioManager == null) return;
            audioManager.SetBGMVolume(value);
        }

        private void SetSFXVolume(float value)
        {
            Debug.Log("SFX Volume: " + value);
        }
    }
}

