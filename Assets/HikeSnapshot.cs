using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class HikeSnapshot : MonoBehaviour
{
    public static HikeSnapshot Instance { get; private set; }

    public int imageSize = 512;
    public int routeWidth = 6;
    public Color routeColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    public Color bgColor = new Color(0.93f, 0.93f, 0.93f, 1f);

    public string tileUrlTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
    public string userAgent = "HikingApp/1.0 (personal-use hiking tracker)";
    public int snapshotZoom = 14;

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

    void Start()
    {
        if (HikeTracker.Instance != null)
        {
            HikeTracker.Instance.OnHikeSaved += hike => StartCoroutine(GenerateSnapshot(hike));
        }
    }

    public void RegenerateSnapshot(HikeRecord hike)
    {
        StartCoroutine(GenerateSnapshot(hike));
    }

    private IEnumerator GenerateSnapshot(HikeRecord hike)
    {
        if (hike == null || hike.waypoints == null || hike.waypoints.Count < 2)
        {
            yield break;
        }

        double minLat = double.MaxValue;
        double maxLat = double.MinValue;
        double minLon = double.MaxValue;
        double maxLon = double.MinValue;

        for (int i = 0; i < hike.waypoints.Count; i++)
        {
            GPSPoint pt = hike.waypoints[i];
            if (pt.latitude < minLat)
            {
                minLat = pt.latitude;
            }
            if (pt.latitude > maxLat)
            {
                maxLat = pt.latitude;
            }
            if (pt.longitude < minLon)
            {
                minLon = pt.longitude;
            }
            if (pt.longitude > maxLon)
            {
                maxLon = pt.longitude;
            }
        }

        double centerLat = (minLat + maxLat) / 2.0;
        double centerLon = (minLon + maxLon) / 2.0;

        double centerTX;
        double centerTY;
        MapController.LatLonToTile(centerLat, centerLon, snapshotZoom, out centerTX, out centerTY);

        int gridHalf = 1;
        int tilePixels = 256;
        int gridSize = gridHalf * 2 + 1;
        int canvasSize = gridSize * tilePixels;

        Texture2D canvas = new Texture2D(canvasSize, canvasSize, TextureFormat.RGBA32, false);
        FillSolid(canvas, bgColor);

        int centerTileX = (int)Math.Floor(centerTX);
        int centerTileY = (int)Math.Floor(centerTY);

        for (int dy = -gridHalf; dy <= gridHalf; dy++)
        {
            for (int dx = -gridHalf; dx <= gridHalf; dx++)
            {
                int mx = 1 << snapshotZoom;
                int tx = ((centerTileX + dx) % mx + mx) % mx;
                int ty = centerTileY + dy;

                if (ty < 0 || ty >= mx)
                {
                    continue;
                }

                string url = tileUrlTemplate.Replace("{z}", snapshotZoom.ToString()).Replace("{x}", tx.ToString()).Replace("{y}", ty.ToString());

                UnityWebRequest req = UnityWebRequestTexture.GetTexture(url);
                req.SetRequestHeader("User-Agent", userAgent);
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    Texture2D tileTex = DownloadHandlerTexture.GetContent(req);

                    int px = (dx + gridHalf) * tilePixels;
                    int py = (gridHalf - dy) * tilePixels;

                    BlitTile(canvas, tileTex, px, py, tilePixels);
                    Destroy(tileTex);
                }
            }
        }

        List<Vector2Int> routePixels = new List<Vector2Int>();

        for (int i = 0; i < hike.waypoints.Count; i++)
        {
            GPSPoint pt = hike.waypoints[i];
            double ptTX;
            double ptTY;
            MapController.LatLonToTile(pt.latitude, pt.longitude, snapshotZoom, out ptTX, out ptTY);

            int px = (int)((ptTX - (centerTX - gridHalf)) * tilePixels);
            int py = canvasSize - (int)((ptTY - (centerTY - gridHalf)) * tilePixels);
            routePixels.Add(new Vector2Int(px, py));
        }

        for (int i = 0; i < routePixels.Count - 1; i++)
        {
            DrawLine(canvas, routePixels[i], routePixels[i + 1], routeColor, routeWidth);
        }

        canvas.Apply();

        Texture2D final = ScaleTexture(canvas, imageSize, imageSize);
        Destroy(canvas);

        byte[] png = final.EncodeToPNG();
        Destroy(final);

        string path = HikeStorage.SnapshotPath(hike.id);
        File.WriteAllBytes(path, png);

        HikeStorage.UpdateSnapshotPath(hike.id, path);
    }

    private static void FillSolid(Texture2D tex, Color c)
    {
        Color[] pixels = new Color[tex.width * tex.height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = c;
        }
        tex.SetPixels(pixels);
    }

    private static void BlitTile(Texture2D dst, Texture2D src, int ox, int oy, int size)
    {
        int w = Mathf.Min(src.width, size);
        int h = Mathf.Min(src.height, size);
        Color[] pixels = src.GetPixels(0, 0, w, h);
        dst.SetPixels(ox, oy, w, h, pixels);
    }

    private static void DrawLine(Texture2D tex, Vector2Int a, Vector2Int b, Color c, int width)
    {
        int half = width / 2;
        int dx = Math.Abs(b.x - a.x);
        int dy = Math.Abs(b.y - a.y);
        int sx = 1;
        int sy = 1;

        if (a.x >= b.x)
        {
            sx = -1;
        }
        if (a.y >= b.y)
        {
            sy = -1;
        }

        int err = dx - dy;
        int x = a.x;
        int y = a.y;

        while (true)
        {
            for (int ox = -half; ox <= half; ox++)
            {
                for (int oy = -half; oy <= half; oy++)
                {
                    SetPixelSafe(tex, x + ox, y + oy, c);
                }
            }

            if (x == b.x && y == b.y)
            {
                break;
            }

            int e2 = 2 * err;

            if (e2 > -dy)
            {
                err -= dy;
                x += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

    private static void SetPixelSafe(Texture2D tex, int x, int y, Color c)
    {
        if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
        {
            tex.SetPixel(x, y, c);
        }
    }

    private static Texture2D ScaleTexture(Texture2D src, int w, int h)
    {
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 0);
        Graphics.Blit(src, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        result.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }
}