using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class MappingController : MonoBehaviour
{
    #region INSPECTOR_VARIABLES
    public GameObject spherePrefab, forwardPrefab;
    public TextMeshProUGUI driftText, informationText, distanceText;
    [Tooltip("Divides the screen along the height and width by that much times for array raycasting")]
    public int screenSplitCount = 5;
    // [Tooltip("Enable this to instantiate objects along distance, only initial anchor will be present; Disable to use array raycasting for finding feature points to anchor objects along the way")]
    [Tooltip("Range in meters for array raycasting")]
    public float raycastRange = 1.5f;
    public float gpsTolerance = 0.00005f;
    public Toggle toggleInstantiationTypeButton;
    public Toggle toggleARFoundationButton;
    #endregion

    #region PRIVATE_VARIABLES
    ARSessionOrigin arSessionOrigin;
    ARRaycastManager arRaycastManager;
    ARPlaneManager arPlaneManager;
    ARPointCloudManager arPointCloudManager;


    List<ARRaycastHit> hits = new List<ARRaycastHit>();
    List<ARRaycastHit> wayHits = new List<ARRaycastHit>();
    List<GameObject> instantiatedObjects = new List<GameObject>();

    GameObject origin;
    bool isPlaying;
    bool isOriginFound;
    bool isVisible;
    bool shouldInstantiateWithDistance = false;
    bool isARFoundationEnabled;
    bool isGPSEnabled;
    bool isAtOriginPoint;

    Vector3 initialPos, currentPos, previousPos;

    LocationService locationService;
    Coordinates initialCoordinates;
    Coordinates finalCoordinates;

    List<Vector2> screenPointsForRaycasting = new List<Vector2>();
    #endregion

    void Awake()
    {

        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            Permission.RequestUserPermission(Permission.FineLocation);

    }

    // Start is called before the first frame update
    void Start()
    {

        isPlaying = false;
        isOriginFound = false;
        isVisible = true;
        isGPSEnabled = false;
        isAtOriginPoint = false;

        arRaycastManager = FindObjectOfType<ARRaycastManager>();
        arPlaneManager = FindObjectOfType<ARPlaneManager>();
        arPointCloudManager = FindObjectOfType<ARPointCloudManager>();

        GetScreenPoints();

        initialCoordinates.gpsLat = 12.99699f;
        initialCoordinates.gpsLong = 77.62651f;
        finalCoordinates.gpsLat = 12.99673f;
        finalCoordinates.gpsLong = 77.62669f;


        StartCoroutine(GenerateRoutePrefabs());

    }

    #region PUBLIC_METHODS
    public void CreateSpheresOnTheGo()
    {
        shouldInstantiateWithDistance = toggleInstantiationTypeButton.isOn;
        toggleInstantiationTypeButton.interactable = false;

        isARFoundationEnabled = toggleARFoundationButton.isOn;
        toggleARFoundationButton.interactable = false;

        isPlaying = true;
        if (shouldInstantiateWithDistance)
            StartCoroutine(InitiateMultiWorldPlacement());
        else
            StartCoroutine(InstantiateAtNearestFeaturePoint());
    }

    public void StopSpheres()
    {
        toggleInstantiationTypeButton.interactable = true;
        toggleARFoundationButton.interactable = true;

        isPlaying = false;
        isOriginFound = false;
    }

    public void ToggleVisibility()
    {
        isVisible = !isVisible;

        foreach (GameObject go in instantiatedObjects)
        {
            go.transform.GetComponent<Renderer>().enabled = isVisible;
        }

    }

    public void StartRouting()
    {
        isAtOriginPoint = true;
    }

    void GetScreenPoints()
    {
        float incrementalWidth = Screen.width / screenSplitCount;
        float incrementalHeight = Screen.height / screenSplitCount;

        screenPointsForRaycasting.Clear();
        for (Vector2 currentPoint = Vector2.zero; currentPoint.y <= Screen.height; currentPoint.y += incrementalHeight)
        {
            for (currentPoint.x = 0; currentPoint.x <= Screen.width; currentPoint.x += incrementalWidth)
            {
                screenPointsForRaycasting.Add(currentPoint);
                Debug.Log("POINTS : " + currentPoint);
            }
        }

    }

    public void ClearAllSpheres()
    {
        foreach (GameObject go in instantiatedObjects)
            Destroy(go.gameObject);

        instantiatedObjects.Clear();
    }

    public void SetOrigin()
    {
        if (isARFoundationEnabled)
        {
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (arRaycastManager.Raycast(touch.position, hits, TrackableType.FeaturePoint))
                {
                    origin = Instantiate(spherePrefab, hits[0].pose.position, Quaternion.identity);
                    instantiatedObjects.Add(origin);
                    previousPos = Camera.main.transform.position;
                    isOriginFound = true;
                    //initialLocationInfo = locationService.lastData;
                    //initialPos = new Vector3(initialLocationInfo.latitude, 0, initialLocationInfo.longitude);
                    //initialPos = hits[0].pose.position;
                }
            }
        }
        else
        {
            origin = Instantiate(spherePrefab, Camera.main.transform.position, Quaternion.identity);
            instantiatedObjects.Add(origin);
            previousPos = Camera.main.transform.position;
            isOriginFound = true;
        }
    }


    public Vector3 GeneratePathInDirection(Vector3 currentPosition,int distanceInMeters,DIRECTION turnDirection)
    {
        currentPos = currentPosition;
        float angle;

        switch (turnDirection)
        {
            case DIRECTION.FORWARD:
                angle = 0;
                break;
            case DIRECTION.RIGHT:
                angle = 90;
                break;
            case DIRECTION.LEFT:
                angle = -90;
                break;
            default:
                angle = 0;
                break;
        }

        for (int meterCount = 0; meterCount < distanceInMeters; meterCount++)
        {
            GameObject go = Instantiate(forwardPrefab, currentPos, Camera.main.transform.rotation);
            Camera.main.transform.DetachChildren();
            go.transform.SetPositionAndRotation(currentPos, Quaternion.Euler(new Vector3(0, go.transform.rotation.eulerAngles.y + angle, go.transform.rotation.eulerAngles.z)));
            instantiatedObjects.Add(go);
            currentPos += go.transform.forward;
        }

        return currentPos;
    }


    #endregion



    #region COROUTINES
    public IEnumerator InstantiateAtNearestFeaturePoint()
    {

        yield return new WaitUntil(() => isPlaying);

        while (!isOriginFound)
        {
            SetOrigin();
            yield return new WaitForEndOfFrame();
        }

        while (isPlaying)
        {
            currentPos = Camera.main.transform.position;
            //  Vector3 instantiatePos = new Vector3(currentPos.x, .5f, currentPos.z);
            if (Vector3.Distance(currentPos, previousPos) > 1)
            {
                foreach (Vector2 screenPoint in screenPointsForRaycasting)
                {
                    wayHits.Clear();
                    if (arRaycastManager.Raycast(screenPoint, wayHits, TrackableType.FeaturePoint))
                    {
                        if (Vector3.Distance(currentPos, wayHits[0].pose.position) < raycastRange)
                        {
                            GameObject go = Instantiate(forwardPrefab, wayHits[0].pose.position, wayHits[0].pose.rotation);
                            instantiatedObjects.Add(go);
                            previousPos = currentPos;
                            break;
                        }
                    }
                }
            }

            yield return new WaitForEndOfFrame();
        }
    }

    public IEnumerator InitiateMultiWorldPlacement()
    {
        yield return new WaitUntil(() => isPlaying);

        while (!isOriginFound)
        {
            SetOrigin();
            yield return new WaitForEndOfFrame();
        }

        while (isPlaying)
        {
            currentPos = Camera.main.transform.position;
            //  Vector3 instantiatePos = new Vector3(currentPos.x, .5f, currentPos.z);
            if (Vector3.Distance(currentPos, previousPos) > 1)
            {
                GameObject go = Instantiate(forwardPrefab, Camera.main.transform);
                Camera.main.transform.DetachChildren();
                go.transform.SetPositionAndRotation(go.transform.position, Quaternion.Euler(new Vector3(0, go.transform.rotation.eulerAngles.y, go.transform.rotation.eulerAngles.z)));

                instantiatedObjects.Add(go);
                previousPos = currentPos;
            }
            yield return new WaitForEndOfFrame();
        }
    }

    public IEnumerator GenerateRoutePrefabs()
    {
        driftText.text = "Please enable GPS";
        yield return new WaitUntil(() => Input.location.isEnabledByUser);
        Input.location.Start();



        while (true)
        {

            driftText.text = "LAT : " + Input.location.lastData.latitude + ", LONG : " + Input.location.lastData.longitude;

            if ((Mathf.Abs(Input.location.lastData.latitude - initialCoordinates.gpsLat) < gpsTolerance) && (Mathf.Abs(Input.location.lastData.longitude - initialCoordinates.gpsLong) < gpsTolerance))
            {
                informationText.text = "INITIAL POSITION";

            }
            else if ((Mathf.Abs(Input.location.lastData.latitude - finalCoordinates.gpsLat) < gpsTolerance) && (Mathf.Abs(Input.location.lastData.longitude - finalCoordinates.gpsLong) < gpsTolerance))
            {
                informationText.text = "FINAL DESINATION";

            }
            else
                informationText.text = "FIND A NODAL POINT.";

            if (isAtOriginPoint)
            {
                Vector3 currentPos = Camera.main.transform.position;

                for (int goStaight = 0; goStaight < 3; goStaight++)
                {
                    GameObject go = Instantiate(forwardPrefab, currentPos, Camera.main.transform.rotation);
                    Camera.main.transform.DetachChildren();
                    go.transform.SetPositionAndRotation(currentPos, Quaternion.Euler(new Vector3(0, go.transform.rotation.eulerAngles.y, go.transform.rotation.eulerAngles.z)));
                    instantiatedObjects.Add(go);
                    currentPos += go.transform.forward;
                }



                for (int goRight = 0; goRight < 16; goRight++)
                {
                    GameObject go = Instantiate(forwardPrefab, currentPos, Camera.main.transform.rotation);
                    Camera.main.transform.DetachChildren();
                    go.transform.SetPositionAndRotation(currentPos, Quaternion.Euler(new Vector3(0, go.transform.rotation.eulerAngles.y + 90, go.transform.rotation.eulerAngles.z)));
                    instantiatedObjects.Add(go);
                    currentPos += go.transform.forward;
                }
                isAtOriginPoint = false;

                //Vector3 currentPos = Camera.main.transform.position;
                //currentPos = GeneratePathInDirection(currentPos, 3, DIRECTION.FORWARD);
                //currentPos = GeneratePathInDirection(currentPos, 16, DIRECTION.RIGHT);
                //isAtOriginPoint = false;

            }



            if (instantiatedObjects.Count != 0)
            {
                float distance = (instantiatedObjects.Count - 1) + Vector3.Distance(instantiatedObjects[instantiatedObjects.Count - 1].transform.position, Camera.main.transform.position);
                distanceText.text = "Distance covered : " + distance + " Meters";
            }
            else
            {
                distanceText.text = "Journey not started";
            }

            yield return new WaitForEndOfFrame();
        }
    }
    #endregion

    // Update is called once per frame
    void Update()
    {

        //if (!isGPSEnabled)
        //{
        //    if (Input.location.isEnabledByUser)
        //    {
        //        Input.location.Start();
        //        isGPSEnabled = true;
        //    }
        //}

        //if (Input.location.isEnabledByUser)
        //{
        //    driftText.text = "LAT : " + Input.location.lastData.latitude + ", LONG : " + Input.location.lastData.longitude;

        //    if ((Input.location.lastData.latitude - initialCoordinates.gpsLat < gpsTolerance) && (Input.location.lastData.longitude - initialCoordinates.gpsLong < gpsTolerance))
        //        informationText.text = "INITIAL POSITION";
        //    else if ((Input.location.lastData.latitude - finalCoordinates.gpsLat < gpsTolerance) && (Input.location.lastData.longitude - finalCoordinates.gpsLong < gpsTolerance))
        //        informationText.text = "FINAL DESINATION";
        //    else
        //        informationText.text = "FIND A NODAL POINT.";

        //}
        //else
        //    driftText.text = "Please enable GPS";

        //if (instantiatedObjects.Count != 0)
        //{
        //    float distance = (instantiatedObjects.Count-1) + Vector3.Distance(instantiatedObjects[instantiatedObjects.Count-1].transform.position, Camera.main.transform.position);
        //    distanceText.text = "Distance covered : " + distance + " Meters";
        //}
        //else
        //{
        //    distanceText.text = "Journey not started";
        //}


    }

    #region STRUCTS,ENUMS AND CLASSES

   public struct Coordinates
    {
        public float gpsLat { get; set; }
        public float gpsLong { get; set; }
    }

   public enum DIRECTION
    {
        FORWARD=0,RIGHT=1,LEFT=2
    }

    #endregion
}