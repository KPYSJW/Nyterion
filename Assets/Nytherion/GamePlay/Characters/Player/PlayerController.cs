using Nytherion.Core;
using Nytherion.Data.ScriptableObjects.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerController : MonoBehaviour
    {
        
        
      
        private float lastDashTime = -999f;
        private Rigidbody2D rb;
        Vector2 moveInput;
        public bool isDash=false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
          
        }
        private void Update()
        {
            if(InputManager.Instance.Dash&&!isDash&&Time.time>=lastDashTime+ PlayerManager.Instance.playerData.dashCooldown)
            {
                StartCoroutine(Dash());
            }
        }
        private void FixedUpdate()
        {
            
            if(!isDash)
            {
                moveInput = InputManager.Instance.MoveInput;
                rb.velocity = moveInput.normalized * PlayerManager.Instance.playerData.moveSpeed;
            }
        }

        private IEnumerator Dash()
        {
            isDash = true;
            lastDashTime = Time.time;

            Vector2 dashDirection = moveInput.normalized;
            if( dashDirection == Vector2.zero )
            {
                dashDirection = Vector2.up;
            }
            rb.velocity=dashDirection* PlayerManager.Instance.playerData.dashSpeed;
            yield return new WaitForSeconds(PlayerManager.Instance.playerData.dashDuration);

            isDash = false;
        }
    }
}

