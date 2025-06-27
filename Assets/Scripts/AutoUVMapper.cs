using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(MeshFilter))]
public class AutoUVMapper : MonoBehaviour
{

    public enum ProjectionMode
    {
        Spherical,
        Cylindrical,
        Planar,
        SmartProjection,
        PreserveOriginal
    }

    public ProjectionMode projectionMode = ProjectionMode.SmartProjection;

    [Header("Spherical Settings")]
    [Range(0f, 1f)]
    public float seamThreshold = 0.5f;
    public bool fixSphericalSeams = true;

    [Header("Cylindrical Settings")]
    public Vector3 cylinderAxis = Vector3.up;
    public bool normalizeHeight = true;

    [Header("Planar Settings")]
    public Vector3 planeNormal = Vector3.up;
    public Vector3 planeRight = Vector3.right;

    [Header("Smart Projection Settings")]
    public float angleThreshold = 45f;
    public bool useIslandDetection = true;

    [Header("Debug")]
    public bool showDebugInfo = false;

    private struct UVIsland
    {
        public List<int> triangles;
        public List<int> vertices;
        public Bounds uvBounds;

        public UVIsland(List<int> tris, List<int> verts)
        {
            triangles = new List<int>(tris);
            vertices = new List<int>(verts);
            uvBounds = new Bounds();
        }
    }

