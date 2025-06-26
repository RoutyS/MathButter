using System.Collections.Generic;
using UnityEngine;

public class DemoLoop : MonoBehaviour
{
    [Header("Loop Subdivision Settings")]
    public Material loopMaterial;
    //public bool showWireframes = true;
    public int subdivisionLevels = 1;
    public Mesh inputMesh;

    private Mesh lastAppliedMesh;

    void Start()
    {
        if (inputMesh != null)
        {
            ApplySelectedMesh();
        }
        else
        {
            Debug.LogWarning("Aucun mesh assigné, création d'un cube par défaut");
            CreateLoopCube();
        }
    }

    void Update()
    {
        if (Application.isPlaying)
        {
            if (inputMesh != null && inputMesh != lastAppliedMesh)
            {
                ApplySelectedMesh();
            }
        }
    }

    public void ApplySelectedMesh()
    {
        lastAppliedMesh = inputMesh;

        // Supprimer les anciens enfants (objets créés)
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        CreateLoopObject(inputMesh, "Loop Subdivision Object");
        Debug.Log("Mesh appliqué depuis l'inspector !");
    }

    void CreateLoopObject(Mesh mesh, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(transform);
        obj.transform.localPosition = Vector3.zero;

        MeshFilter meshFilter = obj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();

        meshFilter.mesh = mesh;

        if (loopMaterial != null)
        {
            meshRenderer.material = loopMaterial;
        }
        else
        {
            Material loopMat = new Material(Shader.Find("Standard"));
            loopMat.color = Color.cyan;
            meshRenderer.material = loopMat;
        }

        Loop loopScript = obj.AddComponent<Loop>();
        loopScript.subdivisionLevels = subdivisionLevels;

       
        SetupCamera();
    }

    void CreateLoopCube()
    {
        GameObject cubeObject = new GameObject("Loop Subdivision Cube");
        cubeObject.transform.SetParent(transform);
        cubeObject.transform.localPosition = Vector3.zero;

        MeshFilter meshFilter = cubeObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = cubeObject.AddComponent<MeshRenderer>();

        meshFilter.mesh = CreateTriangulatedCubeMesh();

        if (loopMaterial != null)
        {
            meshRenderer.material = loopMaterial;
        }
        else
        {
            Material loopMat = new Material(Shader.Find("Standard"));
            loopMat.color = Color.cyan;
            meshRenderer.material = loopMat;
        }

        Loop loopScript = cubeObject.AddComponent<Loop>();
        loopScript.subdivisionLevels = subdivisionLevels;


        SetupCamera();
    }

    Mesh CreateTriangulatedCubeMesh()
    {
        Mesh mesh = new Mesh();

        Vector3[] vertices = new Vector3[]
        {
            new Vector3(-1, -1,  1), // 0
            new Vector3( 1, -1,  1), // 1
            new Vector3( 1,  1,  1), // 2
            new Vector3(-1,  1,  1), // 3
            new Vector3(-1, -1, -1), // 4
            new Vector3( 1, -1, -1), // 5
            new Vector3( 1,  1, -1), // 6
            new Vector3(-1,  1, -1), // 7
        };

        int[] triangles = new int[]
        {
            0, 2, 1,  0, 3, 2,     // Avant
            1, 2, 6,  1, 6, 5,     // Droite
            5, 6, 7,  5, 7, 4,     // Arrière
            4, 7, 3,  4, 3, 0,     // Gauche
            3, 7, 6,  3, 6, 2,     // Haut
            4, 0, 1,  4, 1, 5      // Bas
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    /*void AddWireframe(GameObject target, Color wireframeColor)
    {
        GameObject wireframeObject = new GameObject("Wireframe");
        wireframeObject.transform.SetParent(target.transform);
        wireframeObject.transform.localPosition = Vector3.zero;
        wireframeObject.transform.localScale = Vector3.one * 1.001f;

        MeshFilter wireframeMeshFilter = wireframeObject.AddComponent<MeshFilter>();
        MeshRenderer wireframeMeshRenderer = wireframeObject.AddComponent<MeshRenderer>();

        wireframeMeshFilter.mesh = target.GetComponent<MeshFilter>().mesh;

        Material wireframeMaterial = new Material(Shader.Find("Unlit/Color"));
        wireframeMaterial.color = wireframeColor;
        wireframeMeshRenderer.material = wireframeMaterial;

        wireframeObject.AddComponent<WireframeRenderer>();
    }*/

    void SetupCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.transform.position = new Vector3(0, 2, 6);
            mainCamera.transform.LookAt(Vector3.zero);
        }
    }

    [ContextMenu("Increase Subdivision Level")]
    public void IncreaseSubdivisionLevel()
    {
        subdivisionLevels++;
        UpdateSubdivisionLevels();
    }

    [ContextMenu("Decrease Subdivision Level")]
    public void DecreaseSubdivisionLevel()
    {
        subdivisionLevels = Mathf.Max(0, subdivisionLevels - 1);
        UpdateSubdivisionLevels();
    }

    void UpdateSubdivisionLevels()
    {
        Loop loopScript = FindObjectOfType<Loop>();
        if (loopScript != null)
        {
            loopScript.subdivisionLevels = subdivisionLevels;
            loopScript.ApplyLoopSubdivision();
        }

        Debug.Log($"Updated subdivision levels to: {subdivisionLevels}");
    }
}
