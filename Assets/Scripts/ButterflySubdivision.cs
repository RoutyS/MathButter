using System.Collections.Generic;
using UnityEngine;

/// ------------------------------------------------------------------
///  ButterflySubdivision  –  subdivision Butterfly récursive
/// ------------------------------------------------------------------
public class ButterflySubdivision : MonoBehaviour
{
    [Header("Mesh d’entrée (si laissé vide, appuyer sur B cherchera « CoonsMesh »)")]
    public MeshFilter inputMeshFilter;

    // ── debug visuel ────────────────────────────────────────────────
    private readonly List<(Vector3, Vector3)> debugLines = new();

    // =================================================================
    //  MISE À JOUR – touche B pour subdiviser l’objet courant
    // =================================================================
    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.B)) return;

        // (re)chercher un Mesh si nécessaire
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

        // nettoyage éventuel des anciens points debug
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("ButterflyPoint"))
            Destroy(go);

        // ----------------------------------------------------------------
        Mesh original = inputMeshFilter.sharedMesh;
        Mesh subdivided = SubdivideButterfly(original);          // 1ʳᵉ passe
        // ➜ faites plusieurs passes si nécessaire ici

        // Affichage
        GameObject res = new("ButterflyResult");
        var mf = res.AddComponent<MeshFilter>();
        var mr = res.AddComponent<MeshRenderer>();
        mf.mesh = subdivided;
        mr.material = new Material(Shader.Find("Standard")) { color = Color.cyan };
        res.transform.SetPositionAndRotation(
            inputMeshFilter.transform.position,
            inputMeshFilter.transform.rotation);
    }

    // =================================================================
    //  PASSE DE SUBDIVISION
    // =================================================================
    public Mesh SubdivideButterfly(Mesh inputMesh)
    {
        Vector3[] vertsBase = inputMesh.vertices;
        int[] trisBase = inputMesh.triangles;

        Dictionary<Edge, List<int>> edgeToTri = BuildEdgeToTriangles(inputMesh);
        Dictionary<Edge, int> edgeToNew = new();
        List<Vector3> newVerts = new(vertsBase);
        List<int> newTris = new();

        for (int i = 0; i < trisBase.Length; i += 3)
        {
            int a = trisBase[i];
            int b = trisBase[i + 1];
            int c = trisBase[i + 2];

            int ab = GetOrCreateMid(vertsBase[a], vertsBase[b], new Edge(a, b),
                                    edgeToTri, edgeToNew, newVerts, inputMesh);
            int bc = GetOrCreateMid(vertsBase[b], vertsBase[c], new Edge(b, c),
                                    edgeToTri, edgeToNew, newVerts, inputMesh);
            int ca = GetOrCreateMid(vertsBase[c], vertsBase[a], new Edge(c, a),
                                    edgeToTri, edgeToNew, newVerts, inputMesh);

            // 4 triangles
            newTris.AddRange(new[] { a, ab, ca });
            newTris.AddRange(new[] { ab, b, bc });
            newTris.AddRange(new[] { ca, bc, c });
            newTris.AddRange(new[] { ab, bc, ca });
        }

        Mesh m = new();
        m.vertices = newVerts.ToArray();
        m.triangles = newTris.ToArray();
        m.RecalculateNormals();
        return m;
    }

    // =================================================================
    //  CALCUL / CRÉATION D’UN MIDPOINT BUTTERFLY
    // =================================================================
    int GetOrCreateMid(Vector3 v1, Vector3 v2, Edge e,
                       Dictionary<Edge, List<int>> edgeToTri,
                       Dictionary<Edge, int> edgeToNew,
                       List<Vector3> newVerts, Mesh currentMesh)
    {
        if (edgeToNew.TryGetValue(e, out int idx)) return idx;

        Vector3 mid;
        if (TryGetButterflyNeighbors(currentMesh, edgeToTri, e,
                                     out Vector3 A, out Vector3 B,
                                     out Vector3 C, out Vector3 D,
                                     out Vector3 E, out Vector3 F))
        {
            mid = 0.5f * (v1 + v2) + 0.125f * (A + B) - 0.0625f * (C + D + E + F);
        }
        else
        {
            mid = 0.5f * (v1 + v2); // fallback
        }

        newVerts.Add(mid);
        int newIndex = newVerts.Count - 1;
        edgeToNew[e] = newIndex;

        // ── points & lignes debug (optionnel) ─────────────────────
        

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = mid;
        sphere.transform.localScale = Vector3.one * 0.03f;
        sphere.GetComponent<Renderer>().material.color = Color.red;
        sphere.name = "ButterflyPoint";
        sphere.transform.SetParent(inputMeshFilter.transform, false); // <-- AJOUTÉ


        debugLines.Add((transform.TransformPoint(v1), transform.TransformPoint(mid)));
        debugLines.Add((transform.TransformPoint(v2), transform.TransformPoint(mid)));
        debugLines.Add((0.5f * (transform.TransformPoint(v1) + transform.TransformPoint(v2)),
                        transform.TransformPoint(mid)));

        return newIndex;
    }

    // -----------------------------------------------------------------
    //  TOPOLOGIE : arêtes → triangles
    Dictionary<Edge, List<int>> BuildEdgeToTriangles(Mesh mesh)
    {
        Dictionary<Edge, List<int>> map = new();
        int[] t = mesh.triangles;

        for (int i = 0; i < t.Length; i += 3)
        {
            Edge[] edges = {
                new Edge(t[i],     t[i+1]),
                new Edge(t[i+1],   t[i+2]),
                new Edge(t[i+2],   t[i])
            };
            foreach (Edge e in edges)
            {
                if (!map.ContainsKey(e)) map[e] = new();
                map[e].Add(i);
            }
        }
        return map;
    }

    // -----------------------------------------------------------------
    //  RÉCUPÉRATION DES 6 VOISINS A-F
    bool TryGetButterflyNeighbors(Mesh mesh,
                                  Dictionary<Edge, List<int>> edgeToTri,
                                  Edge edge,
                                  out Vector3 A, out Vector3 B,
                                  out Vector3 C, out Vector3 D,
                                  out Vector3 E, out Vector3 F)
    {
        A = B = C = D = E = F = Vector3.zero;
        if (!edgeToTri.TryGetValue(edge, out List<int> faces) || faces.Count != 2)
            return false; // bord

        int[] tri = mesh.triangles;
        Vector3[] v = mesh.vertices;

        // sommets opposés
        int opp1 = GetOpposite(tri, faces[0], edge);
        int opp2 = GetOpposite(tri, faces[1], edge);
        A = v[opp1];
        B = v[opp2];

        // voisins autour de A et B
        List<Vector3> others = new();
        foreach (int f in faces)
        {
            int a = tri[f]; int b = tri[f + 1]; int c = tri[f + 2];
            foreach (int idx in new[] { a, b, c })
            {
                Vector3 p = v[idx];
                if (p != A && p != B && p != v[edge.v1] && p != v[edge.v2] && !others.Contains(p))
                    others.Add(p);
            }
        }

        if (others.Count < 4) return false;
        C = others[0]; D = others[1]; E = others[2]; F = others[3];
        return true;
    }

    int GetOpposite(int[] tri, int faceStart, Edge e)
    {
        for (int k = 0; k < 3; k++)
        {
            int idx = tri[faceStart + k];
            if (idx != e.v1 && idx != e.v2) return idx;
        }
        return -1;
    }

    // -----------------------------------------------------------------
    //  STRUCTURE D’UNE ARÊTE
    private struct Edge
    {
        public int v1, v2;
        public Edge(int a, int b) { v1 = Mathf.Min(a, b); v2 = Mathf.Max(a, b); }
        public override bool Equals(object o) => o is Edge e && v1 == e.v1 && v2 == e.v2;
        public override int GetHashCode() => v1.GetHashCode() ^ v2.GetHashCode();
    }

    // -----------------------------------------------------------------
    //  GIZMOS debug
    void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        foreach (var l in debugLines) Gizmos.DrawLine(l.Item1, l.Item2);
    }
}
