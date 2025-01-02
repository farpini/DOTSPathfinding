using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainController : MonoBehaviour
{
    [SerializeField] private Transform groundMap;
    [SerializeField] private MapSO mapSettings;
    [SerializeField] private Button createObstacleButton;
    [SerializeField] private Transform obstaclePointTransform;
    [SerializeField] private Obstacle obstaclePrefab;
    [SerializeField] private float obstacleHeight = 5f;
    [SerializeField] private float obstacleWidth = 1f;

    [SerializeField] private Vector2 worldPosition;
    [SerializeField] private Vector2Int tilePosition;

    [SerializeField] private State currentState;

    private Stack<Obstacle> obstaclesStack;

    private Plane groundPlane;
    private EntityManager entityManager;
    private EntityQuery tileMapQuery;

    private Ray ray;

    private Vector2 INVALID_FLOAT2;
    private Vector2Int INVALID_INT2;

    private bool computeObstacle;
    private Bounds currentObstacleBounds;

    public Vector2Int MapDimension => mapSettings.mapDimension;



    private void Awake ()
    {
        groundMap.localScale = new Vector3(mapSettings.mapDimension.x, 0.1f, mapSettings.mapDimension.y);
        groundMap.position = new Vector3(mapSettings.mapDimension.x * 0.5f, 0.1f, mapSettings.mapDimension.y * 0.5f);

        groundPlane = new Plane(Vector3.up, Vector3.zero);
        worldPosition = Vector2.zero;
        tilePosition = Vector2Int.zero;
        currentState = State.None;

        INVALID_FLOAT2 = new Vector2(-1f, -1f);
        INVALID_INT2 = new Vector2Int(-1, -1);

        obstaclesStack = new();

        createObstacleButton.onClick.AddListener(() => OnCreateObstacleButtonClicked());

        //var scaleMatrix = float4x4.Scale(3444f, 1f, 1f);
        //Debug.Log(scaleMatrix);
        //var rotationMatrix = float4x4.RotateY(48f);
        //rotationMatrix.c3.x = 55f;
        //Debug.Log(rotationMatrix);
    }

    private void Start ()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        tileMapQuery = entityManager.CreateEntityQuery(new ComponentType[] { typeof(TileMapComponent) });

        computeObstacle = false;
    }

    private void OnDestroy ()
    {
        createObstacleButton.onClick.RemoveAllListeners();
    }

    private void Update ()
    {
        GetWorldPositions();
        GetObstaclePositions();
        GetObstacleRemove();

        DebugTileMap();

    }

    private void LateUpdate ()
    {
        if (computeObstacle)
        {
            ComputeObstacleBounds();
            computeObstacle = false;
        }
    }

    private void DebugTileMap ()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            var tileMapComponent = tileMapQuery.GetSingleton<TileMapComponent>();
            var tileDim = tileMapComponent.mapDimention;

            for (int i = 0; i < tileDim.x; i++)
            {
                for (int j = 0; j < tileDim.y; j++)
                {
                    var tilePos = new int2(i, j);
                    var tileIndex = tileMapComponent.GetTileIndexFromTilePosition(tilePos);
                    var obstacle = tileMapComponent.map[tileIndex].obstacle;
                    var p = new Vector3(tilePos.x + 0.5f, 0f, tilePos.y + 0.5f);
                    var pD = new Vector3(tilePos.x + 0.5f, 10f, tilePos.y + 0.5f);
                    Debug.DrawLine(p, pD, obstacle ? Color.red : Color.green, 5f);
                }
            }
        }
    }

    private void GetObstacleRemove ()
    {
        if (currentState != State.None)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Delete))
        {
            if (Physics.Raycast(ray, out var hitInfo)) 
            {
                currentObstacleBounds = hitInfo.collider.gameObject.GetComponent<Obstacle>().Bounds;
                computeObstacle = true;
                Object.Destroy(hitInfo.collider.gameObject);
            }
        }
    }

    private void GetObstaclePositions ()
    {
        if (currentState == State.SelectingObstaclePoint_A)
        {
            obstaclePointTransform.gameObject.SetActive(!(tilePosition == INVALID_INT2));

            var obstaclePointPosition = new Vector3(worldPosition.x, 0.2f, worldPosition.y);
            obstaclePointTransform.position = obstaclePointPosition;

            if (Input.GetMouseButtonUp(0))
            {
                if (EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                if (obstaclesStack.TryPeek(out var obstacle))
                {
                    obstacle.VerticeA = worldPosition;
                    currentState = State.SelectingObstaclePoint_B;
                }
            }
        }
        else if (currentState == State.SelectingObstaclePoint_B)
        {
            obstaclePointTransform.gameObject.SetActive(!(tilePosition == INVALID_INT2));

            var obstaclePointPosition = new Vector3(worldPosition.x, 0.2f, worldPosition.y);
            obstaclePointTransform.position = obstaclePointPosition;

            if (obstaclesStack.TryPeek(out var obstacle))
            {
                obstacle.VerticeB = worldPosition;

                var middlePosition = math.lerp(obstacle.VerticeB, obstacle.VerticeA, 0.5f);
                obstacle.Position = new Vector3(middlePosition.x, obstacleHeight * 0.5f, middlePosition.y);

                var distance = math.distance(obstacle.VerticeB, obstacle.VerticeA);
                obstacle.Scale = new Vector3(distance, obstacleHeight, obstacleWidth);

                var angle = math.atan2(obstacle.VerticeB.x - obstacle.VerticeA.x, obstacle.VerticeB.y - obstacle.VerticeA.y);
                angle = math.radians(math.degrees(angle) + 90f);
                obstacle.Rotation = quaternion.AxisAngle(new float3(0f, 1f, 0f), angle);
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                currentState = State.None;

                currentObstacleBounds = obstacle.Bounds;
                computeObstacle = true;

                obstaclePointTransform.gameObject.SetActive(false);
            }
        }
    }

    private void ComputeObstacleBounds ()
    {
        var tileMapComponent = tileMapQuery.GetSingleton<TileMapComponent>();
        var tileMap = tileMapComponent.map;

        var min = new Vector2(currentObstacleBounds.min.x, currentObstacleBounds.min.z);
        var max = new Vector2(currentObstacleBounds.max.x, currentObstacleBounds.max.z);

        var tileMin = new Vector2Int(Mathf.FloorToInt(min.x), Mathf.FloorToInt(min.y));
        var tileMax = new Vector2Int(Mathf.CeilToInt(max.x), Mathf.CeilToInt(max.y));

        for (int i = tileMin.x; i < tileMax.x; i++)
        {
            for (int j = tileMin.y; j < tileMax.y; j++)
            {
                var tilePos = new int2(i, j);

                var tileIndex = tileMapComponent.GetTileIndexFromTilePosition(tilePos);
                var tileData = tileMap[tileIndex];
                tileData.obstacle = Physics.CheckSphere(new Vector3(tilePos.x + 0.5f, 0f, tilePos.y + 0.5f), 0.5f);
                tileMap[tileIndex] = tileData;
            }
        }
    }

    private void GetWorldPositions ()
    {
        ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        var intersectPoint = math.up();

        if (groundPlane.Raycast(ray, out float distance))
        {
            intersectPoint = ray.GetPoint(distance);
        }

        if (intersectPoint.y < 1f && intersectPoint.x >= 0f && intersectPoint.x < mapSettings.mapDimension.x &&
            intersectPoint.z >= 0f && intersectPoint.z < mapSettings.mapDimension.y)
        {
            worldPosition = new Vector2(intersectPoint.x, intersectPoint.z);
            tilePosition = new Vector2Int((int)worldPosition.x, (int)worldPosition.y);
        }
        else
        {
            worldPosition = INVALID_FLOAT2;
            tilePosition = INVALID_INT2;
        }
    }

    private void OnCreateObstacleButtonClicked ()
    {
        CancelObstacleCreation();
        CreateNewObstacle();
    }

    private void CancelObstacleCreation ()
    {
        if (currentState == State.None)
        {
            return;
        }

        if (obstaclesStack.TryPop(out var obstacle))
        {
            Object.Destroy(obstacle.gameObject);
            Debug.Log("Obstacle Destroyed");
            obstacle = null;
        }

        currentState = State.None;

        obstaclePointTransform.gameObject.SetActive(false);
    }

    private void CreateNewObstacle ()
    {
        var obstacle = Instantiate(obstaclePrefab, transform);
        obstaclesStack.Push(obstacle);
        currentState = State.SelectingObstaclePoint_A;
    }
}

public enum State
{
    None, SelectingObstaclePoint_A, SelectingObstaclePoint_B
}