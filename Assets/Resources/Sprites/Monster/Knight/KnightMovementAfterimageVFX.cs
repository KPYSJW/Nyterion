using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Knight), typeof(SpriteRenderer))]
    public sealed class KnightMovementAfterimageVFX : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer sourceRenderer;
        [SerializeField] private Material afterimageMaterial;
        [SerializeField, Min(0f)] private float minimumMoveSpeed = 0.15f;
        [SerializeField, Min(0.01f)] private float spawnDistance = 0.42f;
        [SerializeField, Min(0.01f)] private float minimumSpawnInterval = 0.045f;
        [SerializeField, Min(0.01f)] private float lifetime = 0.22f;
        [SerializeField, Range(0f, 1f)] private float initialAlpha = 0.7f;
        [SerializeField] private int sortingOrderOffset = -1;
        [SerializeField] private Color bossDashColor = new Color(0.15f, 0.65f, 1f, 1f);

        [Header("Boss Preparation Convergence")]
        [SerializeField, Min(1f)] private float preparationStartScale = 1.6f;
        [SerializeField, Min(0.01f)] private float preparationSpawnInterval = 0.18f;
        [SerializeField, Min(0.01f)] private float preparationLifetime = 0.35f;
        private float nextPreparationSpawnTime;

        private readonly List<Afterimage> activeAfterimages = new();
        private readonly Stack<Afterimage> inactiveAfterimages = new();
        private Knight knight;
        private BossKnightCombatBehavior bossCombat;
        private Transform poolRoot;
        private Vector3 lastPosition;
        private float distanceSinceLastSpawn;
        private float nextSpawnTime;
        private bool hasLastPosition;

        private void Awake()
        {
            knight = GetComponent<Knight>();
            bossCombat = GetComponent<BossKnightCombatBehavior>();
            if (sourceRenderer == null)
                sourceRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            hasLastPosition = false;
            distanceSinceLastSpawn = 0f;
            nextSpawnTime = 0f;
            nextPreparationSpawnTime = 0f;
        }

        private void Update()
        {
            UpdateActiveAfterimages();

            if (knight == null || sourceRenderer == null ||
                sourceRenderer.sprite == null || afterimageMaterial == null)
                return;

            Vector3 currentPosition = sourceRenderer.transform.position;
            if (!hasLastPosition)
            {
                lastPosition = currentPosition;
                hasLastPosition = true;
                return;
            }

            float movedDistance = Vector3.Distance(lastPosition, currentPosition);
            lastPosition = currentPosition;

            if (bossCombat != null && bossCombat.IsPreparing)
            {
                distanceSinceLastSpawn = 0f;
                if (Time.time >= nextPreparationSpawnTime)
                {
                    SpawnAfterimage(true);
                    nextPreparationSpawnTime = Time.time + preparationSpawnInterval;
                }
                return;
            }
            nextPreparationSpawnTime = 0f;

            // Boss dashes use Rigidbody2D while the NavMeshAgent is stopped.
            // Keep the normal knight speed check, but allow dash movement too.
            bool isBossDashing = bossCombat != null && bossCombat.IsDashing;
            if (!isBossDashing && knight.CurrentSpeed < minimumMoveSpeed)
            {
                distanceSinceLastSpawn = 0f;
                return;
            }

            distanceSinceLastSpawn += movedDistance;
            if (distanceSinceLastSpawn < spawnDistance || Time.time < nextSpawnTime)
                return;

            SpawnAfterimage();
            distanceSinceLastSpawn = 0f;
            nextSpawnTime = Time.time + minimumSpawnInterval;
        }

        private void SpawnAfterimage(bool converging = false)
        {
            Afterimage afterimage = inactiveAfterimages.Count > 0
                ? inactiveAfterimages.Pop()
                : CreateAfterimage();

            Transform ghostTransform = afterimage.root.transform;
            ghostTransform.SetParent(null, false);
            ghostTransform.SetPositionAndRotation(
                sourceRenderer.transform.position,
                sourceRenderer.transform.rotation);
            ghostTransform.localScale = sourceRenderer.transform.lossyScale;

            afterimage.renderer.sprite = sourceRenderer.sprite;
            afterimage.renderer.flipX = sourceRenderer.flipX;
            afterimage.renderer.flipY = sourceRenderer.flipY;
            afterimage.renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            afterimage.renderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
            // Set the tint on every spawn, including reused ghosts, so a dash
            // ghost stays blue as it fades and normal movement returns to white.
            Color color = converging || (bossCombat != null && bossCombat.IsDashing)
                ? bossDashColor
                : Color.white;
            color.a = converging ? initialAlpha * 0.6f : initialAlpha;
            afterimage.renderer.color = color;
            afterimage.elapsed = 0f;
            afterimage.converging = converging;
            afterimage.startingAlpha = color.a;
            if (converging)
                UpdateConvergenceTransform(afterimage, preparationStartScale);
            afterimage.root.SetActive(true);
            activeAfterimages.Add(afterimage);
        }

        private Afterimage CreateAfterimage()
        {
            if (poolRoot == null)
            {
                GameObject root = new("Knight Afterimage Pool");
                poolRoot = root.transform;
                poolRoot.SetParent(transform, false);
            }

            GameObject ghost = new("Knight Afterimage");
            ghost.transform.SetParent(poolRoot, false);
            SpriteRenderer ghostRenderer = ghost.AddComponent<SpriteRenderer>();
            ghostRenderer.sharedMaterial = afterimageMaterial;
            ghost.SetActive(false);
            return new Afterimage(ghost, ghostRenderer);
        }

        private void UpdateActiveAfterimages()
        {
            for (int i = activeAfterimages.Count - 1; i >= 0; i--)
            {
                Afterimage afterimage = activeAfterimages[i];
                if (afterimage.converging && (bossCombat == null || !bossCombat.IsPreparing))
                {
                    activeAfterimages.RemoveAt(i);
                    Recycle(afterimage);
                    continue;
                }
                afterimage.elapsed += Time.deltaTime;
                float duration = afterimage.converging ? preparationLifetime : lifetime;
                float progress = afterimage.elapsed / Mathf.Max(0.01f, duration);

                if (progress >= 1f)
                {
                    activeAfterimages.RemoveAt(i);
                    Recycle(afterimage);
                    continue;
                }

                Color color = afterimage.renderer.color;
                color.a = Mathf.Lerp(afterimage.startingAlpha, 0f, progress);
                afterimage.renderer.color = color;
                if (afterimage.converging)
                    UpdateConvergenceTransform(afterimage,
                        Mathf.Lerp(preparationStartScale, 1f, progress));
            }
        }

        private void UpdateConvergenceTransform(Afterimage afterimage, float scale)
        {
            Transform source = sourceRenderer.transform;
            Transform ghost = afterimage.root.transform;
            ghost.SetPositionAndRotation(source.position, source.rotation);
            Vector3 sourceScale = source.lossyScale;
            ghost.localScale = new Vector3(sourceScale.x * scale, sourceScale.y * scale, sourceScale.z);
        }

        private void Recycle(Afterimage afterimage)
        {
            afterimage.root.SetActive(false);
            afterimage.root.transform.SetParent(poolRoot, false);
            inactiveAfterimages.Push(afterimage);
        }

        private void OnDisable()
        {
            foreach (Afterimage afterimage in activeAfterimages)
                Recycle(afterimage);

            activeAfterimages.Clear();
            hasLastPosition = false;
            distanceSinceLastSpawn = 0f;
            nextPreparationSpawnTime = 0f;
        }

        private void OnDestroy()
        {
            foreach (Afterimage afterimage in activeAfterimages)
                Destroy(afterimage.root);

            if (poolRoot != null)
                Destroy(poolRoot.gameObject);
        }

        private void OnValidate()
        {
            minimumMoveSpeed = Mathf.Max(0f, minimumMoveSpeed);
            spawnDistance = Mathf.Max(0.01f, spawnDistance);
            minimumSpawnInterval = Mathf.Max(0.01f, minimumSpawnInterval);
            lifetime = Mathf.Max(0.01f, lifetime);
            initialAlpha = Mathf.Clamp01(initialAlpha);
            preparationStartScale = Mathf.Max(1f, preparationStartScale);
            preparationSpawnInterval = Mathf.Max(0.01f, preparationSpawnInterval);
            preparationLifetime = Mathf.Max(0.01f, preparationLifetime);
        }

        private sealed class Afterimage
        {
            public readonly GameObject root;
            public readonly SpriteRenderer renderer;
            public float elapsed;
            public bool converging;
            public float startingAlpha;

            public Afterimage(GameObject root, SpriteRenderer renderer)
            {
                this.root = root;
                this.renderer = renderer;
            }
        }
    }
}
