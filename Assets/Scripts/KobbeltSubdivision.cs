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
    
    // Structure pour représenter une arête
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
            if (obj is Edge other)
                return v1 == other.v1 && v2 == other.v2;
            return false;
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
        
        // Sauvegarder le mesh original
        originalMesh = Instantiate(meshFilter.mesh);
        
        // Appliquer la subdivision
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
        
        Debug.Log($"√3-Kobbelt: Input mesh has {oldVertices.Length} vertices, {oldTriangles.Length/3} triangles");
        
        // ÉTAPE 1: Diviser chaque triangle au centre (1-to-3 scheme)
        List<Vector3> newVertices = new List<Vector3>(oldVertices);
        List<int> newTriangles = new List<int>();
        Dictionary<int, List<int>> vertexNeighbors = new Dictionary<int, List<int>>();
        HashSet<Edge> originalEdges = new HashSet<Edge>();
        
        // Construire la topologie originale
        BuildOriginalTopology(oldTriangles, vertexNeighbors, originalEdges);
        
        // Ajouter les centres des triangles comme nouveaux vertices
        for (int i = 0; i < oldTriangles.Length; i += 3)
        {
            int v1 = oldTriangles[i];
            int v2 = oldTriangles[i + 1];
            int v3 = oldTriangles[i + 2];
            
            // Calculer le centre du triangle: C = (V1 + V2 + V3) / 3
            Vector3 center = (oldVertices[v1] + oldVertices[v2] + oldVertices[v3]) / 3f;
            int centerIndex = newVertices.Count;
            newVertices.Add(center);
            
            // Créer 3 nouveaux triangles connectant le centre à chaque vertex
            newTriangles.AddRange(new int[] { v1, v2, centerIndex });
            newTriangles.AddRange(new int[] { v2, v3, centerIndex });
            newTriangles.AddRange(new int[] { v3, v1, centerIndex });
        }
        
        // ÉTAPE 2: Perturber les vertices originaux
        PerturbOriginalVertices(oldVertices, vertexNeighbors, newVertices);
        
        // ÉTAPE 3: "Flipper" les arêtes originales
        FlipOriginalEdges(newTriangles, originalEdges, oldVertices.Length);
        
        Debug.Log($"√3-Kobbelt: Output mesh has {newVertices.Count} vertices, {newTriangles.Count/3} triangles");
        
        // Créer le nouveau mesh
        Mesh newMesh = new Mesh();
        newMesh.vertices = newVertices.ToArray();
        newMesh.triangles = newTriangles.ToArray();
        newMesh.RecalculateNormals();
        newMesh.RecalculateBounds();
        
        return newMesh;
    }
    
    void BuildOriginalTopology(int[] triangles, Dictionary<int, List<int>> vertexNeighbors, 
                              HashSet<Edge> originalEdges)
    {
        // Initialiser les voisinages
        HashSet<int> allVertices = new HashSet<int>(triangles);
        foreach (int vertex in allVertices)
        {
            vertexNeighbors[vertex] = new List<int>();
        }
        
        // Construire les relations de voisinage et collecter les arêtes originales
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v1 = triangles[i];
            int v2 = triangles[i + 1];
            int v3 = triangles[i + 2];
            
            // Ajouter les voisins
            AddNeighborBidirectional(vertexNeighbors, v1, v2);
            AddNeighborBidirectional(vertexNeighbors, v2, v3);
            AddNeighborBidirectional(vertexNeighbors, v3, v1);
            
            // Collecter les arêtes originales
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
    
    void PerturbOriginalVertices(Vector3[] oldVertices, Dictionary<int, List<int>> vertexNeighbors, 
                               List<Vector3> newVertices)
    {
        // ÉTAPE 2: Perturber les vertices originaux selon la formule de Kobbelt
        for (int i = 0; i < oldVertices.Length; i++)
        {
            List<int> neighbors = vertexNeighbors[i];
            int n = neighbors.Count; // valence du vertex
            
            // Calculer α_n selon la formule: α_n = (1/n) * [1/4 - 2*cos(2π/n)²]
            float alpha = CalculateKobbeltAlpha(n);
            
            // Calculer la somme des voisins
            Vector3 neighborSum = Vector3.zero;
            foreach (int neighborIndex in neighbors)
            {
                neighborSum += oldVertices[neighborIndex];
            }
            
            // Formule de perturbation: V' = (1 - n*α)*V + α*∑neighbors
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

    
    void FlipOriginalEdges(List<int> triangles, HashSet<Edge> originalEdges, int originalVertexCount)
    {
        // ÉTAPE 3: "Flipper" les arêtes originales
        // Une arête est "flippée" si elle connectait deux vertices originaux
        // et maintenant elle doit connecter les centres des triangles adjacents
        
        // Créer une map des triangles pour faciliter la recherche
        Dictionary<Edge, List<int>> edgeToTriangles = new Dictionary<Edge, List<int>>();
        
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int v1 = triangles[i];
            int v2 = triangles[i + 1];
            int v3 = triangles[i + 2];
            int triangleIndex = i / 3;
            
            // Identifier les arêtes de ce triangle
            Edge e1 = new Edge(v1, v2);
            Edge e2 = new Edge(v2, v3);
            Edge e3 = new Edge(v3, v1);
            
            AddTriangleToEdge(edgeToTriangles, e1, triangleIndex);
            AddTriangleToEdge(edgeToTriangles, e2, triangleIndex);
            AddTriangleToEdge(edgeToTriangles, e3, triangleIndex);
        }
        
        // Maintenant, "flipper" signifie remplacer les connexions aux vertices originaux
        // par des connexions aux centres des triangles
        List<int> flippedTriangles = new List<int>();
        
        for (int i = 0; i < triangles.Count; i += 3)
        {
            int v1 = triangles[i];
            int v2 = triangles[i + 1];
            int v3 = triangles[i + 2];
            
            // Vérifier quels vertices sont des centres (index >= originalVertexCount)
            bool v1IsCenter = v1 >= originalVertexCount;
            bool v2IsCenter = v2 >= originalVertexCount;
            bool v3IsCenter = v3 >= originalVertexCount;
            
            // Si exactement un vertex est un centre, c'est un triangle valide après flip
            int centerCount = (v1IsCenter ? 1 : 0) + (v2IsCenter ? 1 : 0) + (v3IsCenter ? 1 : 0);
            
            if (centerCount == 1)
            {
                // Garder ce triangle
                flippedTriangles.AddRange(new int[] { v1, v2, v3 });
            }
        }
        
        // Remplacer les triangles
        triangles.Clear();
        triangles.AddRange(flippedTriangles);
    }
    
    void AddTriangleToEdge(Dictionary<Edge, List<int>> edgeToTriangles, Edge edge, int triangleIndex)
    {
        if (!edgeToTriangles.ContainsKey(edge))
            edgeToTriangles[edge] = new List<int>();
        edgeToTriangles[edge].Add(triangleIndex);
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