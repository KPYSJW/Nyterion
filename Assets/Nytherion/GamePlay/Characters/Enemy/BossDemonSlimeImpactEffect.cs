using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossDemonSlimeImpactEffect : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(1f)] private float framesPerSecond = 20f;
        // Measured outer ring radius in source pixels; texture PPU may change independently.
        [SerializeField, HideInInspector] private float referencePixelRadius = 54.886246f;
        private SpriteRenderer visual;
        private EnemyBase owner;
        private float startedAt;
        private bool initialized;

        public void Initialize(EnemyBase source, float hitRadius)
        {
            if (frames == null || frames.Length == 0 || frames[0] == null)
            {
                Destroy(gameObject);
                return;
            }
            owner = source;
            visual = GetComponent<SpriteRenderer>();
            float referenceRadius = Mathf.Max(0.01f, referencePixelRadius) / Mathf.Max(0.01f, frames[0].pixelsPerUnit);
            transform.localScale = Vector3.one * (Mathf.Max(0.1f, hitRadius) / referenceRadius);
            SpriteRenderer body = owner != null ? owner.GetComponent<SpriteRenderer>() : null;
            visual.sortingLayerID = body != null ? body.sortingLayerID : 0;
            visual.sortingOrder = body != null ? body.sortingOrder - 1 : -1;
            visual.sprite = frames[0];
            startedAt = Time.time;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized) return;
            if (owner != null && (owner.isDead || !owner.isActiveAndEnabled))
            {
                initialized = false;
                Destroy(gameObject);
                return;
            }
            int frame = Mathf.Max(0, Mathf.FloorToInt((Time.time - startedAt) * Mathf.Max(1f, framesPerSecond)));
            if (frame >= frames.Length)
            {
                initialized = false;
                Destroy(gameObject);
                return;
            }
            visual.sprite = frames[frame];
        }
    }
}
