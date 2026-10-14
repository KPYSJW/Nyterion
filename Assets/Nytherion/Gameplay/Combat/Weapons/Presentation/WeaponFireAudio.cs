using Nytherion.Core.Data;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>무기 발사음을 재생하며 같은 순간의 다중 투사체 음량 누적을 막습니다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class WeaponFireAudio : MonoBehaviour
    {
        private AudioSource source;
        private float lastPlayTime = float.NegativeInfinity;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 160;
        }

        public void Play(AudioClip clip, float volume)
        {
            // 한 산탄 묶음은 한 번 재생하고, 시간차 연사와 지연 발사는 각각 재생합니다.
            if (clip == null || !isActiveAndEnabled || Time.time - lastPlayTime < 0.03f) return;
            float gain = Mathf.Clamp01(volume);
            source.volume = UserSettings.GetSfxVolume();
            if (gain <= 0f || source.volume <= 0f) return;
            lastPlayTime = Time.time;
            source.pitch = Random.Range(0.97f, 1.03f);
            source.PlayOneShot(clip, gain);
        }

        private void Update()
        {
            // 이미 재생 중인 잔향도 설정 변경 즉시 음량/음소거를 반영합니다.
            if (source.isPlaying) source.volume = UserSettings.GetSfxVolume();
        }

        private void OnDisable()
        {
            if (source != null) source.Stop();
            lastPlayTime = float.NegativeInfinity;
        }
    }
}
