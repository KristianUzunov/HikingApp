using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GPSSimulator  replaces real GPS with a fake walking route.
/// 
/// SETUP
/// 
/// 
/// To disable simulation and use real GPS: untick "Enable Simulation"
/// in the Inspector, or set the scripting define NO_GPS_SIM.
/// </summary>
public class GPSSimulator : MonoBehaviour
{
    //  Simulation modes 
    public enum SimMode
    {
        StraightLine,       // walks in a straight line
        Loop,               // walks a square loop and repeats
        HillyTrail,         // simulates elevation change on a winding path
        CustomWaypoints,    // follows your own list of lat/lon points
    }

    [Header("Simulation Control")]
    public bool enableSimulation = true;

    [Tooltip("Simulated walking speed in km/h")]
    [Range(1f, 15f)]
    public float walkingSpeedKmh = 5f;

    [Tooltip("How often a new GPS reading is emitted (seconds)")]
    [Range(0.5f, 5f)]
    public float updateInterval = 1f;

    public SimMode simulationMode = SimMode.Loop;

    [Header("Start Position (all modes)")]
    [Tooltip("Starting latitude   default is central London")]
    public double startLatitude = 51.5074;
    [Tooltip("Starting longitude  default is central London")]
    public double startLongitude = -0.1278;
    [Tooltip("Starting altitude in metres")]
    public float startAltitude = 20f;

    [Header("Custom Waypoints (CustomWaypoints mode only)")]
    [Tooltip("List of lat/lon pairs to walk between. Add as many as you like.")]
    public List<SimWaypoint> customWaypoints = new List<SimWaypoint>();

    [Header("GPS Noise")]
    [Tooltip("Add random jitter to simulate real GPS inaccuracy (metres)")]
    [Range(0f, 20f)]
    public float noiseMagnitudeMetres = 3f;

    [Header("Debug UI (optional)")]
    [Tooltip("Optional TMP label to show current simulated position")]
    public TextMeshProUGUI debugLabel;

    //  Public heading (read by CompassController in sim mode) 
    /// <summary>Current simulated compass heading in degrees (0 = north).</summary>
    public float SimulatedHeading { get; private set; }

    //  Private 
    private double _lat, _lon;
    private float _alt;
    private float _heading;   // degrees, 0 = north
    private int _waypointIndex;
    private float _waypointProgress;  // 0–1 between current and next waypoint
    private bool _running;

    // Metres per degree (approximate, good enough for simulation)
    private const double MetresPerDegreeLat = 111_320.0;

    void Start()
    {
        if (!enableSimulation) return;

#if UNITY_EDITOR
        // Always simulate in Editor regardless of the toggle
#else
        if (!enableSimulation) return;
#endif

        _lat = startLatitude;
        _lon = startLongitude;
        _alt = startAltitude;

        // Pre-fill custom waypoints if list is empty
        if (simulationMode == SimMode.CustomWaypoints && customWaypoints.Count < 2)
        {
            Debug.LogWarning("[GPSSimulator] CustomWaypoints mode needs at least 2 waypoints. Falling back to Loop.");
            simulationMode = SimMode.Loop;
        }

        // Stop any real GPS that may have started
        if (GPSManager.Instance != null && GPSManager.Instance.IsRunning)
            GPSManager.Instance.StopGPS();

        _running = true;
        StartCoroutine(SimulationLoop());
        Debug.Log($"[GPSSimulator] Started in {simulationMode} mode at ({_lat:F5}, {_lon:F5})");
    }

    void OnDestroy() => _running = false;

    //  Main loop 

    private IEnumerator SimulationLoop()
    {
        // Fake the GPS ready event so the app initialises normally
        yield return new WaitForSeconds(0.5f);
        InjectPosition(_lat, _lon, _alt);
        GPSManager.Instance?.SimulateReady();

        while (_running)
        {
            yield return new WaitForSeconds(updateInterval);

            float distanceThisStep = (walkingSpeedKmh * 1000f / 3600f) * updateInterval;
            AdvancePosition(distanceThisStep);
            AddNoise();
            InjectPosition(_lat, _lon, _alt);
            UpdateDebugLabel();
        }
    }

    //  Movement logic 

    private void AdvancePosition(float distMetres)
    {
        switch (simulationMode)
        {
            case SimMode.StraightLine:
                MoveInDirection(_heading, distMetres);
                break;

            case SimMode.Loop:
                AdvanceLoop(distMetres);
                break;

            case SimMode.HillyTrail:
                AdvanceHillyTrail(distMetres);
                break;

            case SimMode.CustomWaypoints:
                AdvanceCustomWaypoints(distMetres);
                break;
        }
    }

    // Straight line  just keep walking at current heading
    // Call SetHeading(degrees) to change direction
    public void SetHeading(float degrees)
    {
        _heading = degrees;
        SimulatedHeading = degrees;
    }


    private float _loopDistAccum;
    private int _loopLeg;
    private readonly float[] _loopHeadings = { 0f, 90f, 180f, 270f };
    private const float LoopLegLength = 200f;

    private void AdvanceLoop(float dist)
    {
        MoveInDirection(_loopHeadings[_loopLeg], dist);
        SimulatedHeading = _loopHeadings[_loopLeg]; // expose current heading
        _loopDistAccum += dist;
        if (_loopDistAccum >= LoopLegLength)
        {
            _loopDistAccum = 0f;
            _loopLeg = (_loopLeg + 1) % 4;
        }
    }

