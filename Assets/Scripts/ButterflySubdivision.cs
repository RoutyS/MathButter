using System.Collections.Generic;
using UnityEngine;

public class ButterflySubdivision : MonoBehaviour
{
    [Range(1, 4)] public int subdivisionLevels = 1;

    public void ApplySubdivision()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null || mf.mesh == null) { Debug.LogError("Pas de MeshFilter/Mesh !"); return; }
        Mesh mesh = mf.mesh;
        for (int i = 0; i < subdivisionLevels; i++)
            mesh = SubdivideButterfly(mesh);
        mf.mesh = mesh;
    }

    public Mesh SubdivideButterfly(Mesh mesh)
    {
        Vector3[] V = mesh.vertices;
        int[] T = mesh.triangles;

        var edgeOpp = new Dictionary<Edge, List<int>>(new EdgeComparer());
        var vertNeigh = new Dictionary<int, HashSet<int>>();

        for (int i = 0; i < T.Length; i += 3)
        {
            int a = T[i], b = T[i + 1], c = T[i + 2];
            AddNeigh(vertNeigh, a, b); AddNeigh(vertNeigh, b, c); AddNeigh(vertNeigh, c, a);
            AddOpp(edgeOpp, new Edge(a, b), c);
            AddOpp(edgeOpp, new Edge(b, c), a);
            AddOpp(edgeOpp, new Edge(c, a), b);
        }

        var newV = new List<Vector3>(V);
        var edgeToNew = new Dictionary<Edge, int>(new EdgeComparer());

        foreach (var kv in edgeOpp)
        {
            Edge e = kv.Key;
            var opp = kv.Value;
            Vector3 pos;
            if (opp.Count < 2)
                pos = 0.5f * (V[e.v1] + V[e.v2]);
            else
            {
                int C = opp[0], D = opp[1];
                Vector3 sum = V[e.v1] + V[e.v2];
                pos = 0.5f * sum + 0.125f * (V[C] + V[D]);
                List<int> EFGH = FindEFGH(e, C, D, edgeOpp);
                if (EFGH.Count == 4)
                {
                    pos -= 0.0625f * (V[EFGH[0]] + V[EFGH[1]] + V[EFGH[2]] + V[EFGH[3]]);
                }
                else
                {
                    Vector3 sa = AverageNeighbors(V, vertNeigh[e.v1], e.v1);
                    Vector3 sb = AverageNeighbors(V, vertNeigh[e.v2], e.v2);
                    pos = 0.75f * (V[e.v1] + V[e.v2]) * 0.5f + 0.125f * (sa + sb);
                }
            }
            int idx = newV.Count;
            newV.Add(pos);
            edgeToNew[e] = idx;
        }

        var newT = new List<int>();
        for (int i = 0; i < T.Length; i += 3)
        {
            int a = T[i], b = T[i + 1], c = T[i + 2];
            int ab = edgeToNew[new Edge(a, b)];
            int bc = edgeToNew[new Edge(b, c)];
            int ca = edgeToNew[new Edge(c, a)];
            newT.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }

        Mesh newMesh = new Mesh();
        newMesh.vertices = newV.ToArray();
        newMesh.triangles = newT.ToArray();
        newMesh.RecalculateNormals();
        return newMesh;
    }

    static void AddNeigh(Dictionary<int, HashSet<int>> d, int a, int b)
    {
        if (!d.ContainsKey(a)) d[a] = new HashSet<int>();
        if (!d.ContainsKey(b)) d[b] = new HashSet<int>();
        d[a].Add(b); d[b].Add(a);
    }

    static void AddOpp(Dictionary<Edge, List<int>> d, Edge e, int opp)
    {
        if (!d.ContainsKey(e)) d[e] = new List<int>();
        if (d[e].Count < 2) d[e].Add(opp);
    }

    static List<int> FindEFGH(Edge edge, int C, int D, Dictionary<Edge, List<int>> edgeOpp)
    {
        List<int> result = new List<int>(4);
        Edge eAC = new Edge(edge.v1, C);
        Edge eAD = new Edge(edge.v1, D);
        Edge eBC = new Edge(edge.v2, C);
        Edge eBD = new Edge(edge.v2, D);
        int E = OppOther(eAC, C, edgeOpp);
        int F = OppOther(eAD, D, edgeOpp);
        int G = OppOther(eBC, C, edgeOpp);
        int H = OppOther(eBD, D, edgeOpp);
        if (E != -1 && F != -1 && G != -1 && H != -1)
            result.AddRange(new[] { E, F, G, H });
        return result;
    }

    static int OppOther(Edge e, int known, Dictionary<Edge, List<int>> d)
    {
        if (d.TryGetValue(e, out var ls) && ls.Count == 2)
            return ls[0] == known ? ls[1] : ls[0];
        return -1;
    }

    static Vector3 AverageNeighbors(Vector3[] V, HashSet<int> neigh, int i)
    {
        Vector3 sum = Vector3.zero;
        foreach (int j in neigh) sum += V[j];
        return sum / Mathf.Max(1, neigh.Count);
    }

    struct Edge { public int v1, v2;
        public Edge(int a, int b) { v1 = Mathf.Min(a, b); v2 = Mathf.Max(a, b); }
    }
    class EdgeComparer : IEqualityComparer<Edge>
    {
        public bool Equals(Edge x, Edge y) => x.v1 == y.v1 && x.v2 == y.v2;
        public int GetHashCode(Edge e) => e.v1 * 73856093 ^ e.v2 * 19349669;
    }
}

public struct Edge
{
    public int v1, v2;

    public Edge(int a, int b)
    {
        v1 = Mathf.Min(a, b);
        v2 = Mathf.Max(a, b);
    }

    public override bool Equals(object obj)
    {
        if (!(obj is Edge)) return false;
        Edge other = (Edge)obj;
        return v1 == other.v1 && v2 == other.v2;
    }

    public override int GetHashCode()
    {
        return v1 * 31 + v2;
    }
}

public class EdgeComparer : IEqualityComparer<Edge>
{
    public bool Equals(Edge x, Edge y) => x.v1 == y.v1 && x.v2 == y.v2;
    public int GetHashCode(Edge obj) => obj.GetHashCode();
}

