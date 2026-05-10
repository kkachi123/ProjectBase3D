using UnityEngine;
using System;

public interface IPooledObject
{
    IObjectPool Pool { get; set; }
    GameObject gameObject { get; }
}

public class BulletController : MonoBehaviour, IPooledObject
{
    [SerializeField] protected float moveSpeed;
    [SerializeField] protected int damage = 20;

    [SerializeField] protected Rigidbody rb;
    [SerializeField] protected float destroyDistance;
    protected float distance;
    public Vector3 direction;

    public IObjectPool Pool { get; set; }

    protected virtual void FixedUpdate()
    {
        transform.Translate(direction * moveSpeed * Time.deltaTime, Space.World);
        distance += moveSpeed * Time.deltaTime;
        if (distance > destroyDistance)
        {
		    // 일정 거리 이동 후 Release
            ReturnToPool();
        }
    }
    private void ReturnToPool()
    {
        distance = 0;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        Pool?.Release(this); // Release 시 풀에 객체 반환
    }


    protected virtual void OnTriggerEnter(Collider other)
	{
        // Hit시 Release
        ReturnToPool();
    }
}
