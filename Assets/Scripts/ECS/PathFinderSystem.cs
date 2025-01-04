using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateAfter(typeof(RequestPathSystem))]
public partial struct PathFinderSystem : ISystem, ISystemStartStop
{
    private Unity.Mathematics.Random random;

    private NativeQueue<PathRequestData> m_PathRequests;

    private NativeArray<int2> m_NeighbourPositions;
    private NativeArray<PathNode> m_Nodes;
    private NativeArray<int> m_HeapOpenSet;
    private NativeHashSet<int> m_CloseSet;

    private int2 m_MapDimension;

    private JobHandle m_PathFinderJobHandle;


    [BurstCompile]
    public void OnCreate (ref SystemState state)
    {
        m_PathRequests = new NativeQueue<PathRequestData>(Allocator.Persistent);

        random = new Unity.Mathematics.Random();
        random.InitState(50);

        state.RequireForUpdate<ManagerComponent>();
        state.RequireForUpdate<TileMapComponent>();
    }

    [BurstCompile]
    public void OnDestroy (ref SystemState state)
    {
        m_PathRequests.Dispose();

        if (m_NeighbourPositions.IsCreated) { m_NeighbourPositions.Dispose(); }

        if (m_HeapOpenSet.IsCreated)
        {
            m_CloseSet.Dispose();
            m_HeapOpenSet.Dispose();
            m_Nodes.Dispose();
        }
    }

    public void OnStartRunning (ref SystemState state)
    {
        m_MapDimension = SystemAPI.GetSingleton<TileMapComponent>().mapDimention;

        CreateNeighbourPositions();
        CreateGrid();
    }

    [BurstCompile]
    public void OnStopRunning (ref SystemState state)
    {

    }

    [BurstCompile]
    private void ExecutePathFinding (ref SystemState state, PathRequestData pathRequest)
    {
        var origin = pathRequest.origin;
        var goal = pathRequest.goal;

        // check if origin is equal to goal
        if (origin.Equals(goal))
        {
            NotifyPathRequester(ref state, pathRequest.entity, false, new NativeArray<int2>(0, Allocator.Temp));
            return;
        }

        var mapTileComponent = SystemAPI.GetSingleton<TileMapComponent>();
        var map = mapTileComponent.map;

        NativeArray<int> result = new(1, Allocator.Persistent);
        NativeList<int2> resultWaypoints = new(Allocator.Persistent);

        AStarPathFindingJob aStarPathFindingJob = new()
        {
            origin = origin,
            goal = goal,
            mapDimension = m_MapDimension,
            tiles = map,
            neighbourPositions = m_NeighbourPositions,
            nodes = m_Nodes,
            closeSet = m_CloseSet,
            heap = m_HeapOpenSet,
            result = result,
            resultWaypoints = resultWaypoints
        };

        m_PathFinderJobHandle = aStarPathFindingJob.Schedule();

        m_PathFinderJobHandle.Complete();

        if (result[0] >= 0)
        {
            NotifyPathRequester(ref state, pathRequest.entity, true, new NativeArray<int2>(resultWaypoints.AsArray(), Allocator.Persistent));
        }
        else
        {
            NotifyPathRequester(ref state, pathRequest.entity, false, new NativeArray<int2>(0, Allocator.Temp));
        }

        // clear. code to not be necessary...
        ClearGrid();
        m_CloseSet.Clear();

        result.Dispose();
        resultWaypoints.Dispose();
    }

    [BurstCompile]
    public void OnUpdate (ref SystemState state)
    {
        // check new requests
        CheckNewRequests(ref state, SystemAPI.GetSingletonBuffer<PathRequestEventBuffer>());

        // compute pathfind requests max
        var maxPathComputedPerFrame = SystemAPI.GetSingleton<ManagerComponent>().maxPathsComputedPerFrame;
        var pathsComputed = 0;

        while (pathsComputed <= maxPathComputedPerFrame && m_PathRequests.Count > 0)
        {
            var pathRequest = m_PathRequests.Dequeue();

            if (!state.EntityManager.Exists(pathRequest.entity))
            {
                continue;
            }

            var agentComponent = SystemAPI.GetComponentRO<AgentComponent>(pathRequest.entity);

            if (agentComponent.ValueRO.moveState != MoveState.WaitingPath)
            {
                continue;
            }

            ExecutePathFinding(ref state, pathRequest);

            pathsComputed++;
        }

        /*
        stopWatch.Stop();
        System.TimeSpan ts = stopWatch.Elapsed;

        Debug.Log("RESULT: " + result[0]);

        string elapsedTime = System.String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
        Debug.Log("RunTime " + elapsedTime);
        */
    }

