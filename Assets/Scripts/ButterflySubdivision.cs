// (en haut du fichier)
using System.Collections.Generic;
using UnityEngine;

public class ButterflySubdivision : MonoBehaviour
{
    [Header("Mesh d'entrée")]
    public MeshFilter inputMeshFilter;

    [Header("Paramètres de subdivision")]
    [Range(1, 5)]
    public int subdivisionLevels = 1;

    [Header("Options de debug")]
    public bool showDebugPoints = true;
    public bool showDebugLogs = false;

    [Header("Projection sphérique")]
    public bool projectToSphere = false;
    public float sphereRadiusOverride = -1f;  // -1 = rayon moyen auto

    private readonly List<(Vector3, Vector3)> debugLines = new();

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.B)) return;

        if (inputMeshFilter == null)
        {
            GameObject coons = GameObject.Find("CoonsMesh");
            if (coons) inputMeshFilter = coons.GetComponent<MeshFilter>();
        }

        if (inputMeshFilter == null)
        {
            Debug.LogError("❌ Aucun MeshFilter trouvé pour subdivision.");
            return;
        }

        foreach (GameObject go in GameObject.FindGameObjectsWithTag("ButterflyPoint"))
            Destroy(go);

        Mesh original = inputMeshFilter.sharedMesh;
        Mesh subdivided = original;

        for (int level = 0; level < subdivisionLevels; level++)
        {
            Debug.Log($"🔄 Application du niveau de subdivision {level + 1}/{subdivisionLevels}");
            subdivided = SubdivideButterfly(subdivided);
        }

        Debug.Log($"✅ Subdivision terminée ! Vertices: {original.vertexCount} → {subdivided.vertexCount}");

        var old = GameObject.Find("ButterflyResult");
        if (old) Destroy(old);

        GameObject res = new("ButterflyResult");
        var mf = res.AddComponent<MeshFilter>();
        var mr = res.AddComponent<MeshRenderer>();
        mf.mesh = subdivided;
        mr.material = new Material(Shader.Find("Standard")) { color = Color.cyan };
        res.transform.SetPositionAndRotation(
            inputMeshFilter.transform.position,
            inputMeshFilter.transform.rotation);
    }

    public Mesh SubdivideButterfly(Mesh inputMesh)
    {
        Vector3[] vertsBase = inputMesh.vertices;
        int[] trisBase = inputMesh.triangles;

        Dictionary<Edge, List<int>> edgeToTriangles = BuildEdgeToTriangles(inputMesh);
        Dictionary<Edge, int> edgeToNewVertex = new();
        List<Vector3> newVertices = new(vertsBase);
        List<int> newTriangles = new();

        for (int i = 0; i < trisBase.Length; i += 3)
        {
            int v0 = trisBase[i];
            int v1 = trisBase[i + 1];
            int v2 = trisBase[i + 2];

            int mid01 = GetOrCreateMidpoint(new Edge(v0, v1), vertsBase, edgeToTriangles, edgeToNewVertex, newVertices, inputMesh);
            int mid12 = GetOrCreateMidpoint(new Edge(v1, v2), vertsBase, edgeToTriangles, edgeToNewVertex, newVertices, inputMesh);
            int mid20 = GetOrCreateMidpoint(new Edge(v2, v0), vertsBase, edgeToTriangles, edgeToNewVertex, newVertices, inputMesh);

            newTriangles.AddRange(new[] { v0, mid01, mid20 });
            newTriangles.AddRange(new[] { v1, mid12, mid01 });
            newTriangles.AddRange(new[] { v2, mid20, mid12 });
            newTriangles.AddRange(new[] { mid01, mid12, mid20 });
        }

        /*if (projectToSphere)
        {
            if (sphereRadiusOverride > 0)
                ReprojectToSphere(ref newVertices, sphereRadiusOverride);
            else
                ReprojectToSphere(ref newVertices);
        }*/

        Mesh result = new Mesh();
        result.vertices = newVertices.ToArray();
        result.triangles = newTriangles.ToArray();
        result.RecalculateNormals();
        result.RecalculateBounds();

        Debug.Log($"Subdivision: {vertsBase.Length} → {newVertices.Count} vertices, {trisBase.Length / 3} → {newTriangles.Count / 3} triangles");

        return result;
    }

    int GetOrCreateMidpoint(Edge edge, Vector3[] originalVertices,
                           Dictionary<Edge, List<int>> edgeToTriangles,
                           Dictionary<Edge, int> edgeToNewVertex,
                           List<Vector3> newVertices, Mesh mesh)
    {
        if (edgeToNewVertex.TryGetValue(edge, out int existingIndex))
            return existingIndex;

        Vector3 v1 = originalVertices[edge.v1];
        Vector3 v2 = originalVertices[edge.v2];
        Vector3 midpoint;

        if (TryCalculateButterflyMidpoint(edge, originalVertices, edgeToTriangles, mesh, out midpoint))
        {
            if (showDebugLogs)
                Debug.Log($"✅ Butterfly appliqué pour arête {edge.v1}-{edge.v2}");
        }
        else
        {
            midpoint = 0.5f * (v1 + v2);
            if (showDebugLogs)
                Debug.Log($"⚠️ Fallback pour arête {edge.v1}-{edge.v2}");
        }

        newVertices.Add(midpoint);
        int newIndex = newVertices.Count - 1;
        edgeToNewVertex[edge] = newIndex;

        if (showDebugPoints)
            CreateDebugPoint(midpoint);

        return newIndex;
    }

    bool TryCalculateButterflyMidpoint(Edge edge, Vector3[] vertices,
    Dictionary<Edge, List<int>> edgeToTriangles, Mesh mesh, out Vector3 midpoint)
    {
        midpoint = Vector3.zero;

        if (!edgeToTriangles.TryGetValue(edge, out List<int> triangleIndices) || triangleIndices.Count != 2)
            return false;

        int[] triangles = mesh.triangles;
        int tri0 = triangleIndices[0];
        int tri1 = triangleIndices[1];

        int opp0 = GetOppositeVertex(triangles, tri0, edge);
        int opp1 = GetOppositeVertex(triangles, tri1, edge);
        if (opp0 == -1 || opp1 == -1) return false;

        // Opposés
        Vector3 A = vertices[opp0];
        Vector3 B = vertices[opp1];

        // Extrémités de l'arête
        int V0 = edge.v1;
        int V1 = edge.v2;
        Vector3 P = vertices[V0];
        Vector3 Q = vertices[V1];

        // Cherche voisins des extrémités
        List<int> P_neighbors = FindNeighborOpposites(V0, edgeToTriangles, triangles);
        List<int> Q_neighbors = FindNeighborOpposites(V1, edgeToTriangles, triangles);

        if (P_neighbors.Count < 2 || Q_neighbors.Count < 2) return false;

        Vector3 P1 = vertices[P_neighbors[0]];
        Vector3 P2 = vertices[P_neighbors[1]];
        Vector3 Q1 = vertices[Q_neighbors[0]];
        Vector3 Q2 = vertices[Q_neighbors[1]];

        // Formule complète
        midpoint = 0.5f * (P + Q) + 0.125f * (A + B) - 0.0625f * (P1 + P2 + Q1 + Q2);
        return true;
    }

    List<int> FindNeighborOpposites(int vertex, Dictionary<Edge, List<int>> edgeToTriangles, int[] triangles)
    {
        List<int> opposites = new();

        foreach (var kvp in edgeToTriangles)
        {
            if (kvp.Key.v1 == vertex || kvp.Key.v2 == vertex)
            {
                foreach (int triIndex in kvp.Value)
                {
                    int opp = GetOppositeVertex(triangles, triIndex, kvp.Key);
                    if (opp != -1 && opp != vertex && !opposites.Contains(opp))
                    {
                        opposites.Add(opp);
                        if (opposites.Count == 2)
                            return opposites;
                    }
                }
            }
        }

        return opposites;
    }



    bool TryCalculateBoundaryMidpoint(Edge edge, Vector3[] vertices, int triangleIndex, Mesh mesh, out Vector3 midpoint)
    {
        int[] tris = mesh.triangles;
        int v0 = edge.v1;
        int v1 = edge.v2;

        int vOpp = -1;
        for (int i = 0; i < 3; i++)
        {
            int v = tris[triangleIndex + i];
            if (v != v0 && v != v1)
            {
                vOpp = v;
                break;
            }
        }

        if (vOpp == -1)
        {
            midpoint = 0.5f * (vertices[v0] + vertices[v1]);
            return true;
        }

        midpoint = 0.5f * (vertices[v0] + vertices[v1]) + 0.25f * (vertices[vOpp] - 0.5f * (vertices[v0] + vertices[v1]));
        return true;
    }

    /*void ReprojectToSphere(ref List<Vector3> vertices, float radiusOverride = -1f)
    {
        Vector3 center = Vector3.zero;
        foreach (Vector3 v in vertices) center += v;
        center /= vertices.Count;

        float radius = radiusOverride > 0f ? radiusOverride : 0f;
        if (radius <= 0f)
        {
            foreach (Vector3 v in vertices)
                radius += (v - center).magnitude;
            radius /= vertices.Count;
        }

        for (int i = 0; i < vertices.Count; i++)
            vertices[i] = center + (vertices[i] - center).normalized * radius;
    }*/

    void CreateDebugPoint(Vector3 position)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * 0.05f;
        sphere.GetComponent<Renderer>().material.color = Color.red;
        sphere.name = "ButterflyPoint";
        sphere.tag = "ButterflyPoint";
        if (inputMeshFilter != null)
            sphere.transform.SetParent(inputMeshFilter.transform, false);
    }

    int GetOppositeVertex(int[] triangles, int triangleStartIndex, Edge edge)
    {
        for (int i = 0; i < 3; i++)
        {
            int vertex = triangles[triangleStartIndex + i];
            if (vertex != edge.v1 && vertex != edge.v2)
                return vertex;
        }
        return -1;
    }

    Dictionary<Edge, List<int>> BuildEdgeToTriangles(Mesh mesh)
    {
        Dictionary<Edge, List<int>> edgeToTriangles = new();
        int[] triangles = mesh.triangles;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Edge[] edges = {
                new Edge(triangles[i], triangles[i + 1]),
                new Edge(triangles[i + 1], triangles[i + 2]),
                new Edge(triangles[i + 2], triangles[i])
            };

            foreach (Edge edge in edges)
            {
                if (!edgeToTriangles.ContainsKey(edge))
                    edgeToTriangles[edge] = new List<int>();

                edgeToTriangles[edge].Add(i);
            }
        }

        return edgeToTriangles;
    }

    private struct Edge
    {
        public int v1, v2;

        public Edge(int a, int b)
        {
            v1 = Mathf.Min(a, b);
            v2 = Mathf.Max(a, b);
        }

        public override bool Equals(object obj) =>
            obj is Edge edge && v1 == edge.v1 && v2 == edge.v2;

        public override int GetHashCode() =>
            v1.GetHashCode() ^ (v2.GetHashCode() << 2);

        public override string ToString() => $"({v1}, {v2})";
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        foreach (var line in debugLines)
        {
            Gizmos.DrawLine(line.Item1, line.Item2);
        }
    }
}
