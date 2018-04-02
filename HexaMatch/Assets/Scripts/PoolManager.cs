using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    [System.Serializable]
    public struct Pool
    {
        public string tag;
        public GameObject prefab;
        public int min_size;
    }
    
    public List<Pool> pools;
    public Dictionary<string, List<GameObject>> pool_dictionary;

    private Transform pool_holder;

    private void Awake()
    {
        pool_holder = new GameObject("PoolHolder").transform;
        pool_holder.SetParent(this.transform);

        pool_dictionary = new Dictionary<string, List<GameObject>>();

        foreach (Pool pool in pools)
        {
            List<GameObject> object_pool = new List<GameObject>();

            for (int i = 0; i < pool.min_size; i++)
            {
                GameObject new_object = Instantiate(pool.prefab);
                new_object.SetActive(false);
                new_object.transform.SetParent(pool_holder);
                object_pool.Add(new_object);
            }

            pool_dictionary.Add(pool.tag, object_pool);
        }
    }

    public GameObject SpawnFromPool(string tag)
    {
        if (pool_dictionary.ContainsKey(tag))
        {
            //print("Pool found with tag " + tag + ".");
            //Find available object from pool
            for (int i = 0; i < pool_dictionary[tag].Count; i++)
            {
                if (pool_dictionary[tag][i].activeSelf == false)
                {
                    return pool_dictionary[tag][i];
                }
            }

            //If no objects available in pool, create and add a new one to the pool
            for (int j = 0; j < pools.Count; j++)
            {
                if (pools[j].tag == tag)
                {
                    GameObject object_to_spawn = Instantiate(pools[j].prefab);
                    object_to_spawn.SetActive(false);
                    object_to_spawn.transform.SetParent(pool_holder);
                    pool_dictionary[tag].Add(object_to_spawn);
                    //print("No available objects in pool, creating a new one (pool size: " + pool_dictionary[tag].Count + ").");
                    return object_to_spawn;
                }
            }
        }
        else
        {
            Debug.LogWarning("Pool with tag " + tag + " does not exist.");
        }

        return null;
    }
}
