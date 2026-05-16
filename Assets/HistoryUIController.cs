using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class HistoryUIController : MonoBehaviour
{
    public RectTransform scrollContent;
    public GameObject hikeCardPrefab;

    public GameObject confirmPanel;
    public Button btnConfirmYes;
    public Button btnConfirmNo;

    private List<HikeRecord> hikes = new List<HikeRecord>();
    private HikeRecord pendingDelete;

    void Start()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
        if (btnConfirmYes != null)
        {
            btnConfirmYes.onClick.AddListener(ConfirmDelete);
        }
        if (btnConfirmNo != null)
        {
            btnConfirmNo.onClick.AddListener(CancelDelete);
        }

        Refresh();
    }

    void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (scrollContent == null || hikeCardPrefab == null)
        {
            return;
        }

        foreach (Transform child in scrollContent)
        {
            Destroy(child.gameObject);
        }
        hikes = HikeStorage.LoadLibrary().hikes;

        if (hikes == null || hikes.Count == 0)
        {
            return;
        }

        for (int i = 0; i < hikes.Count; i++)
        {
            HikeRecord hike = hikes[i];
            GameObject go = Instantiate(hikeCardPrefab, scrollContent);
            HikeCardUI card = go.GetComponent<HikeCardUI>();
            HikeRecord h = hike;

            if (card != null)
            {
                card.Populate(
                    h,
                    onTap: () => OnCardTapped(h),
                    onDelete: () => OnDeleteTapped(h),
                    onSaveToGallery: () => SaveToGallery(h)
                );
            }
        }
    }

    private void OnCardTapped(HikeRecord hike)
    {
    }

    private void OnDeleteTapped(HikeRecord hike)
    {
        pendingDelete = hike;
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
        }
    }

    private void ConfirmDelete()
    {
        if (pendingDelete == null)
        {
            return;
        }
        HikeStorage.DeleteHike(pendingDelete.id);
        pendingDelete = null;
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
        Refresh();
    }

    private void CancelDelete()
    {
        pendingDelete = null;
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }

    private void SaveToGallery(HikeRecord hike)
    {
        if (string.IsNullOrEmpty(hike.snapshotPath) || !File.Exists(hike.snapshotPath))
        {
            return;
        }

        AndroidJavaClass env = new AndroidJavaClass("android.os.Environment");
        AndroidJavaObject dirObj = env.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", "Pictures");
        string picDir = dirObj.Call<string>("getAbsolutePath");
        string hikeAppDir = Path.Combine(picDir, "HikeApp");

        if (!Directory.Exists(hikeAppDir))
        {
            Directory.CreateDirectory(hikeAppDir);
        }

        string safeName = string.IsNullOrEmpty(hike.name) ? "hike" : hike.name;

        char[] invalidChars = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidChars.Length; i++)
        {
            safeName = safeName.Replace(invalidChars[i], '_');
        }

        safeName = safeName.Replace(":", "_").Replace(",", "").Replace(" ", "_");

        string dest = Path.Combine(hikeAppDir, safeName + ".png");
        File.Copy(hike.snapshotPath, dest, true);

        AndroidJavaObject scanIntent = new AndroidJavaObject("android.content.Intent", "android.intent.action.MEDIA_SCANNER_SCAN_FILE");
        AndroidJavaObject uri = new AndroidJavaClass("android.net.Uri").CallStatic<AndroidJavaObject>("parse", "file://" + dest);
        scanIntent.Call<AndroidJavaObject>("setData", uri);
        new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity").Call("sendBroadcast", scanIntent);
    }
}