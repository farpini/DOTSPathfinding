using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(PathFinderSystem))]
public partial struct ManageAgentSystem : ISystem, ISystemStartStop
{
    private Entity m_MapEntity;
    private Unity.Mathematics.Random random;

    private EntityQuery m_AgentQuery;


    [BurstCompile]
    public void OnCreate (ref SystemState state)
    {
        random = new Unity.Mathematics.Random();
        random.InitState(12034);

        state.RequireForUpdate<ManagerComponent>();
        state.RequireForUpdate<TileMapComponent>();
        state.RequireForUpdate<AgentPrefabComponent>();
    }

    [BurstCompile]
    public void OnDestroy (ref SystemState state)
    {

    }

    public void OnStartRunning (ref SystemState state)
    {
        m_MapEntity = SystemAPI.GetSingletonEntity<TileMapComponent>();
        m_AgentQuery = state.GetEntityQuery(typeof(AgentComponent));
    }

    [BurstCompile]
    public void OnStopRunning (ref SystemState state)
    {

    }

    [BurstCompile]
    public void OnUpdate (ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        var managerComponent = SystemAPI.GetSingletonRW<ManagerComponent>();

        if (managerComponent.ValueRO.removeAgents)
        {
            managerComponent.ValueRW.removeAgents = false;
            managerComponent.ValueRW.spawnAgentCount = 0;
            RemoveAgents(ref state, ecb);
        }
        else
        {
            if (managerComponent.ValueRO.spawnAgentCount > 0)
            {
                SpawnAgents(ref state, managerComponent.ValueRO.spawnAgentCount, ecb);
                managerComponent.ValueRW.spawnAgentCount = 0;
            }
        }
    }

    [BurstCompile]
    private void SpawnAgents (ref SystemState state, int agentCount, EntityCommandBuffer ecb)
    {
        var mapTileComponent = SystemAPI.GetSingleton<TileMapComponent>();
        var map = mapTileComponent.map;
        var agentPrefabComponent = SystemAPI.GetSingleton<AgentPrefabComponent>();
        var agentPrefabEntity = agentPrefabComponent.agentPrefabEntity;
        var agentEntities = state.EntityManager.Instantiate(agentPrefabEntity, agentCount, Allocator.Temp);
        var tileIndexMin = 0;
        var tileIndexMax = mapTileComponent.mapDimention.x * mapTileComponent.mapDimention.y;

        for (int i = 0; i < agentEntities.Length; i++)
        {
            var tileIndex = random.NextInt(tileIndexMin, tileIndexMax);
            while (map[tileIndex].obstacle)
            {
                tileIndex = random.NextInt(tileIndexMin, tileIndexMax);
            }
            var agentPosition = mapTileComponent.GetTilePositionFromTileIndex(tileIndex);
            SystemAPI.SetComponent(agentEntities[i], new AgentComponent 
            {  
                currentPosition = agentPosition,
                goalPosition = agentPosition,
                currentPathIndex = 0,
                pathLength = 0,
                reversePath = true,
                moveState = MoveState.RequestPath
            });
            var localTransform = SystemAPI.GetComponentRW<LocalTransform>(agentEntities[i]);
            localTransform.ValueRW.Position = new float3(agentPosition.x + 0.5f, agentPrefabComponent.agentHeight * 0.5f, agentPosition.y + 0.5f);
            ecb.AddComponent(agentEntities[i], new AgentPathComponent
            {
            });
        }
    }

    [BurstCompile]
    private void RemoveAgents (ref SystemState state, EntityCommandBuffer ecb)
    {
        ecb.DestroyEntity(m_AgentQuery, EntityQueryCaptureMode.AtPlayback);
    }
}