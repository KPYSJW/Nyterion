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
            int level = player != null ? player.GetSkillLevel(data) : Mathf.Max(1, data.skillLevel);
            var playerData = player != null ? player.currentPlayerData : null;
            int projectileCount = data.GetProjectileCount(level, playerData != null ? playerData.extraProjectiles : 0f);
            float size = playerData != null ? playerData.projectileSizeMultiplier : 1f;
            float range = ChainIgnitionSkillData.GetSpreadRangeMultiplier(size);

            // 타겟 메이커는 연쇄 파동 전체를 조준 지점으로 옮기고, 첫 파동에만 중심 폭발을 추가합니다.
            bool addTargetExplosion = false;
            Vector3 castPosition = caster != null ? caster.position : transform.position;
            castPosition += (Vector3)data.castCenterOffset;
            Vector2 aimDirection = firePoint != null ? (Vector2)firePoint.right : Vector2.right;
            if (TryGetMouseWorldPosition(castPosition, out Vector3 mousePosition))
            {
                aimDirection = (Vector2)(mousePosition - castPosition);
                if (player != null && player.playerRelicManager != null &&
                    player.playerRelicManager.IsRelicActive("TargetMaker"))
                {
                    castPosition = mousePosition;
                    addTargetExplosion = true;
                }
            }
            GetAvailableWave(data, castPosition).Begin(data, castPosition, aimDirection,
                projectileCount, data.GetDamage(level), size, range);
            if (addTargetExplosion)
                GetAvailableWave(data, mousePosition).Begin(data, mousePosition, aimDirection,
                    1, data.GetDamage(level), size, range, true);
        }

        private ChainIgnitionWave GetAvailableWave(ChainIgnitionSkillData data, Vector3 position)
        {
            ChainIgnitionWave availableWave = waves.Find(wave => wave != null && !wave.gameObject.activeSelf);
            if (availableWave == null)
            {
                availableWave = Instantiate(data.wavePrefab, position, Quaternion.identity);
                waves.Add(availableWave);
            }
            return availableWave;
        }

        private bool TryGetMouseWorldPosition(Vector3 center, out Vector3 worldPosition)
        {
            worldPosition = center;
            Camera camera = Camera.main;
            if (camera != null && Mouse.current != null)
            {
                Vector2 screenPosition = Mouse.current.position.ReadValue();
                worldPosition = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y,
                    Mathf.Abs(camera.transform.position.z - center.z)));
                worldPosition.z = center.z;
                return true;
            }
            return false;
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
