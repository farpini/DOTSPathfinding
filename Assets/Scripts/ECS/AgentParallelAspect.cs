using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

public readonly partial struct AgentParallelAspect : IAspect
{
    public readonly Entity Entity;

    private readonly RefRW<LocalTransform> agentTransform;
    private readonly RefRW<AgentComponent> agentData;
    private readonly RefRO<AgentPathExecuting> agentPathExecuting;

    private const float closePosition = 0.05f;

    public float2 AgentPosition
    {
        get => agentTransform.ValueRO.Position.xz;
        set => agentTransform.ValueRW.Position.xz = value;
    }

    public int2 AgentTilePosition
    {
        get => agentData.ValueRO.currentPosition;
        set => agentData.ValueRW.currentPosition = value;
    }

    public int2 AgentGoalPosition
    {
        get => agentData.ValueRO.goalPosition;
        set => agentData.ValueRW.goalPosition = value;
    }

    public ushort AgentPathIndex
    {
        get => agentData.ValueRO.currentPathIndex;
        set => agentData.ValueRW.currentPathIndex = value;
    }

    public ushort AgentPathLength
    {
        get => agentData.ValueRO.pathLength;
        set => agentData.ValueRW.pathLength = value;
    }

    public MoveState MoveState
    {
        get => agentData.ValueRO.moveState;
        set => agentData.ValueRW.moveState = value;
    }

    public int PathNextWaypoint
    {
        get => agentData.ValueRO.reversePath ? -1 : 1;
    }

    [BurstCompile]
    private float2 GetPositionFromTilePosition (int2 tilePosition)
    {
        return (new float2(tilePosition.x, tilePosition.y) + 0.5f);
    }

    [BurstCompile]
    private bool CheckClosePosition (float2 p1, float2 p2)
    {
        return ((p1.x > (p2.x - closePosition)) && (p1.x < (p2.x + closePosition)) &&
            (p1.y > (p2.y - closePosition)) && (p1.y < (p2.y + closePosition)));
    }

    [BurstCompile]
    public void Move (int chunkIndex, EntityCommandBuffer.ParallelWriter ecb, ComponentLookup<AgentPathComponent> agentPathLookUp,
        NativeArray<TileData> tiles, int2 mapTileDimension, float moveSpeed)
    {
        // check path length
        if (AgentPathLength == 0)
        {
            EndMovement(chunkIndex, ecb);
            return;
        }

        // get the path
        var path = agentPathLookUp.GetRefRO(Entity).ValueRO.Path;

        // get the current path index
        ushort pathIndex = AgentPathIndex;

        // get the path position
        int2 pathTilePosition = path[pathIndex];

        // get the target position
        float2 targetPosition = GetPositionFromTilePosition(pathTilePosition);

        // move the agent
        float2 endPosition = MoveTowards(AgentPosition, targetPosition, moveSpeed);

        // end tile position
        AgentTilePosition = (int2)new float2(math.round(endPosition));

        // set the end position
        AgentPosition = endPosition;

        // check if agent achieve the tile center
        if (math.abs(endPosition.x - targetPosition.x) < 0.1f && math.abs(endPosition.y - targetPosition.y) < 0.1f)
        {
            // get the next path index from the path
            pathIndex = (ushort)(pathIndex + PathNextWaypoint);

            // check the path limits, so it reaches the goal
            if (pathIndex < 2 || pathIndex >= path.Length)
            {
                AgentTilePosition = AgentGoalPosition;

                AgentPathIndex = 0;
                AgentPathLength = 0;

                path.Dispose();

                EndMovement(chunkIndex, ecb);
            }

            // set the new path index
            AgentPathIndex = pathIndex;
        }
    }

    [BurstCompile]
    private float2 MoveTowards (float2 current, float2 target, float maxDistanceDelta)
    {
        float num = target.x - current.x;
        float num2 = target.y - current.y;
        float num3 = num * num + num2 * num2;
        if (num3 == 0f || (maxDistanceDelta >= 0f && num3 <= maxDistanceDelta * maxDistanceDelta))
        {
            return target;
        }

        float num4 = (float)math.sqrt(num3);
        return new float2(current.x + num / num4 * maxDistanceDelta, current.y + num2 / num4 * maxDistanceDelta);
    }

    [BurstCompile]
    private bool IsWalkable (int2 tilePosition, int2 mapTileDimension, NativeArray<TileData> tiles)
    {
        int index = tilePosition.x * mapTileDimension.y + tilePosition.y;
        return !tiles[index].obstacle;
    }

    [BurstCompile]
    private void EndMovement (int chunkIndex, EntityCommandBuffer.ParallelWriter ecb)
    {
        ecb.SetComponentEnabled<AgentPathExecuting>(chunkIndex, Entity, false);
        MoveState = MoveState.RequestPath;
    }
}