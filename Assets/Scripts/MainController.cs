using NUnit.Framework;
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
    [SerializeField] private Button undoObstacleButton;
    [SerializeField] private Transform obstaclePointTransform;

    [SerializeField] private Vector2 worldPosition;
    [SerializeField] private Vector2Int tilePosition;

    [SerializeField] private State currentState;

    private Stack<ObstaclesData> obstaclesStack;

    private Plane groundPlane;
    private EntityManager entityManager;
    private EntityQuery obstaclePrefabQuery;

    private Vector2 INVALID_FLOAT2;
    private Vector2Int INVALID_INT2;

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
        undoObstacleButton.onClick.AddListener(() => OnUndoObstacleButtonClicked());
    }

    private void Start ()
    {
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        obstaclePrefabQuery = entityManager.CreateEntityQuery(new ComponentType[] { typeof(ObstacleEntityPrefab) });
    }

    private void OnDestroy ()
    {
        createObstacleButton.onClick.RemoveAllListeners();
        undoObstacleButton.onClick.RemoveAllListeners();
    }

    private void Update ()
    {
        GetWorldPositions();
        GetObstaclePositions();



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

                if (obstaclesStack.TryPeek(out var obstacleData))
                {
                    obstacleData.vertices.xy = worldPosition;
                    currentState = State.SelectingObstaclePoint_B;
                }
            }
        }
        else if (currentState == State.SelectingObstaclePoint_B)
        {
            obstaclePointTransform.gameObject.SetActive(!(tilePosition == INVALID_INT2));

            var obstaclePointPosition = new Vector3(worldPosition.x, 0.2f, worldPosition.y);
            obstaclePointTransform.position = obstaclePointPosition;

            if (obstaclesStack.TryPeek(out var obstacleData))
            {
                obstacleData.vertices.zw = worldPosition;

                var middlePosition = math.lerp(obstacleData.vertices.zw, obstacleData.vertices.xy, 0.5f);
                var distance = math.distance(obstacleData.vertices.zw, obstacleData.vertices.xy);

                var localTransform = entityManager.GetComponentData<LocalTransform>(obstacleData.entity);
                localTransform.Position = new float3(middlePosition.x, 0.2f, middlePosition.y);
                entityManager.SetComponentData(obstacleData.entity, localTransform);
            }
        }
    }

    private void GetWorldPositions ()
    {
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);

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

    private void OnUndoObstacleButtonClicked ()
    {
        CancelObstacleCreation();


    }

    private void CancelObstacleCreation ()
    {
        if (obstaclesStack.TryPop(out var obstacleData))
        {
            entityManager.DestroyEntity(obstacleData.entity);
            Debug.Log("Obstacle Destroyed");
            obstacleData = null;
        }

        currentState = State.None;

        obstaclePointTransform.gameObject.SetActive(false);
    }

    private void CreateNewObstacle ()
    {
        var obstacleEntity = entityManager.Instantiate(obstaclePrefabQuery.GetSingleton<ObstacleEntityPrefab>().obstacle);
        entityManager.SetName(obstacleEntity, "Obstacle" + obstaclesStack.Count);
        Debug.Log("Obstacle Created");
        var obstacleData = new ObstaclesData { entity = obstacleEntity, vertices = int4.zero };
        obstaclesStack.Push(obstacleData);

        currentState = State.SelectingObstaclePoint_A;
    }
}

public class ObstaclesData
{
    public float4 vertices;
    public Entity entity;
}

public enum State
{
    None, SelectingObstaclePoint_A, SelectingObstaclePoint_B
}