    [BurstCompile]
    private void NotifyPathRequester (ref SystemState state, Entity entity, bool success, NativeArray<int2> path)
    {
        var agentComponent = SystemAPI.GetComponentRW<AgentComponent>(entity);

        if (success)
        {
            if (path.Length > 3)
            {
                var agentPathComponent = SystemAPI.GetComponentRW<AgentPathComponent>(entity);

                var pathLength = (ushort)path.Length;

                agentComponent.ValueRW.reversePath = true;
                agentComponent.ValueRW.pathLength = pathLength;
                agentComponent.ValueRW.currentPathIndex = (ushort)(pathLength - 2);

                agentPathComponent.ValueRW.Path = path;

                agentComponent.ValueRW.moveState = MoveState.PathReady;
            }
            else
            {
                agentComponent.ValueRW.pathLength = 0;

                agentComponent.ValueRW.moveState = MoveState.RequestPath;
            }
        }
        else
        {
            agentComponent.ValueRW.pathLength = 0;

            agentComponent.ValueRW.moveState = MoveState.RequestPath;
        }
    }

    [BurstCompile]
    private void CheckNewRequests (ref SystemState state, DynamicBuffer<PathRequestEventBuffer> buffer)
    {
        if (buffer.IsEmpty)
        {
            return;
        }

        //UnityEngine.Debug.Log("New Path Requested Added.");

        foreach (PathRequestData requestData in buffer)
        {
            if (state.EntityManager.Exists(requestData.entity))
            {
                m_PathRequests.Enqueue(new PathRequestData
                {
                    origin = requestData.origin,
                    goal = requestData.goal,
                    entity = requestData.entity
                });
            }
        }

        buffer.Clear();
    }

    [BurstCompile]
    private void CreateNeighbourPositions ()
    {
        if (!m_NeighbourPositions.IsCreated)
        {
            m_NeighbourPositions = new NativeArray<int2>(System.Enum.GetValues(typeof(EightDirection)).Length, Allocator.Persistent);
            for (int i = 0; i < m_NeighbourPositions.Length; i++)
            {
                var pos = GetTileNeighbourPosition(int2.zero, (EightDirection)i);
                m_NeighbourPositions[i] = new int2(pos.x, pos.y);

            }
        }
    }

    [BurstCompile]
    private void CreateGrid ()
    {
        var tileCount = m_MapDimension.x * m_MapDimension.y;
        m_Nodes = new NativeArray<PathNode>(tileCount, Allocator.Persistent);
        m_HeapOpenSet = new NativeArray<int>(tileCount, Allocator.Persistent);
        m_CloseSet = new NativeHashSet<int>(tileCount, Allocator.Persistent);

        for (int i = 0; i < tileCount; i++)
        {
            m_Nodes[i] = new PathNode(i, 0, 0, 0, 0, 0, 0);
            m_HeapOpenSet[i] = 0;
        }
    }

    [BurstCompile]
    private void ClearGrid ()
    {
        if (m_Nodes.IsCreated)
        {
            for (int i = 0; i < m_Nodes.Length; i++)
            {
                PathNode node = m_Nodes[i];
                node.parent = 0;
                node.heapIndex = 0;
                node.gCost = 0;
                node.hCost = 0;
                node.penalty = 0;
                node.version = 0;
                m_Nodes[i] = node;

                m_HeapOpenSet[i] = 0;
            }
        }
    }

    [BurstCompile]
    private int2 GetTileNeighbourPosition (int2 tilePosition, EightDirection neighbourDirection)
    {
        switch (neighbourDirection)
        {
            case EightDirection.North:
            case EightDirection.West:
            case EightDirection.South:
            case EightDirection.East: return GetTileNeighbourPosition(tilePosition, (FourDirection)neighbourDirection);
            case EightDirection.NorthWest: return tilePosition + new int2(-1, 1);
            case EightDirection.SouthWest: return tilePosition + new int2(-1, -1);
            case EightDirection.SouthEast: return tilePosition + new int2(1, -1);
            case EightDirection.NorthEast: return tilePosition + new int2(1, 1);
            default: return tilePosition;
        }
    }

    [BurstCompile]
    private int2 GetTileNeighbourPosition (int2 tilePosition, FourDirection neighbourDirection)
    {
        switch (neighbourDirection)
        {
            case FourDirection.North: return tilePosition + new int2(0, 1);
            case FourDirection.West: return tilePosition + new int2(-1, 0);
            case FourDirection.South: return tilePosition + new int2(0, -1);
            case FourDirection.East: return tilePosition + new int2(1, 0);
            default: return tilePosition;
        }
    }

