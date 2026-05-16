using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MapController : MonoBehaviour
{
    public static MapController Instance { get; private set; }

    public RectTransform tileRoot;
    public float tileDisplaySize = 256f;
    public int gridSize = 5;
    public int zoomLevel = 16;
    public RawImage routeOverlay;
    public Color routeColor = new Color(0.9f, 0.2f, 0.2f, 1f);
    public int routeLineWidth = 6;
    public RectTransform playerDot;
    public string tileUrlTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
    public string userAgent = "HikingApp/1.0 (hiking tracker)";
    private double centerTileX;
    private double centerTileY;
    private double centerLat;
    private double centerLon;
    private int currentZoom;
    private bool hasCenter;
    private Dictionary<string, Texture2D> tileCache = new Dictionary<string, Texture2D>();
    private Dictionary<string, RawImage> tileImages = new Dictionary<string, RawImage>();
    private List<GPSPoint> routePoints = new List<GPSPoint>();
    private Texture2D routeTex;
    private int texWidth;
    private int texHeight;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        currentZoom = zoomLevel;
    }

    void Start()
    {
        if (tileRoot == null)
        {

        }

        SetupRouteTexture();

        if (GPSManager.Instance != null)
        {
            GPSManager.Instance.OnLocationUpdated += OnLocationUpdated;
        }

        if (HikeTracker.Instance != null)
        {
            HikeTracker.Instance.OnHikeStarted += _ => ClearRoute();
            HikeTracker.Instance.OnWaypointAdded += OnWaypointAdded;
            HikeTracker.Instance.OnHikeSaved += _ => ClearRoute();
            HikeTracker.Instance.OnHikeDiscarded += () => ClearRoute();
        }
    }

    void OnDestroy()
    {
        if (GPSManager.Instance != null)
        {
            GPSManager.Instance.OnLocationUpdated -= OnLocationUpdated;
        }
    }

    public void ZoomIn()
    {
        if (currentZoom < 19)
        {
            currentZoom++;
            RefreshTiles();
        }
    }

    public void ZoomOut()
    {
        if (currentZoom > 1)
        {
            currentZoom--;
            RefreshTiles();
        }
    }

    public void Refresh()
    {
        RefreshTiles();
    }

    public void DisplaySavedHike(HikeRecord hike)
    {
        ClearRoute();
        routePoints.AddRange(hike.waypoints);
        if (hike.waypoints.Count > 0)
        {
            SetCenter(hike.waypoints[0].latitude, hike.waypoints[0].longitude);
        }
        RedrawRoute();
    }

    private void SetupRouteTexture()
    {
        if (routeOverlay == null)
        {
            return;
        }

        texWidth = Mathf.RoundToInt(tileRoot.rect.width);
        texHeight = Mathf.RoundToInt(tileRoot.rect.height);

        if (texWidth <= 0)
        {
            texWidth = Screen.width;
        }
        if (texHeight <= 0)
        {
            texHeight = Screen.height;
        }

        routeTex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
        ClearTexture();

        routeOverlay.texture = routeTex;
        routeOverlay.rectTransform.SetAsLastSibling();
    }

    private void ClearTexture()
    {
        if (routeTex == null)
        {
            return;
        }

        Color32[] pixels = new Color32[texWidth * texHeight];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(0, 0, 0, 0);
        }

        routeTex.SetPixels32(pixels);
        routeTex.Apply();
    }

    private void OnLocationUpdated(double lat, double lon, float alt)
    {
        SetCenter(lat, lon);
        MovePlayerDot(lat, lon);
    }

    private void OnWaypointAdded(GPSPoint pt)
    {
        routePoints.Add(pt);
        RedrawRoute();
    }

    private void ClearRoute()
    {
        routePoints.Clear();
        ClearTexture();
    }

    private void SetCenter(double lat, double lon)
    {
        centerLat = lat;
        centerLon = lon;
        hasCenter = true;
        LatLonToTile(lat, lon, currentZoom, out centerTileX, out centerTileY);
        RefreshTiles();
    }

    private void RefreshTiles()
    {
        if (!hasCenter || tileRoot == null)
        {
            return;
        }

        int centerTX = (int)Math.Floor(centerTileX);
        int centerTY = (int)Math.Floor(centerTileY);
        int half = gridSize / 2;

        float subX = (float)(centerTileX - Math.Floor(centerTileX));
        float subY = (float)(centerTileY - Math.Floor(centerTileY));
        float offX = -(subX - 0.5f) * tileDisplaySize;
        float offY = (subY - 0.5f) * tileDisplaySize;

        for (int dy = -half; dy <= half; dy++)
        {
            for (int dx = -half; dx <= half; dx++)
            {
                int mx = 1 << currentZoom;
                int tx = ((centerTX + dx) % mx + mx) % mx;
                int ty = centerTY + dy;

                if (ty < 0 || ty >= mx)
                {
                    continue;
                }

                string key = currentZoom + "/" + tx + "/" + ty;
                RawImage img = GetOrCreateTileImage(key);

                img.rectTransform.anchoredPosition = new Vector2(dx * tileDisplaySize + offX, -dy * tileDisplaySize + offY);
                img.rectTransform.sizeDelta = new Vector2(tileDisplaySize, tileDisplaySize);

                Texture2D cached;
                if (tileCache.TryGetValue(key, out cached))
                {
                    img.texture = cached;
                }
                else
                {
                    StartCoroutine(DownloadTile(tx, ty, currentZoom, key, img));
                }
            }
        }

        RedrawRoute();

        if (hasCenter)
        {
            MovePlayerDot(centerLat, centerLon);
        }
    }

    private RawImage GetOrCreateTileImage(string key)
    {
        RawImage img;
        if (tileImages.TryGetValue(key, out img) && img != null)
        {
            return img;
        }

        GameObject go = new GameObject("Tile_" + key);
        go.transform.SetParent(tileRoot, false);
        RawImage ri = go.AddComponent<RawImage>();
        ri.color = new Color(0.82f, 0.82f, 0.82f);
        ri.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        ri.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        ri.rectTransform.pivot = new Vector2(0.5f, 0.5f);

        if (routeOverlay != null)
        {
            routeOverlay.rectTransform.SetAsLastSibling();
        }
        if (playerDot != null)
        {
            playerDot.SetAsLastSibling();
        }

        tileImages[key] = ri;
        return ri;
    }

    private IEnumerator DownloadTile(int tx, int ty, int zoom, string key, RawImage img)
    {
        string url = tileUrlTemplate.Replace("{z}", zoom.ToString()).Replace("{x}", tx.ToString()).Replace("{y}", ty.ToString());

        UnityEngine.Networking.UnityWebRequest req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url);
        req.SetRequestHeader("User-Agent", userAgent);
        yield return req.SendWebRequest();

        if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            Texture2D tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
            tex.wrapMode = TextureWrapMode.Clamp;
            tileCache[key] = tex;
            if (img != null)
            {
                img.texture = tex;
            }
        }
    }

    private void MovePlayerDot(double lat, double lon)
    {
        if (playerDot == null || !hasCenter)
        {
            return;
        }
        playerDot.anchoredPosition = GpsToUIPos(lat, lon);
        playerDot.SetAsLastSibling();
    }

    private void RedrawRoute()
    {
        if (routeTex == null || routePoints.Count < 2 || !hasCenter)
        {
            return;
        }

        ClearTexture();

        List<Vector2Int> pixels = new List<Vector2Int>();

        for (int i = 0; i < routePoints.Count; i++)
        {
            GPSPoint pt = routePoints[i];
            Vector2 ui = GpsToUIPos(pt.latitude, pt.longitude);
            int px = Mathf.RoundToInt(ui.x + texWidth * 0.5f);
            int py = Mathf.RoundToInt(ui.y + texHeight * 0.5f);
            pixels.Add(new Vector2Int(px, py));
        }

        for (int i = 0; i < pixels.Count - 1; i++)
        {
            DrawLine(pixels[i], pixels[i + 1]);
        }

        routeTex.Apply();
    }

    private void DrawLine(Vector2Int a, Vector2Int b)
    {
        int half = routeLineWidth / 2;
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
                    SetPixelSafe(x + ox, y + oy, routeColor);
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

    private void SetPixelSafe(int x, int y, Color c)
    {
        if (x >= 0 && x < texWidth && y >= 0 && y < texHeight)
        {
            routeTex.SetPixel(x, y, c);
        }
    }

    private Vector2 GpsToUIPos(double lat, double lon)
    {
        double tx;
        double ty;
        LatLonToTile(lat, lon, currentZoom, out tx, out ty);
        return new Vector2(
            (float)(tx - centerTileX) * tileDisplaySize,
            -(float)(ty - centerTileY) * tileDisplaySize
        );
    }

    public static void LatLonToTile(double lat, double lon, int zoom, out double tileX, out double tileY)
    {
        double n = Math.Pow(2, zoom);
        double rLat = lat * Math.PI / 180.0;
        tileX = (lon + 180.0) / 360.0 * n;
        tileY = (1.0 - Math.Log(Math.Tan(rLat) + 1.0 / Math.Cos(rLat)) / Math.PI) / 2.0 * n;
    }
}