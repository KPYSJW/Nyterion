using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>시전 위치에 고정된 세 파동의 연출과 피해를 관리하고, 종료 후 로컬 풀에 반환합니다.</summary>
    public class ChainIgnitionWave : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] explosionRenderers;

        private readonly bool[] damagedWaves = new bool[ChainIgnitionSkillData.WaveCount];
        private readonly HashSet<IDamageable> waveTargets = new HashSet<IDamageable>();
        private readonly List<Collider2D> overlapResults = new List<Collider2D>(32);
        private ChainIgnitionSkillData data;
        private ContactFilter2D targetFilter;
        private float startTime;

        public void Begin(ChainIgnitionSkillData skill, Vector3 center)
        {
            if (skill == null || !skill.HasValidAnimation || explosionRenderers == null ||
                explosionRenderers.Length != ChainIgnitionSkillData.WaveCount * ChainIgnitionSkillData.DirectionCount)
            {
                Debug.LogError("[ChainIgnitionWave] 폭발 애니메이션 또는 24개 렌더러 연결을 확인해 주세요.", this);
                gameObject.SetActive(false);
                return;
            }

            data = skill;
            transform.SetPositionAndRotation(center, Quaternion.identity);
            transform.localScale = Vector3.one;
            targetFilter = new ContactFilter2D { useTriggers = true };
            targetFilter.SetLayerMask(data.targetLayers);
            startTime = Time.time;
            for (int wave = 0; wave < ChainIgnitionSkillData.WaveCount; wave++)
            {
                damagedWaves[wave] = false;
                for (int direction = 0; direction < ChainIgnitionSkillData.DirectionCount; direction++)
                {
                    float angle = direction * Mathf.PI * 2f / ChainIgnitionSkillData.DirectionCount;
                    SpriteRenderer renderer = explosionRenderers[wave * ChainIgnitionSkillData.DirectionCount + direction];
                    renderer.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * data.GetRingRadius(wave);
                    renderer.transform.localScale = Vector3.one * Mathf.Max(0.01f, data.visualScale);
                    renderer.sprite = data.explosionFrames[0];
                    renderer.enabled = false;
                }
            }
            gameObject.SetActive(true);
            Update();
        }

        private void Update()
        {
            if (data == null) return;
            float elapsed = Time.time - startTime;
            for (int wave = 0; wave < ChainIgnitionSkillData.WaveCount; wave++)
            {
                float waveElapsed = elapsed - wave * data.WaveInterval;
                bool visible = waveElapsed >= 0f && waveElapsed < data.AnimationDuration;
                int frame = Mathf.Clamp(Mathf.FloorToInt(waveElapsed * Mathf.Max(1f, data.framesPerSecond)),
                    0, data.explosionFrames.Length - 1);
                for (int direction = 0; direction < ChainIgnitionSkillData.DirectionCount; direction++)
                {
                    SpriteRenderer renderer = explosionRenderers[wave * ChainIgnitionSkillData.DirectionCount + direction];
                    renderer.enabled = visible;
                    if (visible) renderer.sprite = data.explosionFrames[frame];
                }

                // 프레임이 건너뛰어져도 각 파동의 피해를 정확히 한 번 처리합니다.
                if (!damagedWaves[wave] && waveElapsed >= data.DamageDelay)
                {
                    damagedWaves[wave] = true;
                    DealWaveDamage(wave);
                }
            }
            if (elapsed >= (ChainIgnitionSkillData.WaveCount - 1) * data.WaveInterval + data.AnimationDuration)
                gameObject.SetActive(false);
        }

        private void DealWaveDamage(int wave)
        {
            waveTargets.Clear();
            for (int direction = 0; direction < ChainIgnitionSkillData.DirectionCount; direction++)
            {
                Vector3 position = explosionRenderers[wave * ChainIgnitionSkillData.DirectionCount + direction].transform.position;
                overlapResults.Clear();
                Physics2D.OverlapCircle(position, Mathf.Max(0.01f, data.explosionRadius), targetFilter, overlapResults);
                foreach (Collider2D hit in overlapResults)
                {
                    if (hit == null) continue;
                    IDamageable target = hit.GetComponentInParent<IDamageable>();
                    if (target is Component component && component.gameObject.activeInHierarchy && waveTargets.Add(target))
                        target.TakeDamage(data.damage);
                }
            }
        }

        private void OnDisable()
        {
            data = null;
            waveTargets.Clear();
            overlapResults.Clear();
            if (explosionRenderers == null) return;
            foreach (SpriteRenderer renderer in explosionRenderers)
                if (renderer != null) renderer.enabled = false;
        }
    }
}
