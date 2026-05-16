using UnityEngine;
using UnityEngine.UI;

public class BackgroundServiceBridge : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button stopButton;

    private AndroidJavaClass unityClass;
    private AndroidJavaObject unityActivity;
    private AndroidJavaClass pluginClass;

    private void Awake()
    {
        unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        unityActivity = unityClass.GetStatic<AndroidJavaObject>("currentActivity");
        pluginClass = new AndroidJavaClass("com.kdg.toast.plugin.Bridge");
        pluginClass.CallStatic("ReceiveActivityInstance", unityActivity);

        startButton.onClick.AddListener(StartService);
        stopButton.onClick.AddListener(StopService);
    }

    public void StartService()
    {
        if (pluginClass == null)
        {
            return;
        }
        pluginClass.CallStatic("StartService");
    }

    public void StopService()
    {
        if (pluginClass == null)
        {
            return;
        }
        pluginClass.CallStatic("StopService");
    }

    private void OnDestroy()
    {
        if (unityActivity != null)
        {
            unityActivity.Dispose();
        }
        if (unityClass != null)
        {
            unityClass.Dispose();
        }
        if (pluginClass != null)
        {
            pluginClass.Dispose();
        }
    }
}