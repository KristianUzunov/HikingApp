using System.Collections;
using UnityEngine;
using UnityEngine.Android;

public class PermissionManager : MonoBehaviour
{
    public static PermissionManager Instance { get; private set; }

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
        StartCoroutine(RequestAll());
    }

    private IEnumerator RequestAll()
    {
#if UNITY_ANDROID
        yield return RequestPermission(Permission.FineLocation);
        yield return RequestPermission(Permission.CoarseLocation);

        string bgPerm = "android.permission.ACCESS_BACKGROUND_LOCATION";
        while (!Permission.HasUserAuthorizedPermission(bgPerm))
        {
            yield return RequestPermission(bgPerm);
        }

        yield return RequestPermission(Permission.ExternalStorageWrite);
        yield return RequestPermission("android.permission.READ_MEDIA_IMAGES");

        Input.compass.enabled = true;
        Input.gyro.enabled = true;
        Input.location.Start();
#endif
    }

#if UNITY_ANDROID
    private IEnumerator RequestPermission(string permission)
    {
        if (Permission.HasUserAuthorizedPermission(permission))
        {
            yield break;
        }

        bool decided = false;
        PermissionCallbacks callbacks = new PermissionCallbacks();
        callbacks.PermissionGranted += _ => decided = true;
        callbacks.PermissionDenied += _ => decided = true;

        Permission.RequestUserPermission(permission, callbacks);

        float timeout = 30f;
        while (!decided && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
    }
#endif
}

public class ApplicationFocusWatcher : MonoBehaviour
{
    public static System.Action OnFocusGained;

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            if (OnFocusGained != null)
            {
                OnFocusGained.Invoke();
            }
        }
    }
}