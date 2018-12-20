using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

using UnityEngine.SceneManagement;

public class SVGExporter
{
    bool isPathStarted = false;
    string svgContent = "";
    StreamWriter writer = null;
    public enum SVGLine
    {
        polygon,
        polyline

    }
    SVGLine currentType = SVGLine.polygon;

    string filePath = null;

    // For lines
    Vector3 lastPointAdded = Vector3.zero;
    Vector3 firstPointAdded = Vector3.zero;

    int numPointsAdded = 0;

    public SVGExporter(string path, int w, int h, SVGLine type, bool fill = true)
    {
        Debug.Log("Starting saving");
        numPointsAdded = 0;
        isPathStarted = false;
        svgContent = "";

        currentType = type;

        filePath = path;
        File.WriteAllText(filePath, "");
        writer = new StreamWriter(path, true);
        writer.WriteLine("<?xml version =\"1.0\" encoding=\"utf-8\"?><svg version=\"1.1\" id=\"Layer_1\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" x=\"0px\" y=\"0px\"  width=\"" + w + "px\" height=\"" + h + "px\" viewBox=\"0 0 " + w + " " + h + "\" style=\"enable-background:new 0 0 612 792;\" xml:space=\"preserve\"><style type=\"text/css\"> polyline{"+ (fill? "fill:#ffffff" : "fill:transparent") +";stroke:#000000;stroke-miterlimit:10;}</style>");
    }

    public void AddPoint(float x, float y)
    {
        if (!isPathStarted)
            AddPolyLine();

        if (firstPointAdded == Vector3.zero)
        {
            firstPointAdded = new Vector3(x,y,0);
        }

        numPointsAdded++;
        svgContent += x.ToString("f2") + "," + y.ToString("f2") + " ";
    }

    public void AddLine(Vector3 origin, Vector3 dest)
    {

        if (!isPathStarted)
            AddPolyLine();

        if (lastPointAdded == origin)
            svgContent += origin.x.ToString("f2") + "," + origin.y.ToString("f2") + " ";

     

        lastPointAdded = dest;

        svgContent += dest.x.ToString("f2") + "," + dest.y.ToString("f2") + " ";
    }

    public void EndPoint()
    {
        if (!isPathStarted)
            return;

        if (RayCastLines.instance.closeType == RayCastLines.CloseType.Expand && firstPointAdded != Vector3.zero)
        {
            AddPoint(firstPointAdded.x, firstPointAdded.y);
            firstPointAdded = Vector3.zero;
        }


        svgContent += "\"/>";

        if (writer == null)
            throw new System.Exception("You should ini the writer first !!");

        if (numPointsAdded == 1)
        {
           // Debug.Log("only one point");
        } else if (svgContent != "<" + currentType.ToString() + " points =\"" + "\"/>" && svgContent != "\"/>")
            writer.WriteLine(svgContent);

        svgContent = "";
        isPathStarted = false;
        firstPointAdded = Vector3.zero;
    }

    void AddPolyLine()
    {
        svgContent += "<" + currentType.ToString() + " points=\"";
        isPathStarted = true;
        numPointsAdded = 0;
    }

    public void End(bool invertOrder = true)
    {
        numPointsAdded = 0;
        svgContent = "";
        writer.WriteLine("</svg>");


        writer.Close();
        writer = null;
        isPathStarted = false;

        if (invertOrder)
        {
            List<string> myList = System.Linq.Enumerable.ToList(File.ReadAllText(filePath).Split('\n'));
            myList.Reverse();
            myList.Insert(0, myList[myList.Count - 1]);
            myList.RemoveAt(1);
            myList.Add("</svg>");
          //  File.OpenWrite(path);
        }


        Debug.Log("Saving \r\n" + filePath);
    }
}

[CustomEditor(typeof(RayCastLines))]
public class RaycastLinesEditor : Editor
{
    RayCastLines r;
    public override void OnInspectorGUI()
    {
        r = (RayCastLines)target;

        if (GUILayout.Button("Save SVG"))
        {
            r.save = true;
            EditorUtility.SetDirty(target);
        }
        DrawDefaultInspector();

    }
}

[ExecuteInEditMode]
public class RayCastLines : MonoBehaviour
{
    public enum CloseType
    {
        None,
        Expand,
        SmartFill,
        SimpleSmartFill
    }

