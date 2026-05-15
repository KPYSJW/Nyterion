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

        [Header("Effect Animation")]
        [SerializeField] private Animator  slashEffectAnimator;
         [SerializeField] private GameObject slashEffectObject;
        [SerializeField] private string slashEffectClipName = "Sword_Effect";
        [SerializeField] private float slashEffectDuration = 0.09f;


        private Coroutine slashEffectRoutine;
        private WaitForSeconds slashEffectWait;

        private void Start()
        {
            slashEffectWait = new WaitForSeconds(slashEffectDuration);
            if(meleeWeaponAnimation==null)
            {
                meleeWeaponAnimation=GetComponentInParent<Animation>();
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
                meleeWeaponAnimation.Stop();
                meleeWeaponAnimation.Play(attackClipName);
                
            }
            PlaySlashEffect();

            lastAttackTime = Time.time;
        }


         public override void AttackEnd()
        {
            DisableHitbox();
        }

        public void EnableHitbox()
        {
            if (col != null)
            {
                col.enabled = true;
            }
        }

        public void DisableHitbox()
        {
            if (col != null)
            {
                col.enabled = false;
            }
        }

       
    }
}
