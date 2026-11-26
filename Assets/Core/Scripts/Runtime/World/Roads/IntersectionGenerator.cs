
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class RoadIntersectionGenerator : MonoBehaviour
{
    [Serializable]
    public class RoadConnection
    {
        public Transform connectionPoint;

        [Min(0.1f)]
        public float roadWidth = 8f;
    }

    [Header("Connections")]
    [SerializeField] private List<RoadConnection> connections = new();

    [Header("Surface")]
    [SerializeField] private float terrainOffset = 0.17f;

    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    private readonly List<Terrain> terrains = new();

    private int lastHash = int.MinValue;
    private bool warnedMissingTerrain;

    private void OnEnable()
    {
        GetComponents();
        lastHash = int.MinValue;

        if (Application.isPlaying)
            GenerateIntersection();
    }

    private void Update()
    {
        if (Application.isPlaying)
            return;

        int currentHash = CalculateHash();

        if (currentHash != lastHash)
            GenerateIntersection();
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

    private int CalculateHash()
    {
        unchecked
        {
            int hash = 17;

            hash = hash * 31 + terrainOffset.GetHashCode();

            foreach (RoadConnection connection in connections)
            {
                if (connection == null ||
                    connection.connectionPoint == null)
                {
                    hash = hash * 31;
                    continue;
                }

                Transform point = connection.connectionPoint;

                hash = hash * 31 + point.position.GetHashCode();
                hash = hash * 31 + point.forward.GetHashCode();
                hash = hash * 31 + connection.roadWidth.GetHashCode();
            }

            return hash;
        }
    }

    [ContextMenu("Generate Intersection")]
    public void GenerateIntersection()
    {
        if (meshFilter == null)
            GetComponents();

        if (connections == null || connections.Count < 3)
            return;

        FindTerrains();
        warnedMissingTerrain = false;

        List<Vector3> boundaryPoints = new();

        Vector3 center = Vector3.zero;
        int validConnections = 0;

        foreach (RoadConnection connection in connections)
        {
            if (connection == null ||
                connection.connectionPoint == null)
                continue;

            Transform point = connection.connectionPoint;

            Vector3 forward = Vector3.ProjectOnPlane(
                point.forward,
                Vector3.up
            ).normalized;

            if (forward.sqrMagnitude < 0.001f)
                continue;

            Vector3 right = Vector3.Cross(
                Vector3.up,
                forward
            ).normalized;

            float halfWidth = connection.roadWidth * 0.5f;

            Vector3 leftEdge =
                point.position - right * halfWidth;

            Vector3 rightEdge =
                point.position + right * halfWidth;

            boundaryPoints.Add(leftEdge);
            boundaryPoints.Add(rightEdge);

            center += point.position;
            validConnections++;
        }

        if (validConnections < 3 || boundaryPoints.Count < 6)
            return;

        center /= validConnections;

        boundaryPoints.Sort((a, b) =>
        {
            float angleA = Mathf.Atan2(
                a.z - center.z,
                a.x - center.x
            );

            float angleB = Mathf.Atan2(
                b.z - center.z,
                b.x - center.x
            );

            return angleA.CompareTo(angleB);
        });

        List<Vector3> vertices = new();

        Vector3 localCenter = center;
        localCenter.y = GetTerrainHeight(center) + terrainOffset;

        vertices.Add(
            transform.InverseTransformPoint(localCenter)
        );

        foreach (Vector3 point in boundaryPoints)
        {
            Vector3 surfacePoint = point;
            surfacePoint.y =
                GetTerrainHeight(point) + terrainOffset;

            vertices.Add(
                transform.InverseTransformPoint(surfacePoint)
            );
        }

        List<int> triangles = new();

        int boundaryCount = boundaryPoints.Count;

        for (int i = 0; i < boundaryCount; i++)
        {
            int next = (i + 1) % boundaryCount;

            triangles.Add(0);
            triangles.Add(i + 1);
            triangles.Add(next + 1);
        }

        Mesh intersectionMesh = new Mesh
        {
            name = "Generated Intersection (Auto)",
            indexFormat = vertices.Count > 65535
                ? IndexFormat.UInt32
                : IndexFormat.UInt16,
            hideFlags = HideFlags.DontSave
        };

        intersectionMesh.SetVertices(vertices);
        intersectionMesh.SetTriangles(triangles, 0);
        intersectionMesh.RecalculateNormals();
        intersectionMesh.RecalculateBounds();

        Mesh oldMesh = meshFilter.sharedMesh;

        meshCollider.sharedMesh = null;
        meshFilter.sharedMesh = intersectionMesh;
        meshCollider.sharedMesh = intersectionMesh;

        if (oldMesh != null &&
            oldMesh != intersectionMesh &&
            oldMesh.name == "Generated Intersection (Auto)" &&
            (oldMesh.hideFlags & HideFlags.DontSave) != 0)
        {
            if (Application.isPlaying)
                Destroy(oldMesh);
            else
                DestroyImmediate(oldMesh);
        }

        lastHash = CalculateHash();

        Debug.Log(
            $"[RoadIntersectionGenerator] Generated intersection with {validConnections} connections."
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
                "[RoadIntersectionGenerator] Some intersection points are outside all terrain tiles."
            );

            warnedMissingTerrain = true;
        }

        return worldPosition.y;
    }
}
