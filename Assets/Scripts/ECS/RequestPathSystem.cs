using Unity.Burst;
using Unity.Entities;

[UpdateAfter(typeof(MoveAgentSystem))]
public partial struct RequestPathSystem : ISystem
{
    private Unity.Mathematics.Random random;

    [BurstCompile]
    public void OnCreate (ref SystemState state)
    {
        random = new Unity.Mathematics.Random();
        random.InitState(12034);

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

        var tileIndexMin = 0;
        var tileIndexMax = mapTileComponent.mapDimention.x * mapTileComponent.mapDimention.y;

        var pathRequestBuffer = SystemAPI.GetSingletonBuffer<PathRequestEventBuffer>();

        // generate a new path
        foreach ((var agentComponent, var agentEntity) in SystemAPI.Query<RefRW<AgentComponent>>()
            .WithDisabled<AgentPathExecuting>()
            .WithEntityAccess())
        {
            if (agentComponent.ValueRO.moveState == MoveState.RequestPath)
            {
                var tileIndex = random.NextInt(tileIndexMin, tileIndexMax);

                while (map[tileIndex].obstacle)
                {
                    tileIndex = random.NextInt(tileIndexMin, tileIndexMax);
                }

                var newGoalPosition = mapTileComponent.GetTilePositionFromTileIndex(tileIndex);
                agentComponent.ValueRW.goalPosition = newGoalPosition;

                if (!newGoalPosition.Equals(agentComponent.ValueRO.currentPosition))
                {
                    pathRequestBuffer.Add(new PathRequestEventBuffer
                    {
                        Value = new PathRequestData
                        {
                            origin = agentComponent.ValueRO.currentPosition,
                            goal = newGoalPosition,
                            entity = agentEntity
                        }
                    });

                    agentComponent.ValueRW.moveState = MoveState.WaitingPath;
                }

                //UnityEngine.Debug.LogWarning("Request " + newGoalPosition);
            }
            else if (agentComponent.ValueRO.moveState == MoveState.PathReady)
            {
                if (agentComponent.ValueRO.pathLength == 0)
                {
                    agentComponent.ValueRW.moveState = MoveState.RequestPath;
                }
                else
                {
                    agentComponent.ValueRW.moveState = MoveState.Moving;
                    ecb.SetComponentEnabled<AgentPathExecuting>(agentEntity, true);
                }
            }
        }
    }
}