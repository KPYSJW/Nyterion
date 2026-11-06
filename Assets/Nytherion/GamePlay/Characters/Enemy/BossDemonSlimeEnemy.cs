using System.Collections;
using Nytherion.Data.ScriptableObjects.Enemy;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Characters.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BossDemonSlimeMovementBehavior))]
    public sealed class BossDemonSlimeEnemy : EnemyBase
    {
        public enum Form { Slime, SlimeDying, Transforming, Demon }

        [Header("Phase Health / 단계별 체력")]
        [Tooltip("슬라임 단계의 시작 체력입니다. 이 보스는 EnemyData의 Max Health 대신 이 값을 사용합니다.")]
        [SerializeField, Min(1f)] private float slimeHealth = 10f;
        [Tooltip("변신 애니메이션이 끝난 뒤 새로 채워지는 데몬 단계의 체력입니다.")]
        [SerializeField, Min(1f)] private float demonHealth = 100f;

        [Header("Phase Animation")]
        [SerializeField] private RuntimeAnimatorController slimeController;
        [SerializeField] private RuntimeAnimatorController demonController;

        [Header("Body Size")]
        [SerializeField] private Vector2 slimeBodySize = new Vector2(0.65f, 0.65f);
        [SerializeField] private Vector2 slimeBodyOffset = new Vector2(0f, 0.3f);
        [SerializeField] private Vector2 demonBodySize = new Vector2(1.4f, 2f);
        [SerializeField] private Vector2 demonBodyOffset = new Vector2(0f, 1f);
        [SerializeField, Min(0.1f)] private float slimeAgentRadius = 0.3f;
        [SerializeField, Min(0.1f)] private float demonAgentRadius = 0.55f;

        private BossDemonSlimeMovementBehavior movement;
        private Animator bodyAnimator;
        private CapsuleCollider2D bodyCollider;
        private NavMeshAgent navigation;
        private Coroutine transformation;
        private Form form;

        public Form CurrentForm => form;
        protected override float InitialHealth => Mathf.Max(1f, slimeHealth);
        protected override bool CanReceiveDamage => form == Form.Slime || form == Form.Demon;

        private void CachePhaseComponents()
        {
            if (movement == null) movement = GetComponent<BossDemonSlimeMovementBehavior>();
            if (bodyAnimator == null) bodyAnimator = GetComponent<Animator>();
            if (bodyCollider == null) bodyCollider = GetComponent<CapsuleCollider2D>();
            if (navigation == null) navigation = GetComponent<NavMeshAgent>();
            if (aiController == null) aiController = GetComponent<EnemyAIController>();
        }

        public override void Initialize(EnemyData data)
        {
            if (data == null) return;
            CachePhaseComponents();
            if (transformation != null) StopCoroutine(transformation);
            transformation = null;
            form = Form.Slime;
            if (bodyAnimator != null && slimeController != null)
                bodyAnimator.runtimeAnimatorController = slimeController;
            movement.SetPhaseAnimationLocked(false);
            movement.SetDemonForm(false);
            ApplyBodySize(false);
            base.Initialize(data);
        }

        protected override bool TryHandleLethalDamage()
        {
            if (form == Form.Demon) return false;
            if (form != Form.Slime) return true;
            CachePhaseComponents();
            // Set the form before starting the coroutine so repeated contacts cannot restart it.
            form = Form.SlimeDying;
            movement.SetPhaseAnimationLocked(true);
            if (aiController != null)
            {
                aiController.SetMovementAllowed(false);
                aiController.enabled = false;
            }
            transformation = StartCoroutine(TransformIntoDemon());
            return true;
        }

        private IEnumerator TransformIntoDemon()
        {
            yield return PlayPhaseAnimation("SlimeDeath");
            form = Form.Transforming;
            // Change facing while the slime is flattened, before the demon takes shape.
            movement.SetDemonForm(true);
            yield return PlayPhaseAnimation("Transform");

            if (bodyAnimator != null && demonController != null)
            {
                bodyAnimator.runtimeAnimatorController = demonController;
                bodyAnimator.Rebind();
                bodyAnimator.Update(0f);
            }
            ApplyBodySize(true);
            SetPhaseHealth(demonHealth);
            form = Form.Demon;
            movement.SetPhaseAnimationLocked(false);
            movement.StopMovement();
            if (aiController != null)
            {
                aiController.SetMovementAllowed(true);
                aiController.enabled = true;
            }
            transformation = null;
        }

        private IEnumerator PlayPhaseAnimation(string stateName)
        {
            int state = Animator.StringToHash("Base Layer." + stateName);
            if (bodyAnimator == null || !bodyAnimator.isActiveAndEnabled ||
                bodyAnimator.layerCount == 0 || !bodyAnimator.HasState(0, state))
            {
                Debug.LogError("[DemonSlime] Missing phase animation: " + stateName, this);
                yield break;
            }
            bodyAnimator.Play(state, 0, 0f);
            bodyAnimator.Update(0f);
            while (bodyAnimator.isActiveAndEnabled)
            {
                AnimatorStateInfo info = bodyAnimator.GetCurrentAnimatorStateInfo(0);
                if (info.fullPathHash == state && info.normalizedTime >= 1f &&
                    !bodyAnimator.IsInTransition(0)) break;
                yield return null;
            }
        }

        private void ApplyBodySize(bool demon)
        {
            if (bodyCollider != null)
            {
                bodyCollider.size = demon ? demonBodySize : slimeBodySize;
                bodyCollider.offset = demon ? demonBodyOffset : slimeBodyOffset;
            }
            if (navigation != null) navigation.radius = demon ? demonAgentRadius : slimeAgentRadius;
        }

        private void OnDisable()
        {
            if (transformation != null) StopCoroutine(transformation);
            transformation = null;
        }
    }
}
