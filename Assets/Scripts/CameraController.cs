using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    [HideInInspector]
    public static CameraController Instance = null;

    private Transform rotationTarget;
    private Transform target;

    private Vector3 lastPosition = Vector3.zero;

    private Plane horizontalPlane;
    private Vector3 lastWorldPosition = Vector3.zero;

    [SerializeField] private MapSO mapSettings;

    // Zooming
    private float zoomDistance;
    [SerializeField] private float zoomStartDistance = 200f;//1000f;
    [SerializeField] private float zoomMinDistance = 25f; //100f;
    [SerializeField] private float zoomMaxDistance = 200f; //1000f;
    [SerializeField] private float zoomSpeed = 20f; //50f;

    // Panning
    //[SerializeField] private float panSpeed = 0.5f; //0.5f;

    // Orbiting
    [SerializeField] private float orbitStartRotation = 1f;
    [SerializeField] private float orbitMinRotation = 1;
    [SerializeField] private float orbitMaxRotation = 50f;
    [SerializeField] private float orbitSpeed = 0.5f;


    private void Awake ()
    {
        if (Instance != null)
        {
            Debug.LogError("class CameraController Awake : should not have more than one CameraController.");
            return;
        }

        Instance = this;

        rotationTarget = transform.parent;
        target = rotationTarget.transform.parent;

        lastPosition = Vector3.zero;

        rotationTarget.localEulerAngles = new Vector3(orbitStartRotation, 0.0f, 0.0f);
        transform.localPosition = new Vector3(0f, zoomStartDistance, 0f);

        zoomDistance = Vector3.Distance(target.position, transform.position);
    }

    private void Start ()
    {
        target.position = new Vector3(mapSettings.mapDimension.x * 0.5f, 0f, mapSettings.mapDimension.y * 0.5f);
        target.eulerAngles = Vector3.up * 180.0f;

        //mapPlane = new Plane(new Vector3(0.0f, 0f, 0.0f), new Vector3(mapUnitDimension.x, 0f, 0.0f), new Vector3(mapUnitDimension.x, 0f, mapUnitDimension.y));
        horizontalPlane = new Plane(Vector3.up, Vector3.zero);
    }

    private void Update ()
    {
        transform.LookAt(target);

        if (!IsOverUI())
        {
            Zooming();
            Panning();
            Orbiting();
        }
    }

    private bool IsOverUI ()
    {
        return EventSystem.current.IsPointerOverGameObject();
    }

    private void Panning ()
    {
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (horizontalPlane.Raycast(ray, out float rayDistance))
            {
                Vector3 rayPoint = ray.GetPoint(rayDistance);
                lastWorldPosition = new Vector3(rayPoint.x, 0f, rayPoint.z);
            }
        }

        if (Input.GetMouseButton(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (horizontalPlane.Raycast(ray, out float rayDistance))
            {
                Vector3 rayPoint = ray.GetPoint(rayDistance);
                target.position += (lastWorldPosition - new Vector3(rayPoint.x, target.position.y, rayPoint.z));
            }
        }
    }

    private void Orbiting ()
    {
        if (Input.GetMouseButtonDown(2))
        {
            lastPosition = Input.mousePosition;
        }

        if (Input.GetMouseButton(2))
        {
            Vector3 deltaPosition = Input.mousePosition - lastPosition;

            float angleY = deltaPosition.y * orbitSpeed;
            float angleX = deltaPosition.x * orbitSpeed;

            Vector3 orbitAngles = rotationTarget.transform.eulerAngles;

            orbitAngles.x += angleY;
            orbitAngles.x = Mathf.Clamp(orbitAngles.x, orbitMinRotation, orbitMaxRotation);

            rotationTarget.transform.eulerAngles = orbitAngles;

            target.transform.RotateAround(target.position, Vector3.up, angleX);

            lastPosition = Input.mousePosition;
        }
    }

    private void Zooming ()
    {
        float scrollDelta = Input.mouseScrollDelta.y;

        if (scrollDelta != 0f)
        {
            zoomDistance -= (scrollDelta * zoomSpeed);

            zoomDistance = Mathf.Clamp(zoomDistance, zoomMinDistance, zoomMaxDistance);

            Vector3 targetPosition = target.position;
            targetPosition -= transform.forward * zoomDistance;
            transform.position = Vector3.Lerp(transform.position, targetPosition, zoomSpeed);
        }
    }
}