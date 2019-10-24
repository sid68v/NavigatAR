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
    public GameObject spherePrefab;
    public TextMeshProUGUI driftText,informationText;
    [Tooltip("Divides the screen along the height and width by that much times for array raycasting")]
    public int screenSplitCount = 5;
    // [Tooltip("Enable this to instantiate objects along distance, only initial anchor will be present; Disable to use array racasting for finding feature points to anchor objects along the way")]
    [Tooltip("Range in meters for array raycasting")]
    public float raycastRange = 1.5f;
    public float gpsTolerance = 0.00005f;
    public Toggle toggleInstantiationTypeButton;
    public Toggle toggleARFoundationButton;

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

    Vector3 initialPos, currentPos, previousPos;

    LocationService locationService;
    Coordinates initialCoordinates ;
    Coordinates finalCoordinates;

    List<Vector2> screenPointsForRaycasting = new List<Vector2>();

    private void Awake()
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

        arRaycastManager = FindObjectOfType<ARRaycastManager>();
        arPlaneManager = FindObjectOfType<ARPlaneManager>();
        arPointCloudManager = FindObjectOfType<ARPointCloudManager>();

        GetScreenPoints();

        initialCoordinates.gpsLat = 12.99699f;
        initialCoordinates.gpsLong =77.62651f;
        finalCoordinates.gpsLat = 12.99673f;
        finalCoordinates.gpsLong = 77.62669f;

    }

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
                            GameObject go = Instantiate(spherePrefab, wayHits[0].pose.position, wayHits[0].pose.rotation);
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
                GameObject go = Instantiate(spherePrefab, currentPos, Quaternion.identity);
                instantiatedObjects.Add(go);
                previousPos = currentPos;
            }
            yield return new WaitForEndOfFrame();
        }
    }

    // Update is called once per frame
    void Update()
    {

        if (!isGPSEnabled)
        {
            if(Input.location.isEnabledByUser)
            {
                Input.location.Start();
                isGPSEnabled = true;
            }
        }

        if (Input.location.isEnabledByUser)
        {
            driftText.text = "LAT : " + Input.location.lastData.latitude + ", LONG : " + Input.location.lastData.longitude;

            if ((Input.location.lastData.latitude - initialCoordinates.gpsLat < gpsTolerance) && (Input.location.lastData.longitude - initialCoordinates.gpsLong < gpsTolerance))
                informationText.text = "INITIAL POSITION";
            else if ((Input.location.lastData.latitude - finalCoordinates.gpsLat < gpsTolerance) && (Input.location.lastData.longitude - finalCoordinates.gpsLong < gpsTolerance))
                informationText.text = "FINAL DESINATION";
            else
                informationText.text = "FIND A NODAL POINT.";

        }
        else
            driftText.text = "Please enable GPS";


        

    }

    struct Coordinates
    {
        public float gpsLat { get; set;}
        public float gpsLong { get; set; }
    }

}