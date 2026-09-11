using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class SineWaveProj : MonoBehaviour
    {
        public float forwardSpeed = 8f;
        public float waveFrequency = 5f; 
        public float waveMagnitude = 3f;

        private Rigidbody2D rb;
        private Vector2 startDirection;
        private Vector2 perpendicularDirection;
        private float spawnTime;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        private void Start()
        {
            spawnTime = Time.time;

            startDirection = rb.velocity.normalized;
            if (startDirection == Vector2.zero) startDirection = transform.right;

            perpendicularDirection = new Vector2(-startDirection.y, startDirection.x);
        }

        private void FixedUpdate()
        {
            Vector2 forwardVelocity = startDirection * forwardSpeed;

            float sineWave = Mathf.Sin((Time.time - spawnTime) * waveFrequency) * waveMagnitude;
            Vector2 waveVelocity = perpendicularDirection * sineWave;

            rb.velocity = forwardVelocity + waveVelocity;

            float angle = Mathf.Atan2(rb.velocity.y, rb.velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }
}
