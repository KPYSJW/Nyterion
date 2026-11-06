using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nytherion.GamePlay.Skills
{
    public sealed class DecoySkill : SkillBase
    {
        private DecoyDummy dummy;
        private Vector3 spawnPosition;
        public DecoyDummy ActiveDummy => dummy;

        public override bool CanUse() => base.CanUse() && caster != null &&
            caster.gameObject.activeInHierarchy && skillData is DecoySkillData data &&
            data.dummyPrefab != null && data.dummySprite != null &&
            data.summonFrames != null && data.summonFrames.Length > 0;

        public override bool TryUse()
        {
            if (!CanUse()) return false;
            DecoySkillData data = (DecoySkillData)skillData;
            Vector2 direction = firePoint != null ? (Vector2)firePoint.right : Vector2.right;
            Camera camera = Camera.main;
            if (camera != null && Mouse.current != null)
            {
                Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (new Plane(Vector3.forward, caster.position).Raycast(ray, out float distance))
                {
                    Vector2 aim = ray.GetPoint(distance) - caster.position;
                    if (aim.sqrMagnitude > 0.0001f) direction = aim.normalized;
                }
            }
            spawnPosition = caster.position + (Vector3)(direction.normalized * data.spawnDistance);
            // 벽 안이나 벽 너머에는 생성하지 않으며 실패 시 쿨타임을 소비하지 않습니다.
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(data.blockingLayers);
            RaycastHit2D[] hits = new RaycastHit2D[1];
            if (Physics2D.CircleCast(caster.position, data.collisionRadius, direction.normalized,
                filter, hits, data.spawnDistance) > 0) return false;
            return base.TryUse();
        }

        protected override void Activate()
        {
            DecoySkillData data = (DecoySkillData)skillData;
            PlayerManager player = caster.GetComponentInParent<PlayerManager>();
            int level = player != null ? player.GetSkillLevel(data) : Mathf.Max(1, data.skillLevel);
            if (dummy == null) dummy = Instantiate(data.dummyPrefab);
            dummy.Begin(data, caster, spawnPosition, level);
        }

        private void Update()
        {
            if (dummy != null && dummy.gameObject.activeSelf &&
                (caster == null || !caster.gameObject.activeInHierarchy)) dummy.Close(false);
        }

        private void OnDisable() { if (dummy != null) dummy.Close(false); }
        private void OnDestroy() { if (dummy != null) Destroy(dummy.gameObject); }
    }
}
