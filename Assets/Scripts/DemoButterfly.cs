using System.Collections.Generic;
using UnityEngine;
using static DemoButterfly;

public class DemoButterfly : MonoBehaviour
{
    [Header("Réglages")]
    public Material material;

    [Header("Subdivision")]
    [Range(0, 4)]
    public int subdivisionLevels = 1;

    [Header("Options")]
    public bool autoSubdivideOnStart = true;
    public bool showDebugPoints = false;
    public bool showDebugLogs = false;

    private ButterflySubdivision butterflyComponent;

    private Mesh originalMesh;

    public enum MeshType { Cube, Terrain }
    [Header("Type de mesh")]

    public MeshType meshType = MeshType.Cube;

    MeshType previousMeshType;

    void Awake()
    {
        previousMeshType = meshType;
    }

    void Start()
    {
        SetupMesh();


        if (autoSubdivideOnStart)
            ApplySubdivision();
    }

    void SetupCube()
    {
        gameObject.name = "ButterflyCube";

        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null)
            mf = gameObject.AddComponent<MeshFilter>();

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr == null)
            mr = gameObject.AddComponent<MeshRenderer>();

        // Création du cube triangulé
        originalMesh = CreateTriangulatedCubeMesh();
        mf.mesh = originalMesh;

        // Configuration du composant Butterfly
        butterflyComponent = GetComponent<ButterflySubdivision>();
        if (butterflyComponent == null)
            butterflyComponent = gameObject.AddComponent<ButterflySubdivision>();

        butterflyComponent.inputMeshFilter = mf;
        butterflyComponent.subdivisionLevels = subdivisionLevels;
        butterflyComponent.showDebugPoints = showDebugPoints;
        butterflyComponent.showDebugLogs = showDebugLogs;

        // Matériau
        ApplyMaterial(mr);

        transform.position = Vector3.zero;
    }

    void SetupMesh()
    {
        gameObject.name = "ButterflyMesh";

        MeshFilter mf = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();

        if (meshType == MeshType.Cube)
            originalMesh = CreateTriangulatedCubeMesh();
        else
            originalMesh = CreateBumpyPlane(width: 10, height: 10, resolution: 20, heightScale: 1.5f);

        mf.mesh = originalMesh;

        butterflyComponent = GetComponent<ButterflySubdivision>() ?? gameObject.AddComponent<ButterflySubdivision>();
        butterflyComponent.inputMeshFilter = mf;
        butterflyComponent.subdivisionLevels = subdivisionLevels;
        butterflyComponent.showDebugPoints = showDebugPoints;
        butterflyComponent.showDebugLogs = showDebugLogs;
        //butterflyComponent.projectToSphere = (meshType == MeshType.Cube);


        ApplyMaterial(mr);
        transform.position = Vector3.zero;
    }

    Mesh CreateBumpyPlane(int width, int height, int resolution, float heightScale)
    {
        Mesh mesh = new Mesh { name = "BumpyPlane" };

        List<Vector3> vertices = new();
        List<int> triangles = new();

        for (int y = 0; y <= resolution; y++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                float xf = (float)x / resolution * width - width / 2f;
                float yf = (float)y / resolution * height - height / 2f;

                // Générer du relief (bosses avec Perlin Noise)
                float z = Mathf.PerlinNoise(xf * 0.2f, yf * 0.2f) * heightScale;

                vertices.Add(new Vector3(xf, z, yf));
            }
        }

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = y * (resolution + 1) + x;
                int iRight = i + 1;
                int iDown = i + resolution + 1;
                int iDownRight = iDown + 1;

                // Deux triangles par carré
                triangles.AddRange(new[] { i, iDown, iRight });
                triangles.AddRange(new[] { iRight, iDown, iDownRight });
            }
        }

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }



    void ApplyMaterial(MeshRenderer mr)
    {
        if (material != null)
        {
            mr.material = material;
        }
        else
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = Color.cyan;

            // Propriétés Standard Shader (si disponibles)
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", 0.3f);
            if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", 0.6f);

            mr.material = mat;
        }
    }

    [ContextMenu("Apply Subdivision")]
    public void ApplySubdivision()
    {
        if (butterflyComponent == null)
        {
            Debug.LogError("❌ ButterflySubdivision component not found!");
            return;
        }

        // Nettoyer les anciens points de debug
        CleanupDebugPoints();

        // Synchroniser les paramètres
        butterflyComponent.subdivisionLevels = subdivisionLevels;
        butterflyComponent.showDebugPoints = showDebugPoints;
        butterflyComponent.showDebugLogs = showDebugLogs;

        // Appliquer la subdivision
        Mesh subdivided = originalMesh;
        for (int i = 0; i < subdivisionLevels; i++)
            subdivided = butterflyComponent.SubdivideButterfly(subdivided);

        // projection sphérique UNE FOIS
        /*if (meshType == MeshType.Cube)
        {
            List<Vector3> verts = new(subdivided.vertices);

            if (meshType == MeshType.Terrain)
            {
            }
            else
            {
                ReprojectToSphere(ref verts, radiusOverride: 1.732f);
            }

            subdivided.vertices = verts.ToArray();
        }*/



        subdivided.RecalculateNormals();

        // Appliquer le résultat
        GetComponent<MeshFilter>().mesh = subdivided;

        Debug.Log($"✅ Subdivision appliquée avec {subdivisionLevels} niveau(x). " +
                 $"Vertices: {originalMesh.vertexCount} → {subdivided.vertexCount}");
    }

    [ContextMenu("Reset to Original")]
    public void ResetToOriginal()
    {
        CleanupDebugPoints();
        GetComponent<MeshFilter>().mesh = originalMesh;
        Debug.Log("🔄 Mesh reset to original");
    }

    /*void ReprojectToSphere(ref List<Vector3> vertices, float? radiusOverride = null)
    {
        // centre
        Vector3 center = Vector3.zero;
        foreach (var v in vertices) center += v;
        center /= vertices.Count;

        // si un rayon imposé → l’utiliser, sinon rayon moyen
        float radius = radiusOverride ?? 0f;
        if (!radiusOverride.HasValue)
        {
            foreach (var v in vertices) radius += (v - center).magnitude;
            radius /= vertices.Count;
        }

        for (int i = 0; i < vertices.Count; i++)
            vertices[i] = center + (vertices[i] - center).normalized * radius;
    }*/


    void CleanupDebugPoints()
    {
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("ButterflyPoint"))
        {
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }
    }

    // Méthode pour changer le niveau depuis l'inspecteur
    void OnValidate()
    {
        if (butterflyComponent != null)
        {
            butterflyComponent.subdivisionLevels = subdivisionLevels;
            butterflyComponent.showDebugPoints = showDebugPoints;
            butterflyComponent.showDebugLogs = showDebugLogs;

            if (previousMeshType != meshType)
            {
                SetupMesh();
                previousMeshType = meshType;
            }

        }
    }

    // Contrôles clavier
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            if (subdivisionLevels < 4)
            {
                subdivisionLevels++;
                ApplySubdivision();
            }
        }
        else if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            if (subdivisionLevels > 0)
            {
                subdivisionLevels--;
                if (subdivisionLevels == 0)
                    ResetToOriginal();
                else
                    ApplySubdivision();
            }
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            ResetToOriginal();
        }
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            ApplySubdivision();
        }
    }

    Mesh CreateTriangulatedCubeMesh()
    {
        Mesh mesh = new Mesh { name = "TriangulatedCube" };

        Vector3[] vertices = {
            // Face avant (Z+)
            new Vector3(-1, -1,  1), new Vector3( 1, -1,  1), // 0, 1
            new Vector3( 1,  1,  1), new Vector3(-1,  1,  1), // 2, 3
            // Face arrière (Z-)
            new Vector3(-1, -1, -1), new Vector3( 1, -1, -1), // 4, 5
            new Vector3( 1,  1, -1), new Vector3(-1,  1, -1)  // 6, 7
        };

        int[] triangles = {
            // Face avant (Z+) - ordre correct
            0, 1, 2,  0, 2, 3,
            // Face droite (X+)
            1, 5, 6,  1, 6, 2,
            // Face arrière (Z-)
            5, 4, 7,  5, 7, 6,
            // Face gauche (X-)
            4, 0, 3,  4, 3, 7,
            // Face haut (Y+)
            3, 2, 6,  3, 6, 7,
            // Face bas (Y-)
            0, 4, 5,  0, 5, 1
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    void OnDrawGizmos()
    {
        // Afficher les informations de subdivision
        if (Application.isPlaying && GetComponent<MeshFilter>()?.sharedMesh != null)
        {
            var mesh = GetComponent<MeshFilter>().sharedMesh;
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2,
                $"Subdivision Level: {subdivisionLevels}\nVertices: {mesh.vertexCount}\nTriangles: {mesh.triangles.Length / 3}");
#endif
        }
    }


}