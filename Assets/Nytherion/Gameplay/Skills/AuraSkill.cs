using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Skill;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    public class AuraSkill : SkillBase
    {
        private AuraSkillData auraData;
        private GameObject auraObject;
        private CircleCollider2D auraCollider;
        private Coroutine activeAuraCoroutine;
        
        private HashSet<IDamageable> enemiesInRange = new HashSet<IDamageable>();
        private float nextTickTime;

        protected override void Activate()
        {
            if (skillData != null)
            {
                auraData = skillData as AuraSkillData;
                if (auraData == null)
                {
                    Debug.LogError("[AuraSkill] SkillData가 AuraSkillData 타입이 아닙니다.");
                    return;
                }

                if (activeAuraCoroutine != null)
                {
                    StopCoroutine(activeAuraCoroutine);
                }
                activeAuraCoroutine = StartCoroutine(AuraRoutine());
            }
        }

        private IEnumerator AuraRoutine()
        {
            if (auraObject == null)
            {
                CreateAuraObject();
            }

            // 스킬 레벨에 따른 데미지 적용 (레벨은 1부터 시작하므로 -1)
            float finalDamage = auraData.damagePerTick + (auraData.damagePerLevel * Mathf.Max(0, auraData.skillLevel - 1));
            
            auraCollider.radius = auraData.auraRadius;
            auraObject.SetActive(true);
            enemiesInRange.Clear();
            nextTickTime = Time.time + auraData.tickRate;

            Debug.Log($"[AuraSkill] 오라 스킬 활성화! (지속시간: {auraData.auraDuration}초, 반경: {auraData.auraRadius}, 데미지: {finalDamage}, 투사체파괴: {auraData.destroyEnemyProjectiles})");

            float timer = auraData.auraDuration;
            while (timer > 0)
            {
                if (Time.time >= nextTickTime && auraData.dealDamage)
                {
                    DealTickDamage(finalDamage);
                    nextTickTime = Time.time + auraData.tickRate;
                }
                
                timer -= Time.deltaTime;
                yield return null;
            }

            auraObject.SetActive(false);
            Debug.Log("[AuraSkill] 오라 스킬 종료!");
        }

        private void DealTickDamage(float damage)
        {
            List<IDamageable> toRemove = new List<IDamageable>();
            
            foreach (var enemy in enemiesInRange)
            {
                if (enemy != null && enemy is MonoBehaviour mb && mb.gameObject.activeInHierarchy)
                {
                    enemy.TakeDamage(damage);
                }
                else
                {
                    toRemove.Add(enemy);
                }
            }

            foreach (var r in toRemove)
            {
                enemiesInRange.Remove(r);
            }
        }

        private void CreateAuraObject()
        {
            auraObject = new GameObject("AuraArea");
            auraObject.transform.SetParent(caster != null ? caster : transform);
            auraObject.transform.localPosition = Vector3.zero;

            auraCollider = auraObject.AddComponent<CircleCollider2D>();
            auraCollider.isTrigger = true;

            Rigidbody2D rb = auraObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            AuraTriggerHandler triggerHandler = auraObject.AddComponent<AuraTriggerHandler>();
            triggerHandler.Initialize(this, auraData.destroyEnemyProjectiles);

            auraObject.SetActive(false);
        }

        public void AddEnemy(IDamageable enemy)
        {
            enemiesInRange.Add(enemy);
        }

        public void RemoveEnemy(IDamageable enemy)
        {
            enemiesInRange.Remove(enemy);
        }
    }

    public class AuraTriggerHandler : MonoBehaviour
    {
        private AuraSkill parentSkill;
        private bool destroyProjectiles;

        public void Initialize(AuraSkill skill, bool destroy)
        {
            parentSkill = skill;
            destroyProjectiles = destroy;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            // 1. 적 감지
            if (collision.CompareTag("Enemy"))
            {
                IDamageable damageable = collision.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    parentSkill.AddEnemy(damageable);
                }
            }
            
            // 2. 적 투사체 파괴 감지
            if (destroyProjectiles)
            {
                // 게임 내의 EnemyProjectiles 스크립트를 가지고 있는지 확인하여 파괴
                if (collision.GetComponent<EnemyProjectiles>() != null)
                {
                    Destroy(collision.gameObject);
                }
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.CompareTag("Enemy"))
            {
                IDamageable damageable = collision.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    parentSkill.RemoveEnemy(damageable);
                }
            }
        }
    }
}
