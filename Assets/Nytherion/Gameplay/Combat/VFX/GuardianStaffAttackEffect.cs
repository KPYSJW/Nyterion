using Nytherion.Core.Managers;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Animator))]
    [DefaultExecutionOrder(1100)]
    public sealed class GuardianStaffAttackEffect : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float nativeRadius = 1f;
        [SerializeField, Min(0.01f), Tooltip("이 이펙트의 표시 크기와 실제 공격 반지름에 함께 적용할 배율")]
        private float radiusMultiplier = 1f;
        [SerializeField, Tooltip("소환수와 소환물 앞에 표시할 기본 정렬 순서")]
        private int minimumSortingOrder = 100;
        [SerializeField, Min(1), Tooltip("플레이어와 무기의 정렬 순서에 더할 값")]
        private int foregroundSortingOffset = 10;

        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private SpriteRenderer ownerRenderer;
        private SpriteRenderer weaponRenderer;
        private Transform owner;
        private ObjectPoolManager pool;
        private bool playing;

        public float NativeRadius => nativeRadius;
        public float RadiusMultiplier => Mathf.Max(0.01f, radiusMultiplier);
        public float Duration => animator != null && animator.runtimeAnimatorController != null
            ? animator.GetCurrentAnimatorStateInfo(0).length : 0f;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
        }

        public void Play(Transform center, float radius, ObjectPoolManager objectPool, SpriteRenderer sortingSource, SpriteRenderer weaponSortingSource)
        {
            owner = center;
            pool = objectPool;
            ownerRenderer = sortingSource;
            weaponRenderer = weaponSortingSource;
            playing = true;
            // 프레임 전체에서 가장 큰 원의 반지름을 기준으로 표시 크기와 판정을 맞춥니다.
            transform.localScale = Vector3.one * (radius / Mathf.Max(0.01f, nativeRadius));
            // 풀에서 다시 꺼낼 때도 기본 상태의 첫 프레임부터 재생합니다.
            animator.Rebind();
            animator.Play(0, 0, 0f);
            animator.Update(0f);
            SyncTransform();
        }

        private void LateUpdate()
        {
            if (!playing) return;
            if (animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
            {
                playing = false;
                if (pool != null) pool.ReturnToPool(gameObject.name.Replace("(Clone)", "").Trim(), gameObject);
                else Destroy(gameObject);
                return;
            }
            SyncTransform();
        }

        private void SyncTransform()
        {
            if (owner != null) transform.position = owner.position;
            transform.rotation = Quaternion.identity;
            SpriteRenderer sortingSource = ownerRenderer != null ? ownerRenderer : weaponRenderer;
            if (sortingSource == null) return;
            spriteRenderer.sortingLayerID = sortingSource.sortingLayerID;
            int frontOrder = sortingSource.sortingOrder;
            if (weaponRenderer != null) frontOrder = Mathf.Max(frontOrder, weaponRenderer.sortingOrder);
            spriteRenderer.sortingOrder = Mathf.Max(minimumSortingOrder, frontOrder + Mathf.Max(1, foregroundSortingOffset));
        }

        private void OnDisable()
        {
            playing = false;
            owner = null;
            ownerRenderer = null;
            weaponRenderer = null;
            pool = null;
        }
    }
}
