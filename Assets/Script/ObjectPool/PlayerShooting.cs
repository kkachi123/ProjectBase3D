using UnityEngine;
using System;
using System.Collections;

public class PlayerShooting : MonoBehaviour
{
    [Header("ObjectPool")]
    [SerializeField] BulletController bulletPrefab;
    [SerializeField] Transform _bulletParent;
    IObjectPool<BulletController> _bulletPool;
    [SerializeField] int InitBulletCount = 9;
    int bulletCount;
    [SerializeField] float shootingTerm;
    Coroutine shootingCoroutine;

    private void Start()
    {
        _bulletPool = new Objectpool<BulletController>(bulletPrefab, _bulletParent, InitBulletCount);
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.A))
        {
            Shoot();
        }
        
    }
    public void Shoot()
    {
        if (shootingCoroutine == null)
        {
            bulletCount = InitBulletCount;
            shootingCoroutine = StartCoroutine(Shooting());
        }
    }

    private IEnumerator Shooting()
    {
        while (bulletCount > 0)
        {
            BulletController bullet = _bulletPool.GetPooledObject();
            if (bullet != null)
            {
                // 총알 초기화 로직 (위치, 방향 등)
                bullet.gameObject.transform.position = _bulletParent.position; // 예시: 플레이어 위치에서 발사
                bullet.direction = transform.forward; // 예시: 플레이어가 바라보는 방향으로 발사
                bulletCount--;
            }
            yield return new WaitForSeconds(shootingTerm);
        }
        shootingCoroutine = null;
    }
}