    public void GenerateUVs()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf?.mesh == null)
        {
            Debug.LogWarning("Missing MeshFilter or mesh.");
            return;
        }

        Mesh mesh = mf.mesh;

        switch (projectionMode)
        {
            case ProjectionMode.Spherical:
                GenerateSphericalUVs(mesh);
                break;
            case ProjectionMode.Cylindrical:
                GenerateCylindricalUVs(mesh);
                break;
            case ProjectionMode.Planar:
                GeneratePlanarUVs(mesh);
                break;
            case ProjectionMode.SmartProjection:
                GenerateSmartProjectionUVs(mesh);
                break;
            case ProjectionMode.PreserveOriginal:
                PreserveAndCleanUVs(mesh);
                break;
        }

        if (showDebugInfo)
        {
            AnalyzeUVQuality(mesh);
        }
    }

    void GenerateSphericalUVs(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = new Vector2[vertices.Length];

        // Calculer le centre du mesh
        Vector3 center = CalculateMeshCenter(vertices);

        // Premi�re passe : projection sph�rique basique
        for (int i = 0; i < vertices.Length; i++)
        {
            uvs[i] = ProjectSpherical(vertices[i], center);
        }

        // Deuxi�me passe : correction des coutures si activ�e
        if (fixSphericalSeams)
        {
            FixSphericalSeams(mesh, uvs, vertices);
        }

        mesh.uv = uvs;

        if (showDebugInfo)
            Debug.Log($"Generated spherical UVs for {vertices.Length} vertices");
    }

    void GenerateCylindricalUVs(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = new Vector2[vertices.Length];

        Vector3 center = CalculateMeshCenter(vertices);
        Vector3 axis = cylinderAxis.normalized;

        // Calculer les limites de hauteur pour la normalisation
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;

        if (normalizeHeight)
        {
            foreach (var vertex in vertices)
            {
                float height = Vector3.Dot(vertex - center, axis);
                minHeight = Mathf.Min(minHeight, height);
                maxHeight = Mathf.Max(maxHeight, height);
            }
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            uvs[i] = ProjectCylindrical(vertices[i], center, axis, minHeight, maxHeight);
        }

        mesh.uv = uvs;
    }

    void GeneratePlanarUVs(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = new Vector2[vertices.Length];

        Vector3 normal = planeNormal.normalized;
        Vector3 right = planeRight.normalized;
        Vector3 up = Vector3.Cross(normal, right).normalized;

        // Calculer les limites de projection
        float minU = float.MaxValue, maxU = float.MinValue;
        float minV = float.MaxValue, maxV = float.MinValue;

        Vector2[] tempUVs = new Vector2[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 localPos = vertices[i];
            float u = Vector3.Dot(localPos, right);
            float v = Vector3.Dot(localPos, up);

            tempUVs[i] = new Vector2(u, v);

            minU = Mathf.Min(minU, u);
            maxU = Mathf.Max(maxU, u);
            minV = Mathf.Min(minV, v);
            maxV = Mathf.Max(maxV, v);
        }

        // Normaliser les UVs dans [0,1]
        float rangeU = maxU - minU;
        float rangeV = maxV - minV;

        for (int i = 0; i < vertices.Length; i++)
        {
            uvs[i] = new Vector2(
                rangeU > 0 ? (tempUVs[i].x - minU) / rangeU : 0.5f,
                rangeV > 0 ? (tempUVs[i].y - minV) / rangeV : 0.5f
            );
        }

        mesh.uv = uvs;
    }

    void GenerateSmartProjectionUVs(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        if (normals == null || normals.Length != vertices.Length)
        {
            mesh.RecalculateNormals();
            normals = mesh.normals;
        }

        if (useIslandDetection)
        {
            List<UVIsland> islands = DetectUVIslands(mesh, normals);
            ApplyIslandProjection(mesh, islands, vertices, normals);
        }
        else
        {
            // Projection simple bas�e sur la normale dominante
            ApplyDominantAxisProjection(mesh, vertices, normals);
        }
    }

    void PreserveAndCleanUVs(Mesh mesh)
    {
        Vector2[] uvs = mesh.uv;

        if (uvs == null || uvs.Length != mesh.vertexCount)
        {
            Debug.LogWarning("No valid UVs to preserve, generating spherical UVs instead");
            GenerateSphericalUVs(mesh);
            return;
        }

        // Nettoyer les UVs existantes
        CleanUVs(uvs);
        mesh.uv = uvs;
    }

    Vector2 ProjectSpherical(Vector3 vertex, Vector3 center)
    {
        Vector3 dir = (vertex - center).normalized;

        // Utiliser atan2 pour une meilleure distribution
        float u = (Mathf.Atan2(dir.z, dir.x) + Mathf.PI) / (2f * Mathf.PI);
        float v = (Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) + Mathf.PI * 0.5f) / Mathf.PI;

        return new Vector2(u, v);
    }

    Vector2 ProjectCylindrical(Vector3 vertex, Vector3 center, Vector3 axis, float minHeight, float maxHeight)
    {
        Vector3 localPos = vertex - center;
        Vector3 radialDir = localPos - Vector3.Dot(localPos, axis) * axis;

        float u = (Mathf.Atan2(radialDir.z, radialDir.x) + Mathf.PI) / (2f * Mathf.PI);

        float v;
        if (normalizeHeight && maxHeight > minHeight)
        {
            float height = Vector3.Dot(localPos, axis);
            v = (height - minHeight) / (maxHeight - minHeight);
        }
        else
        {
            v = Vector3.Dot(localPos, axis) * 0.5f + 0.5f;
        }

        return new Vector2(u, Mathf.Clamp01(v));
    }

    void FixSphericalSeams(Mesh mesh, Vector2[] uvs, Vector3[] vertices)
    {
        int[] triangles = mesh.triangles;

        // D�tecter les triangles qui traversent la couture U=0/U=1
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int i0 = triangles[i];
            int i1 = triangles[i + 1];
            int i2 = triangles[i + 2];

            Vector2[] triUVs = { uvs[i0], uvs[i1], uvs[i2] };

            // V�rifier si le triangle traverse la couture
            bool hasSeamCrossing = false;
            for (int j = 0; j < 3; j++)
            {
                float deltaU = Mathf.Abs(triUVs[j].x - triUVs[(j + 1) % 3].x);
                if (deltaU > seamThreshold)
                {
                    hasSeamCrossing = true;
                    break;
                }
            }

            if (hasSeamCrossing)
            {
                // Corriger les UVs pour ce triangle
                CorrectTriangleSeam(triUVs);
                uvs[i0] = triUVs[0];
                uvs[i1] = triUVs[1];
                uvs[i2] = triUVs[2];
            }
        }
    }

    void CorrectTriangleSeam(Vector2[] triUVs)
    {
        // Trouver la coordonn�e U m�diane
        float[] uCoords = { triUVs[0].x, triUVs[1].x, triUVs[2].x };
        System.Array.Sort(uCoords);
        float medianU = uCoords[1];

        // Ajuster les UVs vers la m�diane
        for (int i = 0; i < 3; i++)
        {
            if (Mathf.Abs(triUVs[i].x - medianU) > seamThreshold)
            {
                if (triUVs[i].x < medianU)
                    triUVs[i].x += 1f;
                else
                    triUVs[i].x -= 1f;
            }
        }
    }

    List<UVIsland> DetectUVIslands(Mesh mesh, Vector3[] normals)
    {
        List<UVIsland> islands = new List<UVIsland>();
        int[] triangles = mesh.triangles;
        bool[] processed = new bool[triangles.Length / 3];

        for (int i = 0; i < triangles.Length / 3; i++)
        {
            if (processed[i]) continue;

            List<int> islandTriangles = new List<int>();
            List<int> islandVertices = new List<int>();

            Queue<int> toProcess = new Queue<int>();
            toProcess.Enqueue(i);
            processed[i] = true;

            // Croissance de r�gion bas�e sur l'angle des normales
            while (toProcess.Count > 0)
            {
                int currentTri = toProcess.Dequeue();
                islandTriangles.Add(currentTri);

                // Ajouter les vertices de ce triangle
                for (int j = 0; j < 3; j++)
                {
                    int vertIndex = triangles[currentTri * 3 + j];
                    if (!islandVertices.Contains(vertIndex))
                        islandVertices.Add(vertIndex);
                }

                // V�rifier les triangles adjacents
                Vector3 currentNormal = CalculateTriangleNormal(mesh, currentTri);

                for (int j = 0; j < triangles.Length / 3; j++)
                {
                    if (processed[j] || j == currentTri) continue;

                    if (AreTrianglesAdjacent(triangles, currentTri, j))
                    {
                        Vector3 otherNormal = CalculateTriangleNormal(mesh, j);
                        float angle = Vector3.Angle(currentNormal, otherNormal);

                        if (angle < angleThreshold)
                        {
                            toProcess.Enqueue(j);
                            processed[j] = true;
                        }
                    }
                }
            }

            islands.Add(new UVIsland(islandTriangles, islandVertices));
        }

        if (showDebugInfo)
            Debug.Log($"Detected {islands.Count} UV islands");

        return islands;
    }

    void ApplyIslandProjection(Mesh mesh, List<UVIsland> islands, Vector3[] vertices, Vector3[] normals)
    {
        Vector2[] uvs = new Vector2[vertices.Length];

        foreach (var island in islands)
        {
            // Calculer la normale moyenne de l'�le
            Vector3 avgNormal = Vector3.zero;
            foreach (int triIndex in island.triangles)
            {
                avgNormal += CalculateTriangleNormal(mesh, triIndex);
            }
            avgNormal = (avgNormal / island.triangles.Count).normalized;

            // Choisir le meilleur axe de projection
            ProjectionMode bestMode = ChooseBestProjectionForNormal(avgNormal);

            // Appliquer la projection � cette �le
            ApplyProjectionToVertices(island.vertices, vertices, uvs, bestMode, avgNormal);
        }

        mesh.uv = uvs;
    }

    void ApplyDominantAxisProjection(Mesh mesh, Vector3[] vertices, Vector3[] normals)
    {
        Vector2[] uvs = new Vector2[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            ProjectionMode mode = ChooseBestProjectionForNormal(normals[i]);
            uvs[i] = ProjectVertexByMode(vertices[i], mode, normals[i]);
        }

        mesh.uv = uvs;
    }

    ProjectionMode ChooseBestProjectionForNormal(Vector3 normal)
    {
        Vector3 absNormal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));

        if (absNormal.y > absNormal.x && absNormal.y > absNormal.z)
            return ProjectionMode.Planar; // Projection XZ
        else if (absNormal.x > absNormal.z)
            return ProjectionMode.Planar; // Projection YZ
        else
            return ProjectionMode.Planar; // Projection XY
    }

    Vector2 ProjectVertexByMode(Vector3 vertex, ProjectionMode mode, Vector3 normal)
    {
        switch (mode)
        {
            case ProjectionMode.Planar:
                return ProjectPlanarByNormal(vertex, normal);
            case ProjectionMode.Spherical:
                return ProjectSpherical(vertex, Vector3.zero);
            case ProjectionMode.Cylindrical:
                return ProjectCylindrical(vertex, Vector3.zero, Vector3.up, -1f, 1f);
            default:
                return ProjectSpherical(vertex, Vector3.zero);
        }
    }

    Vector2 ProjectPlanarByNormal(Vector3 vertex, Vector3 normal)
    {
        Vector3 absNormal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));

        if (absNormal.y > absNormal.x && absNormal.y > absNormal.z)
            return new Vector2(vertex.x, vertex.z); // Projection XZ
        else if (absNormal.x > absNormal.z)
            return new Vector2(vertex.y, vertex.z); // Projection YZ
        else
            return new Vector2(vertex.x, vertex.y); // Projection XY
    }

    void ApplyProjectionToVertices(List<int> vertexIndices, Vector3[] vertices, Vector2[] uvs, ProjectionMode mode, Vector3 normal)
    {
        foreach (int index in vertexIndices)
        {
            uvs[index] = ProjectVertexByMode(vertices[index], mode, normal);
        }
    }

    Vector3 CalculateMeshCenter(Vector3[] vertices)
    {
        if (vertices.Length == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (var vertex in vertices)
            sum += vertex;

        return sum / vertices.Length;
    }

    Vector3 CalculateTriangleNormal(Mesh mesh, int triangleIndex)
    {
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;

        int i0 = triangles[triangleIndex * 3];
        int i1 = triangles[triangleIndex * 3 + 1];
        int i2 = triangles[triangleIndex * 3 + 2];

        Vector3 v0 = vertices[i0];
        Vector3 v1 = vertices[i1];
        Vector3 v2 = vertices[i2];

        return Vector3.Cross(v1 - v0, v2 - v0).normalized;
    }

    bool AreTrianglesAdjacent(int[] triangles, int tri1, int tri2)
    {
        HashSet<int> vertices1 = new HashSet<int>
        {
            triangles[tri1 * 3],
            triangles[tri1 * 3 + 1],
            triangles[tri1 * 3 + 2]
        };

        HashSet<int> vertices2 = new HashSet<int>
        {
            triangles[tri2 * 3],
            triangles[tri2 * 3 + 1],
            triangles[tri2 * 3 + 2]
        };

        vertices1.IntersectWith(vertices2);
        return vertices1.Count >= 2; // Partagent au moins une ar�te
    }

    void CleanUVs(Vector2[] uvs)
    {
        for (int i = 0; i < uvs.Length; i++)
        {
            // Assurer que les UVs sont dans [0,1]
            uvs[i] = new Vector2(
                Mathf.Repeat(uvs[i].x, 1f),
                Mathf.Repeat(uvs[i].y, 1f)
            );
        }
    }

    void AnalyzeUVQuality(Mesh mesh)
    {
        Vector2[] uvs = mesh.uv;
        if (uvs == null) return;

        float minU = float.MaxValue, maxU = float.MinValue;
        float minV = float.MaxValue, maxV = float.MinValue;

        foreach (var uv in uvs)
        {
            minU = Mathf.Min(minU, uv.x);
            maxU = Mathf.Max(maxU, uv.x);
            minV = Mathf.Min(minV, uv.y);
            maxV = Mathf.Max(maxV, uv.y);
        }

        Debug.Log($"UV Analysis - Range U: [{minU:F3}, {maxU:F3}], Range V: [{minV:F3}, {maxV:F3}]");

        // Compter les UVs hors limites
        int outOfBounds = 0;
        foreach (var uv in uvs)
        {
            if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)
                outOfBounds++;
        }

        if (outOfBounds > 0)
            Debug.LogWarning($"{outOfBounds}/{uvs.Length} UVs are out of [0,1] bounds");
    }

    [ContextMenu("Generate UVs")]
    public void GenerateUVsMenu()
    {
        GenerateUVs();
    }

    [ContextMenu("Analyze UV Quality")]
    public void AnalyzeUVQualityMenu()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf?.mesh != null)
        {
            showDebugInfo = true;
            AnalyzeUVQuality(mf.mesh);
        }
    }
}