using UnityEngine;
using System.Collections;

public enum SiteType
{
    SectorA,
    SectorB,
    Max,
}

public class CursorUI : MonoBehaviour
{
    [SerializeField] Transform[] targetWorldPositions = new Transform[(int)SiteType.Max];
    [SerializeField] RectTransform[] targetCursorUI = new RectTransform[(int)SiteType.Max];
    [SerializeField] Camera mainCamera;

    #region Correct Code

    void Update()
    {
        if (mainCamera == null) return;
        for(int i = 0; i < (int)SiteType.Max; i++)
        {
            targetCursorUI[i].position = GetCursorScreenPosition(targetWorldPositions[i].position);
        }
    }

    public Vector2 GetCursorScreenPosition(Vector3 targetPos)
    {
        // x: 화면 가로 위치(0 ~1)
        // y: 화면 세로 위치(0 ~1)
        // z: 카메라로부터의 거리(depth)
        // z > 0 : 카메라 앞에 있음
        // z < 0 : 카메라 뒤에 있음 (뒤에 있을 때는 x, y를 반전시켜서 표시)

        Vector3 viewPos = mainCamera.WorldToViewportPoint(targetPos);

        if (viewPos.z < 0)
        {
            viewPos.x = 1 - viewPos.x;
            viewPos.y = 1 - viewPos.y;
        }

        // Clamp : 화면 밖으로 나가는 것을 방지하기 위해 0.05 ~ 0.95 범위로 제한
        viewPos.x = Mathf.Clamp(viewPos.x, 0.05f, 0.95f);
        viewPos.y = Mathf.Clamp(viewPos.y, 0.05f, 0.95f);
        return mainCamera.ViewportToScreenPoint(viewPos);
    }
    #endregion
    //#region MyOldCode
    //void Update()
    //{
    //    if (mainCamera == null) return;
    //    for (int i = 0; i < (int)SiteType.Max; i++)
    //    {
    //        targetCursorUI[i].position = GetCursorScreenPosition(targetWorldPositions[i].position);
    //    }
    //}
    //public Vector2 GetCursorScreenPosition(Vector3 targetPos)
    //{
    //    Vector3 dir = (targetPos - mainCamera.transform.position).normalized;
    //    Vector3 viewPos = mainCamera.WorldToViewportPoint(mainCamera.transform.position + dir);

    //    float dot = Vector3.Dot(mainCamera.transform.forward, dir);
    //    if (dot < 0)
    //    {
    //        viewPos.x = 1 - viewPos.x;
    //        viewPos.y = 1 - viewPos.y;
    //    }

    //    viewPos.x = Mathf.Clamp(viewPos.x, 0.05f, 0.95f);
    //    viewPos.y = Mathf.Clamp(viewPos.y, 0.05f, 0.95f);
    //    return mainCamera.ViewportToScreenPoint(viewPos);
    //}
    //#endregion
}