    private enum FourDirection { West = 0, South = 1, East = 2, North = 3 };
    private enum EightDirection { West = 0, South = 1, East = 2, North = 3, NorthWest = 4, SouthWest = 5, SouthEast = 6, NorthEast = 7 };
}

public struct PathNode
{
    public readonly int index;
    public int parent;
    public int heapIndex;
    public int gCost;
    public int hCost;
    public int version;
    public int penalty;

    public int fCost { get { return gCost + hCost; } }

    public PathNode (int _index, int _parent, int _heapIndex, int _gCost, int _hCost, int _version, int _penalty)
    {
        index = _index;
        parent = _parent;
        heapIndex = _heapIndex;
        gCost = _gCost;
        hCost = _hCost;
        version = _version;
        penalty = _penalty;
    }

    public int CompareTo (PathNode nodeToCompare)
    {
        int compare = fCost.CompareTo(nodeToCompare.fCost);
        if (compare == 0)
        {
            compare = hCost.CompareTo(nodeToCompare.hCost);
        }
        return -compare;
    }
}

[BurstCompile]
public struct AStarPathFindingJob : IJob
{
    [ReadOnly] public int2 origin;
    [ReadOnly] public int2 goal;
    [ReadOnly] public int2 mapDimension;
    [ReadOnly] public NativeArray<TileData> tiles;
    [ReadOnly] public NativeArray<int2> neighbourPositions;

    [WriteOnly] public NativeArray<int> result;

    // for parallel here..
    //[NativeDisableParallelForRestriction][WriteOnly] public NativeList<int2> resultWaypoints;
    [WriteOnly] public NativeList<int2> resultWaypoints;

    public NativeHashSet<int> closeSet;
    public NativeArray<int> heap;
    public NativeArray<PathNode> nodes;

    private int c_heapCount;

    private bool c_pathSuccess;


    [BurstCompile]
    private int GetTileIndexFromTilePosition (int2 tilePosition)
    {
        return tilePosition.x * mapDimension.y + tilePosition.y;
    }

    [BurstCompile]
    public int2 GetTilePositionFromTileIndex (int tileIndex)
    {
        return new int2(tileIndex / mapDimension.y, tileIndex % mapDimension.y);
    }

    [BurstCompile]
    public void Execute ()
    {
        // set the init values
        c_pathSuccess = false;
        c_heapCount = 0;
        result[0] = -1;

        // first set the first two resultWaypoints as data header
        resultWaypoints.Add(int2.zero);
        resultWaypoints.Add(int2.zero);

        // get the start and target nodes
        PathNode startNode = nodes[GetTileIndexFromTilePosition(origin)];
        PathNode targetNode = nodes[GetTileIndexFromTilePosition(goal)];

        HeapAdd(startNode);

        while (c_heapCount > 0)
        {
            PathNode currentNode = HeapRemoveFirst();
            closeSet.Add(currentNode.index);

            if (currentNode.index == targetNode.index)
            {
                result[0] = RetracePath(startNode, targetNode);
                c_pathSuccess = true;
                break;
            }

            int2 currentNodePosition = GetTilePositionFromTileIndex(currentNode.index);

            foreach (int2 neighbourPosition in neighbourPositions)
            {
                int2 neighbourNodePosition = currentNodePosition + neighbourPosition;

                if (neighbourNodePosition.x < 0 || neighbourNodePosition.y < 0 ||
                    neighbourNodePosition.x >= mapDimension.x ||
                    neighbourNodePosition.y >= mapDimension.y)
                {
                    continue;
                }

                PathNode neighbourNode = nodes[GetTileIndexFromTilePosition(neighbourNodePosition)];

                if (!IsWalkable(neighbourNodePosition, tiles) || closeSet.Contains(neighbourNode.index))
                {
                    continue;
                }

                int newMovementCostToNeighbour = currentNode.gCost + GetDistance(currentNodePosition, neighbourNodePosition) +
                    GetPenalty(neighbourNode.index, tiles);

                bool containNeighbourNode = HeapContains(neighbourNode);

                if (newMovementCostToNeighbour < neighbourNode.gCost || !containNeighbourNode)
                {
                    neighbourNode.gCost = newMovementCostToNeighbour;
                    neighbourNode.hCost = GetDistance(neighbourNodePosition, goal);
                    neighbourNode.parent = currentNode.index;
                    HeapUpdateNode(neighbourNode);

                    if (!containNeighbourNode)
                    {
                        HeapAdd(neighbourNode);
                    }
                    else
                    {
                        HeapSortUp(neighbourNode);
                    }
                }
            }
        }
        
        resultWaypoints.Add(origin);
    }

