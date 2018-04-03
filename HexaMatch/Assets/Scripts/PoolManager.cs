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

    public Transform canvas_transform;
    public List<Pool> pools;
    public List<Pool> ui_object_pools;

    private Dictionary<string, List<GameObject>> pool_dictionary;
    private Dictionary<string, List<GameObject>> ui_object_pool_dictionary;
    private Transform pool_holder;

    private void Awake()
    {
        pool_holder = new GameObject("PoolHolder").transform;
        pool_holder.SetParent(this.transform);

        pool_dictionary = new Dictionary<string, List<GameObject>>();
        ui_object_pool_dictionary = new Dictionary<string, List<GameObject>>();

        foreach (Pool pool in pools)
        {
            pool_dictionary.Add(pool.tag, InitializePoolObjects(pool, false));
        }

        foreach (Pool ui_object_pool in ui_object_pools)
        {
            ui_object_pool_dictionary.Add(ui_object_pool.tag, InitializePoolObjects(ui_object_pool, true));
        }
    }

    private List<GameObject> InitializePoolObjects(Pool pool, bool ui_object_pool)
    {
        List<GameObject> object_pool = new List<GameObject>();

        for (int i = 0; i < pool.min_size; i++)
        {
            Transform initial_parent = ui_object_pool ? canvas_transform : pool_holder;
            GameObject new_object = Instantiate(pool.prefab, initial_parent);
            new_object.SetActive(false);
            object_pool.Add(new_object);
        }

        //print("Initialized pool for objects with tag " + pool.tag);
        return object_pool;
    }

    private GameObject AddObjectToPool(string tag, bool ui_object_pool)
    {
        Transform initial_parent = ui_object_pool ? canvas_transform : pool_holder;
        List<Pool> pool_list = ui_object_pool ? ui_object_pools : pools;
        GameObject object_to_spawn = null;

        for (int j = 0; j < pool_list.Count; j++)
        {
            if (pool_list[j].tag == tag)
            {
                object_to_spawn = Instantiate(pool_list[j].prefab, initial_parent);
                object_to_spawn.SetActive(false);
                break;
            }
        }

        if (object_to_spawn != null)
        {
            if (ui_object_pool)
                ui_object_pool_dictionary[tag].Add(object_to_spawn);
            else
                pool_dictionary[tag].Add(object_to_spawn);
        }
        else
        {
            Debug.LogWarning("No pool with tag " + tag + " was found when trying to create and add a new object to pool.");
        }

        return object_to_spawn;
    }

    private GameObject FindAvailableObjectFromPool(string tag, bool ui_object_pool)
    {
        List<GameObject> object_pool = ui_object_pool ? ui_object_pool_dictionary[tag] : pool_dictionary[tag];

        for (int i = 0; i < object_pool.Count; i++)
        {
            if (object_pool[i].activeSelf == false)
            {
                return object_pool[i];
            }
        }

        return null;
    }

    public GameObject SpawnFromPool(string tag)
    {
        if (pool_dictionary.ContainsKey(tag))
        {
            //print("Non-UI object pool found with tag " + tag + ".");
            //Find available object from pool
            GameObject object_from_pool = FindAvailableObjectFromPool(tag, false);
            if (object_from_pool != null)
            {
                return object_from_pool;
            }

            //If no objects available in pool, create and add a new one to the pool
            object_from_pool = AddObjectToPool(tag, false);
            if (object_from_pool != null)
            {
                return object_from_pool;
            }
        }
        else if (ui_object_pool_dictionary.ContainsKey(tag))
        {

            //print("UI object pool found with tag " + tag + ".");
            //Find available object from pool
            GameObject object_from_pool = FindAvailableObjectFromPool(tag, true);
            if (object_from_pool != null)
            {
                return object_from_pool;
            }

            //If no objects available in pool, create and add a new one to the pool
            object_from_pool = AddObjectToPool(tag, true);
            if (object_from_pool != null)
            {
                return object_from_pool;
            }
        }
        else
        {
            Debug.LogWarning("Pool with tag " + tag + " does not exist.");
        }

        return null;
    }

}
