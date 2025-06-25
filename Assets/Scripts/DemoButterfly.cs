using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class DemoButterfly : MonoBehaviour
{
    [Header("Butterfly Settings")]
    public int levels = 1;
    public Material cubeMaterial;

    void Start()
    {
        // Crée un cube de base si aucun mesh
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf.sharedMesh == null)
            mf.sharedMesh = CreateCube();

        // Applique Butterfly n fois
        ButterflySubdivision bs = GetComponent<ButterflySubdivision>();
        if (bs == null)
            bs = gameObject.AddComponent<ButterflySubdivision>();

        Mesh m = mf.sharedMesh;
        for (int i = 0; i < levels; i++)
            m = bs.SubdivideButterfly(m);

        mf.sharedMesh = m;
        Debug.Log($"Butterfly (level {levels}) → vertices = {m.vertexCount}, triangles = {m.triangles.Length / 3}");

        // Matériau
        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr.sharedMaterial == null)
        {
            Material mat = new Material(Shader.Find("Unlit/Color"));
            mat.color = Color.cyan;
            mr.sharedMaterial = cubeMaterial != null ? cubeMaterial : mat;
        }
    }

    Mesh CreateCube()
    {
        Mesh mesh = new Mesh { name = "ButterflyCube" };

        Vector3[] v = {
            new Vector3(-1,-1, 1), new Vector3(1,-1, 1),
            new Vector3(1, 1, 1), new Vector3(-1, 1, 1),
            new Vector3(-1,-1,-1), new Vector3(1,-1,-1),
            new Vector3(1, 1,-1), new Vector3(-1, 1,-1)
        };

        // Petite bosse pour éviter les coplanaires
        v[2] += new Vector3(0, 0.2f, 0);

        int[] t = {
            0,2,1, 0,3,2,
            1,2,6, 1,6,5,
            5,6,7, 5,7,4,
            4,7,3, 4,3,0,
            3,7,6, 3,6,2,
            4,0,1, 4,1,5
        };

        mesh.vertices = v;
        mesh.triangles = t;
        mesh.RecalculateNormals();
        return mesh;
    }
}