    public static RayCastLines instance = null;

    [Header("ray settings")]
    public int nx = 5;
    public int ny = 5;
    public float width = 10;
    public float height = 10;
    public bool fromCenter = true;

    [Header("Extra")]
    public bool skipLongDistance = false;
    [Range(0, 5)]
    public float skipDistance = 2;
    public bool stopWhenHidden = false;
    public bool skipLongDistanceHidden = false;
    [Range(0, 5)]
    public float stopHiddenTolerance = 2;
    //   public bool smartCloseShape = true;
    //  public bool simpleSmartCloseShape = true; // only close to the previous first/last
    public bool useLine = false;

    [Header("Fade")]
    [Range(0, 15)]
    public float fadeMaxRadius;
    [Range(0, 15)]
    public float fadeMinRadius;
    [Range(0, 2)]
    public float fadePercentChange = 0.5f;
    [Range(0, 1)]
    public float previousInfluence = 0.5f;
    public bool influence = true;

    [HideInInspector]
    public bool save = false;
    [Header("Export")]
    public CloseType closeType = CloseType.None;
    public int expandMargin = 10;
    public string outputFolder = "Assets/";
  //  public bool invertPathOrder = false;
    SVGExporter svgCanvas;
    public bool fill = true;

    [Header("Other")]
    public bool pause = false;
    public int randomSeed = 1234;
    public bool useSeed = true;

    int w;
    int h;
    Vector3 start;

    public Vector3 t;

    Random.State state;

    void OnEnable()
    {
        Random.InitState(255);
        Random.Range(0, 1);
        state = Random.state;
        instance = this;
    }

    bool ShouldFadeByDistance(Vector3 point, bool isPrevSkipped)
    {
        bool shouldFade = false;

        if (fadeMinRadius > fadeMaxRadius) fadeMinRadius = fadeMaxRadius;
        if (fadeMaxRadius < fadeMinRadius) fadeMinRadius = fadeMaxRadius;

        float distance = Vector3.Distance(transform.position, point);

        if (distance > fadeMaxRadius)
        {
            shouldFade = true;
        } else if (distance < fadeMinRadius)
        {
           shouldFade = false;
        } else {

            float pos = Remap(distance, fadeMinRadius, fadeMaxRadius, 0, 1);
            int posInt = Mathf.FloorToInt(pos * 10);
            float chances = Remap(pos * ( 0.5f+ fadePercentChange) + (float)Random.Range(0.0f, 1.0f), 0.0f, 3.0f, 0.0f, 1.0f); //pos); // * ( (float)Random.Range(0.0f, 1.0f)  ));

            // Debug.Log(pos + " = " + chances + " = " + Mathf.RoundToInt(chances));
            // Debug.Log(pos + " = " + Mathf.RoundToInt(pos));
            /*
            float chancesToFollowPrev = Remap((isPrevSkipped ? 1f : 0f) + (float)Random.Range(0.0f, 1.0f), 0,2,-0.5f,0.5f);
            chances = chances + chancesToFollowPrev;
            */
           /* float chancesToFollowPrev = Remap(previousInfluence * (isPrevSkipped ? 1f : 0f), 0, 2, 0.0f, 1f);
            chances = chances + chancesToFollowPrev;
*/
            // chances d'avoir la valeur d'avant sont égales a la distance avec du random
            //                                               -> Plus c'est loin moins c'est influencé
                
            // int final = Mathf.RoundToInt(chances);

            if (chances < 0.5f)
            {
                shouldFade = false;
            }
            else if (chances > 0.5f)
            {
                shouldFade = true;
            }
            if (influence)
            {
                if (isPrevSkipped != shouldFade)
                {
                    if ((float)Random.Range(0.0f, 1.0f) < previousInfluence)
                        shouldFade = isPrevSkipped;
                }
            }
        }

        return shouldFade;
    }


    public  float Remap( float value, float from1, float to1, float from2, float to2)
    {
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    }


