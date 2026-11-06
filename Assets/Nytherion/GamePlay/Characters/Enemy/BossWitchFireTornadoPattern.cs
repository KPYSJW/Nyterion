using System.Collections.Generic;
using Nytherion.Core.Systems;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Dungeon;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyAIController), typeof(BossWitchMovementBehavior))]
    public sealed class BossWitchFireTornadoPattern : MonoBehaviour, IBossWitchPattern
    {
        [Header("Random Count / Timing")]
        [SerializeField, Min(1)] private int minimumCount = 8;
        [SerializeField, Min(1)] private int maximumCount = 12;
        [SerializeField, Min(0.05f)] private float minimumSpawnInterval = 0.15f;
        [SerializeField, Min(0.05f)] private float maximumSpawnInterval = 0.45f;
        [SerializeField, Min(0.05f)] private float warningDuration = 0.8f;
        [SerializeField, Min(0.05f)] private float activeDuration = 5f;
        [SerializeField, Min(0.05f)] private float damageInterval = 0.5f;

        [Header("Placement / Damage")]
        [SerializeField] private BossWitchTornadoHazard hazardPrefab;
        [SerializeField, Min(0.1f)] private float tornadoRadius = 0.75f;
        [SerializeField, Min(0f)] private float minimumSpacing = 3f;
        [SerializeField, Min(0f)] private float wallClearance = 0.15f;
        [SerializeField, Min(0f)] private float damageMultiplier = 1f;
        [SerializeField] private LayerMask wallLayers = (1 << 9) | (1 << 11);
        [SerializeField] private bool reverseCastingFacing;
        [SerializeField] private DungeonManager dungeonManager;

        private enum Stage { Idle, Preparation, Finished }
        private Stage stage;
        private EnemyBase enemyBody;
        private BossWitchMovementBehavior movement;
        private StatusEffectManager statusEffects;
        private readonly List<Vector2> candidates = new List<Vector2>();
        private readonly List<Vector2> positions = new List<Vector2>();
        private readonly List<BossWitchTornadoHazard> hazards = new List<BossWitchTornadoHazard>();
        private readonly Collider2D[] blockingHits = new Collider2D[64];
        private static readonly int PrepareState = Animator.StringToHash("Base Layer.TornadoUpCast");
        public BossWitchPatternType PatternType => BossWitchPatternType.FireTornado;

        private void CacheComponents()
        {
            if (enemyBody == null) enemyBody = GetComponent<EnemyBase>();
            if (movement == null) movement = GetComponent<BossWitchMovementBehavior>();
            if (statusEffects == null) statusEffects = GetComponent<StatusEffectManager>();
            if (dungeonManager == null) dungeonManager = FindObjectOfType<DungeonManager>();
        }

        private RoomFirstDungeonGenerator.Room OwnerRoom()
        {
            return enemyBody.homeRoom ?? dungeonManager.FindRoomAt(Vector2Int.FloorToInt(transform.position));
        }

        public bool CanStart(EnemyAIController enemy)
        {
            CacheComponents();
            return isActiveAndEnabled && stage == Stage.Idle && hazardPrefab != null && dungeonManager != null &&
                enemyBody != null && !enemyBody.isDead && OwnerRoom() != null &&
                enemy != null && enemy.CurrentTarget != null && enemy.animator != null &&
                enemy.animator.isActiveAndEnabled && enemy.animator.layerCount > 0 &&
                enemy.animator.HasState(0, PrepareState);
        }

        public void Begin(EnemyAIController enemy)
        {
            CacheComponents();
            hazards.RemoveAll(hazard => hazard == null);
            SelectPositions(OwnerRoom());
            stage = Stage.Preparation;
            movement.SetCastingFacingReversed(reverseCastingFacing);
            FaceTarget(enemy);
            enemy.PlayAnimation("TornadoUpCast");
        }

        public bool UpdatePattern(EnemyAIController enemy)
        {
            if (stage == Stage.Idle || stage == Stage.Finished) return true;
            if (stage == Stage.Preparation)
            {
                FaceTarget(enemy);
                if (!AnimationFinished(enemy, PrepareState)) return false;
                float low = Mathf.Max(0.05f, Mathf.Min(minimumSpawnInterval, maximumSpawnInterval));
                float high = Mathf.Max(low, Mathf.Max(minimumSpawnInterval, maximumSpawnInterval));
                float warningDelay = 0f;
                // Each hazard owns its delayed start, so movement and later attacks cannot overwrite this batch.
                foreach (Vector2 position in positions)
                {
                    SpawnHazard(enemy, position, warningDelay);
                    warningDelay += Random.Range(low, high);
                }
                stage = Stage.Finished;
                return true;
            }
            return false;
        }

        private void SpawnHazard(EnemyAIController enemy, Vector2 position, float warningDelay)
        {
            BossWitchTornadoHazard hazard = Instantiate(hazardPrefab,
                new Vector3(position.x, position.y, transform.position.z), Quaternion.identity);
            float damage = (enemy.enemyData != null ? enemy.enemyData.damageAmount : 2f) * damageMultiplier;
            if (statusEffects != null) damage *= statusEffects.GetOutgoingDamageMultiplier();
            hazard.Initialize(enemyBody, tornadoRadius, warningDuration, activeDuration, damageInterval, damage, warningDelay);
            hazards.Add(hazard);
        }

        private void SelectPositions(RoomFirstDungeonGenerator.Room room)
        {
            candidates.Clear();
            positions.Clear();
            BoundsInt bounds = room.Bounds;
            GameObject roomPrefab = dungeonManager.CurrentDungeonData != null ? dungeonManager.CurrentDungeonData.bossRoomPrefab : null;
            Tilemap prefabFloor = roomPrefab != null && room.type == RoomFirstDungeonGenerator.RoomType.Boss ?
                roomPrefab.GetComponentInChildren<Tilemap>() : null;
            if (prefabFloor != null)
            {
                bounds = prefabFloor.cellBounds;
                bounds.position += Vector3Int.RoundToInt(room.center);
            }
            for (int x = bounds.xMin; x < bounds.xMax; x++)
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                    if (dungeonManager.FindRoomAt(new Vector2Int(x, y)) == room)
                        candidates.Add(new Vector2(x + 0.5f, y + 0.5f));

            int low = Mathf.Max(1, Mathf.Min(minimumCount, maximumCount));
            int high = Mathf.Max(low, Mathf.Max(minimumCount, maximumCount));
            int requested = Random.Range(low, high + 1);
            for (int i = 0; i < candidates.Count && positions.Count < requested; i++)
            {
                int chosen = Random.Range(i, candidates.Count);
                Vector2 candidate = candidates[chosen];
                candidates[chosen] = candidates[i];
                candidates[i] = candidate;
                Vector2 position = candidate + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(-0.35f, 0.35f));
                if (IsValidPosition(position, room)) positions.Add(position);
            }
            if (positions.Count < requested)
                Debug.LogWarning($"[WitchBoss] 소용돌이 {requested}개 중 {positions.Count}개만 안전하게 배치했습니다. " +
                    "방 크기, Tornado Radius, Minimum Spacing을 확인하세요.", this);
        }

        private bool IsValidPosition(Vector2 position, RoomFirstDungeonGenerator.Room room)
        {
            float radius = Mathf.Max(0.1f, tornadoRadius) + wallClearance;
            for (int x = Mathf.FloorToInt(position.x - radius); x <= Mathf.FloorToInt(position.x + radius); x++)
                for (int y = Mathf.FloorToInt(position.y - radius); y <= Mathf.FloorToInt(position.y + radius); y++)
                {
                    Vector2 nearest = new Vector2(Mathf.Clamp(position.x, x, x + 1), Mathf.Clamp(position.y, y, y + 1));
                    if ((nearest - position).sqrMagnitude < radius * radius &&
                        dungeonManager.FindRoomAt(new Vector2Int(x, y)) != room) return false;
                }
            float spacing = Mathf.Max(minimumSpacing, tornadoRadius * 2f);
            foreach (Vector2 other in positions)
                if ((position - other).sqrMagnitude < spacing * spacing) return false;
            foreach (BossWitchTornadoHazard hazard in hazards)
                if (hazard != null && Vector2.Distance(position, hazard.transform.position) <
                    Mathf.Max(minimumSpacing, tornadoRadius + hazard.Radius)) return false;
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(Physics2D.AllLayers);
            filter.useTriggers = true;
            int count = Physics2D.OverlapCircle(position, radius, filter, blockingHits);
            for (int i = 0; i < count; i++)
                if (blockingHits[i] != null && (blockingHits[i].CompareTag(Tags.Wall) ||
                    (wallLayers.value & (1 << blockingHits[i].gameObject.layer)) != 0)) return false;
            return true;
        }

        private void FaceTarget(EnemyAIController enemy)
        {
            if (enemy.CurrentTarget == null) return;
            float horizontal = enemy.CurrentTarget.position.x - transform.position.x;
            if (Mathf.Abs(horizontal) <= 0.01f) return;
            Vector2 facing = horizontal > 0f ? Vector2.right : Vector2.left;
            enemy.SetForcedFacingDirection(facing);
            movement.UpdateFacing(facing);
        }

        private static bool AnimationFinished(EnemyAIController enemy, int hash)
        {
            if (enemy.animator == null || !enemy.animator.isActiveAndEnabled || enemy.animator.layerCount == 0) return false;
            AnimatorStateInfo state = enemy.animator.GetCurrentAnimatorStateInfo(0);
            return state.fullPathHash == hash && state.normalizedTime >= 1f && !enemy.animator.IsInTransition(0);
        }

        public void End(EnemyAIController enemy, bool completed)
        {
            if (!completed) ClearHazards();
            movement.SetCastingFacingReversed(false);
            enemy.ClearForcedFacingDirection();
            stage = Stage.Idle;
        }

        private void ClearHazards()
        {
            foreach (BossWitchTornadoHazard hazard in hazards)
                if (hazard != null) hazard.Cancel();
            hazards.Clear();
        }

        public void ResetForReuse()
        {
            CacheComponents();
            ClearHazards();
            candidates.Clear();
            positions.Clear();
            stage = Stage.Idle;
        }

        private void OnDisable()
        {
            ClearHazards();
            stage = Stage.Idle;
        }
    }
}
