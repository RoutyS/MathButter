using System.Collections.Generic;
using UnityEngine;
using static Unity.VisualScripting.Member;

public class LineDrawer : MonoBehaviour
{
    [Header("Réglages")]
    public float clickDepth = 5f;            // Distance depuis la caméra
    public int chaikinIterations = 3;      // Nb d’itérations de lissage

    [Header("Visuels")]
    public GameObject pointPrefab;
    public Material originalLineMaterial;
    public Material chaikinLineMaterial;

    // ----- Données -----
    private List<Vector3> originalPoints = new List<Vector3>();

    // ----- Renderers -----
    private LineRenderer originalLine;
    private LineRenderer chaikinLine;

    // ----- Stockage courbes -----
    private List<List<Vector3>> coonsEdges = new List<List<Vector3>>(); 


    void Start()
    {
        // Ligne originale (rouge)
        originalLine = gameObject.AddComponent<LineRenderer>();
        originalLine.material = originalLineMaterial;
        originalLine.widthMultiplier = 0.05f;
        originalLine.positionCount = 0;

        // Ligne Chaikin (jaune) – dans un objet enfant
        GameObject chaikinObj = new GameObject("ChaikinLine");
        chaikinObj.transform.parent = transform;
        chaikinLine = chaikinObj.AddComponent<LineRenderer>();
        chaikinLine.material = chaikinLineMaterial;
        chaikinLine.widthMultiplier = 0.05f;
        chaikinLine.positionCount = 0;

        // Ligne originale (rouge)
        originalLine.startColor = Color.red;
        originalLine.endColor = Color.red;
        originalLine.material = new Material(Shader.Find("Sprites/Default"));

        // Ligne Chaikin (bleue)
        chaikinLine.startColor = Color.cyan;
        chaikinLine.endColor = Color.cyan;
        chaikinLine.material = new Material(Shader.Find("Sprites/Default"));

    }

    void Update()
    {
        // ---- Clic gauche : ajout d’un point ----
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Vector3 point = ray.GetPoint(clickDepth);   // profondeur fixe
            AddPoint(point);
        }

        // ---- Touche C : lissage Chaikin ----
        if (Input.GetKeyDown(KeyCode.C))
        {
            DisplayCurrentChaikin(); 
        }

        // ---- Touche N : Valide la courbe actuelle et passe à la suivante ----
        if (Input.GetKeyDown(KeyCode.N))
        {
            ValidateCurrentCurve(); 
        }

