using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HikeCardUI : MonoBehaviour
{
    public RawImage snapshotImage;
    public TextMeshProUGUI labelName;
    public Button btnDelete;
    public Button btnSaveToGallery;

    private Button button;
    private Texture2D tex;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
        }
    }

    void OnDestroy()
    {
        if (tex != null)
        {
            Destroy(tex);
        }
    }

    public void Populate(HikeRecord hike, Action onTap, Action onDelete, Action onSaveToGallery)
    {
        labelName.text = hike.name;

        bool hasSnapshot = !string.IsNullOrEmpty(hike.snapshotPath) && File.Exists(hike.snapshotPath);

        if (hasSnapshot)
        {
            if (tex != null)
            {
                Destroy(tex);
            }
            tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(hike.snapshotPath));
            snapshotImage.texture = tex;
            snapshotImage.color = Color.white;
        }
        else
        {
            snapshotImage.texture = null;
            snapshotImage.color = new Color(0.8f, 0.8f, 0.8f);
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onTap?.Invoke());

        btnDelete.onClick.RemoveAllListeners();
        btnDelete.onClick.AddListener(() => onDelete?.Invoke());

        btnSaveToGallery.onClick.RemoveAllListeners();
        btnSaveToGallery.onClick.AddListener(() => onSaveToGallery?.Invoke());
        btnSaveToGallery.gameObject.SetActive(hasSnapshot);
    }
}