    // Winding hilly trail with elevation variation
    private float _hillyAngle;
    private void AdvanceHillyTrail(float dist)
    {
        _hillyAngle += dist * 0.5f;  // heading slowly curves
        float heading = _hillyAngle % 360f;
        MoveInDirection(heading, dist);
        SimulatedHeading = heading; // expose current heading
        _alt += Mathf.Sin(_hillyAngle * Mathf.Deg2Rad * 0.1f) * 2f;  // gentle hills
    }

    // Custom waypoints: interpolate between each pair
    private void AdvanceCustomWaypoints(float dist)
    {
        if (customWaypoints.Count < 2) return;

        var a = customWaypoints[_waypointIndex];
        var b = customWaypoints[(_waypointIndex + 1) % customWaypoints.Count];

        double segDistM = HaversineMetres(a.latitude, a.longitude, b.latitude, b.longitude);
        if (segDistM < 1.0) segDistM = 1.0;

        _waypointProgress += (float)(dist / segDistM);

        if (_waypointProgress >= 1f)
        {
            _waypointProgress = 0f;
            _waypointIndex = (_waypointIndex + 1) % customWaypoints.Count;
        }

        var next = customWaypoints[(_waypointIndex + 1) % customWaypoints.Count];

        double prevLat = _lat, prevLon = _lon;
        _lat = Lerp(a.latitude, next.latitude, _waypointProgress);
        _lon = Lerp(a.longitude, next.longitude, _waypointProgress);
        _alt = Mathf.Lerp(a.altitude, next.altitude, _waypointProgress);

        // Derive heading from movement direction
        SimulatedHeading = BearingDegrees(prevLat, prevLon, _lat, _lon);
    }



    private void MoveInDirection(float headingDeg, float distMetres)
    {
        double headingRad = headingDeg * Mathf.Deg2Rad;
        double metresPerDegreeLon = MetresPerDegreeLat * System.Math.Cos(_lat * Mathf.Deg2Rad);

        _lat += (distMetres * System.Math.Cos(headingRad)) / MetresPerDegreeLat;
        _lon += (distMetres * System.Math.Sin(headingRad)) / metresPerDegreeLon;
    }

    private void AddNoise()
    {
        if (noiseMagnitudeMetres <= 0f) return;
        double metresPerDegreeLon = MetresPerDegreeLat * System.Math.Cos(_lat * Mathf.Deg2Rad);
        _lat += (Random.Range(-noiseMagnitudeMetres, noiseMagnitudeMetres)) / MetresPerDegreeLat;
        _lon += (Random.Range(-noiseMagnitudeMetres, noiseMagnitudeMetres)) / metresPerDegreeLon;
        _alt += Random.Range(-0.5f, 0.5f);
    }

    private void InjectPosition(double lat, double lon, float alt)
    {
        if (GPSManager.Instance == null) return;
        GPSManager.Instance.InjectSimulatedPosition(lat, lon, alt, noiseMagnitudeMetres);
    }

    private void UpdateDebugLabel()
    {
        if (debugLabel == null) return;
        debugLabel.text =
            $"SIM  {simulationMode}\n" +
            $"Lat {_lat:F6}  Lon {_lon:F6}\n" +
            $"Alt {_alt:F1} m   Heading {SimulatedHeading:F0}°   Speed {walkingSpeedKmh} km/h";
    }

    //  Math helpers 

    private static double Lerp(double a, double b, float t) => a + (b - a) * t;

    private static double HaversineMetres(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000;
        double dLat = (lat2 - lat1) * Mathf.Deg2Rad;
        double dLon = (lon2 - lon1) * Mathf.Deg2Rad;
        double s = System.Math.Sin(dLat / 2);
        double s2 = System.Math.Sin(dLon / 2);
        double h = s * s + System.Math.Cos(lat1 * Mathf.Deg2Rad) * System.Math.Cos(lat2 * Mathf.Deg2Rad) * s2 * s2;
        return 2 * R * System.Math.Asin(System.Math.Sqrt(h));
    }

    /// <summary>Returns the compass bearing (0–360°) from point A to point B.</summary>
    private static float BearingDegrees(double lat1, double lon1, double lat2, double lon2)
    {
        double dLon = (lon2 - lon1) * Mathf.Deg2Rad;
        double lat1R = lat1 * Mathf.Deg2Rad;
        double lat2R = lat2 * Mathf.Deg2Rad;
        double y = System.Math.Sin(dLon) * System.Math.Cos(lat2R);
        double x = System.Math.Cos(lat1R) * System.Math.Sin(lat2R)
                 - System.Math.Sin(lat1R) * System.Math.Cos(lat2R) * System.Math.Cos(dLon);
        float bearing = (float)(System.Math.Atan2(y, x) * (180.0 / System.Math.PI));
        return (bearing + 360f) % 360f;
    }
}


[System.Serializable]
public class SimWaypoint
{
    public double latitude;
    public double longitude;
    public float altitude;

    public SimWaypoint() { }
    public SimWaypoint(double lat, double lon, float alt = 0f)
    { latitude = lat; longitude = lon; altitude = alt; }
}