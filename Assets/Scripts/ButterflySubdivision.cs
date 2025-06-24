using System.Collections.Generic;
using UnityEngine;

// Classe utilitaire pour représenter une arête (edge)
public class Edge
{
    public int v1, v2;

    public Edge(int a, int b)
    {
        v1 = Mathf.Min(a, b);
        v2 = Mathf.Max(a, b);
    }

    public override bool Equals(object obj)
    {
        if (obj is Edge other)
            return v1 == other.v1 && v2 == other.v2;
        return false;
    }

    public override int GetHashCode()
    {
        return v1.GetHashCode() ^ v2.GetHashCode();
    }
}

public class ButterflySubdivision : MonoBehaviour
{
    [Header("Mesh d'entrée")]
    public MeshFilter inputMeshFilter;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            if (inputMeshFilter == null)
            {
                GameObject coons = GameObject.Find("CoonsMesh");
                if (coons != null)
                {
                    inputMeshFilter = coons.GetComponent<MeshFilter>();
                    Debug.Log("Mesh Coons trouvé automatiquement !");
                }
            }

            if (inputMeshFilter == null)
            {
                Debug.LogError("Aucun MeshFilter trouvé pour subdivision !");
                return;
            }

            Mesh originalMesh = inputMeshFilter.sharedMesh;
            Mesh subdividedMesh = SubdivideButterfly(originalMesh);

            GameObject result = new GameObject("ButterflyResult");
            var mf = result.AddComponent<MeshFilter>();
            var mr = result.AddComponent<MeshRenderer>();
            mf.mesh = subdividedMesh;

