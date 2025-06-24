using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class KobbeltSubdivision : MonoBehaviour
{
    [Header("√3 Subdivision Settings")]
    public int subdivisionLevels = 1;
    public bool autoUpdate = false;

    private Mesh originalMesh;
    private MeshFilter meshFilter;

    public struct Edge
    {
        public int v1, v2;

        public Edge(int vertex1, int vertex2)
        {
            v1 = Mathf.Min(vertex1, vertex2);
            v2 = Mathf.Max(vertex1, vertex2);
        }

        public override bool Equals(object obj)
        {
            return obj is Edge other && v1 == other.v1 && v2 == other.v2;
        }

        public override int GetHashCode()
        {
            return v1.GetHashCode() ^ (v2.GetHashCode() << 2);
        }
    }

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            Debug.LogError("MeshFilter component required!");
            return;
        }

        originalMesh = Instantiate(meshFilter.mesh);
        ApplyKobbeltSubdivision();
    }

    void Update()
    {
        if (autoUpdate)
        {
            ApplyKobbeltSubdivision();
        }
    }

    [ContextMenu("Apply √3-Kobbelt Subdivision")]
    public void ApplyKobbeltSubdivision()
    {
        if (originalMesh == null) return;

        Mesh currentMesh = Instantiate(originalMesh);

        for (int i = 0; i < subdivisionLevels; i++)
        {
            currentMesh = PerformKobbeltSubdivision(currentMesh);
        }

        meshFilter.mesh = currentMesh;
    }

    Mesh PerformKobbeltSubdivision(Mesh inputMesh)
    {
        Vector3[] oldVertices = inputMesh.vertices;
        int[] oldTriangles = inputMesh.triangles;

        Debug.Log($"√3-Kobbelt: Input mesh has {oldVertices.Length} vertices, {oldTriangles.Length / 3} triangles");

        List<Vector3> newVertices = new List<Vector3>(oldVertices);
        List<int> newTriangles = new List<int>();
        Dictionary<int, List<int>> vertexNeighbors = new Dictionary<int, List<int>>();
        HashSet<Edge> originalEdges = new HashSet<Edge>();

        BuildOriginalTopology(oldTriangles, vertexNeighbors, originalEdges);

        for (int i = 0; i < oldTriangles.Length; i += 3)
        {
            int v1 = oldTriangles[i];
            int v2 = oldTriangles[i + 1];
            int v3 = oldTriangles[i + 2];

            Vector3 center = (oldVertices[v1] + oldVertices[v2] + oldVertices[v3]) / 3f;
            int centerIndex = newVertices.Count;
            newVertices.Add(center);

            newTriangles.AddRange(new int[] { v1, v2, centerIndex });
            newTriangles.AddRange(new int[] { v2, v3, centerIndex });
            newTriangles.AddRange(new int[] { v3, v1, centerIndex });
        }

        PerturbOriginalVertices(oldVertices, vertexNeighbors, newVertices);

        // ✅ Correction ici : passe newVertices à FlipOriginalEdges
        FlipOriginalEdges(newTriangles, originalEdges, oldVertices.Length, newVertices);

        Debug.Log($"√3-Kobbelt: Output mesh has {newVertices.Count} vertices, {newTriangles.Count / 3} triangles");

        Mesh newMesh = new Mesh();
        newMesh.vertices = newVertices.ToArray();
        newMesh.triangles = newTriangles.ToArray();
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();

        return newMesh;
    }

    void BuildOriginalTopology(int[] triangles, Dictionary<int, List<int>> vertexNeighbors, HashSet<Edge> originalEdges)
    {
        HashSet<int> allVertices = new HashSet<int>(triangles);
        foreach (int vertex in allVertices)
        {
            vertexNeighbors[vertex] = new List<int>();
        }

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v1 = triangles[i];
            int v2 = triangles[i + 1];
            int v3 = triangles[i + 2];

            AddNeighborBidirectional(vertexNeighbors, v1, v2);
            AddNeighborBidirectional(vertexNeighbors, v2, v3);
            AddNeighborBidirectional(vertexNeighbors, v3, v1);

            originalEdges.Add(new Edge(v1, v2));
            originalEdges.Add(new Edge(v2, v3));
            originalEdges.Add(new Edge(v3, v1));
        }
    }

    void AddNeighborBidirectional(Dictionary<int, List<int>> vertexNeighbors, int v1, int v2)
    {
        if (!vertexNeighbors[v1].Contains(v2))
            vertexNeighbors[v1].Add(v2);
        if (!vertexNeighbors[v2].Contains(v1))
            vertexNeighbors[v2].Add(v1);
    }

    void PerturbOriginalVertices(Vector3[] oldVertices, Dictionary<int, List<int>> vertexNeighbors, List<Vector3> newVertices)
    {
        for (int i = 0; i < oldVertices.Length; i++)
        {
            List<int> neighbors = vertexNeighbors[i];
            int n = neighbors.Count;

            float alpha = CalculateKobbeltAlpha(n);

            Vector3 neighborSum = Vector3.zero;
            foreach (int neighborIndex in neighbors)
            {
                neighborSum += oldVertices[neighborIndex];
            }

            Vector3 perturbedVertex = (1f - n * alpha) * oldVertices[i] + alpha * neighborSum;
            newVertices[i] = perturbedVertex;
        }
    }

    float CalculateKobbeltAlpha(int n)
    {
        if (n <= 0) return 0f;
        float cosTerm = Mathf.Cos((2f * Mathf.PI) / n);
        float alpha = (1f / (9f * n)) * (4f - 2f * cosTerm);
        return alpha;
    }

    // ✅ Fonction corrigée ici
    void FlipOriginalEdges(List<int> triangles, HashSet<Edge> originalEdges, int originalVertexCount, List<Vector3> vertices)
    {
        var edgeToTriangles = new Dictionary<Edge, List<int>>();

        for (int i = 0; i < triangles.Count; i += 3)
        {
            int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
            foreach (var pair in new[] { (a, b), (b, c), (c, a) })
            {
                var e = new Edge(pair.Item1, pair.Item2);
                if (!edgeToTriangles.ContainsKey(e))
                    edgeToTriangles[e] = new List<int>();
                edgeToTriangles[e].Add(i);
            }
        }

        var flipped = new List<int>();
        int badCount = 0;

        foreach (var edge in originalEdges)
        {
            if (!edgeToTriangles.TryGetValue(edge, out var tris) || tris.Count != 2)
                continue;

            var t0 = tris[0]; var t1 = tris[1];
            var tri0 = new[] { triangles[t0], triangles[t0 + 1], triangles[t0 + 2] };
            var tri1 = new[] { triangles[t1], triangles[t1 + 1], triangles[t1 + 2] };

            int c0 = tri0.FirstOrDefault(v => v >= originalVertexCount);
            int c1 = tri1.FirstOrDefault(v => v >= originalVertexCount);
            if (c0 < originalVertexCount || c1 < originalVertexCount)
            {
                badCount++;
                continue;
            }

            Vector3 p0 = vertices[edge.v1];
            Vector3 p1 = vertices[edge.v2];
            Vector3 q0 = vertices[c0];
            Vector3 q1 = vertices[c1];

            flipped.AddRange(new[] { c0, c1, edge.v1 });
            flipped.AddRange(new[] { c0, c1, edge.v2 });
        }

        Debug.Log($"Flip: {flipped.Count / 3} triangles générés, {badCount} arêtes ignorées (pas de centre).");
        triangles.AddRange(flipped);
    }

    [ContextMenu("Reset to Original")]
    public void ResetToOriginal()
    {
        if (originalMesh != null)
        {
            meshFilter.mesh = Instantiate(originalMesh);
        }
    }
}
