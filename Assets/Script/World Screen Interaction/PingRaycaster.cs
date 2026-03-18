using UnityEngine;

public class PingRaycaster : MonoBehaviour
{
    [SerializeField] private LayerMask groundLayer;

    [SerializeField] private Camera mainCamera;
    [SerializeField] private RectTransform pingMarker;

    private void Update()
    {
        if(Input.GetMouseButtonDown(0)) // Left mouse button click
        {
            if (TryGetPingPosition(Input.mousePosition, out Vector3 worldPos))
            {
                pingMarker.position = mainCamera.WorldToScreenPoint(worldPos);
            }
        }
    }

    public bool TryGetPingPosition(Vector2 screenPos, out Vector3 worldPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            worldPos = hit.point;
            return true;
        }

        worldPos = default;
        return false;
    }
}
