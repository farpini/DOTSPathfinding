using Unity.Entities;
using UnityEngine;

public class AgentAuthoring : MonoBehaviour
{
    private class AgentBaker : Baker<AgentAuthoring>
    {
        public override void Bake (AgentAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent<AgentComponent>(entity);
            AddComponent<AgentPathExecuting>(entity);
            SetComponentEnabled<AgentPathExecuting>(entity, false);
        }
    }
}