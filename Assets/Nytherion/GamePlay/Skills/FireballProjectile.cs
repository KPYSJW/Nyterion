using Nytherion.Core.Managers;
using UnityEngine;

public class FireballProjectile : MonoBehaviour
{
    private float damage;
    private float speed;
    private float range;
    private string poolTag;
    private Vector3 startPosition;

    public void Initialize(float damage, float speed, float range, string poolTag)
    {
        this.damage = damage;
        this.speed = speed;
        this.range = range;
        this.poolTag = poolTag;
        startPosition = transform.position;
    }

    void Update()
    {
        transform.Translate(Vector3.forward * (speed * Time.deltaTime));

        if (Vector3.Distance(startPosition, transform.position) >= range)
        {
            Destroy(gameObject);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        
        ReturnToPool(); 
    }
    private void ReturnToPool()
    {
        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.ReturnToPool(poolTag, gameObject);
        }
        else
        {
            Destroy(gameObject); 
        }
    }
    
}