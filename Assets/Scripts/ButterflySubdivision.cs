// ✅ ButterflySubdivision.cs avec meilleure gestion des coins
// - Nouveau masque bord pour éviter artefacts
// - Cas régulier, irrégulier, bord améliorés

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ButterflySubdivision : MonoBehaviour
{
    public Mesh SubdivideButterfly(Mesh input)
    {
        var originalVertices = input.vertices;
        var originalTriangles = input.triangles;
        var edgeMidpointMap = new Dictionary<(int, int), int>();
        var newVertices = new List<Vector3>(originalVertices);
        var triangleMap = new Dictionary<int, List<int>>();

        for (int i = 0; i < originalTriangles.Length; i += 3)
        {
            int a = originalTriangles[i];
            int b = originalTriangles[i + 1];
            int c = originalTriangles[i + 2];
            AddTriangle(triangleMap, a, i / 3);
            AddTriangle(triangleMap, b, i / 3);
            AddTriangle(triangleMap, c, i / 3);
        }

        List<int> newTriangles = new();
        for (int i = 0; i < originalTriangles.Length; i += 3)
        {
            int v0 = originalTriangles[i];
            int v1 = originalTriangles[i + 1];
            int v2 = originalTriangles[i + 2];

            int m01 = GetOrCreateMidpoint(v0, v1, originalVertices, triangleMap, edgeMidpointMap, newVertices);
            int m12 = GetOrCreateMidpoint(v1, v2, originalVertices, triangleMap, edgeMidpointMap, newVertices);
            int m20 = GetOrCreateMidpoint(v2, v0, originalVertices, triangleMap, edgeMidpointMap, newVertices);

            newTriangles.AddRange(new[] {
                v0, m01, m20,
                m01, v1, m12,
                m20, m12, v2,
                m01, m12, m20
            });
        }

        Mesh subdivided = new Mesh();
        subdivided.vertices = newVertices.ToArray();
        subdivided.triangles = newTriangles.ToArray();
        subdivided.RecalculateNormals();
        return subdivided;
    }

    int GetOrCreateMidpoint(int a, int b, Vector3[] verts, Dictionary<int, List<int>> triangleMap, Dictionary<(int, int), int> edgeMap, List<Vector3> newVerts)
    {
        var key = (Mathf.Min(a, b), Mathf.Max(a, b));
        if (edgeMap.TryGetValue(key, out int idx)) return idx;

        Vector3 p1 = verts[a];
        Vector3 p2 = verts[b];

        Vector3 midpoint;
        List<int> adjacentA = triangleMap.ContainsKey(a) ? triangleMap[a] : new();
        List<int> adjacentB = triangleMap.ContainsKey(b) ? triangleMap[b] : new();
        var sharedTris = new HashSet<int>(adjacentA);
        sharedTris.IntersectWith(adjacentB);

        if (sharedTris.Count == 1)
        {
            // ✅ Cas coin/bord → poids custom plus doux
            midpoint = 0.375f * (p1 + p2);
            Vector3 avg1 = AverageNeighbor(verts, triangleMap, a);
            Vector3 avg2 = AverageNeighbor(verts, triangleMap, b);
            midpoint += 0.125f * (avg1 + avg2);
        }
        else if (sharedTris.Count == 2)
        {
            // ✅ Cas régulier
            midpoint = CalculateButterflyMask(a, b, verts, triangleMap);
        }
        else
        {
            // Fallback cas irrégulier
            midpoint = 0.5f * (p1 + p2);
        }

        newVerts.Add(midpoint);
        int newIndex = newVerts.Count - 1;
        edgeMap[key] = newIndex;
        return newIndex;
    }

    Vector3 CalculateButterflyMask(int i0, int i1, Vector3[] verts, Dictionary<int, List<int>> triangleMap)
    {
        Vector3 p0 = verts[i0];
        Vector3 p1 = verts[i1];
        Vector3 mid = 0.5f * (p0 + p1);

        Vector3[] opp = new Vector3[6];
        int count = 0;

        foreach (var tri in triangleMap[i0])
        {
            int[] face = GetTriangleVerts(tri, triangleMap);
            foreach (int v in face)
            {
                if (v != i0 && v != i1 && !opp.Contains(verts[v]))
                {
                    opp[count++] = verts[v];
                    if (count == 6) break;
                }
            }
            if (count == 6) break;
        }

        if (count < 2) return mid; // Sécurité

        Vector3 butterfly = 0.5f * (p0 + p1);
        butterfly += 0.125f * (opp[0] + opp[1]);
        if (count >= 6)
            butterfly -= 0.0625f * (opp[2] + opp[3] + opp[4] + opp[5]);

        return butterfly;
    }

    Vector3 AverageNeighbor(Vector3[] verts, Dictionary<int, List<int>> triangleMap, int idx)
    {
        HashSet<int> neighbors = new();
        foreach (int tri in triangleMap[idx])
        {
            int[] face = GetTriangleVerts(tri, triangleMap);
            foreach (int v in face) if (v != idx) neighbors.Add(v);
        }
        Vector3 sum = Vector3.zero;
        foreach (int v in neighbors) sum += verts[v];
        return neighbors.Count > 0 ? sum / neighbors.Count : verts[idx];
    }

    int[] GetTriangleVerts(int triIndex, Dictionary<int, List<int>> triangleMap)
    {
        foreach (var kvp in triangleMap)
        {
            foreach (int t in kvp.Value)
                if (t == triIndex) return new int[] { kvp.Key };
        }
        return new int[0];
    }

    void AddTriangle(Dictionary<int, List<int>> map, int vertexIndex, int triangleIndex)
    {
        if (!map.ContainsKey(vertexIndex))
            map[vertexIndex] = new List<int>();
        map[vertexIndex].Add(triangleIndex);
    }
}