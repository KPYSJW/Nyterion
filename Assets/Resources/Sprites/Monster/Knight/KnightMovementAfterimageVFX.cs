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

        private readonly List<Afterimage> activeAfterimages = new();
        private readonly Stack<Afterimage> inactiveAfterimages = new();
        private Knight knight;
        private Transform poolRoot;
        private Vector3 lastPosition;
        private float distanceSinceLastSpawn;
        private float nextSpawnTime;
        private bool hasLastPosition;

        private void Awake()
        {
            knight = GetComponent<Knight>();
            if (sourceRenderer == null)
                sourceRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            hasLastPosition = false;
            distanceSinceLastSpawn = 0f;
            nextSpawnTime = 0f;
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

            if (knight.CurrentSpeed < minimumMoveSpeed)
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

        private void SpawnAfterimage()
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
            afterimage.renderer.color = new Color(1f, 1f, 1f, initialAlpha);
            afterimage.elapsed = 0f;
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
                afterimage.elapsed += Time.deltaTime;
                float progress = afterimage.elapsed / lifetime;

                if (progress >= 1f)
                {
                    activeAfterimages.RemoveAt(i);
                    Recycle(afterimage);
                    continue;
                }

                Color color = afterimage.renderer.color;
                color.a = Mathf.Lerp(initialAlpha, 0f, progress);
                afterimage.renderer.color = color;
            }
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
        }

        private sealed class Afterimage
        {
            public readonly GameObject root;
            public readonly SpriteRenderer renderer;
            public float elapsed;

            public Afterimage(GameObject root, SpriteRenderer renderer)
            {
                this.root = root;
                this.renderer = renderer;
            }
        }
    }
}
