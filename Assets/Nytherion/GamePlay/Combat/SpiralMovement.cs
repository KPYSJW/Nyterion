using UnityEngine;
using Nytherion.GamePlay.Combat;

namespace Nytherion.GamePlay.Skills
{
    public class SpiralMovement : MonoBehaviour
    {
        [Header("Spiral Settings")]
        public float spinSpeed = 720f;

        public float expandSpeed = 3f;

        public float maxRadius = 10f;

        private float currentAngle;
        private float currentRadius;
        private Vector2 centerPosition;
        private CollisionObject myCol;

        private bool isInitialized = false;

        private void Awake()
        {
            myCol = GetComponent<CollisionObject>();
        }

        private void OnEnable()
        {
            currentRadius = 0f;
            isInitialized = false;
        }

        public void SetInitialAngle(float angle)
        {
            currentAngle = angle;
        }
        public void SetupSpiral(float startAngle, Vector2 startPosition)
        {
            currentAngle = startAngle;
            centerPosition = startPosition;
            isInitialized = true;
        }
        private void Update()
        {
            if (!isInitialized) return;

            currentAngle += spinSpeed * Time.deltaTime;
            currentRadius += expandSpeed * Time.deltaTime;

            if (currentRadius >= maxRadius)
            {
                if (myCol != null) myCol.ReturnToPool();
                else gameObject.SetActive(false);
                return;
            }

            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0) * currentRadius;

            transform.position = (Vector3)centerPosition + offset;
            transform.rotation = Quaternion.Euler(0, 0, currentAngle + 90f);
        }
    }
}