        // ---- Touche R : reset ----
        if (Input.GetKeyDown(KeyCode.R))
        {
            ClearPoints();    // NEW : méthode maintenant présente
        }
    }

    // ------------------------------------------------------------------
    //  Ajout d’un point et mise à jour de la ligne originale
    // ------------------------------------------------------------------
    void AddPoint(Vector3 point)
    {
        originalPoints.Add(point);

        originalLine.positionCount = originalPoints.Count;
        originalLine.SetPositions(originalPoints.ToArray());

        if (pointPrefab != null)
        {
            // Facultatif : donne un tag pour pouvoir les supprimer au Reset
            GameObject p = Instantiate(pointPrefab, point, Quaternion.identity);
            p.tag = "ChaikinPoint";
        }
    }

    // ------------------------------------------------------------------
    //  APPEL PUBLIC : génère la courbe Chaikin et l’affiche
    // ------------------------------------------------------------------
    void ApplyChaikin()
    {
        if (originalPoints.Count < 2) return;

        List<Vector3> smooth = ChaikinSubdivision(originalPoints, chaikinIterations);

        chaikinLine.positionCount = smooth.Count;
        chaikinLine.SetPositions(smooth.ToArray());

        // Ajouter cette courbe à la liste pour la surface de Coons
        if (coonsEdges.Count < 4)
        {
            coonsEdges.Add(new List<Vector3>(smooth));
            Debug.Log($"Courbe {coonsEdges.Count}/4 ajoutée !");
        }

        if (coonsEdges.Count == 4)
        {
            GenerateCoonsPatch(coonsEdges);
            coonsEdges.Clear(); // reset pour recommencer
        }
    }

    // ------------------------------------------------------------------
    // Générer la surface de Coons
    // ------------------------------------------------------------------

    void GenerateCoonsPatch(List<List<Vector3>> edges)
    {
        int resolution = edges[0].Count; // suppose qu'elles ont toutes le même nb de points
        GameObject parent = new GameObject("CoonsSurface");

        List<List<Vector3>> grid = new List<List<Vector3>>();


        for (int i = 0; i < resolution; i++)
        {
            float u = i / (float)(resolution - 1);
            List<Vector3> row = new List<Vector3>();

            for (int j = 0; j < resolution; j++)
            {
                float v = j / (float)(resolution - 1);

                // Bordures
                Vector3 bottom = edges[0][j];
                Vector3 top = edges[1][j];
                Vector3 left = edges[2][i];
                Vector3 right = edges[3][i];

                // Coins
                Vector3 p00 = edges[0][0];
                Vector3 p01 = edges[0][resolution - 1];
                Vector3 p10 = edges[1][0];
                Vector3 p11 = edges[1][resolution - 1];

                // Coons patch
                Vector3 S = (1 - v) * bottom + v * top +
                            (1 - u) * left + u * right -
                            ((1 - u) * (1 - v) * p00 + (1 - u) * v * p01 + u * (1 - v) * p10 + u * v * p11);

                row.Add(S);
            }

            grid.Add(row);
        }

        GenerateMeshFromPoints(grid);

    }

    // ------------------------------------------------------------------
    // Generation de mesh a partir des points 
    // ------------------------------------------------------------------

    void GenerateMeshFromPoints(List<List<Vector3>> grid)
    {
        int rows = grid.Count;
        int cols = grid[0].Count;

        Vector3[] vertices = new Vector3[rows * cols];
        int[] triangles = new int[(rows - 1) * (cols - 1) * 6];

        // Aplatir la grille en tableau
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                vertices[i * cols + j] = grid[i][j];
            }
        }

        // Créer les triangles
        int t = 0;
        for (int i = 0; i < rows - 1; i++)
        {
            for (int j = 0; j < cols - 1; j++)
            {
                int a = i * cols + j;
                int b = a + 1;
                int c = a + cols;
                int d = c + 1;

                triangles[t++] = a;
                triangles[t++] = c;
                triangles[t++] = b;

                triangles[t++] = b;
                triangles[t++] = c;
                triangles[t++] = d;
            }
        }

        // Créer le Mesh
        GameObject surface = new GameObject("CoonsMesh", typeof(MeshFilter), typeof(MeshRenderer));

        // Tenter d’assigner automatiquement le CoonsMesh au script ButterflySubdivision
        ButterflySubdivision bs = FindObjectOfType<ButterflySubdivision>();
        if (bs != null)
        {
            bs.inputMeshFilter = surface.GetComponent<MeshFilter>();
            Debug.Log("✅ CoonsMesh assigné automatiquement à ButterflySubdivision");
        }


        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals(); // pour l’éclairage

        surface.GetComponent<MeshFilter>().mesh = mesh;

        // Appliquer un matériau simple
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = Color.cyan;
        surface.GetComponent<MeshRenderer>().material = mat;
    }



    // ------------------------------------------------------------------
    // cloturer une courbe et cree une nouvelle 
    // ------------------------------------------------------------------

    void StartNewCurve()
    {
        originalPoints.Clear();
        originalLine.positionCount = 0;
        chaikinLine.positionCount = 0;

        // Supprimer sphères si tu en as placé
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("ChaikinPoint"))
            Destroy(go);
    }

    // ------------------------------------------------------------------
    // afficher la courbe Chaikin
    // ------------------------------------------------------------------

    void DisplayCurrentChaikin()
    {
        if (originalPoints.Count < 2) return;

        List<Vector3> smooth = ChaikinSubdivision(originalPoints, chaikinIterations);
        chaikinLine.positionCount = smooth.Count;
        chaikinLine.SetPositions(smooth.ToArray());
    }

    // ------------------------------------------------------------------
    // valider une courbe et la garder
    // ------------------------------------------------------------------

    void ValidateCurrentCurve()
    {
        if (chaikinLine.positionCount == 0) return;

        // Sauvegarde la courbe actuelle
        List<Vector3> validatedPoints = new List<Vector3>();
        for (int i = 0; i < chaikinLine.positionCount; i++)
            validatedPoints.Add(chaikinLine.GetPosition(i));

        coonsEdges.Add(validatedPoints);
        Debug.Log($"Courbe {coonsEdges.Count}/4 validée.");

        // Garde une copie visible
        GameObject copy = new GameObject("StoredCurve_" + coonsEdges.Count);
        LineRenderer lr = copy.AddComponent<LineRenderer>();
        lr.positionCount = validatedPoints.Count;
        lr.SetPositions(validatedPoints.ToArray());
        lr.widthMultiplier = 0.05f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.magenta;
        lr.endColor = Color.magenta;

        if (coonsEdges.Count == 4)
        {
            GenerateCoonsPatch(coonsEdges);
            coonsEdges.Clear();
        }

        StartNewCurve(); // Efface juste la courbe temporaire, pas les précédentes
    }






    // ------------------------------------------------------------------
    //  ALGO Chaikin (private) – renvoie une nouvelle liste de points
    // ------------------------------------------------------------------
    List<Vector3> ChaikinSubdivision(List<Vector3> input, int iterations)
    {
        List<Vector3> pts = new List<Vector3>(input);

        for (int it = 0; it < iterations; it++)
        {
            List<Vector3> next = new List<Vector3>();

            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 p0 = pts[i];
                Vector3 p1 = pts[i + 1];

                Vector3 Q = Vector3.Lerp(p0, p1, 0.25f); // 75% p0 + 25% p1
                Vector3 R = Vector3.Lerp(p0, p1, 0.75f); // 25% p0 + 75% p1

                next.Add(Q);
                next.Add(R);
            }
            pts = next;
        }
        return pts;
    }

    // ------------------------------------------------------------------
    //  Reset complet (points, lignes, sphères)
    // ------------------------------------------------------------------
    void ClearPoints()
    {
        originalPoints.Clear();
        originalLine.positionCount = 0;
        chaikinLine.positionCount = 0;

        // Détruire les sphères si on leur a mis le tag
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("ChaikinPoint"))
            Destroy(go);
    }
}
