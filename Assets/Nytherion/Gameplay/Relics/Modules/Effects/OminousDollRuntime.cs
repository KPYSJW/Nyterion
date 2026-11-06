using System.Collections.Generic;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.Gameplay.Relics.Modules
{
    public sealed class OminousDollRuntime : MonoBehaviour
    {
        private struct Bonus
        {
            public float Attack, Defense, Damage, Radius;
            public GameObject Visual;
        }
        private PlayerManager player;
        private readonly Dictionary<object, Bonus> bonuses = new Dictionary<object, Bonus>();
        private readonly HashSet<DecoyDummy> active = new HashSet<DecoyDummy>();
        private readonly List<StatModifier> applied = new List<StatModifier>();
        private readonly List<Collider2D> hits = new List<Collider2D>();
        private readonly HashSet<IDamageable> targets = new HashSet<IDamageable>();
        public int ExplosionCount { get; private set; }

        public void SetBonus(object source, PlayerManager owner, float attack, float defense, float damage, float radius, GameObject visual)
        {
            player = owner;
            bonuses[source] = new Bonus { Attack = Mathf.Max(0f, attack), Defense = Mathf.Max(0f, defense),
                Damage = Mathf.Max(0f, damage), Radius = Mathf.Max(0f, radius), Visual = visual };
            PlayerSkillManager skills = owner.GetComponent<PlayerSkillManager>();
            if (skills != null)
                foreach (SkillBase skill in skills.equippedSkills)
                    if (skill is DecoySkill decoy && decoy.ActiveDummy != null && decoy.ActiveDummy.IsAlive)
                        active.Add(decoy.ActiveDummy);
            RefreshModifiers();
        }

        public void RemoveBonus(object source)
        {
            bonuses.Remove(source);
            RefreshModifiers();
        }

        public void NotifyActivated(DecoyDummy dummy)
        {
            if (dummy != null && active.Add(dummy)) RefreshModifiers();
        }

        public void NotifyClosed(DecoyDummy dummy, bool explode)
        {
            if (!active.Remove(dummy)) return;
            Vector3 position = dummy.transform.position;
            bool canExplode = explode && bonuses.Count > 0 && player != null && player.gameObject.activeInHierarchy;
            PlayerHealth health = player != null ? player.GetComponent<PlayerHealth>() : null;
            if (health != null && health.CurrentHealth <= 0f) canExplode = false;
            RefreshModifiers();
            if (canExplode) Explode(position);
        }

        private void RefreshModifiers()
        {
            if (player == null) return;
            foreach (StatModifier modifier in applied) player.RemoveTemporaryStatModifier(modifier);
            applied.Clear();
            active.RemoveWhere(dummy => dummy == null || !dummy.IsAlive);
            if (!isActiveAndEnabled || active.Count == 0 || bonuses.Count == 0) return;
            float attack = 0f, defense = 0f;
            foreach (Bonus bonus in bonuses.Values) { attack += bonus.Attack; defense += bonus.Defense; }
            applied.Add(new StatModifier { stat = StatType.RangedDamage, value = attack, isPercentage = true });
            applied.Add(new StatModifier { stat = StatType.Defense, value = -defense });
            foreach (StatModifier modifier in applied) player.AddTemporaryStatModifier(modifier);
        }

        private void Explode(Vector3 position)
        {
            float damage = 0f, radius = 0f;
            GameObject visual = null;
            foreach (Bonus bonus in bonuses.Values)
            {
                damage += bonus.Damage;
                radius = Mathf.Max(radius, bonus.Radius);
                if (bonus.Visual != null) visual = bonus.Visual;
            }
            if (player.currentPlayerData != null)
            {
                damage *= Mathf.Max(0f, player.currentPlayerData.skillDamageMultiplier);
                radius *= Mathf.Max(0.01f, player.currentPlayerData.projectileSizeMultiplier);
            }
            ExplosionCount++;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(LayerMask.GetMask("Enemy", "FlyingBody"));
            hits.Clear();
            targets.Clear();
            Physics2D.OverlapCircle(position, radius, filter, hits);
            foreach (Collider2D hit in hits)
            {
                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target is MonoBehaviour behaviour && behaviour.isActiveAndEnabled &&
                    (!(target is EnemyBase enemy) || !enemy.isDead)) targets.Add(target);
            }
            // 복수 콜라이더를 가진 적도 폭발 한 번에 한 번만 피해를 받습니다.
            foreach (IDamageable target in new List<IDamageable>(targets))
                if (target is MonoBehaviour behaviour && behaviour.isActiveAndEnabled) target.TakeDamage(damage);
            if (visual == null) return;
            ObjectPoolManager pool = player.ObjectPool;
            GameObject effect = pool != null ? pool.SpawnFromPool(visual, position, Quaternion.identity, 3)
                : Instantiate(visual, position, Quaternion.identity);
            if (effect != null && effect.TryGetComponent(out ThermalShockExplosionVFX animation))
                animation.Play(pool, radius, player.GetComponentInChildren<SpriteRenderer>());
        }

        private void OnDisable()
        {
            active.Clear();
            RefreshModifiers();
        }
    }
}
