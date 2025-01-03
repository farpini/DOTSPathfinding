using Unity.Entities;
using System;
using Unity.Collections;
using Unity.Mathematics;

public struct TileMapComponent : IComponentData, IDisposable
{
    public int2 mapDimention;
    public NativeArray<TileData> map;

    public void Dispose ()
    {
        map.Dispose();
    }

    public int GetTileIndexFromTilePosition (int2 tilePosition)
    {
        return tilePosition.x * mapDimention.y + tilePosition.y;
    }

    public int2 GetTilePositionFromTileIndex (int tileIndex)
    {
        return new int2(tileIndex / mapDimention.y, tileIndex % mapDimention.y);
    }
}

public struct TileData
{
    public int2 position;
    public bool obstacle;
}

public struct ManagerComponent : IComponentData
{
    public bool removeAgents;
    public int spawnAgentCount;
    public int maxPathsComputedPerFrame;
}

public struct AgentPrefabComponent : IComponentData
{
    public float agentHeight;
    public Entity agentPrefabEntity;
}

public struct AgentComponent : IComponentData
{
    public int2 goalPosition;
    public int2 currentPosition;
    public ushort currentPathIndex;
    public ushort pathLength;
    public bool reversePath;
    public MoveState moveState;
}

public enum MoveState
{
    RequestPath = 0,
    WaitingPath = 1,
    PathReady = 2,
    Moving = 3
}

public struct AgentPathExecuting : IComponentData, IEnableableComponent
{
}

public struct AgentPathComponent : IComponentData, IDisposable
{
    public NativeArray<int2> Path;

    public void Dispose ()
    {
        if (Path.IsCreated)
        {
            Path.Dispose();
        }
    }
}

[InternalBufferCapacity(32)]
public struct PathRequestEventBuffer : IBufferElementData
{
    public static implicit operator PathRequestData (PathRequestEventBuffer e) { return e.Value; }
    public static implicit operator PathRequestEventBuffer (PathRequestData e) { return new PathRequestEventBuffer { Value = e }; }

    public PathRequestData Value;
}

public struct PathRequestData
{
    public int2 origin;
    public int2 goal;
    public Entity entity;
}