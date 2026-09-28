using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    public class ChainIgnitionSkill : SkillBase
    {
        // 장착된 스킬이 소유하는 로컬 풀입니다. 중첩 시전도 기존 폭발을 중단하지 않습니다.
        private readonly List<ChainIgnitionWave> waves = new List<ChainIgnitionWave>();

        protected override void Activate()
        {
            if (!(skillData is ChainIgnitionSkillData data) || data.wavePrefab == null || !data.HasValidAnimation)
            {
                Debug.LogError("[ChainIgnitionSkill] 스킬 데이터, 폭발 프리팹 또는 애니메이션 참조가 없습니다.", this);
                return;
            }

            // 무기 발사점 대신 시전 순간의 플레이어 위치를 복사합니다.
            Vector3 castPosition = caster != null ? caster.position : transform.position;
            castPosition += (Vector3)data.castCenterOffset;
            ChainIgnitionWave availableWave = waves.Find(wave => wave != null && !wave.gameObject.activeSelf);
            if (availableWave == null)
            {
                availableWave = Instantiate(data.wavePrefab, castPosition, Quaternion.identity);
                waves.Add(availableWave);
            }
            availableWave.Begin(data, castPosition);
        }

        private void OnDisable()
        {
            foreach (ChainIgnitionWave wave in waves)
                if (wave != null) wave.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (ChainIgnitionWave wave in waves)
                if (wave != null) Destroy(wave.gameObject);
            waves.Clear();
        }
    }
}
