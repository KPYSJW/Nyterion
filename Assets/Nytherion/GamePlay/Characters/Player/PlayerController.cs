using Nytherion.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public float dashSpeed = 12f;
        public float dashDuration = 0.2f;
        public float dashCooldown = 3f;

      
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
            if(InputManager.Instance.Dash&&!isDash&&Time.time>=lastDashTime+dashCooldown)
            {
                StartCoroutine(Dash());
            }
        }
        private void FixedUpdate()
        {
            
            if(!isDash)
            {
                moveInput = InputManager.Instance.MoveInput;
                rb.velocity = moveInput.normalized * moveSpeed;
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
            rb.velocity=dashDirection*dashSpeed;
            yield return new WaitForSeconds(dashDuration);

            isDash = false;
        }
    }
}

