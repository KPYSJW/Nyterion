using UnityEngine;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Core.Managers;
using System.Collections;

namespace Nytherion.GamePlay.Relics
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    public class DroppedRelic : MonoBehaviour
    {
        private RelicData relicData;
        private Transform playerTransform;
        private RelicManager relicManager;

        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private Collider2D col;

        private bool isAttracted = false;
        private float attractSpeed = 2f;
        private float maxAttractSpeed = 15f;
        private float acceleration = 12f;
        private float delayBeforeAttract = 1.0f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            col = GetComponent<Collider2D>();

            // 충돌은 감지만 하되 뚫고 지나가도록 설정할 수도 있으나,
            // 흩뿌려질 때는 벽에 부딪힐 수 있도록 Trigger를 끄거나 켜둘 수 있습니다.
            // 여기서는 충돌을 감지하기만 하는 트리거로 설계합니다.
            col.isTrigger = true;
        }

        public void Init(RelicData data, Transform player, RelicManager manager)
        {
            relicData = data;
            playerTransform = player;
            relicManager = manager;

            if (relicData != null && relicData.Image != null)
            {
                spriteRenderer.sprite = relicData.Image;
            }

            // 흩뿌려지는 연출: 사방으로 임의의 힘을 가함
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomForce = Random.Range(3f, 6f);
            rb.AddForce(randomDir * randomForce, ForceMode2D.Impulse);
            rb.drag = 3f; // 서서히 멈추도록 drag 설정

            StartCoroutine(StartAttractCoroutine());
        }

        private IEnumerator StartAttractCoroutine()
        {
            yield return new WaitForSeconds(delayBeforeAttract);
            isAttracted = true;
            rb.drag = 0f; // 끌려갈 때는 감속하지 않음
            rb.velocity = Vector2.zero;
        }

        private void Update()
        {
            if (!isAttracted || playerTransform == null) return;

            // 플레이어 방향 벡터 계산
            Vector3 targetPos = playerTransform.position;
            Vector2 direction = (Vector2)(targetPos - transform.position).normalized;

            // 가속하며 플레이어로 이동
            attractSpeed = Mathf.Min(attractSpeed + acceleration * Time.deltaTime, maxAttractSpeed);
            transform.Translate(direction * attractSpeed * Time.deltaTime, Space.World);

            // 거리가 충분히 가까워지면 보관함에 추가하고 파괴
            float distance = Vector2.Distance(transform.position, playerTransform.position);
            if (distance < 0.3f)
            {
                CollectRelic();
            }
        }

        private void CollectRelic()
        {
            if (relicManager != null && relicData != null)
            {
                relicManager.AddNewRelicToStorage(relicData);
            }
            Destroy(gameObject);
        }
    }
}
