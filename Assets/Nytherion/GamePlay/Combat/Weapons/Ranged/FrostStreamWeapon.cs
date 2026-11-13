using System;
using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Data.ScriptableObjects.Weapons;
using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapons
{
    [Serializable]
    public sealed class FrostStreamEmissionFrame
    {
        public Sprite sprite;
        public Vector2[] points;
    }

    /// <summary>공격 버튼을 누르는 동안 냉기를 방사하고 현재 이미지 윤곽 안의 적을 주기적으로 타격합니다.</summary>
    public sealed class FrostStreamWeapon : RangedWeapon
    {
        [Header("냉기 방사")]
        [SerializeField] private Sprite[] frames;
        [SerializeField, Tooltip("발사를 시작할 때 한 번 재생한 뒤 반복 프레임으로 이어집니다.")]
        private Sprite[] startupFrames;
        [SerializeField, Min(1f)] private float framesPerSecond = 16f;
        [SerializeField] private LayerMask enemyLayers;

        [Header("방사 관성 (끄면 기존 직선 방식)")]
        [SerializeField] private bool useInertia = true;
        [SerializeField, Range(0.02f, 0.6f)] private float tipFollowDelay = 0.2f;
        [SerializeField, Range(0f, 85f)] private float maximumBendAngle = 65f;

        [Header("남는 서리 입자")]
        [SerializeField, Tooltip("작은 눈 결정을 가져올 ChillStatusSheet 첫 프레임")]
        private Sprite frostParticleSource;
        [SerializeField] private bool leaveFrostParticles = true;
        [SerializeField, Min(0f), Tooltip("방사 중 초당 떨어져 나올 서리 입자 수")]
        private float frostParticlesPerSecond = 32f;
        [SerializeField, Range(0.1f, 2f), Tooltip("발사를 멈춘 뒤에도 남아 있을 입자의 최대 수명")]
        private float frostParticleLifetime = 0.75f;
        [SerializeField, Range(0f, 89f), Tooltip("공격 방향을 기준으로 입자가 퍼질 좌우 최대 각도")]
        private float frostParticleSpreadHalfAngle = 35f;
        [SerializeField, HideInInspector] private FrostStreamEmissionFrame[] particleEmissionFrames;

        private readonly List<Vector2> shape = new List<Vector2>();
        private readonly HashSet<IDamageable> damagedTargets = new HashSet<IDamageable>();
        private Collider2D[] hits = new Collider2D[64];
        private GameObject stream;
        private SpriteRenderer streamRenderer;
        private PolygonCollider2D streamCollider;
        private SpriteRenderer weaponRenderer;
        private FrostStreamDeformation deformation;
        private FrostStreamResidue residue;
        private bool firing;
        private float startedAt;
        private float nextDamageAt;
        private Sprite currentFrame;

        // 지속 방사는 누름/해제 입력을 그대로 사용합니다.
        public override bool SupportsGenericCharging => false;

        public override bool CanAttack() => !firing && firePoint != null && weaponData != null &&
            frames != null && frames.Length == 8 && base.CanAttack();

        public override void Initialize(WeaponData data)
        {
            AttackEnd();
            base.Initialize(data);
        }

        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
            if (!CanAttack()) return;
            if (stream == null)
            {
                // 무기 본체의 축소/좌우 반전과 독립된 월드 크기로 관리합니다.
                stream = new GameObject("FrostStream");
                streamRenderer = stream.AddComponent<SpriteRenderer>();
                weaponRenderer = GetComponent<SpriteRenderer>();
                streamRenderer.sharedMaterial = weaponRenderer.sharedMaterial;
                streamCollider = stream.AddComponent<PolygonCollider2D>();
                streamCollider.isTrigger = true;
                // 물리 충돌 이벤트 없이 OverlapCollider 조회에만 사용합니다.
                streamCollider.enabled = false;
                deformation = stream.AddComponent<FrostStreamDeformation>();
            }
            stream.SetActive(true);
            firing = true;
            startedAt = Time.time;
            nextDamageAt = Time.time;
            currentFrame = null;
            if (residue != null) residue.ResetEmission();
            deformation.ResetDirection(firePoint.TransformVector(Vector3.right));
            lastAttackTime = Time.time;
        }

        private void LateUpdate()
        {
            if (!firing) return;
            if (weaponData == null || firePoint == null) { AttackEnd(); return; }
            int elapsedFrames = Mathf.FloorToInt((Time.time - startedAt) * Mathf.Max(1f, framesPerSecond));
            int startupCount = startupFrames != null ? startupFrames.Length : 0;
            Sprite frame = elapsedFrames < startupCount
                ? startupFrames[elapsedFrames]
                : frames[(elapsedFrames - startupCount) % frames.Length];
            if (frame == null) { AttackEnd(); return; }
            bool damageDue = Time.time >= nextDamageAt;
            bool drawInertia = useInertia && deformation.SetVisible(true);
            if (!drawInertia) deformation.SetVisible(false);
            if (frame != currentFrame || !drawInertia)
            {
                currentFrame = frame;
                streamRenderer.sprite = frame;
            }
            // 물리 윤곽은 실제 피해 틱에만 필요하며, 입자는 별도의 픽셀 위치를 사용합니다.
            if (damageDue && !drawInertia)
            {
                streamCollider.pathCount = frame.GetPhysicsShapeCount();
                for (int i = 0; i < streamCollider.pathCount; i++)
                {
                    frame.GetPhysicsShape(i, shape);
                    streamCollider.SetPath(i, shape);
                }
            }
            Vector2 aim = firePoint.TransformVector(Vector3.right);
            Vector2 muzzleUp = firePoint.TransformVector(Vector3.up);
            // 왼쪽에서 맞춘 이미지 방향을 기준으로 오른쪽에서도 같은 높이 관계를 유지합니다.
            float widthSign = aim.x * muzzleUp.y - aim.y * muzzleUp.x < 0f ? 1f : -1f;
            // 이미지의 왼쪽 끝은 총구에, 오른쪽 끝은 조준 방향에 맞춥니다.
            stream.transform.SetPositionAndRotation(firePoint.position,
                Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg));
            float streamScale = Mathf.Max(0.01f, weaponData.range) * EffectSizeMultiplier / frame.bounds.size.x;
            stream.transform.localScale = new Vector3(streamScale, streamScale * widthSign, 1f);
            streamRenderer.sortingLayerID = weaponRenderer.sortingLayerID;
            // 무기 순서 대신 공용 광선 기준을 사용해 바닥 위, 적 본체 아래에 표시합니다.
            streamRenderer.sortingOrder = WeaponVFXHelper.BackgroundBeamSortingOrder;
            streamRenderer.enabled = !drawInertia;
            if (drawInertia)
            {
                // 메시에 사용한 변형을 피해 윤곽에도 동일하게 적용합니다.
                stream.transform.SetPositionAndRotation(firePoint.position, Quaternion.identity);
                stream.transform.localScale = Vector3.one;
                deformation.Draw(frame, aim, Mathf.Max(0.01f, weaponData.range) * EffectSizeMultiplier,
                    tipFollowDelay, maximumBendAngle, Time.deltaTime, streamCollider,
                    weaponRenderer.sortingLayerID, WeaponVFXHelper.BackgroundBeamSortingOrder, widthSign, damageDue);
            }
            if (leaveFrostParticles && frostParticlesPerSecond > 0f && frostParticleSource != null)
            {
                if (residue == null)
                {
                    // 방사 오브젝트와 분리해 버튼을 놓아도 이미 나온 입자는 수명을 유지합니다.
                    GameObject particles = new GameObject("FrostStream 서리 입자");
                    residue = particles.AddComponent<FrostStreamResidue>();
                    residue.Initialize(frostParticleSource);
                }
                Vector2[] points = GetEmissionPoints(frame);
                residue.EmitFrom(stream.transform, frame, points, drawInertia ? deformation : null,
                    Mathf.Max(0.01f, weaponData.range) * EffectSizeMultiplier, aim, frostParticleSpreadHalfAngle,
                    frostParticlesPerSecond, frostParticleLifetime, EffectSizeMultiplier, Time.deltaTime,
                    weaponRenderer.sortingLayerID, WeaponVFXHelper.BackgroundBeamSortingOrder - 1);
            }
            if (!damageDue) return;
            nextDamageAt = Time.time + Mathf.Max(0.02f, weaponData.cooldown);
            DealStreamDamage();
        }

        private Vector2[] GetEmissionPoints(Sprite frame)
        {
            if (particleEmissionFrames == null) return null;
            foreach (FrostStreamEmissionFrame entry in particleEmissionFrames)
                if (entry != null && entry.sprite == frame) return entry.points;
            return null;
        }

        private void DealStreamDamage()
        {
            streamCollider.enabled = true;
            Physics2D.SyncTransforms();
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(enemyLayers);
            int count;
            do
            {
                count = streamCollider.OverlapCollider(filter, hits);
                if (count < hits.Length) break;
                Array.Resize(ref hits, hits.Length * 2);
            } while (true);
            streamCollider.enabled = false;
            damagedTargets.Clear();
            float damage = Mathf.Max(0f, weaponData.damage * EffectiveDamageMultiplier);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;
                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target == null || !damagedTargets.Add(target)) continue;
                if (target is MonoBehaviour behaviour && !behaviour.gameObject.activeInHierarchy) continue;
                Vector3 hitPosition = hit.bounds.center;
                DealAttackDamage(target, damage);
                ApplyStatusEffects(target);
                WeaponVFXHelper.PlayHitEffect(weaponData.hitEffectPrefab, hitPosition);
            }
            Array.Clear(hits, 0, count);
        }

        public override void AttackEnd()
        {
            firing = false;
            if (stream != null) stream.SetActive(false);
            damagedTargets.Clear();
        }

        private void OnDisable() => AttackEnd();

        private void OnDestroy()
        {
            if (residue != null) residue.Release();
            if (stream != null) Destroy(stream);
        }
    }
}
