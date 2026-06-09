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
        private Dictionary<string, Transform> categoryRoots;

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
            categoryRoots = new Dictionary<string, Transform>();

            if (pools == null)
            {
                pools = new List<Pool>();
            }

            foreach (Pool pool in pools)
            {
                if (pool.prefab == null) continue;
                CreateNewPool(pool.tag, pool.prefab, pool.size);
            }
        }

        public GameObject SpawnFromPool(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            if (poolDictionary == null)
            {
                Initialize();
            }

            string tag = prefab.name;

            if (!poolDictionary.ContainsKey(tag))
            {
                CreateNewPool(tag, prefab, 10);
            }

            return SpawnFromPool(tag, position, rotation);
        }

        private Transform GetCategoryRoot(string prefabName)
        {
            string category = "Etc";
            
            if (prefabName.StartsWith("Player_")) category = "Player";
            else if (prefabName.StartsWith("Enemy_")) category = "Enemy";
            else if (prefabName.StartsWith("Effect_")) category = "Effect";
            else if (prefabName.StartsWith("UI_")) category = "UI";
            else if (prefabName.StartsWith("Item_")) category = "Item";

            if (!categoryRoots.TryGetValue(category, out Transform categoryRoot))
            {
                GameObject catObj = new GameObject($"[Category] {category}");
                catObj.transform.SetParent(this.transform);
                categoryRoot = catObj.transform;
                categoryRoots.Add(category, categoryRoot);
            }

            return categoryRoot;
        }

        private void CreateNewPool(string tag, GameObject prefab, int size)
        {
            if (poolDictionary.ContainsKey(tag)) return;

            Transform categoryRoot = GetCategoryRoot(prefab.name);

            GameObject rootObj = new GameObject($"[Pool] {tag}");
            rootObj.transform.SetParent(categoryRoot);
            poolRoots.Add(tag, rootObj.transform);

            Queue<GameObject> objectPool = new Queue<GameObject>();
            for (int i = 0; i < size; i++)
            {
                GameObject obj = container != null ? container.Instantiate(prefab) : Instantiate(prefab);
                obj.transform.SetParent(rootObj.transform);
                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }

            // 중복 추가 방지
            if (!pools.Exists(p => p.tag == tag))
            {
                pools.Add(new Pool { tag = tag, prefab = prefab, size = size });
            }
            
            poolDictionary.Add(tag, objectPool);
        }

        public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
        {
            if (!poolDictionary.ContainsKey(tag))
            {
                Debug.LogWarning($"Pool with tag '{tag}' doesn't exist.");
                return null;
            }

            Pool pool = pools.Find(p => p.tag == tag);
            Vector3 originalScale = Vector3.one;
            if (pool != null && pool.prefab != null)
            {
                originalScale = pool.prefab.transform.localScale;
            }

            if (poolDictionary[tag].Count == 0)
            {
                if (pool != null && pool.prefab != null)
                {
                    GameObject newObj = container != null ? container.Instantiate(pool.prefab) : Instantiate(pool.prefab);
                    
                    if (poolRoots.TryGetValue(tag, out Transform root))
                    {
                        newObj.transform.SetParent(root);
                    }
                    
                    newObj.transform.position = position;
                    newObj.transform.rotation = rotation;
                    newObj.transform.localScale = originalScale;
                    newObj.SetActive(true);
                    return newObj;
                }
                return null;
            }

            GameObject obj = poolDictionary[tag].Dequeue();
            obj.SetActive(true);
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.transform.localScale = originalScale;
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
