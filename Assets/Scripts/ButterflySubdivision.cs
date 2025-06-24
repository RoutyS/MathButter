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

        // -------- Affichage visuel : point ajouté ----------
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = mid;
        sphere.transform.localScale = Vector3.one * 0.03f;
        sphere.GetComponent<Renderer>().material.color = Color.red;
        sphere.name = "ButterflyPoint";

        return index;
    }


    bool TryGetButterflyNeighbors(Mesh mesh, Dictionary<Edge, List<int>> edgeToTriangles, Edge edge, out Vector3 A, out Vector3 B, out Vector3 C, out Vector3 D, out Vector3 E, out Vector3 F)
    {
        A = B = C = D = E = F = Vector3.zero;

        if (!edgeToTriangles.TryGetValue(edge, out List<int> triIndices) || triIndices.Count != 2)
            return false; // Bord => fallback

        int[] triangles = mesh.triangles;
        Vector3[] verts = mesh.vertices;

        // Obtenir les 2 triangles voisins de l'arête
        int t1 = triIndices[0];
        int t2 = triIndices[1];

        int[] tri1 = { triangles[t1], triangles[t1 + 1], triangles[t1 + 2] };
        int[] tri2 = { triangles[t2], triangles[t2 + 1], triangles[t2 + 2] };

        int v1 = edge.v1;
        int v2 = edge.v2;

        // Identifie les sommets opposés (non sur l'arête)
        int a = FindOppositeVertex(tri1, v1, v2);
        int b = FindOppositeVertex(tri2, v1, v2);
        A = verts[a];
        B = verts[b];

        // C et D : voisins de A dans son triangle
        GetOtherTwoVertices(tri1, v1, v2, out int c, out int d);
        C = verts[c];
        D = verts[d];

        // E et F : voisins de B dans son triangle
        GetOtherTwoVertices(tri2, v1, v2, out int e, out int f);
        E = verts[e];
        F = verts[f];

        return true;
    }

    int FindOppositeVertex(int[] triangle, int v1, int v2)
    {
        foreach (int v in triangle)
        {
            if (v != v1 && v != v2)
                return v;
        }
        return -1; // Ne devrait jamais arriver
    }

    void GetOtherTwoVertices(int[] triangle, int v1, int v2, out int vOther1, out int vOther2)
    {
        List<int> result = new List<int>();
        foreach (int v in triangle)
        {
            if (v != v1 && v != v2)
                result.Add(v);
        }
        vOther1 = result[0];
        vOther2 = result.Count > 1 ? result[1] : result[0];
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
