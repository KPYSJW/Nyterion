using System.Collections.Generic;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Nytherion.Core.Data;

namespace Nytherion.Core.Managers
{
    public class ObjectPoolManager : BaseManager
    {
        public static ObjectPoolManager Instance { get; private set; }

        [System.Serializable]
        public class Pool
        {
            public string tag;
            public GameObject prefab;
            public int size;
        }
        public List<Pool> pools;
        public Dictionary<string, Queue<GameObject>> poolDictionary;

        private Dictionary<string, Transform> poolRoots;

        private IObjectResolver container;

        [Inject]
        public void Construct(IObjectResolver container)
        {
            this.container = container;
        }

        protected override void Awake()
        {
            base.Awake();

            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        protected override void OnInitializeInternal()
        {
            poolDictionary = new Dictionary<string, Queue<GameObject>>();
            poolRoots = new Dictionary<string, Transform>();

            foreach (Pool pool in pools)
            {
                if (pool.prefab == null)
                {
                    continue;
                }

                GameObject rootObj = new GameObject($"[Pool] {pool.tag}");
                rootObj.transform.SetParent(this.transform);
                poolRoots.Add(pool.tag, rootObj.transform);

                Queue<GameObject> objectPool = new Queue<GameObject>();
                for (int i = 0; i < pool.size; i++)
                {
                    GameObject obj = container.Instantiate(pool.prefab);
                    
                    obj.transform.SetParent(rootObj.transform);
                    
                    obj.SetActive(false);
                    objectPool.Enqueue(obj);
                }
                poolDictionary.Add(pool.tag, objectPool);
            }
        }
        public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
        {
            if (!poolDictionary.ContainsKey(tag))
            {
                Debug.LogWarning($"Pool with tag '{tag}' doesn't exist.");
                return null;
            }

            if (poolDictionary[tag].Count == 0)
            {
                Pool pool = pools.Find(p => p.tag == tag);
                if (pool != null && pool.prefab != null)
                {
                    GameObject newObj = container.Instantiate(pool.prefab);
                    
                    if (poolRoots.TryGetValue(tag, out Transform root))
                    {
                        newObj.transform.SetParent(root);
                    }
                    
                    newObj.transform.position = position;
                    newObj.transform.rotation = rotation;
                    newObj.SetActive(true);
                    return newObj;
                }
                return null;
            }

            GameObject obj = poolDictionary[tag].Dequeue();
            obj.SetActive(true);
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            return obj;
        }

        public void ReturnToPool(string tag, GameObject objectToReturn)
        {
            if (!poolDictionary.ContainsKey(tag))
            {
                Destroy(objectToReturn);
                return;
            }

            objectToReturn.SetActive(false);
            poolDictionary[tag].Enqueue(objectToReturn);
        }

        public override void PopulateSaveData(SaveData saveData)
        {
            // ObjectPoolManager는 저장할 데이터가 없음 (런타임 풀 관리)
        }

        public override void LoadFromSaveData(SaveData saveData)
        {
            // ObjectPoolManager는 로드할 데이터가 없음 (런타임 풀 관리)
        }
    }
}
