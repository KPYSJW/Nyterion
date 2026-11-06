using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;
using Nytherion.Gameplay.Relics.Modules;

namespace Nytherion.GamePlay.Skills
{
    [RequireComponent(typeof(SpriteRenderer), typeof(CircleCollider2D), typeof(Rigidbody2D))]
    public sealed class DecoyDummy : MonoBehaviour, IDamageable
    {
        private readonly List<Collider2D> hits = new List<Collider2D>();
        private readonly HashSet<EnemyAIController> affectedEnemies = new HashSet<EnemyAIController>();
        private DecoySkillData data;
        private Transform owner;
        private SpriteRenderer visual;
        private CircleCollider2D body;
        private float summonStartedAt, expiresAt, nextScanAt, lifetime;
        public float CurrentDurability { get; private set; }
        public bool IsAlive { get; private set; }
        public float RemainingLifetime => IsAlive ? Mathf.Max(0f, expiresAt - Time.time) : 0f;

        public void Begin(DecoySkillData skill, Transform caster, Vector3 position, int level)
        {
            Close();
            data = skill;
            owner = caster;
            visual = GetComponent<SpriteRenderer>();
            body = GetComponent<CircleCollider2D>();
            body.radius = data.collisionRadius;
            body.enabled = false;
            transform.position = position;
            CurrentDurability = data.GetDurability(level);
            lifetime = data.GetLifetime(level);
            summonStartedAt = Time.time;
            visual.sprite = data.summonFrames[0];
            visual.color = Color.white;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (data == null || owner == null || !owner.gameObject.activeInHierarchy)
            { Close(false); return; }
            PlayerHealth health = owner.GetComponent<PlayerHealth>();
            if (health != null && health.CurrentHealth <= 0f) { Close(false); return; }
            if (!IsAlive)
            {
                int frame = Mathf.FloorToInt((Time.time - summonStartedAt) * Mathf.Max(1f, data.framesPerSecond));
                if (frame < data.summonFrames.Length) { visual.sprite = data.summonFrames[frame]; return; }
                // 소환 연출이 모두 끝난 시점부터 피격·유인·지속시간을 시작합니다.
                ContactFilter2D wallFilter = new ContactFilter2D { useTriggers = false };
                wallFilter.SetLayerMask(data.blockingLayers);
                hits.Clear();
                if (Physics2D.OverlapCircle(transform.position, data.collisionRadius, wallFilter, hits) > 0)
                { Close(); return; }
                visual.sprite = data.dummySprite;
                visual.color = new Color(0.65f, 0.9f, 1f, 1f);
                body.enabled = true;
                IsAlive = true;
                owner.GetComponentInParent<OminousDollRuntime>()?.NotifyActivated(this);
                expiresAt = Time.time + lifetime;
                nextScanAt = Time.time;
            }
            if (Time.time >= expiresAt) { Close(); return; }
            if (Time.time < nextScanAt) return;
            nextScanAt = Time.time + 0.1f;
            // 도약 중 몸체가 트리거로 바뀌는 적도 루트 위치가 반경 안이면 유인합니다.
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(LayerMask.GetMask("Enemy", "FlyingBody"));
            hits.Clear();
            Physics2D.OverlapCircle(transform.position, data.tauntRadius, filter, hits);
            foreach (Collider2D hit in hits)
            {
                EnemyAIController enemy = hit.GetComponentInParent<EnemyAIController>();
                EnemyBase target = enemy != null ? enemy.GetComponent<EnemyBase>() : null;
                if (enemy == null || !enemy.isActiveAndEnabled || (target != null && target.isDead) ||
                    !CanTaunt(enemy.transform.position)) continue;
                enemy.AddDecoy(this);
                affectedEnemies.Add(enemy);
            }
        }

        public bool CanTaunt(Vector3 position) => IsAlive && isActiveAndEnabled && Time.time < expiresAt &&
            ((Vector2)(position - transform.position)).sqrMagnitude <= data.tauntRadius * data.tauntRadius;

        public void TakeDamage(float damageAmount, bool isChain = false)
        {
            if (!IsAlive || damageAmount <= 0f) return;
            CurrentDurability = Mathf.Max(0f, CurrentDurability - damageAmount);
            if (CurrentDurability <= 0f) Close();
        }

        public void Close(bool explode = true)
        {
            bool wasAlive = IsAlive;
            IsAlive = false;
            if (wasAlive && owner != null)
                owner.GetComponentInParent<OminousDollRuntime>()?.NotifyClosed(this, explode);
            if (body != null) body.enabled = false;
            foreach (EnemyAIController enemy in affectedEnemies)
                if (enemy != null) enemy.RemoveDecoy(this);
            affectedEnemies.Clear();
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private void OnDisable() => Close(false);
    }
}
