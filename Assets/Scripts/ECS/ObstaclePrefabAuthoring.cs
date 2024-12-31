using System.Collections;
using Unity.Entities;
using UnityEngine;

public class ObstaclePrefabAuthoring : MonoBehaviour
{
    public GameObject obstaclePrefab;

    private class ObstacleBaker : Baker<ObstaclePrefabAuthoring>
    {
        public override void Bake (ObstaclePrefabAuthoring authoring)
        {
            var obstaclePrefabEntity = GetEntity(TransformUsageFlags.Dynamic);
            var obstacleEntity = GetEntity(authoring.obstaclePrefab, TransformUsageFlags.Dynamic);
            AddComponent(obstaclePrefabEntity, new ObstacleEntityPrefab { obstacle = obstacleEntity });
        }
    }
}