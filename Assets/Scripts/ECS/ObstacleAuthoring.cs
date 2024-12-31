using System.Collections;
using Unity.Entities;
using UnityEngine;

public class ObstacleAuthoring : MonoBehaviour
{
    private class ObstacleBaker : Baker<ObstacleAuthoring>
    {
        public override void Bake (ObstacleAuthoring authoring)
        {
            var obstacleEntity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(obstacleEntity, new ObstacleComponent { });
        }
    }
}