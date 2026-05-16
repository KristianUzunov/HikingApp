using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CompassController : MonoBehaviour
{
    public Transform playerDotTransform;

    public float metersPerPixel = 1f;
    public float rotationSmoothing = 8f;

    private float targetHeading;
    private float currentHeading;
    private bool ready;

    void Start()
    {
        StartCoroutine(InitCompass());
    }

    IEnumerator InitCompass()
    {
        Input.compass.enabled = true;
        Input.gyro.enabled = true;

        while (Input.location.status != LocationServiceStatus.Running)
        {
            yield return null;
        }

        yield return new WaitForSeconds(1f);
        ready = true;
    }

    void Update()
    {
        if (!ready)
        {
            return;
        }

        float heading = Input.compass.magneticHeading;

        if (Input.compass.trueHeading >= 0)
        {
            heading = Input.compass.trueHeading;
        }

        targetHeading = heading;
        currentHeading = Mathf.LerpAngle(currentHeading, targetHeading, Time.deltaTime * rotationSmoothing);

        if (playerDotTransform != null)
        {
            playerDotTransform.localRotation = Quaternion.Euler(0f, 0f, -currentHeading);
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused)
        {
            Input.compass.enabled = true;
        }
    }


}