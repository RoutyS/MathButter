using System.Collections.Generic;
using UnityEngine;

/// ------------------------------------------------------------------
///  ButterflySubdivision  –  subdivision Butterfly récursive CORRIGÉE
/// ------------------------------------------------------------------
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

        // Nettoyage des anciens points debug
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("ButterflyPoint"))
            Destroy(go);

        Mesh original = inputMeshFilter.sharedMesh;
        Mesh subdivided = original;

        // Appliquer plusieurs niveaux de subdivision
        for (int level = 0; level < subdivisionLevels; level++)
        {
            Debug.Log($"🔄 Application du niveau de subdivision {level + 1}/{subdivisionLevels}");
            subdivided = SubdivideButterfly(subdivided);
        }

        Debug.Log($"✅ Subdivision terminée ! Vertices: {original.vertexCount} → {subdivided.vertexCount}");

        // Affichage du résultat
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

        // Construction des structures topologiques
        Dictionary<Edge, List<int>> edgeToTriangles = BuildEdgeToTriangles(inputMesh);
        Dictionary<Edge, int> edgeToNewVertex = new();
        List<Vector3> newVertices = new(vertsBase);
        List<int> newTriangles = new();

        // Pour chaque triangle original, créer 4 nouveaux triangles
        for (int i = 0; i < trisBase.Length; i += 3)
        {
            int v0 = trisBase[i];
            int v1 = trisBase[i + 1];
            int v2 = trisBase[i + 2];

            // Calculer les nouveaux sommets sur les arêtes
            int mid01 = GetOrCreateMidpoint(new Edge(v0, v1), vertsBase, edgeToTriangles, edgeToNewVertex, newVertices, inputMesh);
            int mid12 = GetOrCreateMidpoint(new Edge(v1, v2), vertsBase, edgeToTriangles, edgeToNewVertex, newVertices, inputMesh);
            int mid20 = GetOrCreateMidpoint(new Edge(v2, v0), vertsBase, edgeToTriangles, edgeToNewVertex, newVertices, inputMesh);

            // Créer 4 nouveaux triangles (ordre correct pour éviter l'inversion des faces)

            // ─────────────────── TRIANGULATION CONSISTENTE (sens antihoraire) ───────────────────
            // Triangle coin v0
            newTriangles.AddRange(new[] { v0, mid01, mid20 });
            // Triangle coin v1
            newTriangles.AddRange(new[] { v1, mid12, mid01 });
            // Triangle coin v2
            newTriangles.AddRange(new[] { v2, mid20, mid12 });
            // Triangle central
            newTriangles.AddRange(new[] { mid01, mid12, mid20 });
            // ─────────────────────────────────────────────────────────────────────────────────────


        }

        Mesh result = new Mesh();

        ReprojectToSphere(ref newVertices);

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
        // Si le midpoint existe déjà, le retourner
        if (edgeToNewVertex.TryGetValue(edge, out int existingIndex))
            return existingIndex;

        Vector3 v1 = originalVertices[edge.v1];
        Vector3 v2 = originalVertices[edge.v2];
        Vector3 midpoint;

        // Essayer d'appliquer le masque Butterfly
        if (TryCalculateButterflyMidpoint(edge, originalVertices, edgeToTriangles, mesh, out midpoint))
        {
            if (showDebugLogs)
                Debug.Log($"✅ Butterfly appliqué pour arête {edge.v1}-{edge.v2}");
        }
        else
        {
            // Fallback : simple moyenne
            midpoint = 0.5f * (v1 + v2);
            if (showDebugLogs)
                Debug.Log($"⚠️ Fallback pour arête {edge.v1}-{edge.v2} (arête de bord ou pas assez de voisins)");
        }

        newVertices.Add(midpoint);
        int newIndex = newVertices.Count - 1;
        edgeToNewVertex[edge] = newIndex;

        // Debug visuel
        if (showDebugPoints)
            CreateDebugPoint(midpoint);

        return newIndex;
    }

    bool TryCalculateButterflyMidpoint(Edge edge, Vector3[] vertices,
                                      Dictionary<Edge, List<int>> edgeToTriangles,
                                      Mesh mesh, out Vector3 midpoint)
    {
        midpoint = Vector3.zero;

        // Vérifier que l'arête a exactement 2 triangles adjacents
        if (!edgeToTriangles.TryGetValue(edge, out List<int> triangleIndices) || triangleIndices.Count != 2)
        {
            return false; // Arête de bord
        }

        int[] triangles = mesh.triangles;

        // Trouver les sommets opposés dans chaque triangle
        int opposite1 = GetOppositeVertex(triangles, triangleIndices[0], edge);
        int opposite2 = GetOppositeVertex(triangles, triangleIndices[1], edge);

        if (opposite1 == -1 || opposite2 == -1)
            return false;

        Vector3 v1 = vertices[edge.v1];
        Vector3 v2 = vertices[edge.v2];
        Vector3 A = vertices[opposite1]; // Premier sommet opposé
        Vector3 B = vertices[opposite2]; // Deuxième sommet opposé

        // Pour un cube, utiliser le masque Butterfly simplifié qui fonctionne mieux
        // Masque classique à 4 points : poids 1/2 pour les sommets de l'arête, 1/8 pour les opposés
        midpoint = 0.5f * (v1 + v2) + 0.125f * (A + B);

        return true;
    }

    List<Vector3> FindOuterVertices(Edge centralEdge, int opposite1, int opposite2,
                                   Vector3[] vertices, int[] triangles,
                                   Dictionary<Edge, List<int>> edgeToTriangles)
    {
        List<Vector3> outerVertices = new();
        HashSet<int> usedVertices = new HashSet<int> { centralEdge.v1, centralEdge.v2, opposite1, opposite2 };

        // Chercher les voisins du premier sommet opposé
        FindVertexNeighbors(opposite1, usedVertices, vertices, triangles, edgeToTriangles, outerVertices, 2);

        // Chercher les voisins du deuxième sommet opposé
        FindVertexNeighbors(opposite2, usedVertices, vertices, triangles, edgeToTriangles, outerVertices, 2);

        return outerVertices;
    }

    void FindVertexNeighbors(int vertexIndex, HashSet<int> usedVertices, Vector3[] vertices,
                            int[] triangles, Dictionary<Edge, List<int>> edgeToTriangles,
                            List<Vector3> result, int maxCount)
    {
        int added = 0;

        // Parcourir toutes les arêtes pour trouver celles qui touchent ce sommet
        foreach (var kvp in edgeToTriangles)
        {
            if (added >= maxCount) break;

            Edge edge = kvp.Key;
            if (edge.v1 == vertexIndex || edge.v2 == vertexIndex)
            {
                int otherVertex = (edge.v1 == vertexIndex) ? edge.v2 : edge.v1;
                if (!usedVertices.Contains(otherVertex))
                {
                    result.Add(vertices[otherVertex]);
                    usedVertices.Add(otherVertex);
                    added++;
                }
            }
        }
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

    void ReprojectToSphere(ref List<Vector3> vertices)
    {
        // Calcul du centre
        Vector3 center = Vector3.zero;
        foreach (Vector3 v in vertices)
            center += v;
        center /= vertices.Count;

        // Rayon moyen
        float radius = 0f;
        foreach (Vector3 v in vertices)
            radius += (v - center).magnitude;
        radius /= vertices.Count;

        // Projection sur sphère
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 dir = (vertices[i] - center).normalized;
            vertices[i] = center + dir * radius;
        }
    }


    private struct Edge
    {
        public int v1, v2;

        public Edge(int a, int b)
        {
            v1 = Mathf.Min(a, b);
            v2 = Mathf.Max(a, b);
        }

        public override bool Equals(object obj)
        {
            return obj is Edge edge && v1 == edge.v1 && v2 == edge.v2;
        }

        public override int GetHashCode()
        {
            return v1.GetHashCode() ^ (v2.GetHashCode() << 2);
        }

        public override string ToString()
        {
            return $"({v1}, {v2})";
        }
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