            Material mat = new Material(Shader.Find("Standard"));
            mat.color = Color.cyan;
            mr.material = mat;
        }
    }


    Vector3 ComputeButterflyPoint(Vector3 v1, Vector3 v2, Vector3 A, Vector3 B, Vector3 C, Vector3 D, Vector3 E, Vector3 F)
    {
        return 0.5f * (v1 + v2)
             + 0.125f * (A + B)
             - 0.0625f * (C + D + E + F);
    }

    Dictionary<Edge, List<int>> BuildEdgeToTriangles(Mesh mesh)
    {
        Dictionary<Edge, List<int>> edgeToTriangles = new Dictionary<Edge, List<int>>();
        int[] triangles = mesh.triangles;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];

            Edge[] edges = {
                new Edge(a, b),
                new Edge(b, c),
                new Edge(c, a)
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

    Mesh SubdivideButterfly(Mesh inputMesh)
    {
        Vector3[] vertices = inputMesh.vertices;
        int[] triangles = inputMesh.triangles;

        Dictionary<Edge, List<int>> edgeToTriangles = BuildEdgeToTriangles(inputMesh);
        Dictionary<Edge, int> edgeToNewVertex = new Dictionary<Edge, int>();
        List<Vector3> newVertices = new List<Vector3>(vertices);
        List<int> newTriangles = new List<int>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];

            int m0 = GetOrCreateMidpoint(edgeToNewVertex, newVertices, vertices[i0], vertices[i1], new Edge(i0, i1), edgeToTriangles);
            int m1 = GetOrCreateMidpoint(edgeToNewVertex, newVertices, vertices[i1], vertices[i2], new Edge(i1, i2), edgeToTriangles);
            int m2 = GetOrCreateMidpoint(edgeToNewVertex, newVertices, vertices[i2], vertices[i0], new Edge(i2, i0), edgeToTriangles);

            newTriangles.Add(i0); newTriangles.Add(m0); newTriangles.Add(m2);
            newTriangles.Add(i1); newTriangles.Add(m1); newTriangles.Add(m0);
            newTriangles.Add(i2); newTriangles.Add(m2); newTriangles.Add(m1);
            newTriangles.Add(m0); newTriangles.Add(m1); newTriangles.Add(m2);
        }

        Mesh newMesh = new Mesh();
        newMesh.vertices = newVertices.ToArray();
        newMesh.triangles = newTriangles.ToArray();
        newMesh.RecalculateNormals();
        return newMesh;
    }

    int GetOrCreateMidpoint(Dictionary<Edge, int> dict, List<Vector3> verts, Vector3 v1, Vector3 v2, Edge e, Dictionary<Edge, List<int>> edgeToTriangles)
    {
        if (dict.ContainsKey(e))
            return dict[e];

        Vector3 mid;
        if (TryGetButterflyNeighbors(inputMeshFilter.sharedMesh, edgeToTriangles, e, out Vector3 A, out Vector3 B, out Vector3 C, out Vector3 D, out Vector3 E1, out Vector3 F1))
        {
            mid = ComputeButterflyPoint(v1, v2, A, B, C, D, E1, F1);
        }
        else
        {
            mid = 0.5f * (v1 + v2);
        }

        verts.Add(mid);
        int index = verts.Count - 1;
        dict[e] = index;
        return index;
    }

    bool TryGetButterflyNeighbors(Mesh mesh, Dictionary<Edge, List<int>> edgeToTriangles, Edge edge, out Vector3 A, out Vector3 B, out Vector3 C, out Vector3 D, out Vector3 E, out Vector3 F)
    {
        A = B = C = D = E = F = Vector3.zero;

        Vector3[] verts = mesh.vertices;
        int[] tris = mesh.triangles;

        if (!edgeToTriangles.ContainsKey(edge)) return false;
        var trisList = edgeToTriangles[edge];
        if (trisList.Count != 2) return false;

        int t0 = trisList[0];
        int t1 = trisList[1];

        int[] tri0 = { tris[t0], tris[t0 + 1], tris[t0 + 2] };
        int[] tri1 = { tris[t1], tris[t1 + 1], tris[t1 + 2] };

        int other0 = GetThirdVertex(tri0, edge.v1, edge.v2);
        int other1 = GetThirdVertex(tri1, edge.v1, edge.v2);

        A = verts[other0];
        B = verts[other1];

        // Trouver C/D autour de A
        GetTwoOppositeVerts(mesh, other0, edge.v1, edge.v2, out C, out D);

        // Trouver E/F autour de B
        GetTwoOppositeVerts(mesh, other1, edge.v1, edge.v2, out E, out F);

        Debug.Log($"Butterfly: A={A}, B={B}, C={C}, D={D}, E={E}, F={F}");

        if (!edgeToTriangles.ContainsKey(edge))
        {
            Debug.LogWarning("Edge not found in edgeToTriangles");
            return false;
        }
        if (trisList.Count != 2)
        {
            Debug.LogWarning($"Edge {edge.v1}-{edge.v2} has {trisList.Count} triangle(s), skipping.");
            return false;
        }

        if (trisList.Count != 2)
        {
            Debug.LogWarning($"Pas assez de triangles pour l’arête {edge.v1}-{edge.v2}");
            return false;
        }

        return true;

        


    }

    int GetThirdVertex(int[] triangle, int v1, int v2)
    {
        foreach (int v in triangle)
            if (v != v1 && v != v2)
                return v;
        return -1;
    }

    void GetTwoOppositeVerts(Mesh mesh, int center, int e1, int e2, out Vector3 v1, out Vector3 v2)
    {
        v1 = v2 = Vector3.zero;
        List<Vector3> found = new List<Vector3>();
        int[] tris = mesh.triangles;
        Vector3[] verts = mesh.vertices;

        for (int i = 0; i < tris.Length; i += 3)
        {
            int[] tri = { tris[i], tris[i + 1], tris[i + 2] };
            if (System.Array.Exists(tri, v => v == center))
            {
                foreach (int v in tri)
                {
                    if (v != center && v != e1 && v != e2 && !found.Contains(verts[v]))
                        found.Add(verts[v]);
                }
            }
        }

        if (found.Count >= 2)
        {
            v1 = found[0];
            v2 = found[1];
        }
    }
}
