using UnityEngine;
using UnityEngine.UI;

public class AppManager : MonoBehaviour
{
    public static AppManager Instance { get; private set; }

    public enum Screen { Map, History }

    public GameObject screenMap;
    public GameObject screenHistory;
    public Button btnNavMap;
    public Button btnNavHistory;

    private Screen current;

    void Awake()
    {
        Application.runInBackground = true;
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
        btnNavMap.onClick.AddListener(() => NavigateTo(Screen.Map));
        btnNavHistory.onClick.AddListener(() => NavigateTo(Screen.History));

        NavigateTo(Screen.Map);
        GPSManager.Instance.StartGPS();
    }

    public void NavigateTo(Screen screen)
    {
        current = screen;

        screenMap.SetActive(screen == Screen.Map);
        screenHistory.SetActive(screen == Screen.History);

        if (screen == Screen.History)
        {
            HistoryUIController hist = FindFirstObjectByType<HistoryUIController>();
            hist.Refresh();
        }

        UpdateNavBarHighlight();
    }

    void UpdateNavBarHighlight()
    {
        Color activeColor = new Color(0.18f, 0.72f, 0.38f);
        Color inactiveColor = Color.white;

        if (current == Screen.Map)
        {
            btnNavMap.image.color = activeColor;
            btnNavHistory.image.color = inactiveColor;
        }
        else
        {
            btnNavMap.image.color = inactiveColor;
            btnNavHistory.image.color = activeColor;
        }
    }
}