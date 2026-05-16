using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RecordingUIController : MonoBehaviour
{
    public GameObject panelMap;
    public GameObject panelSaveDialog;

    public Button btnStart;
    public Button btnPause;
    public Button btnResume;
    public Button btnStop;

    public TMP_InputField inputHikeName;
    public Button btnConfirmSave;
    public Button btnCancelSave;
    public Button btnDiscard;

    void Start()
    {
        btnStart.onClick.AddListener(OnStartClicked);
        btnPause.onClick.AddListener(OnPauseClicked);
        btnResume.onClick.AddListener(OnResumeClicked);
        btnStop.onClick.AddListener(OnStopClicked);
        btnConfirmSave.onClick.AddListener(OnConfirmSaveClicked);
        btnCancelSave.onClick.AddListener(OnCancelSaveClicked);
        btnDiscard.onClick.AddListener(OnDiscardClicked);
        HikeTracker.Instance.OnHikeStarted += _ => RefreshButtonStates();
        HikeTracker.Instance.OnHikeSaved += _ => RefreshButtonStates();
        HikeTracker.Instance.OnHikeDiscarded += RefreshButtonStates;
        RefreshButtonStates();
        InvokeRepeating(nameof(UpdateStats), 1f, 1f);
    }

    private void OnStartClicked()
    {
        GPSManager.Instance.StartGPS();
        HikeTracker.Instance.StartHike();
        SetPanel(panelSaveDialog, false);
    }

    private void OnPauseClicked()
    {
        HikeTracker.Instance.PauseHike();
        RefreshButtonStates();
    }

    private void OnResumeClicked()
    {
        HikeTracker.Instance.ResumeHike();
        RefreshButtonStates();
    }

    private void OnStopClicked()
    {
        HikeTracker.Instance.PauseHike();
        if (HikeTracker.Instance.ActiveHike != null)
        {
            inputHikeName.text = HikeTracker.Instance.ActiveHike.name;
        }
        SetPanel(panelSaveDialog, true);
    }

    private void OnConfirmSaveClicked()
    {
        HikeTracker.Instance.SaveHike(inputHikeName.text);
        SetPanel(panelSaveDialog, false);
        RefreshButtonStates();
    }

    private void OnCancelSaveClicked()
    {
        HikeTracker.Instance.ResumeHike();
        SetPanel(panelSaveDialog, false);
        RefreshButtonStates();
    }

    private void OnDiscardClicked()
    {
        HikeTracker.Instance.DiscardHike();
        SetPanel(panelSaveDialog, false);
        RefreshButtonStates();
    }

    private void UpdateStats()
    {
    }

    private void RefreshButtonStates(HikeRecord ignored)
    {
        RefreshButtonStates();
    }

    private void RefreshButtonStates()
    {
        HikeTracker.TrackerState state = HikeTracker.Instance.State;

        bool idle = state == HikeTracker.TrackerState.Idle;
        bool recording = state == HikeTracker.TrackerState.Recording;
        bool paused = state == HikeTracker.TrackerState.Paused;

        if (btnStart != null)
        {
            btnStart.gameObject.SetActive(idle);
        }
        if (btnPause != null)
        {
            btnPause.gameObject.SetActive(recording);
        }
        if (btnResume != null)
        {
            btnResume.gameObject.SetActive(paused);
        }
        if (btnStop != null)
        {
            btnStop.gameObject.SetActive(recording || paused);
        }
    }


    private static void SetPanel(GameObject panel, bool visible)
    {
        if (panel != null)
        {
            panel.SetActive(visible);
        }
    }
}