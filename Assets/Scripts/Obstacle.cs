using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class Obstacle : MonoBehaviour
{
    [SerializeField] private BoxCollider boxCollider;

    public float2 VerticeA { get; set; }
    public float2 VerticeB { get; set; }

    public Vector3 Position { get { return transform.position; } set { transform.position = value; } }
    public Vector3 Scale { get { return transform.localScale; } set { transform.localScale = value; } }
    public Quaternion Rotation { get { return transform.rotation; } set { transform.rotation = value; } }

    public Bounds Bounds => boxCollider.bounds;
}