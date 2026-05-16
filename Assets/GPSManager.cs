using System;
using System.Collections;
using UnityEngine;

public class GPSManager : MonoBehaviour
{
    public static GPSManager Instance { get; private set; }

    public float updateInterval = 3f;
    public float desiredAccuracyMeters = 10f;
    public float updateDistanceMeters = 5f;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public float Altitude { get; private set; }
    public float Accuracy { get; private set; }
    public bool IsRunning { get; private set; }
    public bool HasFix { get; private set; }
    public event Action<double, double, float> OnLocationUpdated;
    public event Action<string> OnGPSError;
    public event Action OnGPSReady;
    private Coroutine updateCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartGPS()
    {
        if (IsRunning)
        {
            return;
        }
        updateCoroutine = StartCoroutine(InitAndPollGPS());
    }

    public void InjectSimulatedPosition(double lat, double lon, float alt, float accuracy = 5f)
    {
        Latitude = lat;
        Longitude = lon;
        Altitude = alt;
        Accuracy = accuracy;
        HasFix = true;
        OnLocationUpdated?.Invoke(lat, lon, alt);
    }

    public void SimulateReady()
    {
        IsRunning = true;
        HasFix = true;
        OnGPSReady?.Invoke();
    }

    public void StopGPS()
    {
        if (updateCoroutine != null)
        {
            StopCoroutine(updateCoroutine);
        }
        Input.location.Stop();
        IsRunning = false;
        HasFix = false;
    }

    IEnumerator InitAndPollGPS()
    {
#if UNITY_EDITOR
        yield return new WaitForSeconds(1f);
        Latitude = 42.6977;
        Longitude = 23.3219;
        Altitude = 550f;
        Accuracy = 5f;
        HasFix = true;
        IsRunning = true;
        OnGPSReady?.Invoke();

        while (true)
        {
            yield return new WaitForSeconds(updateInterval);
            SimulateMovement();
        }
#else
        if (!Input.location.isEnabledByUser)
        {
            OnGPSError?.Invoke("Location services are disabled. Please enable them in Settings.");
            yield break;
        }

        Input.location.Start(desiredAccuracyMeters, updateDistanceMeters);

        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1f);
            maxWait--;
        }

        if (maxWait <= 0)
        {
            OnGPSError?.Invoke("GPS timed out. Please check your signal and try again.");
            yield break;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            OnGPSError?.Invoke("Unable to determine device location.");
            yield break;
        }

        IsRunning = true;
        HasFix = true;
        OnGPSReady?.Invoke();

        while (IsRunning)
        {
            LocationInfo data = Input.location.lastData;
            Latitude = data.latitude;
            Longitude = data.longitude;
            Altitude = data.altitude;
            Accuracy = data.horizontalAccuracy;

            OnLocationUpdated?.Invoke(Latitude, Longitude, Altitude);
            yield return new WaitForSeconds(updateInterval);
        }
#endif
    }

#if UNITY_EDITOR
    private float simAngle = 0f;

    void SimulateMovement()
    {
        simAngle += 5f;
        Latitude += Math.Cos(simAngle * Mathf.Deg2Rad) * 0.0001;
        Longitude += Math.Sin(simAngle * Mathf.Deg2Rad) * 0.0001;
        Altitude += UnityEngine.Random.Range(-1f, 1f);
        OnLocationUpdated?.Invoke(Latitude, Longitude, Altitude);
    }
#endif
}