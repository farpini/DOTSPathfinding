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