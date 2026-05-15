using Nytherion.GamePlay.Combat;
using UnityEngine;

namespace Nytherion.GamePlay.Combat.Effects
{
    [RequireComponent(typeof(CollisionObject))]
    public class ProjectileDistanceLimit : MonoBehaviour
    {
        private float maxDistance;
        private Vector3 startPosition;
        private bool isInitialized = false;
        private CollisionObject collisionObj;

        private void Awake()
        {
            collisionObj = GetComponent<CollisionObject>();
        }

        public void Initialize(float distance)
        {
            this.maxDistance = distance;
            this.startPosition = transform.position;
            this.isInitialized = true;
        }

        private void Update()
        {
            if (!isInitialized) return;

            if ((transform.position - startPosition).sqrMagnitude >= maxDistance * maxDistance)
            {
                isInitialized = false;
                if (collisionObj != null)
                {
                    collisionObj.ReturnToPool();
                }
                else
                {
                    gameObject.SetActive(false);
                }
            }
        }

        private void OnDisable()
        {
            isInitialized = false;
        }
    }
}
