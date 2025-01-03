using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


public partial struct MoveAgentSystem : ISystem
{
    [BurstCompile]
    public void OnCreate (ref SystemState state)
    {
        state.RequireForUpdate<ManagerComponent>();
        state.RequireForUpdate<TileMapComponent>();
    }

    [BurstCompile]
    public void OnDestroy (ref SystemState state)
    {

    }

    [BurstCompile]
    public void OnUpdate (ref SystemState state)
    {
        var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        var mapTileComponent = SystemAPI.GetSingleton<TileMapComponent>();
        var map = mapTileComponent.map;

        float deltaTime = SystemAPI.Time.DeltaTime;

        // move the entity
        var parallelUpdateJob = new MoveAgentParallelJob()
        {
            m_EntityCommandBuffer = ecb.AsParallelWriter(),
            m_AgentPathComponentLookup = SystemAPI.GetComponentLookup<AgentPathComponent>(true),
            m_Tiles = map,
            m_MapTileDimension = mapTileComponent.mapDimention,
            m_MoveSpeed = 0.1f,
            m_DeltaTime = deltaTime
        };

        var jobHandle = parallelUpdateJob.ScheduleParallel(state.Dependency);
        jobHandle.Complete();
    }
}

[BurstCompile]
public partial struct MoveAgentParallelJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter m_EntityCommandBuffer;

    [ReadOnly]
    [NativeDisableParallelForRestriction]
    [NativeDisableContainerSafetyRestriction]
    public ComponentLookup<AgentPathComponent> m_AgentPathComponentLookup;

    [ReadOnly] public NativeArray<TileData> m_Tiles;
    [ReadOnly] public int2 m_MapTileDimension;
    [ReadOnly] public float m_MoveSpeed;
    [ReadOnly] public float m_DeltaTime;


    [BurstCompile]
    void Execute ([ChunkIndexInQuery] int chunkIndex, AgentParallelAspect agent)
    {
        //UnityEngine.Debug.Log("Parallel - Moving");
        agent.Move(chunkIndex, m_EntityCommandBuffer, m_AgentPathComponentLookup, m_Tiles, m_MapTileDimension, m_MoveSpeed);
    }
}