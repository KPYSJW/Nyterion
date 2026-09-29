using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;
using UnityEngine.InputSystem;

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

            PlayerManager player = caster != null ? caster.GetComponentInParent<PlayerManager>() : GetComponentInParent<PlayerManager>();
            ChainIgnitionDebugSettings debugSettings = player != null ? player.GetComponent<ChainIgnitionDebugSettings>() : null;
            bool useDebugSettings = debugSettings != null && debugSettings.useTestSettings;
            int level = player != null ? player.GetSkillLevel(data) : Mathf.Max(1, data.skillLevel);
            var playerData = player != null ? player.currentPlayerData : null;
            int projectileCount = debugSettings != null
                ? debugSettings.GetProjectileCount(data, level, playerData != null ? playerData.extraProjectiles : 0f)
                : data.GetProjectileCount(level, playerData != null ? playerData.extraProjectiles : 0f);
            float size = playerData != null ? playerData.projectileSizeMultiplier : 1f;
            float range = playerData != null ? playerData.attackRangeMultiplier : 1f;
            if (useDebugSettings)
            {
                size *= Mathf.Max(0.01f, debugSettings.sizeMultiplier);
                range *= Mathf.Max(0.01f, debugSettings.rangeMultiplier);
                data = debugSettings.CreateCastData(data);
            }
            range = ChainIgnitionSkillData.GetSpreadRangeMultiplier(size, range);

            // 무기 발사점 대신 시전 순간의 플레이어 위치를 복사합니다.
            Vector3 castPosition = caster != null ? caster.position : transform.position;
            castPosition += (Vector3)data.castCenterOffset;
            ChainIgnitionWave availableWave = waves.Find(wave => wave != null && !wave.gameObject.activeSelf);
            if (availableWave == null)
            {
                availableWave = Instantiate(data.wavePrefab, castPosition, Quaternion.identity);
                waves.Add(availableWave);
            }
            availableWave.Begin(data, castPosition, GetAimDirection(castPosition),
                projectileCount, data.GetDamage(level), size, range, useDebugSettings);
        }

        private Vector2 GetAimDirection(Vector3 center)
        {
            Camera camera = Camera.main;
            if (camera != null && Mouse.current != null)
            {
                Vector2 screenPosition = Mouse.current.position.ReadValue();
                Vector3 worldPosition = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y,
                    Mathf.Abs(camera.transform.position.z - center.z)));
                return (Vector2)(worldPosition - center);
            }
            return firePoint != null ? (Vector2)firePoint.right : Vector2.right;
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
