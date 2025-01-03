using Unity.Entities;
using UnityEngine;

public class AgentPrefabAuthoring : MonoBehaviour
{
    public GameObject agentPrefab;
    public float agentHeight;

    private class AgentPrefabBaker : Baker<AgentPrefabAuthoring>
    {
        public override void Bake (AgentPrefabAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new AgentPrefabComponent 
            { 
                agentPrefabEntity = GetEntity(authoring.agentPrefab, TransformUsageFlags.Dynamic),
                agentHeight = authoring.agentHeight
            });
        }
    }
}