    void Update()
    {

        if (useSeed)
            Random.InitState(randomSeed);

        if (Application.isPlaying)
            return;

        if (pause)
            return;

        if (save)
        {
            h = Camera.main.pixelHeight;
            w = Camera.main.pixelWidth;
            svgCanvas = new SVGExporter(outputFolder + SceneManager.GetActiveScene().name + "-" + closeType + ".svg", w, h, SVGExporter.SVGLine.polyline, fill);
        }

        start = transform.position;

        // Calculate steps
        if (nx == 0 || ny == 0) return;

        float stepsX = (float)width / (float)nx;
        float stepsY = (float)height / (float)ny;

        if (stepsX == 0 || stepsY == 0)
            return;


        Vector3 firstPointOfLine = Vector3.zero;
        List<Vector3> lastPoints = new List<Vector3>();

        List<Vector3> closeShapePoints = new List<Vector3>();

        int firstLineDisplayed = -1;

        for (int x = 0; x < nx; x++)
        {

            Vector3 lastSavedAdded = Vector3.zero;

            Vector3 prevPoint = Vector3.zero;
            Vector3 prevPointDisplayed = Vector3.zero;
            Vector3 firstPointDisplayed = Vector3.zero;

            bool skipNext = false;
            //    bool prevHasFoundY = false;
            bool firstY = false;

            bool isFirstPointFound = false;

            if (closeType == CloseType.SimpleSmartFill)
                closeShapePoints = new List<Vector3>();


            for (int y = 0; y < ny; y++)
            {
                RaycastHit hit;
                Vector3 or = new Vector3(start.x + (x * stepsX) - (fromCenter ? width / 2 : 0), start.y, start.z + (y * stepsY) - (fromCenter ? height / 2 : 0));
                or = RotatePointAroundPivot(or, transform.position, transform.localEulerAngles);
                Vector3 des = -transform.up;
                Ray r = new Ray(or, des);
                bool isHidden = false;

                if (Physics.Raycast(r, out hit, Mathf.Infinity))
                {
                    #region detect if hidden
                    if (stopWhenHidden)
                    {
                        // Verify if the first points are hidden is behind !g
                        Vector3 viewPos = Camera.main.WorldToViewportPoint(hit.point);
                        Vector3 p = Camera.main.ViewportToWorldPoint(new Vector3(viewPos.x, viewPos.y, Camera.main.nearClipPlane));
                        RaycastHit outHit;
                    //   Debug.DrawRay(p, hit.point-p, Color.magenta);
                        Ray rOut = new Ray(p, (hit.point - p) / 2f);

                        if (Physics.Raycast(rOut, out outHit, Mathf.Infinity))
                        {
                            // if (Vector3.Distance(outHit.point, hit.point) < stopHiddenTolerance / 1000.0f)
                          //  Debug.DrawRay(outHit.point, Vector3.up/10.0f, Color.blue);

                             if (Vector3.Distance(outHit.point, hit.point) > stopHiddenTolerance / 10000.0f)
                           // if (outHit.point != hit.point)
                          {

                                 //   Debug.DrawLine(outHit.point, hit.point, Color.green);
                               // if(prevPoint!=Vector3.zero) Debug.DrawLine(prevPoint, prevPoint + Vector3.down * 5f, Color.green);
                                isHidden = true;
                                skipNext = true;
                            }
                            else
                            {
                               // skipNext = false;
                             //   isHidden = false;

                            }
                        }
                        else
                        {
                                Debug.DrawLine(outHit.point, hit.point, Color.cyan);
                        }
                    }
                    #endregion

                    #region skip radius

                    if(fadeMinRadius != 0 && fadeMaxRadius != 0 && ShouldFadeByDistance(hit.point, skipNext))
                    {
                        isHidden = true;
                        skipNext = true;
                    }
                    #endregion


                    if (!skipNext && !isHidden)
                        if (firstLineDisplayed == -1)
                            firstLineDisplayed = x;

                    if (prevPoint != Vector3.zero)
                    {
                        if (skipLongDistance && prevPointDisplayed != Vector3.zero)
                            if (!isHidden && Vector3.Distance(hit.point, prevPointDisplayed) > skipDistance)
                            {
                                isHidden = true;
                                // skipNext = true;
                                Debug.DrawLine(hit.point, prevPointDisplayed, Color.red);
                                prevPoint = hit.point;
                                prevPointDisplayed = hit.point;
                            }


                        if (!skipNext && !isHidden)
                        {
                            // Here we draw the main line
                            Debug.DrawLine(hit.point, prevPoint, Color.black);

                            if (firstPointOfLine == Vector3.zero)
                                firstPointOfLine = prevPoint;

                            if (closeType == CloseType.SimpleSmartFill || closeType == CloseType.SmartFill)
                            {
                                if (x == firstLineDisplayed)
                                {
                                    closeShapePoints.Add(prevPoint);
                                }
                                else
                                {
                                    if (prevPointDisplayed == Vector3.zero) // if it's the first one
                                        closeShapePoints.Insert(0, prevPoint);
                                }
                                prevPointDisplayed = prevPoint;
                            }

                            if (save) {
                                Vector3 p = Camera.main.WorldToScreenPoint(prevPoint);
                              //  Debug.DrawRay(prevPoint, Vector3.up*0.2f, Color.magenta, 1.0f);

                                // Vector3 prevP = Camera.main.WorldToScreenPoint(prevPoint);
                                if (isInCanvasBounds(p))
                                {
                                    // old close shape
                                    if (!isFirstPointFound)
                                    {
                                        isFirstPointFound = true;
                                        if (closeType == CloseType.Expand)
                                            svgCanvas.AddPoint(p.x, (h - p.y + h));
                                    }

                                    svgCanvas.AddPoint(p.x, (h - p.y));
                                    lastSavedAdded = prevPoint;
                                }
                            }
                        }

                        if (!firstY)
                        {
                            firstY = true;
                        }
                    }
                    if (!isHidden)
                    {
                        prevPoint = hit.point;
                        skipNext = false;
                    }
                    if(isHidden) // Si c'est caché, on ajoute celui d'avant et on ferme
                    {
                        if (save && prevPoint != Vector3.zero)
                        {

                            if (skipLongDistanceHidden)
                            {
                                if (lastSavedAdded != Vector3.zero && Vector3.Distance(lastSavedAdded, prevPoint) > skipDistance)
                                {

                                    /*Debug.Log(Vector3.Distance(lastSavedAdded, prevPoint));
                                    Debug.Log(skipDistance);
                                    Debug.Log("---");
                                    Debug.DrawRay(lastSavedAdded, Vector3.up, Color.red, 1.0f);
                                    Debug.DrawRay(prevPoint, Vector3.up, Color.red, 1.0f);
                                    */
                                    svgCanvas.EndPoint();
                                    Vector3 ap = Camera.main.WorldToScreenPoint(prevPoint);
                                    if (closeType == CloseType.Expand)
                                        svgCanvas.AddPoint(ap.x, (h - ap.y + h));

                                    //  Debug.Log("skip");
                                }
                            }


                            
                            Vector3 p = Camera.main.WorldToScreenPoint(prevPoint);
                          //  Debug.DrawRay(prevPoint, Vector3.up * 0.2f, Color.magenta, 1.0f);

                            // Vector3 prevP = Camera.main.WorldToScreenPoint(prevPoint);
                            if (isInCanvasBounds(p))
                            {
                                // old close shape
                                if (!isFirstPointFound)
                                {
                                    isFirstPointFound = true;
                                    if (closeType == CloseType.Expand)
                                        svgCanvas.AddPoint(p.x, (h - p.y + h));
                                }

                                svgCanvas.AddPoint(p.x, (h - p.y));
                                lastSavedAdded = prevPoint;
                            }


                            if (!skipLongDistanceHidden)
                            {
                                svgCanvas.EndPoint();
                            }

                        }

                        //  

                    }
                }
                else // si aucun rayon n'est trouvé
                {
                    if (y > 0)
                    {
                        prevPointDisplayed = Vector3.zero;
                        skipNext = true;
                        //   hasFoundInX = false;
                        /*
                        if (save)
                        {
                            // On force lajout de point précédent
                            Vector3 p = Camera.main.WorldToScreenPoint(prevPoint);
                            Debug.DrawRay(prevPoint, Vector3.up * 0.2f, Color.magenta, 1.0f);

                            // Vector3 prevP = Camera.main.WorldToScreenPoint(prevPoint);
                            if (isInCanvasBounds(p))
                            {
                                // old close shape
                                if (!isFirstPointFound)
                                {
                                    isFirstPointFound = true;
                                    if (closeType == CloseType.Expand)
                                        svgCanvas.AddPoint(p.x, (h - p.y + h));
                                }

                                svgCanvas.AddPoint(p.x, (h - p.y));
                                lastSavedAdded = prevPoint;
                            }


                            svgCanvas.EndPoint();
                        }
                        */
                    }
                }

            } // End loop Y;




            // force saving last point

           

            if (closeType == CloseType.SimpleSmartFill || closeType == CloseType.SmartFill)
            {
                if (x != firstLineDisplayed)
                    closeShapePoints.Add(prevPoint); //  we add the last one

                for (int i = closeShapePoints.Count - 1; i >= 0; i--)
                {
                    if (i < 0)
                        Debug.DrawLine(closeShapePoints[i], closeShapePoints[i - 1], Color.blue);

                    if (save)
                    {
                        // Debug.DrawRay(point, Vector3.up);
                        Vector3 p = Camera.main.WorldToScreenPoint(closeShapePoints[i]);
                        // Vector3 prevP = Camera.main.WorldToScreenPoint(prevPoint);
                        if (isInCanvasBounds(p))
                        {
                            svgCanvas.AddPoint(p.x, (h - p.y));
                        }
                    }
                } // end foreach
                // end save
            }

            // Fin de la ligne Ajout d'un poin a perpet
            if (save)
            {
                // Ajout d'un point a distance
                if (lastSavedAdded != Vector3.zero && closeType == CloseType.Expand)
                {
                    Vector3 p = Camera.main.WorldToScreenPoint(lastSavedAdded);
                    svgCanvas.AddPoint(p.x, (h - p.y + h));
                }
                svgCanvas.EndPoint();
            }


        } /// End loop X

        if (save)
        {
            save = false;
            svgCanvas.End();
        }


    }


