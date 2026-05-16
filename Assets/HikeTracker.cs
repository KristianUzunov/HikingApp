using System;
using UnityEngine;

public class HikeTracker : MonoBehaviour
{
    public static HikeTracker Instance { get; private set; }

    public enum TrackerState { Idle, Recording, Paused }
    public TrackerState State { get; private set; } = TrackerState.Idle;
    public HikeRecord ActiveHike { get; private set; }
    public float CurrentSpeedKmh { get; private set; }
    public float ElapsedSeconds { get; private set; }

    public event Action<HikeRecord> OnHikeStarted;
    public event Action<HikeRecord> OnHikeSaved;
    public event Action<GPSPoint> OnWaypointAdded;
    public event Action OnHikeDiscarded;

    public float minWaypointDistanceM = 10f;
    public float maxReasonableJumpM = 200f;

    private GPSPoint lastSavedPoint;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaClass unityClass;
    private AndroidJavaObject unityActivity;
    private const string serviceClassName = "com.kdg.toast.plugin.HikeTrackerService";
#endif

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
    }

    void OnEnable()
    {
        if (GPSManager.Instance != null)
        {
            GPSManager.Instance.OnLocationUpdated += HandleLocationUpdate;
        }
    }

    void OnDisable()
    {
        if (GPSManager.Instance != null)
        {
            GPSManager.Instance.OnLocationUpdated -= HandleLocationUpdate;
        }
    }

    void Update()
    {
        if (State == TrackerState.Recording)
        {
            ElapsedSeconds += Time.deltaTime;
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused && State == TrackerState.Recording)
        {
            SyncPointsFromService();
        }
    }

    public void StartHike(string name = null)
    {
        if (State == TrackerState.Recording)
        {
            return;
        }

        ActiveHike = new HikeRecord();
        ActiveHike.id = Guid.NewGuid().ToString("N")[..12];
        ActiveHike.startTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (string.IsNullOrWhiteSpace(name))
        {
            ActiveHike.name = "Hike " + DateTime.Now.ToString("MMM d HH-mm");
        }
        else
        {
            ActiveHike.name = name;
        }

        lastSavedPoint = null;
        ElapsedSeconds = 0f;
        State = TrackerState.Recording;

        if (!GPSManager.Instance.IsRunning)
        {
            GPSManager.Instance.StartGPS();
        }

        StartNativeService();
        OnHikeStarted?.Invoke(ActiveHike);
    }

    public void PauseHike()
    {
        if (State != TrackerState.Recording)
        {
            return;
        }
        State = TrackerState.Paused;
    }

    public void ResumeHike()
    {
        if (State != TrackerState.Paused)
        {
            return;
        }
        State = TrackerState.Recording;
        lastSavedPoint = null;
    }

    public void SaveHike(string customName = null, string notes = null)
    {
        if (ActiveHike == null)
        {
            return;
        }

        SyncPointsFromService();

        ActiveHike.endTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (!string.IsNullOrWhiteSpace(customName))
        {
            ActiveHike.name = customName;
        }
        if (!string.IsNullOrWhiteSpace(notes))
        {
            ActiveHike.notes = notes;
        }

        HikeStorage.SaveHike(ActiveHike);
        StopNativeService();

        HikeRecord saved = ActiveHike;
        ActiveHike = null;
        State = TrackerState.Idle;

        OnHikeSaved?.Invoke(saved);
    }

    public void DiscardHike()
    {
        StopNativeService();
        ActiveHike = null;
        State = TrackerState.Idle;
        OnHikeDiscarded?.Invoke();
    }

    public void OnBackgroundLocationUpdate(string data)
    {
        string[] parts = data.Split(',');
        if (parts.Length < 3)
        {
            return;
        }

        double lat = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
        double lon = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
        float alt = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);

        HandleLocationUpdate(lat, lon, alt);
    }

    void StartNativeService()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        unityActivity = unityClass.GetStatic<AndroidJavaObject>("currentActivity");

        AndroidJavaObject intent = new AndroidJavaObject(
            "android.content.Intent",
            unityActivity,
            new AndroidJavaClass(serviceClassName)
        );

        unityActivity.Call<AndroidJavaObject>("startForegroundService", intent);
