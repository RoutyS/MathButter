using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Unity.VisualScripting.Member;

public class LineDrawer : MonoBehaviour
{
    [Header("Réglages")]
    public float clickDepth = 5f;            
    public int chaikinIterations = 3;      

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

    private Vector3? nextStartPoint = null;
    private Vector3? finalEndPoint = null;




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
        originalLine.positionCount = originalPoints.Count;
        originalLine.SetPositions(originalPoints.ToArray());


        if (nextStartPoint.HasValue)
        {
            originalPoints.Clear();
            originalPoints.Add(nextStartPoint.Value);
            nextStartPoint = null;

            // Affiche le premier point de la nouvelle courbe
            originalLine.positionCount = originalPoints.Count;
            originalLine.SetPositions(originalPoints.ToArray());

            CreatePointSphere(point, Color.red);

        }


        originalPoints.Add(point);

        originalLine.positionCount = originalPoints.Count;
        originalLine.SetPositions(originalPoints.ToArray());

        CreatePointSphere(originalPoints[0], Color.red);


        if (coonsEdges.Count == 3 && finalEndPoint.HasValue && originalPoints.Count >= 2 && !originalPoints.Contains(finalEndPoint.Value))

        {
            originalPoints.Add(finalEndPoint.Value); // Fin imposée
            finalEndPoint = null;
        }

    }


    // ------------------------------------------------------------------
    //  génère la courbe Chaikin et l’affiche
    // ------------------------------------------------------------------
    
    void ApplyChaikin()
    {
        if (originalPoints.Count < 2) return;

        List<Vector3> smooth = ChaikinSubdivision(originalPoints, chaikinIterations);

        // Affichage
        chaikinLine.positionCount = smooth.Count;
        chaikinLine.SetPositions(smooth.ToArray());

        foreach (Vector3 p in smooth)
            CreatePointSphere(p, Color.gray);

        // Stockage avec orientation corrigée
        switch (coonsEdges.Count)
        {
            case 0: // Bordure BAS : gauche → droite
                coonsEdges.Add(new List<Vector3>(smooth));
                Debug.Log($"✔ Bordure BAS ajoutée : {smooth[0]} → {smooth[smooth.Count - 1]}");
                break;

            case 1: // Bordure DROITE : bas → haut 
                coonsEdges.Add(new List<Vector3>(smooth));
                Debug.Log($"✔ Bordure DROITE ajoutée : {smooth[0]} → {smooth[smooth.Count - 1]}");
                break;

            case 2: // Bordure HAUT : droite → gauche, donc on inverse pour avoir gauche → droite
                smooth.Reverse();
                coonsEdges.Add(new List<Vector3>(smooth));
                Debug.Log($"✔ Bordure HAUT ajoutée (inversée) : {smooth[0]} → {smooth[smooth.Count - 1]}");
                break;

            case 3: // Bordure GAUCHE : haut → bas, donc on inverse pour avoir bas → haut  
                smooth.Reverse();
                coonsEdges.Add(new List<Vector3>(smooth));
                Debug.Log($"✔ Bordure GAUCHE ajoutée (inversée) : {smooth[0]} → {smooth[smooth.Count - 1]}");
                break;
        }

        // Génération quand on a les 4 courbes
        if (coonsEdges.Count == 4)
        {
            // Vérification de la cohérence des coins
            Debug.Log("=== VÉRIFICATION DE LA COHÉRENCE DES COINS ===");
            Debug.Log($"Bas-gauche: {coonsEdges[0][0]} vs Gauche-bas: {coonsEdges[3][0]}");
            Debug.Log($"Bas-droite: {coonsEdges[0][coonsEdges[0].Count - 1]} vs Droite-bas: {coonsEdges[1][0]}");
            Debug.Log($"Haut-droite: {coonsEdges[2][0]} vs Droite-haut: {coonsEdges[1][coonsEdges[1].Count - 1]}");
            Debug.Log($"Haut-gauche: {coonsEdges[2][coonsEdges[2].Count - 1]} vs Gauche-haut: {coonsEdges[3][coonsEdges[3].Count - 1]}");

            int n = coonsEdges[0].Count;
            bool ok = coonsEdges.TrueForAll(c => c.Count == n);

            if (!ok)
            {
                Debug.LogError("x Les 4 courbes n'ont pas le même nombre de points.");
            }
            else
            {
                GenerateCoonsPatch(coonsEdges);
                Debug.Log("v Surface de Coons générée avec succès !");
            }

            coonsEdges.Clear();
        }
    }


    // ------------------------------------------------------------------
    // Générer la surface de Coons
    // ------------------------------------------------------------------

    void GenerateCoonsPatch(List<List<Vector3>> edges)
    {
        int resolution = edges[0].Count;

        // Vérification des coins pour déboguer
        Vector3 p00 = edges[0][0];                    // coin bas-gauche
        Vector3 p01 = edges[0][resolution - 1];       // coin bas-droite  
        Vector3 p10 = edges[2][resolution - 1];       // coin haut-droite 
        Vector3 p11 = edges[2][0];                    // coin haut-gauche 

        Debug.Log($"Coins de contrôle:");
        Debug.Log($"P00 (bas-gauche): {p00}");
        Debug.Log($"P01 (bas-droite): {p01}");
        Debug.Log($"P10 (haut-droite): {p10}");
        Debug.Log($"P11 (haut-gauche): {p11}");

        // Créer des sphères pour visualiser les coins
        CreatePointSphere(p00, Color.red);      // bas-gauche
        CreatePointSphere(p01, Color.green);    // bas-droite
        CreatePointSphere(p10, Color.blue);     // haut-droite
        CreatePointSphere(p11, Color.yellow);   // haut-gauche

        List<List<Vector3>> grid = new List<List<Vector3>>();

        for (int i = 0; i < resolution; i++)
        {
            float u = i / (float)(resolution - 1);
            List<Vector3> row = new List<Vector3>();

            for (int j = 0; j < resolution; j++)
            {
                float v = j / (float)(resolution - 1);

                // Évaluation des courbes de bordure
                Vector3 c0 = EvaluateCurveAtParameter(edges[0], u);  // bordure bas 
                Vector3 c1 = EvaluateCurveAtParameter(edges[2], u);  // bordure haut 
                Vector3 d0 = EvaluateCurveAtParameter(edges[3], v);  // bordure gauche  
                Vector3 d1 = EvaluateCurveAtParameter(edges[1], v);  // bordure droite 

                

                Vector3 ruled_uv = (1 - v) * c0 + v * c1;           // interpolation entre bordures bas/haut
                Vector3 ruled_vu = (1 - u) * d0 + u * d1;           // interpolation entre bordures gauche/droite

                // Correction bilinéaire des coins
                Vector3 bilinear = (1 - u) * (1 - v) * p00 +        // influence coin bas-gauche
                                  u * (1 - v) * p01 +                // influence coin bas-droite  
                                  (1 - u) * v * p11 +                // influence coin haut-gauche
                                  u * v * p10;                       // influence coin haut-droite

                // Surface de Coons finale
                Vector3 S = ruled_uv + ruled_vu - bilinear;

                row.Add(S);
            }
            grid.Add(row);
        }

        GenerateMeshFromPoints(grid);
    }

    Vector3 EvaluateCurveAtParameter(List<Vector3> curve, float t)
    {
        if (curve.Count == 0) return Vector3.zero;
        if (curve.Count == 1) return curve[0];

        // Assurer que t est dans [0,1]
        t = Mathf.Clamp01(t);

        // Calcul de l'index exact
        float exactIndex = t * (curve.Count - 1);
        int baseIndex = Mathf.FloorToInt(exactIndex);
        float localT = exactIndex - baseIndex;

        // Cas limite
        if (baseIndex >= curve.Count - 1)
            return curve[curve.Count - 1];

        // Interpolation linéaire
        return Vector3.Lerp(curve[baseIndex], curve[baseIndex + 1], localT);
    }

    // ------------------------------------------------------------------
    // Interpolation linéaire dans une liste de points
    // ------------------------------------------------------------------
    Vector3 EvaluateCurve(List<Vector3> curve, float t)
    {
        int count = curve.Count;
        if (count == 0) return Vector3.zero;

        float scaledT = t * (count - 1);
        int index = Mathf.FloorToInt(scaledT);
        float localT = scaledT - index;

        if (index >= count - 1) return curve[count - 1];

        return Vector3.Lerp(curve[index], curve[index + 1], localT);
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

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                vertices[i * cols + j] = grid[i][j];
            }
        }

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

        // Crée le mesh 
        GameObject coons = new GameObject("CoonsMesh", typeof(MeshFilter), typeof(MeshRenderer));
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        coons.GetComponent<MeshFilter>().mesh = mesh;

        Material mat = new Material(Shader.Find("Standard"));
        mat.color = Color.cyan;
        coons.GetComponent<MeshRenderer>().material = mat;

      
    }


    // ------------------------------------------------------------------
    // cloturer une courbe et cree une nouvelle 
    // ------------------------------------------------------------------

    void StartNewCurve()
    {
        originalPoints.Clear();
        originalLine.positionCount = 0;
        chaikinLine.positionCount = 0;


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
        if (originalPoints.Count < 2) return;

        //  Courbe 4 : Ajouter manuellement début et fin
        if (coonsEdges.Count == 3 && finalEndPoint.HasValue)
        {
            // Forcer le début (juste au cas où)
            Vector3 forcedStart = coonsEdges[0][0];
            if (originalPoints.Count == 0 || originalPoints[0] != forcedStart)
                originalPoints.Insert(0, forcedStart);

            // Forcer la fin avec homothétie
            Vector3 lastPoint = originalPoints[originalPoints.Count - 1];           
            Vector3 beforeLast = originalPoints[originalPoints.Count - 2];         
            Vector3 target = finalEndPoint.Value;

            float lambda = 1f; // 1 → point exactement au bon endroit
            Vector3 adjusted = (1 - lambda) * beforeLast + lambda * target;

            // Remplacer le dernier point par le bon
            originalPoints[originalPoints.Count - 1] = adjusted;
            CreatePointSphere(adjusted, Color.red);

            Debug.Log($" Dernier point replacé par homothétie vers {target}");
        }



        ApplyChaikin(); // maintenant on a les 4 coins

        // Préparer le prochain départ
        if (coonsEdges.Count < 4)
            nextStartPoint = originalPoints[originalPoints.Count - 1];

        originalPoints.Clear();
        originalLine.positionCount = 0;
        chaikinLine.positionCount = 0;
    }


    // ------------------------------------------------------------------
    //  ALGO Chaikin – renvoie une nouvelle liste de points
    // ------------------------------------------------------------------
    List<Vector3> ChaikinSubdivision(List<Vector3> input, int iterations)
    {
        List<Vector3> pts = new List<Vector3>(input);

        for (int it = 0; it < iterations; it++)
        {
            List<Vector3> next = new List<Vector3>();

            // CONSERVER le premier point
            next.Add(pts[0]);

            // Générer les points intermédiaires
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 p0 = pts[i];
                Vector3 p1 = pts[i + 1];

                Vector3 Q = Vector3.Lerp(p0, p1, 0.25f);
                Vector3 R = Vector3.Lerp(p0, p1, 0.75f); 

                next.Add(Q);
                next.Add(R);
            }

            // CONSERVER le dernier point
            next.Add(pts[pts.Count - 1]);

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

    // ------------------------------------------------------------------
    //  Instancie une sphère de contrôle et lui applique la couleur voulue
    // ------------------------------------------------------------------
    void CreatePointSphere(Vector3 position, Color color)
    {
        if (pointPrefab == null) return;

        GameObject p = Instantiate(pointPrefab, position, Quaternion.identity);
        p.tag = "ChaikinPoint";

        Renderer r = p.GetComponent<Renderer>();
        if (r != null)
            r.material.color = color;
    }

}
