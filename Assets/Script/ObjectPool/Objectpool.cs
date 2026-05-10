using UnityEngine;
using System;
using System.Collections.Generic;

public interface IObjectPool
{
    void Release(IPooledObject obj);
}

public interface IObjectPool<T> : IObjectPool where T : IPooledObject
{
    int Count { get; }
    T CreatePooledObject();
    T GetPooledObject();
    void Release(T obj);
}

public class Objectpool<T> : IObjectPool<T> where T : MonoBehaviour, IPooledObject
{
    public int initPoolSize = 0;
    private readonly Transform parent;
    private readonly T prefab;
    private readonly Queue<T> queue;
    public int Count { get => queue.Count; }

    public Objectpool(T prefab, Transform parent, int poolSize)
    {
        this.parent = parent;
        this.prefab = prefab;
        initPoolSize = poolSize;
        queue = new Queue<T>();

        SetupPool();
    }

    private void SetupPool()
    {
        if (parent == null || prefab == null)
        {
            Debug.LogError("Parent or Prefab is null. Cannot setup pool.");
            return;
        }

        for (int i = 0; i < initPoolSize; i++)
        {
            T obj = CreatePooledObject();
            queue.Enqueue(obj);
        }
    }

    public T CreatePooledObject()
    {
        T instance = UnityEngine.Object.Instantiate(prefab.gameObject, parent).GetComponent<T>();
        instance.gameObject.SetActive(false);
        instance.transform.localPosition = Vector3.zero;
        instance.Pool = this;
        return instance;
    }

    public T GetPooledObject()
    {
        T nextObject;
        if (queue.Count == 0) nextObject = CreatePooledObject();
        else nextObject = queue.Dequeue();
        nextObject.gameObject.SetActive(true);
        return nextObject;
    }

    public void Release(T obj)
    {
        if (obj == null) return;
        obj.gameObject.SetActive(false);
        queue.Enqueue(obj);
    }

    public void Release(IPooledObject obj)
    {
        if (obj is T genericObj)
        {
            Release(genericObj);
        }
    }
}
