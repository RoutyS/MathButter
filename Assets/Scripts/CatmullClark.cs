using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CatmullClark : MonoBehaviour
{
    [Header("Catmull-Clark Subdivision")]
    [Range(0, 5)] public int subdivisionLevels = 1;

    private MeshFilter meshFilter;
    private Mesh originalMesh;

    // Classes internes pour Edge et Face (identiques à ton script)
    public class Edge
    {
        public int v1, v2;
        public List<int> adjacentFaces;

        public Edge(int a, int b)
        {
            v1 = Mathf.Min(a, b);
            v2 = Mathf.Max(a, b);
            adjacentFaces = new List<int>();
        }

        public override bool Equals(object obj)
        {
            return obj is Edge other && v1 == other.v1 && v2 == other.v2;
        }

        public override int GetHashCode()
        {
            return v1 * 100000 + v2;
        }
    }

    public class Face
    {
        public List<int> vertices;
        public Vector3 facePoint;

        public Face(List<int> verts)
        {
            vertices = new List<int>(verts);
        }
    }

    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();

        originalMesh = CreateCubeMesh();
        meshFilter.mesh = originalMesh;

        ApplyCatmullClarkSubdivision();
    }

    [ContextMenu("Apply Catmull-Clark Subdivision")]
    public void ApplyCatmullClarkSubdivision()
    {
        if (originalMesh == null || meshFilter == null) return;

        Mesh currentMesh = Instantiate(originalMesh);

        for (int level = 0; level < subdivisionLevels; level++)
        {
            currentMesh = SubdivideMesh(currentMesh);
        }

        meshFilter.mesh = currentMesh;
    }

    Mesh CreateCubeMesh()
    {
        // Cube 8 vertices, 12 triangles (2 per face * 6 faces)
        Vector3[] verts = new Vector3[]
        {
            new Vector3(-1,-1,-1), //0
            new Vector3(1,-1,-1),  //1
            new Vector3(1,-1,1),   //2
            new Vector3(-1,-1,1),  //3
            new Vector3(-1,1,-1),  //4
            new Vector3(1,1,-1),   //5
            new Vector3(1,1,1),    //6
            new Vector3(-1,1,1)    //7
        };

        int[] tris = new int[]
        {
            // Bottom
            0,2,1,
            0,3,2,
            // Top
            4,5,6,
            4,6,7,
            // Front
            3,6,2,
            3,7,6,
            // Back
            0,1,5,
            0,5,4,
            // Left
            0,4,7,
            0,7,3,
            // Right
            1,2,6,
            1,6,5
        };

        Mesh m = new Mesh();
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }

    Mesh SubdivideMesh(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        List<Face> faces = ConvertTrianglesToFaces(triangles);
        Dictionary<Edge, int> edgeToIndex = BuildEdgeStructure(faces);
        List<Edge> edges = edgeToIndex.Keys.ToList();

        CalculateFacePoints(vertices, faces);
        List<Vector3> edgePoints = CalculateEdgePoints(vertices, faces, edges);
        List<Vector3> vertexPoints = CalculateNewVertexPoints(vertices, faces, edges);

        return BuildNewMesh(vertices, faces, edges, edgePoints, vertexPoints, edgeToIndex);
    }

    List<Face> ConvertTrianglesToFaces(int[] triangles)
    {
        var faces = new List<Face>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            faces.Add(new Face(new List<int> { triangles[i], triangles[i + 1], triangles[i + 2] }));
        }
        return faces;
    }

    Dictionary<Edge, int> BuildEdgeStructure(List<Face> faces)
    {
        Dictionary<Edge, int> edgeToIndex = new Dictionary<Edge, int>();
        Dictionary<Edge, Edge> edgeInstances = new Dictionary<Edge, Edge>();
        int counter = 0;

        for (int f = 0; f < faces.Count; f++)
        {
            var face = faces[f];
            for (int i = 0; i < face.vertices.Count; i++)
            {
                int v1 = face.vertices[i];
                int v2 = face.vertices[(i + 1) % face.vertices.Count];
                Edge edge = new Edge(v1, v2);

                if (!edgeToIndex.ContainsKey(edge))
                {
                    edgeToIndex[edge] = counter++;
                    edgeInstances[edge] = edge;
                }

                edgeInstances[edge].adjacentFaces.Add(f);
            }
        }

        var result = new Dictionary<Edge, int>();
        foreach (var kvp in edgeToIndex)
        {
            result[edgeInstances[kvp.Key]] = kvp.Value;
        }

        return result;
    }

    void CalculateFacePoints(Vector3[] vertices, List<Face> faces)
    {
        foreach (var face in faces)
        {
            Vector3 sum = Vector3.zero;
            foreach (var v in face.vertices)
                sum += vertices[v];
            face.facePoint = sum / face.vertices.Count;
        }
    }

    List<Vector3> CalculateEdgePoints(Vector3[] vertices, List<Face> faces, List<Edge> edges)
    {
        List<Vector3> edgePoints = new List<Vector3>();

        foreach (Edge edge in edges)
        {
            Vector3 v1 = vertices[edge.v1];
            Vector3 v2 = vertices[edge.v2];
            Vector3 edgePoint;

            if (edge.adjacentFaces.Count == 2)
            {
                Vector3 f1 = faces[edge.adjacentFaces[0]].facePoint;
                Vector3 f2 = faces[edge.adjacentFaces[1]].facePoint;
                edgePoint = (v1 + v2 + f1 + f2) / 4f;
            }
            else
            {
                edgePoint = (v1 + v2) * 0.5f;
            }

            edgePoints.Add(edgePoint);
        }

        return edgePoints;
    }

    List<Vector3> CalculateNewVertexPoints(Vector3[] vertices, List<Face> faces, List<Edge> edges)
    {
        List<Vector3> newVerts = new List<Vector3>();

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 P = vertices[i];

            var touchingFaces = faces.Where(f => f.vertices.Contains(i)).ToList();
            var touchingEdges = edges.Where(e => e.v1 == i || e.v2 == i).ToList();
            int n = touchingFaces.Count;

            bool boundary = touchingEdges.Any(e => e.adjacentFaces.Count == 1);

            if (boundary)
            {
                var boundaryEdges = touchingEdges.Where(e => e.adjacentFaces.Count == 1).ToList();

                if (boundaryEdges.Count == 2)
                {
                    Vector3 p1 = vertices[boundaryEdges[0].v1 == i ? boundaryEdges[0].v2 : boundaryEdges[0].v1];
                    Vector3 p2 = vertices[boundaryEdges[1].v1 == i ? boundaryEdges[1].v2 : boundaryEdges[1].v1];
                    Vector3 newV = (6f * P + p1 + p2) / 8f;
                    newVerts.Add(newV);
                }
                else
                {
                    newVerts.Add(P);
                }
            }
            else
            {
                Vector3 F = Vector3.zero;
                foreach (var face in touchingFaces)
                    F += face.facePoint;
                F /= n;

                Vector3 R = Vector3.zero;
                foreach (var edge in touchingEdges)
                {
                    Vector3 vA = vertices[edge.v1];
                    Vector3 vB = vertices[edge.v2];
                    Vector3 midpoint = (vA + vB) * 0.5f;
                    R += midpoint;
                }
                R /= n;

                Vector3 newV = (F + 2f * R + (n - 3f) * P) / n;
                newVerts.Add(newV);
            }
        }

        return newVerts;
    }

    Mesh BuildNewMesh(Vector3[] originalVertices, List<Face> faces, List<Edge> edges,
                      List<Vector3> edgePoints, List<Vector3> newVertexPoints,
                      Dictionary<Edge, int> edgeToIndex)
    {
        List<Vector3> allVerts = new List<Vector3>();
        allVerts.AddRange(newVertexPoints);
        int edgeOffset = allVerts.Count;
        allVerts.AddRange(edgePoints);
        int faceOffset = allVerts.Count;
        foreach (var face in faces)
            allVerts.Add(face.facePoint);

        List<int> newTriangles = new List<int>();

        for (int f = 0; f < faces.Count; f++)
        {
            var face = faces[f];
            int facePtIndex = faceOffset + f;

            int count = face.vertices.Count;
            for (int i = 0; i < count; i++)
            {
                int curr = face.vertices[i];
                int next = face.vertices[(i + 1) % count];
                int prev = face.vertices[(i - 1 + count) % count];

                Edge edge1 = new Edge(curr, next);
                Edge edge0 = new Edge(prev, curr);

                int edgePt1 = -1, edgePt0 = -1;

                foreach(var e in edgeToIndex)
                {
                    if (e.Key.Equals(edge1))
                        edgePt1 = edgeOffset + e.Value;
                    if (e.Key.Equals(edge0))
                        edgePt0 = edgeOffset + e.Value;
                }
                int newV = curr;
                newTriangles.AddRange(new int[] {
                    newV, edgePt1, facePtIndex,
                    newV, facePtIndex, edgePt0
                });
            }
        }

        Mesh newMesh = new Mesh();
        newMesh.vertices = allVerts.ToArray();
        newMesh.triangles = newTriangles.ToArray();
        newMesh.RecalculateNormals();
        return newMesh;
    }
}