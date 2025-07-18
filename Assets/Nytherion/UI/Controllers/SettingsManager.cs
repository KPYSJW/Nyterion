using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Nytherion.Core.Managers;
using TMPro;
using Zenject;

namespace Nytherion.UI.Controllers
{
    public class SettingsManager : MonoBehaviour
    {
        private Slider masterSlider;
        private Slider bgmSlider;
        private Slider sfxSlider;
        private Toggle fullscreenToggle;
        private TMP_Dropdown resolutionDropdown;
        private AudioManager _audioManager;
        private List<Resolution> customResolutions = new List<Resolution>
        {
            new Resolution { width = 1280, height = 720 },
            new Resolution { width = 1600, height = 900 },
            new Resolution { width = 1920, height = 1080 },
            new Resolution { width = 2560, height = 1440 }
        };
        [Inject]
        public void Construct(
            AudioManager audioManager,
            [Inject(Id = "MasterSlider")] Slider masterSlider,
            [Inject(Id = "BGMSlider")] Slider bgmSlider,
            [Inject(Id = "SFXSlider")] Slider sfxSlider,
            [Inject(Id = "FullscreenToggle")] Toggle fullscreenToggle,
            [Inject(Id = "ResolutionDropdown")] TMP_Dropdown resolutionDropdown)
        {
            _audioManager = audioManager;
            this.masterSlider = masterSlider;
            this.bgmSlider = bgmSlider;
            this.sfxSlider = sfxSlider;
            this.fullscreenToggle = fullscreenToggle;
            this.resolutionDropdown = resolutionDropdown;
        }
        private void Start()
        {
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
            bgmSlider.onValueChanged.AddListener(SetBGMVolume);
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);

            fullscreenToggle.isOn = Screen.fullScreen;
            fullscreenToggle.onValueChanged.AddListener(SetFullscreen);

            PopulateResolutions();
            resolutionDropdown.onValueChanged.AddListener(SetResolution);

            if (_audioManager != null)
            {
                bgmSlider.value = _audioManager.GetBGMVolume();
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

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(options);
            resolutionDropdown.value = currentIndex;
            resolutionDropdown.RefreshShownValue();
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
            if (_audioManager == null) return;
            _audioManager.SetBGMVolume(value);
        }

        private void SetSFXVolume(float value)
        {
            Debug.Log("SFX Volume: " + value);
        }
    }
}

