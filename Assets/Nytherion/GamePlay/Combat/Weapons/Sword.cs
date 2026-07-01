using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

namespace Nytherion.GamePlay.Combat.Weapon
{
    public class Sword : MeleeWeapon
    {
        [Header("Melee Settings")]
        public SpriteRenderer sprite;
        [SerializeField] private Animation meleeWeaponAnimation;
        [SerializeField] private string attackClipName  = "PlayerMeleeAnim";
        
        [Header("Animator Settings (Optional)")]
        [Tooltip("Animator 기반으로 작동 시 할당합니다 (예: Flamberge)")]
        [SerializeField] private Animator myAnimator;
        [SerializeField] private string attackTriggerName = "Attack";

        [Header("Effect Animation")]
        [SerializeField] private Animator  slashEffectAnimator;
         [SerializeField] private GameObject slashEffectObject;
        [SerializeField] private string slashEffectClipName = "Sword_Effect";
        [SerializeField] private float slashEffectDuration = 0.09f;


        private Coroutine slashEffectRoutine;
        private WaitForSeconds slashEffectWait;

        public override void Start()
        {
            base.Start();
            slashEffectWait = new WaitForSeconds(slashEffectDuration);
            if(meleeWeaponAnimation==null)
            {
                meleeWeaponAnimation=GetComponentInParent<Animation>();
            }
            if(myAnimator==null)
            {
                myAnimator=GetComponent<Animator>();
            }
        }

           private void PlaySlashEffect()
        {
            if (slashEffectObject == null || slashEffectAnimator == null)
            {
                return;
            }

            if (slashEffectRoutine != null)
            {
                StopCoroutine(slashEffectRoutine);
            }

            slashEffectRoutine = StartCoroutine(PlaySlashEffectRoutine());
        }

        private IEnumerator PlaySlashEffectRoutine()
        {
            slashEffectObject.SetActive(true);

            slashEffectAnimator.Play(slashEffectClipName, 0, 0f);
            slashEffectAnimator.Update(0f);

            yield return slashEffectWait;

            slashEffectObject.SetActive(false);
            slashEffectRoutine = null;
        }


        public override void Attack(Vector2 direction, Vector3 targetPosition = default)
        {
        
            if (!CanAttack())
            {
               
                return;
            }
           
           if (meleeWeaponAnimation != null)
            {
                //meleeWeaponAnimation.Stop();
                meleeWeaponAnimation.Play(attackClipName);
                
            }

            if (myAnimator != null)
            {
                myAnimator.SetTrigger(attackTriggerName);
            }

            //PlaySlashEffect();

            lastAttackTime = Time.time;
        }


         public override void AttackEnd()
        {
            //DisableHitbox();
        }

        

       
    }
}
