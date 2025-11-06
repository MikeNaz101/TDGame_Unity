using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

public class JTowerPlacingManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject towerPrefab;
    [SerializeField] private GameObject towerPreviewPrefab;
    [SerializeField] private Camera mainCam;

    [Header("Settings")]
    [SerializeField] private float rayDistance = 10f;
    [SerializeField] private LayerMask placementMask;

    private GameObject currentPreview;
    private bool canPlace = false;

    void Update()
    {
        GetInput();
    }

    private void GetInput()
    {
        if (Input.GetMouseButton(2))
        {
            HandlePreview();
        }
        else if (Input.GetMouseButtonUp(2))
        {
            CancelPreview();
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (currentPreview != null && canPlace)
            {
                PlaceTower();
            }
        }
    }

    private void HandlePreview()
    {
        Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
        Debug.DrawRay(ray.origin, ray.direction * rayDistance, Color.yellow);

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, placementMask))
        {
            if (currentPreview == null)
            {
                currentPreview = Instantiate(towerPreviewPrefab);
            }

            currentPreview.transform.position = hit.point;
            currentPreview.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

            var currentPreviewScript = currentPreview.GetComponent<TowerPreview>();
            canPlace = currentPreviewScript != null && currentPreviewScript.IsPlacable;
        }
    }

    void PlaceTower()
    {
        Instantiate(towerPrefab, currentPreview.transform.position, currentPreview.transform.rotation);
    }

    private void CancelPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }
    }
}
