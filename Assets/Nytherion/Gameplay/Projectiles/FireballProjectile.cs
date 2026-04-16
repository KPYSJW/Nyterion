using Nytherion.Core.Managers;
using UnityEngine;
using VContainer;

public class FireballProjectile : MonoBehaviour
{
    private float damage;
    private float speed;
    private float range;
    private string poolTag;
    private Vector3 startPosition;
    private bool isInitialized = false;

    public void Initialize(float damage, float speed, float range, string poolTag)
    {
        this.damage = damage;
        this.speed = speed;
        this.range = range;
        this.poolTag = poolTag;
        startPosition = transform.position;
        isInitialized = true; 
    }

    void Update()
    {
        if (!isInitialized) return;

        transform.Translate(Vector3.right * (speed * Time.deltaTime));

        if (Vector3.Distance(startPosition, transform.position) >= range)
        {
            ReturnToPool();
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            return;
        }
        Debug.Log($"파이어볼이 [{other.name}] 오브젝트와 부딪혀서 사라짐!");
        ReturnToPool();
    }
    private void ReturnToPool()
    {
        isInitialized = false;
        if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(poolTag))
        {
            ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
        }
        else
        {
            Destroy(gameObject); 
        }
    }

}