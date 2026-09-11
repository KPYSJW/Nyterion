using UnityEngine;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    public sealed class BatAerialCollisionController : MonoBehaviour
    {
        [Header("Collision Roles")]
        [Tooltip("공중에서도 플레이어 공격을 받을 수 있게 항상 유지되는 트리거입니다.")]
        [SerializeField] private Collider2D hurtbox;
        [Tooltip("착지한 공격 구간에만 플레이어와 물리 충돌하는 콜라이더입니다.")]
        [SerializeField] private Collider2D landingBodyCollider;

        [Header("Attack Preview")]
        [SerializeField] private GameObject attackRangePreview;

        public bool IsLanded { get; private set; }

        private void Awake()
        {
            SetAirborne();
        }

        private void OnEnable()
        {
            SetAirborne();
        }

        private void OnDisable()
        {
            HideAttackPreview();
        }

        public void BatShowAttackPreview()
        {
            if (attackRangePreview != null)
            {
                attackRangePreview.SetActive(true);
            }
        }

        public void BatLand()
        {
            HideAttackPreview();
            SetLanded();
        }

        public void BatTakeOff()
        {
            SetAirborne();
        }

        public void SetLanded()
        {
            IsLanded = true;

            if (hurtbox != null)
            {
                hurtbox.isTrigger = true;
            }

            if (landingBodyCollider != null)
            {
                landingBodyCollider.enabled = true;
            }
        }

        public void SetAirborne()
        {
            IsLanded = false;
            HideAttackPreview();

            if (hurtbox != null)
            {
                hurtbox.isTrigger = true;
            }

            if (landingBodyCollider != null)
            {
                landingBodyCollider.enabled = false;
            }
        }

        private void HideAttackPreview()
        {
            if (attackRangePreview != null)
            {
                attackRangePreview.SetActive(false);
            }
        }
    }
}
