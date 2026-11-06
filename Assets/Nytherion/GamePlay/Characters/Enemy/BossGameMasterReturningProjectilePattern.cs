using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class BossGameMasterReturningProjectilePattern : MonoBehaviour
    {
        [Header("Basic Attack / 왕복 투사체 기본공격")]
        [SerializeField, Min(1)] private int projectileCount = 3;
        [SerializeField, Range(0f, 180f)] private float spreadAngle = 100f;
        [SerializeField, Min(0.1f)] private float travelDistance = 11f;
        [SerializeField, Min(0.1f)] private float outwardSpeed = 7f;
        [SerializeField, Min(0.1f)] private float returnSpeed = 10.5f;
        [SerializeField, Min(0.1f)] private float projectileScale = 1.7f;
        [Tooltip("크기 배율 1일 때의 판정 반경입니다. Projectile Scale과 함께 조절됩니다.")]
        [SerializeField, Min(0.01f)] private float hitRadius = 0.18f;
        [SerializeField, Min(0f)] private float damage = 1f;
        [SerializeField, Min(1f)] private float framesPerSecond = 20f;
        [Tooltip("일반 ATK 039 프레임의 손끝 위치입니다. 좌우 반전 시 X도 반전됩니다.")]
        [SerializeField] private Vector2 originOffset = new Vector2(2.75f, 2.35f);
        [Tooltip("강화 후 모션에서의 발사 위치입니다. 좌우 반전 시 X도 반전됩니다.")]
        [SerializeField] private Vector2 upgradedOriginOffset = new Vector2(0.9f, 2.1f);
        [SerializeField] private LayerMask wallLayers = (1 << 9) | (1 << 11);
        [Header("Visuals / 이미지")]
        [Tooltip("0~4: 출발 및 역재생 정지, 5번 이후: 이동 루프")]
        [SerializeField] private Sprite[] projectileFrames;
        [SerializeField] private Material projectileMaterial;

        private BossGameMasterEnemy owner;
        private EnemyAIController ai;
        private GameObject root;
        private readonly List<BossGameMasterReturningProjectile> projectiles = new List<BossGameMasterReturningProjectile>();

        private void Cache()
        {
            if (owner == null) owner = GetComponent<BossGameMasterEnemy>();
            if (ai == null) ai = GetComponent<EnemyAIController>();
        }

        public bool CanStart()
        {
            Cache();
            if (!isActiveAndEnabled || owner == null || owner.isDead || owner.IsUpgrading ||
                ai == null || ai.CurrentTarget == null || projectileMaterial == null ||
                projectileFrames == null || projectileFrames.Length <= 5) return false;
            foreach (Sprite frame in projectileFrames) if (frame == null) return false;
            return true;
        }

        public void Begin()
        {
            if (!CanStart()) return;
            projectiles.RemoveAll(shot => shot == null || shot.IsFinished);
            SpriteRenderer body = owner.GetComponent<SpriteRenderer>();
            BossGameMasterIdleBehavior movement = owner.GetComponent<BossGameMasterIdleBehavior>();
            if (movement != null) movement.FacePattern(ai.CurrentTarget.position - owner.transform.position);
            Vector2 offset = owner.IsUpgraded ? upgradedOriginOffset : originOffset;
            if (body != null && body.flipX) offset.x = -offset.x;
            Vector3 origin = owner.transform.TransformPoint(offset);
            Vector2 aim = (Vector2)ai.CurrentTarget.position - (Vector2)origin;
            if (aim.sqrMagnitude < 0.0001f) aim = Vector2.right;
            float centerAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            int count = Mathf.Max(1, projectileCount);
            if (root == null) root = new GameObject("GameMaster Basic Projectiles");
            for (int i = 0; i < count; i++)
            {
                float angle = centerAngle + (count == 1 ? 0f : Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, i / (float)(count - 1)));
                Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                GameObject shot = new GameObject("Returning Projectile " + (i + 1));
                shot.transform.SetParent(root.transform, false);
                shot.layer = 2;
                BossGameMasterReturningProjectile projectile = shot.AddComponent<BossGameMasterReturningProjectile>();
                projectiles.Add(projectile);
                projectile.Initialize(owner, origin, direction, travelDistance, outwardSpeed, returnSpeed,
                    projectileScale, hitRadius, damage, framesPerSecond, wallLayers, projectileFrames, projectileMaterial,
                    body != null ? body.sortingLayerID : 0, body != null ? body.sortingOrder + 8 : 28);
            }
        }

        public bool Tick()
        {
            if (root == null || owner == null || owner.isDead || owner.IsUpgrading) { Clear(); return true; }
            foreach (BossGameMasterReturningProjectile projectile in projectiles)
                if (projectile != null && !projectile.IsFinished) return false;
            Clear();
            return true;
        }

        public void Clear()
        {
            foreach (BossGameMasterReturningProjectile projectile in projectiles)
                if (projectile != null) projectile.Cancel();
            projectiles.Clear();
            if (root != null) { root.SetActive(false); Destroy(root); }
            root = null;
        }

        private void Update()
        {
            if (root == null) return;
            if (owner == null || !owner.isActiveAndEnabled || owner.isDead || owner.IsUpgrading) Clear();
            else Tick();
        }

        private void OnDisable() => Clear();
    }
}
