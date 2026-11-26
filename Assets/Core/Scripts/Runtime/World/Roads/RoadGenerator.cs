
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.Rendering;
using Unity.Mathematics;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class RoadGenerator : MonoBehaviour
{
    [Header("Spline")]
    [SerializeField] private SplineContainer splineContainer;

    [Header("Road Settings")]
    [SerializeField] private float roadWidth = 8f;
    [SerializeField] private int resolution = 100;
    [SerializeField] private float terrainOffset = 0.15f;

    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    private readonly List<Terrain> terrains = new();

    private int lastSplineHash = int.MinValue;
    private bool warnedMissingTerrain;

    private void OnEnable()
    {
        GetComponents();
        lastSplineHash = int.MinValue;

        if (Application.isPlaying)
            GenerateRoad();
    }

    private void OnDisable()
    {
        // Keep generated mesh when disabling.
    }

    private void Update()
    {
        if (Application.isPlaying) return;
        if (splineContainer == null) return;

        int currentHash = CalculateSplineHash();

        if (currentHash != lastSplineHash)
            GenerateRoad();
    }

    private void GetComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();
    }

    private void FindTerrains()
    {
        terrains.Clear();
        terrains.AddRange(FindObjectsByType<Terrain>());
    }

    private int CalculateSplineHash()
    {
        unchecked
        {
            int hash = 17;

            hash = hash * 31 + roadWidth.GetHashCode();
            hash = hash * 31 + resolution.GetHashCode();
            hash = hash * 31 + terrainOffset.GetHashCode();

            if (splineContainer == null)
                return hash;

            int samples = Mathf.Max(2, resolution);

            for (int i = 0; i <= samples; i++)
            {
                float t = (float)i / samples;

                float3 position =
                    splineContainer.EvaluatePosition(t);

                float3 tangent =
                    splineContainer.EvaluateTangent(t);

                hash = hash * 31 + position.GetHashCode();
                hash = hash * 31 + tangent.GetHashCode();
            }

            return hash;
        }
    }

    [ContextMenu("Generate Road")]
    public void GenerateRoad()
    {
        if (meshFilter == null)
            GetComponents();

        if (splineContainer == null) return;
        if (resolution < 2 || roadWidth <= 0f) return;

        FindTerrains();
        warnedMissingTerrain = false;

        int segments = resolution;

        List<Vector3> vertices = new();
        List<int> triangles = new();
        List<Vector2> uvs = new();

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;

            float3 splinePosition =
                splineContainer.EvaluatePosition(t);

            float3 splineTangent =
                splineContainer.EvaluateTangent(t);

            Vector3 center = new Vector3(
                splinePosition.x,
                splinePosition.y,
                splinePosition.z
            );

            Vector3 tangent = new Vector3(
                splineTangent.x,
                splineTangent.y,
                splineTangent.z
            );

            tangent = Vector3.ProjectOnPlane(
                tangent,
                Vector3.up
            ).normalized;

            if (tangent.sqrMagnitude < 0.001f)
                tangent = Vector3.forward;

            Vector3 right =
                Vector3.Cross(Vector3.up, tangent).normalized;

            Vector3 leftPoint =
                center - right * roadWidth * 0.5f;

            Vector3 rightPoint =
                center + right * roadWidth * 0.5f;

            leftPoint.y =
                GetTerrainHeight(leftPoint) + terrainOffset;

            rightPoint.y =
                GetTerrainHeight(rightPoint) + terrainOffset;

            vertices.Add(
                transform.InverseTransformPoint(leftPoint)
            );

            vertices.Add(
                transform.InverseTransformPoint(rightPoint)
            );

            float uvY = t * 10f;

            uvs.Add(new Vector2(0f, uvY));
            uvs.Add(new Vector2(1f, uvY));

            if (i < segments)
            {
                int index = i * 2;

                triangles.Add(index);
                triangles.Add(index + 2);
                triangles.Add(index + 1);

                triangles.Add(index + 1);
                triangles.Add(index + 2);
                triangles.Add(index + 3);
            }
        }

        Mesh roadMesh = new Mesh
        {
            name = "Generated Road (Auto)",
            indexFormat = vertices.Count > 65535
                ? IndexFormat.UInt32
                : IndexFormat.UInt16,
            hideFlags = HideFlags.DontSave
        };

        roadMesh.SetVertices(vertices);
        roadMesh.SetTriangles(triangles, 0);
        roadMesh.SetUVs(0, uvs);
        roadMesh.RecalculateNormals();
        roadMesh.RecalculateBounds();

        Mesh oldMesh = meshFilter.sharedMesh;

        meshCollider.sharedMesh = null;
        meshFilter.sharedMesh = roadMesh;
        meshCollider.sharedMesh = roadMesh;

        if (oldMesh != null &&
            oldMesh != roadMesh &&
            oldMesh.name == "Generated Road (Auto)" &&
            (oldMesh.hideFlags & HideFlags.DontSave) != 0)
        {
            if (Application.isPlaying)
                Destroy(oldMesh);
            else
                DestroyImmediate(oldMesh);
        }

        lastSplineHash = CalculateSplineHash();

        Debug.Log(
            $"[RoadGenerator] Road generated: {segments} segments."
        );
    }

    private float GetTerrainHeight(Vector3 worldPosition)
    {
        foreach (Terrain terrain in terrains)
        {
            if (terrain == null || terrain.terrainData == null)
                continue;

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;

            bool inside =
                worldPosition.x >= terrainPosition.x &&
                worldPosition.x <= terrainPosition.x + terrainSize.x &&
                worldPosition.z >= terrainPosition.z &&
                worldPosition.z <= terrainPosition.z + terrainSize.z;

            if (inside)
                return terrain.SampleHeight(worldPosition);
        }

        if (!warnedMissingTerrain)
        {
            Debug.LogWarning(
                "[RoadGenerator] Some road points are outside all terrain tiles."
            );

            warnedMissingTerrain = true;
        }

        return worldPosition.y;
    }
}
