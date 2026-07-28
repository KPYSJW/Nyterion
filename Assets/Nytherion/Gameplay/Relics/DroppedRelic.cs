using UnityEngine;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Relics
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(RelicPickupDetector))]
    public class DroppedRelic : MonoBehaviour
    {
        private RelicData relicData;
        private Transform playerTransform;
        private RelicManager relicManager;

        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private Collider2D col;
        private RelicPickupDetector relicPickupDetector;
        private bool isCollected;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            col = GetComponent<Collider2D>();
            relicPickupDetector = GetComponent<RelicPickupDetector>();

            if (relicPickupDetector == null)
            {
                relicPickupDetector = gameObject.AddComponent<RelicPickupDetector>();
            }

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
                Debug.Log($"[DroppedRelic Init Debug] GameObject: '{gameObject.name}' | RelicName: '{relicData.relicName}' | Assigned Sprite: '{relicData.Image.name}' | Texture: '{relicData.Image.texture.name}'", this);
            }
            else
            {
                Debug.LogWarning($"[DroppedRelic Init Debug] GameObject: '{gameObject.name}' | RelicData가 null이거나 Image가 null입니다! (RelicData: '{relicData?.relicName ?? "null"}')", this);
            }

            // 흩뿌려지는 연출: 사방으로 임의의 힘을 가함
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomForce = Random.Range(3f, 6f);
            rb.AddForce(randomDir * randomForce, ForceMode2D.Impulse);
            rb.drag = 3f; // 서서히 멈추도록 drag 설정

            relicPickupDetector.Initialize(playerTransform, CollectRelic);
        }

        private void CollectRelic()
        {
            if (isCollected)
            {
                return;
            }

            isCollected = true;
            if (relicManager != null && relicData != null)
            {
                relicManager.AddNewRelicToStorage(relicData);
            }
            Destroy(gameObject);
        }
    }
}
