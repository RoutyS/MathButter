using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class KobbeltSubdivision : MonoBehaviour
{
    [Header("√3 Subdivision Settings")]
    public int subdivisionLevels = 1;
    public bool autoUpdate = false;

    [Header("Debug")]
    public bool showDebugInfo = true;

    private Mesh originalMesh;
    private MeshFilter meshFilter;

    public struct Edge
    {
        public int v1, v2;

        public Edge(int v1, int v2)
        {
            this.v1 = Mathf.Min(v1, v2);
            this.v2 = Mathf.Max(v1, v2);
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

        // Souder et sauvegarder le mesh original pour éviter les problèmes de vertices dupliqués
        originalMesh = Weld(Instantiate(meshFilter.mesh));
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

        if (showDebugInfo)
            Debug.Log($"√3-Kobbelt Input: {oldVertices.Length} vertices, {oldTriangles.Length / 3} triangles");

        // Étape 1: Analyser la topologie
        var topology = AnalyzeTopology(oldVertices, oldTriangles);

        // Étape 2: Créer les centres de triangles et subdiviser
        var subdivisionResult = CreateSubdividedMesh(oldVertices, oldTriangles);

        // Étape 3: Perturber les vertices originaux
        Vector3[] newVertices = PerturbVertices(subdivisionResult.vertices.ToArray(),
                                               topology.vertexNeighbors,
                                               oldVertices.Length);

        // Étape 4: Appliquer le flipping des arêtes
        var finalResult = ApplyEdgeFlipping(newVertices, subdivisionResult.triangles.ToArray(),
                                          oldTriangles, oldVertices.Length);

        if (showDebugInfo)
            Debug.Log($"√3-Kobbelt Output: {finalResult.vertices.Count} vertices, {finalResult.triangles.Count / 3} triangles");

        Mesh newMesh = new Mesh();
        newMesh.vertices = finalResult.vertices.ToArray();
        newMesh.triangles = finalResult.triangles.ToArray();
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();

        return newMesh;
    }

    struct MeshTopology
    {
        public Dictionary<int, List<int>> vertexNeighbors;
        public Dictionary<Edge, List<int>> edgeToTriangles;
        public List<Edge> boundaryEdges;
    }

    MeshTopology AnalyzeTopology(Vector3[] vertices, int[] triangles)
    {
        var topology = new MeshTopology
        {
            vertexNeighbors = new Dictionary<int, List<int>>(),
            edgeToTriangles = new Dictionary<Edge, List<int>>(),
            boundaryEdges = new List<Edge>()
        };

        for (int i = 0; i < vertices.Length; i++)
            topology.vertexNeighbors[i] = new List<int>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];
            int triIndex = i / 3;

            AddNeighbor(topology.vertexNeighbors, v0, v1);
            AddNeighbor(topology.vertexNeighbors, v1, v2);
            AddNeighbor(topology.vertexNeighbors, v2, v0);

            AddEdgeTriangle(topology.edgeToTriangles, new Edge(v0, v1), triIndex);
            AddEdgeTriangle(topology.edgeToTriangles, new Edge(v1, v2), triIndex);
            AddEdgeTriangle(topology.edgeToTriangles, new Edge(v2, v0), triIndex);
        }

        foreach (var kvp in topology.edgeToTriangles)
            if (kvp.Value.Count == 1)
                topology.boundaryEdges.Add(kvp.Key);

        return topology;
    }

    void AddNeighbor(Dictionary<int, List<int>> neighbors, int v1, int v2)
    {
        if (!neighbors[v1].Contains(v2)) neighbors[v1].Add(v2);
        if (!neighbors[v2].Contains(v1)) neighbors[v2].Add(v1);
    }

    void AddEdgeTriangle(Dictionary<Edge, List<int>> dict, Edge e, int tri)
    {
        if (!dict.ContainsKey(e))
            dict[e] = new List<int>();
        dict[e].Add(tri);
    }

    struct SubdivisionResult
    {
        public List<Vector3> vertices;
        public List<int> triangles;
    }

    SubdivisionResult CreateSubdividedMesh(Vector3[] oldVertices, int[] oldTriangles)
    {
        var result = new SubdivisionResult
        {
            vertices = new List<Vector3>(oldVertices),
            triangles = new List<int>()
        };

        for (int i = 0; i < oldTriangles.Length; i += 3)
        {
            int v0 = oldTriangles[i];
            int v1 = oldTriangles[i + 1];
            int v2 = oldTriangles[i + 2];

            Vector3 center = (oldVertices[v0] + oldVertices[v1] + oldVertices[v2]) / 3f;
            int centerIndex = result.vertices.Count;
            result.vertices.Add(center);

            result.triangles.AddRange(new int[] { v0, v1, centerIndex });
            result.triangles.AddRange(new int[] { v1, v2, centerIndex });
            result.triangles.AddRange(new int[] { v2, v0, centerIndex });
        }

        return result;
    }

    Vector3[] PerturbVertices(Vector3[] allVertices, Dictionary<int, List<int>> neighbors, int originalVertexCount)
    {
        Vector3[] result = new Vector3[allVertices.Length];

        for (int i = 0; i < allVertices.Length; i++)
            result[i] = allVertices[i];

        for (int i = 0; i < originalVertexCount; i++)
        {
            if (!neighbors.ContainsKey(i)) continue;

            var neighborList = neighbors[i];
            int n = neighborList.Count;

            if (n == 0) continue;

            float alpha = (4f - 2f * Mathf.Cos(2f * Mathf.PI / n)) / (9f * n);
            alpha = Mathf.Max(0f, alpha);

            Vector3 neighborSum = Vector3.zero;
            foreach (int j in neighborList)
                neighborSum += allVertices[j];

            result[i] = (1f - n * alpha) * allVertices[i] + alpha * neighborSum;
        }

        return result;
    }

    SubdivisionResult ApplyEdgeFlipping(Vector3[] vertices, int[] triangles, int[] originalTriangles, int originalVertexCount)
    {
        var result = new SubdivisionResult
        {
            vertices = new List<Vector3>(vertices),
            triangles = new List<int>()
        };

        Dictionary<Edge, EdgeInfo> originalEdgeInfo = new Dictionary<Edge, EdgeInfo>();

        for (int i = 0; i < originalTriangles.Length; i += 3)
        {
            int v0 = originalTriangles[i];
            int v1 = originalTriangles[i + 1];
            int v2 = originalTriangles[i + 2];
            int centerIndex = originalVertexCount + (i / 3);

            AddEdgeInfo(originalEdgeInfo, v0, v1, centerIndex);
            AddEdgeInfo(originalEdgeInfo, v1, v2, centerIndex);
            AddEdgeInfo(originalEdgeInfo, v2, v0, centerIndex);
        }

        foreach (var kvp in originalEdgeInfo)
        {
            EdgeInfo info = kvp.Value;

            if (info.centers.Count == 2)
            {
                int c1 = info.centers[0];
                int c2 = info.centers[1];

                result.triangles.AddRange(new int[] { info.v1, c1, c2 });
                result.triangles.AddRange(new int[] { c2, c1, info.v2 });
            }
            else if (info.centers.Count == 1)
            {
                int c = info.centers[0];
                result.triangles.AddRange(new int[] { info.v1, info.v2, c });
            }
        }

        return result;
    }

    struct EdgeInfo
    {
        public int v1, v2;
        public List<int> centers;
    }

    void AddEdgeInfo(Dictionary<Edge, EdgeInfo> dict, int v1, int v2, int center)
    {
        Edge edge = new Edge(v1, v2);

        if (!dict.ContainsKey(edge))
        {
            dict[edge] = new EdgeInfo
            {
                v1 = v1,
                v2 = v2,
                centers = new List<int>()
            };
        }
        dict[edge].centers.Add(center);
    }

    // Fonction de soudage pour fusionner les vertices identiques
    Mesh Weld(Mesh m)
    {
        var oldV = m.vertices;
        var oldT = m.triangles;
        var map = new Dictionary<Vector3, int>();
        var newV = new List<Vector3>();
        var remap = new int[oldV.Length];

        // 1) Créer la table de hachage
        for (int i = 0; i < oldV.Length; i++)
        {
            if (!map.TryGetValue(oldV[i], out int idx))
            {
                idx = newV.Count;
                newV.Add(oldV[i]);
                map[oldV[i]] = idx;
            }
            remap[i] = idx;
        }

        // 2) Réindexer les triangles
        var newT = new int[oldT.Length];
        for (int i = 0; i < oldT.Length; i++)
            newT[i] = remap[oldT[i]];

        // 3) Construire et retourner le mesh
        var nm = new Mesh();
        nm.vertices = newV.ToArray();
        nm.triangles = newT;
        nm.RecalculateNormals();
        nm.RecalculateBounds();
        return nm;
    }

    [ContextMenu("Reset to Original")]
    public void ResetToOriginal()
    {
        if (originalMesh != null)
            meshFilter.mesh = Instantiate(originalMesh);
    }
}