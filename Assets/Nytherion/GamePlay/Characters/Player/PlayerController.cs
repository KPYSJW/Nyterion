using Nytherion.Core;
using Nytherion.Data.ScriptableObjects.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 이동과 대시 동작을 제어하는 클래스입니다.
/// Rigidbody2D를 사용한 물리 기반 이동과 대시 기능을 구현합니다.
/// </summary>

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerController : MonoBehaviour
    {
        
        
      
        /// <summary>기본 이동 속도</summary>
        [Tooltip("플레이어의 기본 이동 속도")]
        public float moveSpeed = 5f;
        
        /// <summary>대시 시 이동 속도</summary>
        [Tooltip("대시 시 적용되는 이동 속도")]
        public float dashSpeed = 12f;
        
        /// <summary>대시 지속 시간(초)</summary>
        [Tooltip("대시가 지속되는 시간(초)")]
        public float dashDuration = 0.2f;
        
        /// <summary>대시 쿨다운 시간(초)</summary>
        [Tooltip("대시 후 다음 대시까지의 쿨다운 시간(초)")]
        public float dashCooldown = 3f;

        /// <summary>마지막 대시 시간</summary>
        private float lastDashTime = -999f;
        
        /// <summary>Rigidbody2D 컴포넌트 캐시</summary>
        private Rigidbody2D rb;
        Vector2 moveInput;
        public bool isDash=false;


        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
          
        }
        /// <summary>
        /// 매 프레임 호출됩니다.
        /// 대시 입력을 감지하고 대시를 시작합니다.
        /// </summary>
        private void Update()
        {
            if(InputManager.Instance.Dash&&!isDash&&Time.time>=lastDashTime+ PlayerManager.Instance.playerData.dashCooldown)

            {
                StartCoroutine(Dash());
            }
        }
        /// <summary>
        /// 물리 업데이트 주기로 호출됩니다.
        /// 플레이어의 이동을 처리합니다.
        /// </summary>
        private void FixedUpdate()
        {
            // 대시 중이 아닐 때만 일반 이동 처리
            if (!isDash)
            {
                // 입력 매니저로부터 이동 입력 받기
                moveInput = InputManager.Instance.MoveInput;
                rb.velocity = moveInput.normalized * PlayerManager.Instance.playerData.moveSpeed;

            }
        }

        /// <summary>
        /// 대시 동작을 처리하는 코루틴입니다.
        /// </summary>
        /// <returns>IEnumerator</returns>
        private IEnumerator Dash()
        {
            // 대시 상태로 설정
            isDash = true;
            
            // 마지막 대시 시간 갱신
            lastDashTime = Time.time;

            // 대시 방향 결정 (입력 방향이 없으면 위쪽으로 대시)
            Vector2 dashDirection = moveInput.normalized;
            if (dashDirection == Vector2.zero)
            {
                dashDirection = Vector2.up;
            }
            rb.velocity=dashDirection* PlayerManager.Instance.playerData.dashSpeed;
            yield return new WaitForSeconds(PlayerManager.Instance.playerData.dashDuration);


            // 대시 상태 해제 (이후 FixedUpdate에서 일반 이동으로 복귀)
            isDash = false;
        }
    }
}