#endif
    }

    void StopNativeService()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (unityActivity == null)
        {
            return;
        }

        AndroidJavaObject intent = new AndroidJavaObject(
            "android.content.Intent",
            unityActivity,
            new AndroidJavaClass(serviceClassName)
        );
        unityActivity.Call<bool>("stopService", intent);
#endif
    }

    void SyncPointsFromService()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (unityActivity == null)
        {
            return;
        }

        AndroidJavaClass serviceClass = new AndroidJavaClass(serviceClassName);
        string json = serviceClass.CallStatic<string>("GetSavedPoints", unityActivity);

        if (string.IsNullOrEmpty(json) || json == "[]")
        {
            return;
        }

        json = json.Trim('[', ']');
        string[] entries = json.Split(new string[] { "},{" }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (string entry in entries)
        {
            string clean = entry.Trim('{', '}');
            var fields = new System.Collections.Generic.Dictionary<string, double>();

            foreach (string pair in clean.Split(','))
            {
                string[] kv = pair.Split(':');
                if (kv.Length == 2)
                {
                    string key = kv[0].Trim('"');
                    if (double.TryParse(kv[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                    {
                        fields[key] = val;
                    }
                }
            }

            if (fields.ContainsKey("lat") && fields.ContainsKey("lon"))
            {
                float alt = 0f;
                if (fields.ContainsKey("alt"))
                {
                    alt = (float)fields["alt"];
                }
                HandleLocationUpdate(fields["lat"], fields["lon"], alt);
            }
        }
#endif
    }

    void HandleLocationUpdate(double lat, double lon, float alt)
    {
        if (State != TrackerState.Recording || ActiveHike == null)
        {
            return;
        }

        float accuracy = 5f;
        if (GPSManager.Instance != null)
        {
            accuracy = GPSManager.Instance.Accuracy;
        }

        GPSPoint newPoint = new GPSPoint(lat, lon, alt, accuracy);

        if (lastSavedPoint == null)
        {
            AppendWaypoint(newPoint);
            return;
        }

        float dist = HaversineMeters(lastSavedPoint, newPoint);

        if (maxReasonableJumpM > 0 && dist > maxReasonableJumpM)
        {
            return;
        }

        if (dist >= minWaypointDistanceM)
        {
            AppendWaypoint(newPoint);
        }

        float dtSec = (newPoint.timestampMs - lastSavedPoint.timestampMs) / 1000f;

        if (dtSec > 0)
        {
            CurrentSpeedKmh = (dist / dtSec) * 3.6f;
        }
        else
        {
            CurrentSpeedKmh = 0f;
        }
    }

    void AppendWaypoint(GPSPoint point)
    {
        if (lastSavedPoint != null)
        {
            float d = HaversineMeters(lastSavedPoint, point);
            float dAlt = point.altitude - lastSavedPoint.altitude;
            ActiveHike.totalDistanceMeters += d;

            if (dAlt > 0)
            {
                ActiveHike.elevationGainMeters += dAlt;
            }
            else
            {
                ActiveHike.elevationLossMeters += Mathf.Abs(dAlt);
            }
        }

        ActiveHike.waypoints.Add(point);
        lastSavedPoint = point;
        OnWaypointAdded?.Invoke(point);
    }

    static float HaversineMeters(GPSPoint a, GPSPoint b)
    {
        const double R = 6371000;
        double dLat = (b.latitude - a.latitude) * Math.PI / 180.0;
        double dLon = (b.longitude - a.longitude) * Math.PI / 180.0;
        double sinLat = Math.Sin(dLat / 2);
        double sinLon = Math.Sin(dLon / 2);
        double h = sinLat * sinLat + Math.Cos(a.latitude * Math.PI / 180.0) * Math.Cos(b.latitude * Math.PI / 180.0) * sinLon * sinLon;
        return (float)(2 * R * Math.Asin(Math.Sqrt(h)));
    }
}