    public void saveSVGPoint(Vector3 point)
    {

    }


    public bool isInCanvasBounds(Vector3 p)
    {
        return p.z > 0 &&
                            p.x > -expandMargin &&
                            (h - p.y) > -expandMargin &&
                            p.x < w + expandMargin &&
                            (h - p.y) < h + expandMargin;
    }

    public Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 angles)
    {
        return Quaternion.Euler(angles) * (point - pivot) + pivot;
    }

    void PointsToSVG()
    {

        /*
        <polyline class="st0" points="219.5,377 252.5,411 313,377 385.5,447.5 "/>
        <polyline class="st0" points="210.5,421.5 263,377 306,421.5 363,477 457,477 "/>
        <polyline class="st0" points="191,405 285,447.5 347.5,426.3 506.5,426.3 "/>
        */
    }


    private void OnDrawGizmos()
    {
        DrawArrow.ForGizmo(transform.position, -transform.up * 8, 1.0f);
        Gizmos.DrawWireCube(transform.position, new Vector3(0.2f, 0.2f, 0.2f));
        int cubeHeight = 5;
        Vector3 cubeSize = new Vector3(width , cubeHeight, height );

        Gizmos.color = new Color(0, 0, 1, 0.4f);
        Gizmos.DrawWireSphere(transform.position, fadeMinRadius);
        Gizmos.color = new Color(0, 0, 1, 0.2f);
        Gizmos.DrawWireSphere(transform.position, fadeMaxRadius);


        Vector3 halfExtents = Vector3.one;
        Gizmos.matrix = Matrix4x4.TRS(this.transform.position, this.transform.rotation, this.transform.lossyScale);
        Gizmos.color = new Color(1, 1, 1, 0.2f);
        Gizmos.DrawCube(Vector3.zero, cubeSize);
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(Vector3.zero, cubeSize);

        /*
        DrawArrow.ForGizmo(transform.position - new Vector3(width / 2, 0, 0), -transform.up * 3, 1.0f);
        DrawArrow.ForGizmo(transform.position + new Vector3(width / 2, 0, 0), -transform.up * 3, 1.0f);
        DrawArrow.ForGizmo(transform.position - new Vector3(width / 2, 0, height / 2), -transform.up * 3, 1.0f);
        DrawArrow.ForGizmo(transform.position + new Vector3(width / 2, 0, height / 2), -transform.up * 3, 1.0f);*/
    }
}
