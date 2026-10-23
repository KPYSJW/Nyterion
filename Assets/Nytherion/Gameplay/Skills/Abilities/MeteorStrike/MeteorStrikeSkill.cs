using System.Collections;
using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>시전 순간 화면 안의 모든 적에게 운석을 하나씩 순서대로 배정합니다.</summary>
    public sealed class MeteorStrikeSkill : SkillBase
    {
        private readonly List<Collider2D> hits = new List<Collider2D>();
        private readonly List<EnemyBase> targets = new List<EnemyBase>();
        private readonly HashSet<EnemyBase> uniqueTargets = new HashSet<EnemyBase>();
        private readonly List<MeteorStrikeImpact> impacts = new List<MeteorStrikeImpact>();

        public override bool CanUse() => base.CanUse() && isActiveAndEnabled &&
            skillData is MeteorStrikeSkillData data && data.impactPrefab != null && Camera.main != null;

        public override bool TryUse()
        {
            if (!CanUse()) return false;
            CollectVisibleTargets((MeteorStrikeSkillData)skillData, Camera.main);
            // 대상이 없으면 쿨다운과 스킬 사용 진척도를 소모하지 않습니다.
            return targets.Count > 0 && base.TryUse();
        }

        private void CollectVisibleTargets(MeteorStrikeSkillData data, Camera camera)
        {
            targets.Clear();
            uniqueTargets.Clear();
            hits.Clear();
            float targetZ = caster != null ? caster.position.z : transform.position.z;
            float depth = Mathf.Abs(targetZ - camera.transform.position.z);
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int y = 0; y <= 1; y++)
            for (int x = 0; x <= 1; x++)
            {
                Vector2 corner = camera.ViewportToWorldPoint(new Vector3(x, y, depth));
                minimum = Vector2.Min(minimum, corner);
                maximum = Vector2.Max(maximum, corner);
            }

            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(data.targetLayers);
            // 가변 길이 결과를 사용해 적이 많아도 고정 버퍼의 상한으로 누락되지 않습니다.
            Physics2D.OverlapArea(minimum, maximum, filter, hits);
            foreach (Collider2D hit in hits)
            {
                EnemyBase enemy = hit.GetComponentInParent<EnemyBase>();
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.isDead ||
                    !enemy.TryGetGroundHitBounds(out Bounds bounds)) continue;
                Vector3 viewport = camera.WorldToViewportPoint(bounds.center);
                if (viewport.z < camera.nearClipPlane || viewport.z > camera.farClipPlane ||
                    viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f ||
                    (camera.cullingMask & (1 << enemy.gameObject.layer)) == 0) continue;
                if (uniqueTargets.Add(enemy)) targets.Add(enemy);
            }
        }

        protected override void Activate()
        {
            MeteorStrikeSkillData data = (MeteorStrikeSkillData)skillData;
            PlayerManager player = caster != null ? caster.GetComponentInParent<PlayerManager>() : GetComponentInParent<PlayerManager>();
            int level = player != null ? player.GetSkillLevel(data) : Mathf.Max(1, data.skillLevel);
            // 중첩 시전 중에도 이전 시전의 대상 목록과 피해량을 유지합니다.
            StartCoroutine(StrikeTargets(targets.ToArray(), data, data.GetDamage(level), EffectSizeMultiplier));
        }

        private IEnumerator StrikeTargets(EnemyBase[] castTargets, MeteorStrikeSkillData data, float damage, float size)
        {
            WaitForSeconds interval = new WaitForSeconds(Mathf.Max(0.01f, data.strikeInterval));
            bool hasLaunched = false;
            foreach (EnemyBase target in castTargets)
            {
                if (target == null || !target.isActiveAndEnabled || target.isDead) continue;
                if (hasLaunched) yield return interval;
                if (target == null || !target.isActiveAndEnabled || target.isDead) continue;
                MeteorStrikeImpact impact = impacts.Find(candidate => candidate != null && !candidate.gameObject.activeSelf);
                if (impact == null)
                {
                    impact = Instantiate(data.impactPrefab);
                    impacts.Add(impact);
                }
                impact.Begin(data, target, damage, size);
                hasLaunched = true;
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            foreach (MeteorStrikeImpact impact in impacts)
                if (impact != null) impact.gameObject.SetActive(false);
            targets.Clear();
            hits.Clear();
            uniqueTargets.Clear();
        }

        private void OnDestroy()
        {
            foreach (MeteorStrikeImpact impact in impacts)
                if (impact != null) Destroy(impact.gameObject);
            impacts.Clear();
        }
    }
}
