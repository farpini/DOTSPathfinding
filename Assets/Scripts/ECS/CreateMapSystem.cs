using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public partial struct CreateMapSystem : ISystem, ISystemStartStop
{
    // get maincontroller just to get Map settings in a straight way
    private static MainController MainController;

    private Entity m_MapEntity;
    private TileMapComponent m_TileMapComponent;

    private bool oneHit;


    public void OnCreate (ref SystemState state)
    {
        oneHit = false;
    }

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
        }
    }

    private void CreateMap (ref SystemState state, int2 mapDimention)
    {
        m_MapEntity = state.EntityManager.CreateEntity(state.EntityManager.CreateArchetype(typeof(TileMapComponent)));
        state.EntityManager.SetName(m_MapEntity, "MapEntity");
        var mapTiles = new NativeArray<TileData>(mapDimention.x * mapDimention.y, Allocator.Persistent);
        var tileIndex = 0;
        for (int i = 0; i < mapDimention.x; i++)
        {
            for (int j = 0; j < mapDimention.y; j++)
            {
                mapTiles[tileIndex++] = new TileData { position = new int2(i, j), obstacle = false };
            }
        }
        m_TileMapComponent = new TileMapComponent { map = mapTiles, mapDimention = mapDimention };
        SystemAPI.SetComponent(m_MapEntity, m_TileMapComponent);
    }

    public void OnStopRunning (ref SystemState state)
    {
        
    }

    public void OnUpdate (ref SystemState state)
    {
        /*
        if (!oneHit)
        {
            var obstacleEntity = state.EntityManager.Instantiate(SystemAPI.GetSingleton<ObstacleEntityPrefab>().obstacle);
            state.EntityManager.SetName(obstacleEntity, "ObstacleaaaaADWDAWD");
            oneHit = true;
        }
        */
    }
}