using System.Collections;
using System.Collections.Generic;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Dungeon;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class BossGameMasterEchoes : MonoBehaviour
    {
        private enum Echo { Knight, Witch, Demon }
        private enum Selection { Random, Knight, Witch, Demon }

        [SerializeField] private Selection selection;
        [SerializeField] private AnimationClip knightReady, knightRun, knightStop, witchCast, demonSmash;
        [SerializeField] private Sprite knightFirstFrame, witchFirstFrame, demonFirstFrame;
        [SerializeField] private BossWitchIcePillarPattern witchPattern;
        [SerializeField] private BossDemonSlimeSmashPattern demonPattern;
        [SerializeField] private Material warningMaterial;
        [SerializeField] private Color silhouetteColor = Color.black;
        [SerializeField, Min(0.05f)] private float revealDuration = 0.55f;
        [SerializeField, Min(0.05f)] private float vanishDuration = 0.45f;
        [SerializeField, Min(1f)] private float summonDistance = 4f;
        [SerializeField] private float knightDashCenterOffsetY = -2.2f;
        [SerializeField, Min(0f)] private float knightDamage = 2f, witchDamage = 2f, demonDamage = 2f;
        [SerializeField] private LayerMask walls = (1 << 9) | (1 << 11);
        private readonly List<BossWitchIcePillarHazard> pillars = new();
        private readonly List<BossDemonSlimeMeteorHazard> meteors = new();
        private readonly RaycastHit2D[] dashHits = new RaycastHit2D[32];
        private BossGameMasterEnemy owner;
        private EnemyAIController ai;
        private DungeonManager dungeon;
        private Coroutine sequence;
        private GameObject echo;
        private SpriteRenderer visual;
        private PlayableGraph animationGraph;
        private AnimationPlayableOutput animationOutput;
        private AnimationClipPlayable animationPlayable;
        private AnimationClip currentClip;
        private Echo? lastEcho;

        public void Begin()
        {
            if (!isActiveAndEnabled || sequence != null) return;
            if (owner == null) owner = GetComponent<BossGameMasterEnemy>();
            if (ai == null) ai = GetComponent<EnemyAIController>();
            if (dungeon == null) dungeon = FindObjectOfType<DungeonManager>();
            if (owner == null || !owner.IsUpgraded || owner.IsUpgrading || owner.isDead) return;
            if (ai == null || ai.CurrentTarget == null || dungeon == null)
            {
                Debug.LogWarning("[GameMaster Echo] No target or dungeon for summoning.", this);
                return;
            }
            var room = owner.homeRoom ?? dungeon.FindRoomAt(Vector2Int.FloorToInt(transform.position)) ??
                dungeon.FindRoomAt(Vector2Int.FloorToInt(ai.CurrentTarget.position));
            if (room == null || !TrySummonPosition(room, out Vector3 position))
            {
                Debug.LogWarning("[GameMaster Echo] No safe summon point in the current room.", this);
                return;
            }
            Echo kind = SelectEcho();
            AnimationClip clip = kind == Echo.Knight ? knightReady : kind == Echo.Witch ? witchCast : demonSmash;
            Sprite firstFrame = kind == Echo.Knight ? knightFirstFrame : kind == Echo.Witch ? witchFirstFrame : demonFirstFrame;
            if (firstFrame == null || clip == null || (kind == Echo.Knight && (knightRun == null || knightStop == null || warningMaterial == null)) ||
                (kind == Echo.Witch && (witchPattern == null || !witchPattern.HasEchoEffect)) ||
                (kind == Echo.Demon && (demonPattern == null || !demonPattern.HasEchoEffect)))
            {
                Debug.LogWarning($"[GameMaster Echo] Missing first frame, animation or attack reference for {kind}.", this);
                return;
            }
            pillars.RemoveAll(pillar => pillar == null);
            meteors.RemoveAll(meteor => meteor == null);
            echo = new GameObject("GameMaster Echo - " + kind);
            echo.transform.position = position;
            visual = echo.AddComponent<SpriteRenderer>();
            visual.sprite = firstFrame;
            visual.color = silhouetteColor;
            SpriteRenderer body = GetComponent<SpriteRenderer>();
            if (body != null)
            {
                visual.sortingLayerID = body.sortingLayerID;
                visual.sortingOrder = body.sortingOrder + 1;
            }
            Face(Aim(position), kind == Echo.Demon);
            sequence = StartCoroutine(Run(kind, clip, room));
            lastEcho = kind;
        }

        private Echo SelectEcho()
        {
            if (selection != Selection.Random) return (Echo)((int)selection - 1);
            if (!lastEcho.HasValue) return (Echo)Random.Range(0, 3);

            int choice = Random.Range(0, 2);
            if (choice >= (int)lastEcho.Value) choice++;
            return (Echo)choice;
        }

        private bool TrySummonPosition(RoomFirstDungeonGenerator.Room room, out Vector3 position)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < 48; i++)
            {
                float radius = summonDistance * (1f - (i / 12) * 0.2f);
                float step = angle + i * Mathf.PI / 6f;
                position = transform.position + new Vector3(Mathf.Cos(step), Mathf.Sin(step)) * radius;
                if (Vector2.Distance(position, ai.CurrentTarget.position) < 1.5f ||
                    Physics2D.OverlapCircle((Vector2)position + Vector2.up, 1f, walls) != null) continue;
                if (InRoom(position, room)) return true;
            }
            position = default;
            return false;
        }

        private bool InRoom(Vector2 point, RoomFirstDungeonGenerator.Room room) =>
            dungeon.FindRoomAt(Vector2Int.FloorToInt(point)) == room;

        private Vector2 Aim(Vector2 origin)
        {
            Vector2 delta = ai.CurrentTarget != null ? (Vector2)ai.CurrentTarget.position - origin : Vector2.right;
            return delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.right;
        }

        private void Face(Vector2 direction, bool facesLeft) => visual.flipX = (direction.x < 0f) != facesLeft;

        private IEnumerator Run(Echo kind, AnimationClip clip, RoomFirstDungeonGenerator.Room room)
        {
            EnemyDeathDissolve dissolve = echo.AddComponent<EnemyDeathDissolve>();
            bool transitionComplete = false;
            dissolve.Reveal(visual, revealDuration, 3f, () => transitionComplete = true);
            yield return new WaitUntil(() => transitionComplete);
            Animator animator = echo.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.fireEvents = false;
            animationGraph = PlayableGraph.Create("GameMaster Echo");
            animationGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            animationOutput = AnimationPlayableOutput.Create(animationGraph, "Echo Sprite", animator);
            animationGraph.Play();
            if (kind == Echo.Knight)
            {
                yield return KnightAttack(room);
            }
            else
            {
                Vector2 aim = Aim(echo.transform.position);
                Face(aim, kind == Echo.Demon);
                if (kind == Echo.Witch)
                    witchPattern.SpawnPillars(owner, echo.transform.position, aim, room, dungeon, witchDamage, pillars,
                        sortingOrderOffset: -2);
                yield return PlayClip(clip);
                if (kind == Echo.Demon)
                    demonPattern.SpawnEchoMeteors(owner, room, dungeon, demonDamage, meteors);
            }
            transitionComplete = false;
            dissolve.Play(visual, vanishDuration, 3f, () => transitionComplete = true);
            yield return new WaitUntil(() => transitionComplete);
            StopAnimation();
            Destroy(echo);
            echo = null;
            sequence = null;
        }

        private IEnumerator PlayClip(AnimationClip clip)
        {
            for (float elapsed = 0f; elapsed < clip.length; elapsed += Time.deltaTime)
            {
                SampleClip(clip, elapsed);
                yield return null;
            }
            SampleClip(clip, Mathf.Max(0f, clip.length - 0.001f));
        }

        private void SampleClip(AnimationClip clip, float time)
        {
            if (currentClip != clip)
            {
                AnimationClipPlayable previous = animationPlayable;
                animationPlayable = AnimationClipPlayable.Create(animationGraph, clip);
                animationPlayable.SetSpeed(0);
                animationOutput.SetSourcePlayable(animationPlayable);
                if (previous.IsValid()) animationGraph.DestroyPlayable(previous);
                currentClip = clip;
            }
            animationPlayable.SetTime(time);
            animationGraph.Evaluate(0f);
        }

        private void StopAnimation()
        {
            if (animationGraph.IsValid()) animationGraph.Destroy();
            currentClip = null;
            animationPlayable = default;
        }

        private IEnumerator KnightAttack(RoomFirstDungeonGenerator.Room room)
        {
            const float radius = 0.6f;
            const float speed = 18f;
            LineRenderer warning = new GameObject("Echo Dash Warning").AddComponent<LineRenderer>();
            warning.transform.SetParent(echo.transform, false);
            warning.sharedMaterial = warningMaterial;
            warning.useWorldSpace = true;
            warning.positionCount = 2;
            warning.widthMultiplier = radius * 2f;
            warning.numCapVertices = 8;
            warning.startColor = warning.endColor = new Color(1f, 0.12f, 0.15f, 0.45f);
            warning.sortingLayerID = visual.sortingLayerID;
            warning.sortingOrder = visual.sortingOrder - 1;
            for (int dash = 0; dash < 3; dash++)
            {
                Vector3 origin = echo.transform.position;
                Vector2 center = (Vector2)origin + Vector2.up * knightDashCenterOffsetY;
                Vector2 direction = Aim(center);
                Face(direction, false);
                float distance = speed * 0.4f;
                RaycastHit2D wall = Physics2D.CircleCast(center, radius, direction, distance, walls);
                if (wall.collider != null) distance = Mathf.Max(0f, wall.distance - 0.05f);
                for (float step = 0.2f; step <= distance; step += 0.2f)
                    if (!InRoom(center + direction * step, room))
                    {
                        distance = step - 0.2f;
                        break;
                    }
                warning.SetPosition(0, new Vector3(center.x, center.y, origin.z));
                warning.SetPosition(1, new Vector3(center.x, center.y, origin.z) + (Vector3)(direction * distance));
                warning.enabled = distance > 0f;
                float preparation = dash == 0 ? 1f : 0.35f;
                for (float elapsed = 0f; elapsed < preparation; elapsed += Time.deltaTime)
                {
                    SampleClip(knightReady, Mathf.Min(elapsed, Mathf.Max(0f, knightReady.length - 0.001f)));
                    yield return null;
                }
                warning.enabled = false;
                bool hit = false;
                float travelled = 0f;
                while (travelled < distance)
                {
                    float step = Mathf.Min(speed * Time.deltaTime, distance - travelled);
                    if (!hit && step > 0f)
                    {
                        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
                        int count = Physics2D.CircleCast(center + direction * travelled, radius, direction, filter, dashHits, step);
                        for (int i = 0; i < count && !hit; i++)
                            hit = EnemyAttackTarget.TryDamage(dashHits[i].collider, knightDamage, dashHits[i].point);
                    }
                    travelled += step;
                    echo.transform.position = origin + (Vector3)(direction * travelled);
                    SampleClip(knightRun, Mathf.Repeat(travelled / speed, knightRun.length));
                    yield return null;
                }
                yield return PlayClip(knightStop);
            }
        }

        private void Update()
        {
            if (owner != null && (owner.isDead || owner.IsUpgrading || !owner.isActiveAndEnabled)) Clear();
        }

        public void Clear()
        {
            if (sequence != null) StopCoroutine(sequence);
            sequence = null;
            StopAnimation();
            if (echo != null)
            {
                echo.SetActive(false);
                Destroy(echo);
            }
            echo = null;
            foreach (var pillar in pillars) if (pillar != null) pillar.Cancel();
            foreach (var meteor in meteors) if (meteor != null) meteor.Cancel();
            pillars.Clear();
            meteors.Clear();
            lastEcho = null;
        }

        private void OnDisable() => Clear();
    }
}
