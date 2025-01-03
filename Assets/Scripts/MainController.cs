using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainController : MonoBehaviour
{
    [SerializeField] private Transform groundMap;
    [SerializeField] private MapSO mapSettings;
    [SerializeField] private TMP_Text agentCountText;
    [SerializeField] private Button createObstacleButton;
    [SerializeField] private Button destroyObstacleButton;
    [SerializeField] private Slider obstacleWidthSlider;
    [SerializeField] private TMP_Text obstacleWidthText;
    [SerializeField] private Button spawnAgentButton;
    [SerializeField] private Button destroyAgentButton;
    [SerializeField] private TMP_Text agentSpawnCountText;
    [SerializeField] private Slider agentSpawnCountSlider;
    [SerializeField] private TMP_Text pathComputePerFrameCountText;
    [SerializeField] private Slider pathComputePerFrameSlider;
    [SerializeField] private Transform obstaclePointTransform;
    [SerializeField] private Obstacle obstaclePrefab;
    [SerializeField] private float obstacleHeight = 5f;
    [SerializeField] private float obstacleWidth;

    [SerializeField] private Vector2 worldPosition;
    [SerializeField] private Vector2Int tilePosition;

    [SerializeField] private State currentState;

    private int agentCount = 0;
    private int agentToSpawnAmount = 1;
    private int pathComputePerFrameCount = 8;

    private Stack<Obstacle> obstaclesStack;

    private Plane groundPlane;
    private EntityManager entityManager;
    private EntityQuery tileMapQuery;
    private EntityQuery managerQuery;

    private Ray ray;

    private Vector2 INVALID_FLOAT2;
    private Vector2Int INVALID_INT2;

    private bool isDestroyingObstacle;
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
        destroyObstacleButton.onClick.AddListener(() => OnDestroyObstacleButtonClicked());
        obstacleWidthSlider.onValueChanged.AddListener((float v) => OnObstacleWidthChanged(v));

        spawnAgentButton.onClick.AddListener(() => OnSpawnAgentButtonClicked());
        destroyAgentButton.onClick.AddListener(() => OnRemoveAgentButtonClicked());
        agentSpawnCountSlider.onValueChanged.AddListener((float v) => OnAgentSpawnCountChanged(v));

        pathComputePerFrameSlider.onValueChanged.AddListener((float v) => OnPathComputePerFrameCountChanged(v));

        obstacleWidth = (int)obstacleWidthSlider.value;
        obstacleWidthText.text = "Obstacle Width: " + obstacleWidth.ToString();

        agentToSpawnAmount = (int)agentSpawnCountSlider.value;
        agentSpawnCountText.text = "Spawn nº: " + agentToSpawnAmount.ToString();

        pathComputePerFrameCount = (int)pathComputePerFrameSlider.value;
        pathComputePerFrameCountText.text = "PerFrame Compute Path Count: " + pathComputePerFrameCount.ToString();
    }

    private void Start ()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        tileMapQuery = entityManager.CreateEntityQuery(new ComponentType[] { typeof(TileMapComponent) });
        managerQuery = entityManager.CreateEntityQuery(new ComponentType[] { typeof(ManagerComponent) });

        agentCountText.text = "Agent Count: " + agentCount.ToString();

        isDestroyingObstacle = false;
        computeObstacle = false;
    }

    private void OnDestroy ()
    {
        createObstacleButton.onClick.RemoveAllListeners();
        destroyObstacleButton.onClick.RemoveAllListeners();
        obstacleWidthSlider.onValueChanged.RemoveAllListeners();

        spawnAgentButton.onClick.RemoveAllListeners();
        destroyAgentButton.onClick.RemoveAllListeners();
        agentSpawnCountSlider.onValueChanged.RemoveAllListeners();

        pathComputePerFrameSlider.onValueChanged.RemoveAllListeners();
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
        if (Input.GetKeyDown(KeyCode.F1))
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

    private void OnObstacleWidthChanged (float value)
    {
        obstacleWidth = (int)value;
        obstacleWidthText.text = "Obstacle Width: " + obstacleWidth.ToString();
    }

    private void OnAgentSpawnCountChanged (float value)
    {
        agentToSpawnAmount = (int)value;
        agentSpawnCountText.text = "Spawn nº: " + agentToSpawnAmount.ToString();
    }

    private void OnPathComputePerFrameCountChanged (float value)
    {
        pathComputePerFrameCount = (int)value;
        pathComputePerFrameCountText.text = "PerFrame Compute Path Count: " + pathComputePerFrameCount.ToString();

        if (managerQuery.HasSingleton<ManagerComponent>())
        {
            var managerComponent = managerQuery.GetSingletonRW<ManagerComponent>();
            managerComponent.ValueRW.maxPathsComputedPerFrame = pathComputePerFrameCount;
        }
    }

    private void GetObstacleRemove ()
    {
        if (currentState != State.None)
        {
            return;
        }

        if (isDestroyingObstacle && Input.GetMouseButtonDown(0))
        {
            if (Physics.Raycast(ray, out var hitInfo)) 
            {
                currentObstacleBounds = hitInfo.collider.gameObject.GetComponent<Obstacle>().Bounds;
                computeObstacle = true;
                Object.Destroy(hitInfo.collider.gameObject);
            }

            isDestroyingObstacle = false;
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

    private void OnDestroyObstacleButtonClicked ()
    {
        CancelObstacleCreation();
        isDestroyingObstacle = true;
    }

    private void CancelObstacleCreation ()
    {
        isDestroyingObstacle = false;

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

    private void OnSpawnAgentButtonClicked ()
    {
        var managerComponent = managerQuery.GetSingletonRW<ManagerComponent>();
        managerComponent.ValueRW.spawnAgentCount = agentToSpawnAmount;
        agentCount += agentToSpawnAmount;
        agentCountText.text = "Agent Count: " + agentCount.ToString();
    }

    private void OnRemoveAgentButtonClicked ()
    {
        var managerComponent = managerQuery.GetSingletonRW<ManagerComponent>();
        managerComponent.ValueRW.spawnAgentCount = 0;
        managerComponent.ValueRW.removeAgents = true;
        agentCount = 0;
        agentCountText.text = "Agent Count: " + agentCount.ToString();
    }
}

public enum State
{
    None, SelectingObstaclePoint_A, SelectingObstaclePoint_B
}