    [BurstCompile]
    private bool IsWalkable (int2 pos, NativeArray<TileData> tiles)
    {
        int index = GetTileIndexFromTilePosition(pos);
        return !tiles[index].obstacle;
    }

    [BurstCompile]
    private int GetDistance (int2 nodeA, int2 nodeB)
    {
        int dstX = math.abs(nodeA.x - nodeB.x);
        int dstY = math.abs(nodeA.y - nodeB.y);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }

    [BurstCompile]
    private int GetPenalty (int index, NativeArray<TileData> tiles)
    {
        return 0;
    }

    [BurstCompile]
    private int RetracePath (PathNode startNode, PathNode endNode)
    {
        int count = 1;

        PathNode currentNode = nodes[endNode.index];

        int2 directionOld = int2.zero;
        int2 previousNodePosition = GetTilePositionFromTileIndex(currentNode.index);

        do
        {
            currentNode = nodes[currentNode.parent];
            int2 nodePosition = GetTilePositionFromTileIndex(currentNode.index);

            int2 directionNew = previousNodePosition - nodePosition;

            if (!directionNew.Equals(directionOld))
            {
                resultWaypoints.Add(previousNodePosition); count++;
            }

            directionOld = directionNew;

            previousNodePosition = nodePosition;

        } while (currentNode.index != startNode.index);

        return count;
    }

    [BurstCompile]
    private void HeapAdd (PathNode node)
    {
        node.heapIndex = c_heapCount;
        heap[node.heapIndex] = node.index;
        HeapUpdateNode(node);
        HeapSortUp(node);
        c_heapCount++;
    }

    [BurstCompile]
    private PathNode HeapRemoveFirst ()
    {
        PathNode firstNode = nodes[heap[0]];
        c_heapCount--;
        PathNode lastNode = nodes[heap[c_heapCount]];
        lastNode.heapIndex = 0;
        heap[0] = lastNode.index;
        HeapUpdateNode(lastNode);
        HeapSortDown(lastNode);

        return firstNode;
    }

    [BurstCompile]
    private void HeapUpdateNode (PathNode node)
    {
        nodes[node.index] = node;
    }

    [BurstCompile]
    private void HeapSwap (PathNode nodeA, PathNode nodeB)
    {
        int heapIndexNodeA = nodeA.heapIndex;
        int heapIndexNodeB = nodeB.heapIndex;
        nodeA.heapIndex = heapIndexNodeB;
        nodeB.heapIndex = heapIndexNodeA;
        heap[heapIndexNodeA] = nodeB.index;
        heap[heapIndexNodeB] = nodeA.index;
        nodes[nodeA.index] = nodeA;
        nodes[nodeB.index] = nodeB;
    }

    [BurstCompile]
    private bool HeapContains (PathNode node)
    {
        return heap[node.heapIndex] == node.index;
    }

    [BurstCompile]
    private void HeapSortUp (PathNode node)
    {
        while (true)
        {
            int parentHeapIndex = (node.heapIndex - 1) / 2;

            PathNode parentNode = nodes[heap[parentHeapIndex]];

            if (node.CompareTo(parentNode) > 0)
            {
                HeapSwap(node, parentNode);
            }
            else
            {
                break;
            }

            // now the node is the parent, get it
            node = nodes[heap[parentHeapIndex]];
        }
    }

    [BurstCompile]
    private void HeapSortDown (PathNode node)
    {
        while (true)
        {
            int childHeapIndexLeft = node.heapIndex * 2 + 1;
            int childHeapIndexRight = node.heapIndex * 2 + 2;
            int swapHeapIndex = 0;

            if (childHeapIndexLeft < c_heapCount)
            {
                swapHeapIndex = childHeapIndexLeft;

                if (childHeapIndexRight < c_heapCount)
                {
                    PathNode childLeftNode = nodes[heap[childHeapIndexLeft]];
                    PathNode childRightNode = nodes[heap[childHeapIndexRight]];

                    if (childLeftNode.CompareTo(childRightNode) < 0)
                    {
                        swapHeapIndex = childHeapIndexRight;
                    }
                }

                PathNode swapNode = nodes[heap[swapHeapIndex]];

                if (node.CompareTo(swapNode) < 0)
                {
                    HeapSwap(node, swapNode);
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }

            // now the node is the swaped one
            node = nodes[heap[swapHeapIndex]];
        }
    }
}