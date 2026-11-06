using System.Collections.Generic;
using Nytherion.Core.Systems;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Dungeon;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class BossDemonSlimeSmashPattern : MonoBehaviour, IBossDemonSlimePattern
    {
        [Header("Meteor Shower / 메테오 낙하")]
        [SerializeField, Min(1)] private int meteorCount = 5;
        [SerializeField] private BossDemonSlimeMeteorHazard meteorPrefab;
        [Tooltip("착지 이펙트와 공격 범위를 함께 키웁니다.")]
        [SerializeField, Min(0.1f)] private float impactScale = 1.5f;
        [SerializeField, Min(0f)] private float minimumMeteorInterval = 0.15f;
        [SerializeField, Min(0f)] private float maximumMeteorInterval = 0.4f;
        [SerializeField, Min(0f)] private float warningDuration = 0.5f;
        [SerializeField, Min(0.05f)] private float fallDuration = 1.2f;
        [SerializeField, Min(0.1f)] private float fallHeight = 12f;
        [SerializeField, Min(0.1f)] private float meteorScale = 1.5f;
        [SerializeField, Min(1f)] private float fallFramesPerSecond = 20f;
        [SerializeField, Min(1f)] private float impactFramesPerSecond = 16f;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [Header("Room Placement / 방 전체 무작위 배치")]
        [SerializeField, Min(0f)] private float minimumSpacing = 5f;
        [SerializeField, Min(0f)] private float wallClearance = 0.15f;
        [SerializeField] private LayerMask wallLayers = (1 << 9) | (1 << 11);
        [SerializeField] private DungeonManager dungeonManager;

        private static readonly int SmashState = Animator.StringToHash("Base Layer.Smash");
        private BossDemonSlimeEnemy body;
        private BossDemonSlimeMovementBehavior movement;
        private StatusEffectManager statusEffects;
        private bool casting;
        private readonly List<Vector2> candidates = new List<Vector2>();
        private readonly List<Vector2> positions = new List<Vector2>();
        private readonly List<Collider2D> blockingHits = new List<Collider2D>(32);
        private readonly List<BossDemonSlimeMeteorHazard> meteors = new List<BossDemonSlimeMeteorHazard>();

        public BossDemonSlimePatternType PatternType => BossDemonSlimePatternType.MeteorShower;

        private void CacheComponents()
        {
            if (body == null) body = GetComponent<BossDemonSlimeEnemy>();
            if (movement == null) movement = GetComponent<BossDemonSlimeMovementBehavior>();
            if (statusEffects == null) statusEffects = GetComponent<StatusEffectManager>();
            if (dungeonManager == null) dungeonManager = FindObjectOfType<DungeonManager>();
        }

        private RoomFirstDungeonGenerator.Room OwnerRoom() => body.homeRoom ??
            dungeonManager.FindRoomAt(Vector2Int.FloorToInt(transform.position));

        public bool CanStart(EnemyAIController enemy)
        {
            CacheComponents();
            return isActiveAndEnabled && !casting && body != null && !body.isDead &&
                body.CurrentForm == BossDemonSlimeEnemy.Form.Demon && movement != null &&
                meteorPrefab != null && meteorPrefab.HasSprites && dungeonManager != null && OwnerRoom() != null &&
                enemy != null && enemy.CurrentTarget != null && enemy.animator != null &&
                enemy.animator.isActiveAndEnabled && enemy.animator.layerCount > 0 && enemy.animator.HasState(0, SmashState);
        }

        public void Begin(EnemyAIController enemy, BossDemonSlimeCombatBehavior combat)
        {
            CacheComponents();
            meteors.RemoveAll(meteor => meteor == null);
            SelectPositions(OwnerRoom());
            casting = true;
            Vector2 aim = (Vector2)enemy.CurrentTarget.position - (Vector2)transform.position;
            if (aim.sqrMagnitude < 0.0001f) aim = movement.FacingRight ? Vector2.right : Vector2.left;
            movement.FaceAttackDirection(aim);
            enemy.SetForcedFacingDirection(aim.normalized);
            enemy.PlayAnimation("Smash");
        }

        public bool UpdatePattern(EnemyAIController enemy)
        {
            if (!casting || body == null || body.isDead || !body.isActiveAndEnabled) return true;
            AnimatorStateInfo state = enemy.animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != SmashState || state.normalizedTime < 1f || enemy.animator.IsInTransition(0)) return false;
            float damage = (enemy.enemyData != null ? enemy.enemyData.damageAmount : 2f) * Mathf.Max(0f, damageMultiplier);
            if (statusEffects != null) damage *= statusEffects.GetOutgoingDamageMultiplier();
            float low = Mathf.Max(0f, Mathf.Min(minimumMeteorInterval, maximumMeteorInterval));
            float high = Mathf.Max(low, Mathf.Max(minimumMeteorInterval, maximumMeteorInterval));
            float delay = 0f;
            for (int i = 0; i < positions.Count; i++)
            {
                Vector2 point = positions[i];
                BossDemonSlimeMeteorHazard meteor = Instantiate(meteorPrefab,
                    new Vector3(point.x, point.y, transform.position.z), Quaternion.identity);
                meteor.Initialize(body, impactScale, meteorScale, warningDuration, fallDuration, fallHeight,
                    fallFramesPerSecond, impactFramesPerSecond, damage, delay, wallLayers);
                meteors.Add(meteor);
                if (i + 1 < positions.Count) delay += Random.Range(low, high);
            }
            casting = false;
            enemy.PlayAnimation("Idle");
            return true;
        }

        private void SelectPositions(RoomFirstDungeonGenerator.Room room)
        {
            candidates.Clear();
            positions.Clear();
            BoundsInt bounds = room.Bounds;
            GameObject roomPrefab = dungeonManager.CurrentDungeonData != null ? dungeonManager.CurrentDungeonData.bossRoomPrefab : null;
            Tilemap floor = roomPrefab != null && room.type == RoomFirstDungeonGenerator.RoomType.Boss ?
                roomPrefab.GetComponentInChildren<Tilemap>() : null;
            if (floor != null)
            {
                bounds = floor.cellBounds;
                bounds.position += Vector3Int.RoundToInt(room.center);
            }
            for (int x = bounds.xMin; x < bounds.xMax; x++)
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                    if (dungeonManager.FindRoomAt(new Vector2Int(x, y)) == room)
                        candidates.Add(new Vector2(x + 0.5f, y + 0.5f));

            float radius = meteorPrefab.GetImpactRadius(impactScale);
            int requested = Mathf.Max(1, meteorCount);
            // Shuffle the whole room's floor cells; placement never depends on the player's position.
            for (int i = 0; i < candidates.Count && positions.Count < requested; i++)
            {
                int chosen = Random.Range(i, candidates.Count);
                Vector2 candidate = candidates[chosen];
                candidates[chosen] = candidates[i];
                candidates[i] = candidate;
                Vector2 point = candidate + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(-0.35f, 0.35f));
                if (IsValidPosition(point, room, radius)) positions.Add(point);
            }
            if (positions.Count < requested)
                Debug.LogWarning($"[DemonSlime] 메테오 {requested}개 중 {positions.Count}개만 배치했습니다. " +
                    "방 크기, Impact Scale, Minimum Spacing을 확인하세요.", this);
        }

        private bool IsValidPosition(Vector2 point, RoomFirstDungeonGenerator.Room room, float hitRadius)
        {
            float radius = hitRadius + Mathf.Max(0f, wallClearance);
            for (int x = Mathf.FloorToInt(point.x - radius); x <= Mathf.FloorToInt(point.x + radius); x++)
                for (int y = Mathf.FloorToInt(point.y - radius); y <= Mathf.FloorToInt(point.y + radius); y++)
                {
                    Vector2 nearest = new Vector2(Mathf.Clamp(point.x, x, x + 1), Mathf.Clamp(point.y, y, y + 1));
                    if ((nearest - point).sqrMagnitude < radius * radius &&
                        dungeonManager.FindRoomAt(new Vector2Int(x, y)) != room) return false;
                }
            float spacing = Mathf.Max(minimumSpacing, hitRadius);
            foreach (Vector2 other in positions)
                if ((point - other).sqrMagnitude < spacing * spacing) return false;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = true;
            Physics2D.OverlapCircle(point, radius, filter, blockingHits);
            foreach (Collider2D hit in blockingHits)
                if (hit != null && (hit.CompareTag(Tags.Wall) || (wallLayers.value & (1 << hit.gameObject.layer)) != 0)) return false;
            return true;
        }

        public void OnAnimationHit(EnemyAIController enemy) { }
        public void End(EnemyAIController enemy, bool completed)
        {
            casting = false;
            if (!completed) ClearMeteors();
        }
        private void ClearMeteors()
        {
            foreach (BossDemonSlimeMeteorHazard meteor in meteors)
                if (meteor != null) meteor.Cancel();
            meteors.Clear();
        }
        public void ResetForReuse()
        {
            casting = false;
            ClearMeteors();
            candidates.Clear();
            positions.Clear();
        }
        private void OnDisable() => ResetForReuse();
    }
}
