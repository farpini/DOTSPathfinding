using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public partial struct CreateMapSystem : ISystem, ISystemStartStop
{
    // get maincontroller just to get Map settings in a straight way
    private static MainController MainController;

    private Entity m_MapEntity;
    private Entity m_ManagerEntity;

    public void OnCreate (ref SystemState state)
    {
        m_ManagerEntity = state.EntityManager.CreateEntity(state.EntityManager.CreateArchetype(typeof(ManagerComponent), typeof(PathRequestEventBuffer)));
        SystemAPI.SetComponent(m_ManagerEntity, new ManagerComponent
        {
            removeAgents = false,
            spawnAgentCount = 0,
            maxPathsComputedPerFrame = 8
        });
    }

    [BurstCompile]
    public void OnDestroy (ref SystemState state)
    {
        if (SystemAPI.TryGetSingleton<TileMapComponent>(out var mapComponent))
        {
            mapComponent.Dispose();
        }
    }

    public void OnStartRunning (ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<TileMapComponent>())
        {
            MainController = GameObject.Find("MainController").GetComponent<MainController>();
            CreateMap(ref state, new int2(MainController.MapDimension.x, MainController.MapDimension.y));
            state.EntityManager.SetName(m_MapEntity, "MapEntity");
        }
    }

    [BurstCompile]
    private void CreateMap (ref SystemState state, int2 mapDimention)
    {
        m_MapEntity = state.EntityManager.CreateEntity(state.EntityManager.CreateArchetype(typeof(TileMapComponent)));
        var mapTiles = new NativeArray<TileData>(mapDimention.x * mapDimention.y, Allocator.Persistent);
        var tileIndex = 0;
        for (int i = 0; i < mapDimention.x; i++)
        {
            for (int j = 0; j < mapDimention.y; j++)
            {
                mapTiles[tileIndex++] = new TileData { position = new int2(i, j), obstacle = false };
            }
        }
        SystemAPI.SetComponent(m_MapEntity, new TileMapComponent { map = mapTiles, mapDimention = mapDimention });
    }

    [BurstCompile]
    public void OnStopRunning (ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnUpdate (ref SystemState state)
    {
    }
}