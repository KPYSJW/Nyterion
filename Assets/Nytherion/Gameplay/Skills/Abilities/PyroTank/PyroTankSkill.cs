using System.Collections.Generic;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>시전 위치에 전차를 생성하고 종료된 전차를 로컬 풀에서 재사용합니다.</summary>
    public sealed class PyroTankSkill : SkillBase
    {
        private readonly List<PyroTankController> tanks = new List<PyroTankController>();
        private readonly List<RaycastHit2D> spawnHits = new List<RaycastHit2D>();

        protected override void Activate()
        {
            if (!(skillData is PyroTankSkillData data) || data.tankPrefab == null || data.explosionAnimation == null)
            {
                Debug.LogError("[PyroTankSkill] 스킬 데이터, 전차 프리팹 또는 폭발 애니메이션 참조가 없습니다.", this);
                return;
            }

            Vector3 playerPosition = caster != null ? caster.position : transform.position;
            Vector2 aimDirection = GetAimDirection(playerPosition);
            Vector3 position = GetSpawnPosition(playerPosition, aimDirection, data);
            PyroTankController tank = tanks.Find(candidate => candidate != null && !candidate.gameObject.activeSelf);
            if (tank == null)
            {
                tank = Instantiate(data.tankPrefab, position, Quaternion.identity);
                tanks.Add(tank);
            }
            tank.Begin(data, position, aimDirection);
        }

        private Vector2 GetAimDirection(Vector3 playerPosition)
        {
            Camera camera = Camera.main;
            if (camera != null && Mouse.current != null)
            {
                Vector2 screenPosition = Mouse.current.position.ReadValue();
                Vector3 worldPosition = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y,
                    Mathf.Abs(camera.transform.position.z - playerPosition.z)));
                Vector2 aim = worldPosition - playerPosition;
                if (aim.sqrMagnitude > 0.0001f) return aim.normalized;
            }
            Vector2 fallback = firePoint != null ? (Vector2)firePoint.right : Vector2.right;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector2.right;
        }

        private Vector3 GetSpawnPosition(Vector3 playerPosition, Vector2 direction, PyroTankSkillData data)
        {
            float offset = Mathf.Max(0f, data.spawnForwardOffset);
            // 플레이어와 생성 위치 사이에 벽이 있으면 벽 앞에서 등장합니다.
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(data.obstacleLayers);
            spawnHits.Clear();
            Physics2D.CircleCast(playerPosition,
                Mathf.Max(0.01f, data.collisionRadius * Mathf.Abs(data.tankPrefab.transform.localScale.x)),
                direction, filter, spawnHits, offset);
            foreach (RaycastHit2D wall in spawnHits)
                offset = Mathf.Min(offset, Mathf.Max(0f, wall.distance - 0.001f));
            return playerPosition + (Vector3)(direction * offset);
        }

        private void OnDisable()
        {
            foreach (PyroTankController tank in tanks)
                if (tank != null) tank.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            foreach (PyroTankController tank in tanks)
                if (tank != null) Destroy(tank.gameObject);
            tanks.Clear();
        }
    }
}
