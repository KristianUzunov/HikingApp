using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class GPSPoint
{
    public double latitude;
    public double longitude;
    public float altitude;
    public float accuracy;
    public long timestampMs;

    public GPSPoint() { }

    public GPSPoint(double lat, double lon, float alt, float acc)
    {
        latitude = lat;
        longitude = lon;
        altitude = alt;
        accuracy = acc;
        timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    public DateTime ToDateTime()
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).LocalDateTime;
    }
}

[Serializable]
public class HikeRecord
{
    public string id;
    public string name;
    public string notes;
    public long startTimeMs;
    public long endTimeMs;
    public float totalDistanceMeters;
    public float elevationGainMeters;
    public float elevationLossMeters;
    public string snapshotPath;
    public List<GPSPoint> waypoints = new List<GPSPoint>();

    public TimeSpan Duration
    {
        get { return TimeSpan.FromMilliseconds(endTimeMs - startTimeMs); }
    }

    public DateTime StartDateTime
    {
        get { return DateTimeOffset.FromUnixTimeMilliseconds(startTimeMs).LocalDateTime; }
    }

    public float AverageSpeedKmh
    {
        get
        {
            float hours = (float)Duration.TotalHours;
            if (hours > 0)
            {
                return (totalDistanceMeters / 1000f) / hours;
            }
            return 0f;
        }
    }

    public string FormattedDuration
    {
        get
        {
            TimeSpan d = Duration;
            if (d.Hours > 0)
            {
                return d.Hours + "h " + d.Minutes.ToString("D2") + "m";
            }
            return d.Minutes + "m " + d.Seconds.ToString("D2") + "s";
        }
    }
}

[Serializable]
public class HikeLibrary
{
    public List<HikeRecord> hikes = new List<HikeRecord>();
}

public static class HikeStorage
{
    private static string saveDir = Path.Combine(Application.persistentDataPath, "Hikes");
    private static string libraryPath = Path.Combine(Application.persistentDataPath, "hike_library.json");

    static HikeStorage()
    {
        if (!Directory.Exists(saveDir))
        {
            Directory.CreateDirectory(saveDir);
        }
    }

    public static void SaveHike(HikeRecord hike)
    {
        string path = HikePath(hike.id);
        File.WriteAllText(path, JsonUtility.ToJson(hike, true));

        HikeLibrary lib = LoadLibrary();
        lib.hikes.RemoveAll(h => h.id == hike.id);

        HikeRecord summary = new HikeRecord();
        summary.id = hike.id;
        summary.name = hike.name;
        summary.notes = hike.notes;
        summary.startTimeMs = hike.startTimeMs;
        summary.endTimeMs = hike.endTimeMs;
        summary.totalDistanceMeters = hike.totalDistanceMeters;
        summary.elevationGainMeters = hike.elevationGainMeters;
        summary.elevationLossMeters = hike.elevationLossMeters;
        summary.snapshotPath = hike.snapshotPath;

        lib.hikes.Add(summary);
        lib.hikes.Sort((a, b) => b.startTimeMs.CompareTo(a.startTimeMs));
        SaveLibrary(lib);
    }

    public static HikeRecord LoadHike(string id)
    {
        string path = HikePath(id);
        if (!File.Exists(path))
        {
            return null;
        }
        return JsonUtility.FromJson<HikeRecord>(File.ReadAllText(path));
    }

    public static void UpdateSnapshotPath(string hikeId, string snapshotPath)
    {
        HikeRecord hike = LoadHike(hikeId);
        if (hike == null)
        {
            return;
        }
        hike.snapshotPath = snapshotPath;
        File.WriteAllText(HikePath(hikeId), JsonUtility.ToJson(hike, true));

        HikeLibrary lib = LoadLibrary();
        HikeRecord entry = lib.hikes.Find(h => h.id == hikeId);
        if (entry != null)
        {
            entry.snapshotPath = snapshotPath;
            SaveLibrary(lib);
        }
    }

    public static void DeleteHike(string id)
    {
        HikeRecord hike = LoadHike(id);
        if (hike != null && !string.IsNullOrEmpty(hike.snapshotPath) && File.Exists(hike.snapshotPath))
        {
            File.Delete(hike.snapshotPath);
        }

        string path = HikePath(id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        HikeLibrary lib = LoadLibrary();
        lib.hikes.RemoveAll(h => h.id == id);
        SaveLibrary(lib);
    }

    public static HikeLibrary LoadLibrary()
    {
        if (!File.Exists(libraryPath))
        {
            return new HikeLibrary();
        }
        return JsonUtility.FromJson<HikeLibrary>(File.ReadAllText(libraryPath));
    }

    private static void SaveLibrary(HikeLibrary lib)
    {
        File.WriteAllText(libraryPath, JsonUtility.ToJson(lib, true));
    }

    private static string HikePath(string id)
    {
        return Path.Combine(saveDir, "hike_" + id + ".json");
    }

    public static string SnapshotPath(string id)
    {
        return Path.Combine(saveDir, "snapshot_" + id + ".png");
    }
}