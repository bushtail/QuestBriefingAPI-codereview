using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Comfort.Common;
using EFT.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Manimal.QuestBriefingAPI.Briefings;

// Lives on the vanilla description panel, not on the hideable controls. This lets
// F12 changes take effect immediately even while the controls are hidden.
public sealed class BriefingPlayer : MonoBehaviour
{
    private static BriefingPlayer _active;
    private readonly BriefingSelection _selection = new();
    private readonly BriefingFocusGate _focus = new();
    private readonly List<CanvasGroup> _canvasGroups = [];
    private bool _paused;
    private TMP_Text _description;
    private RectTransform _bar;
    private LayoutElement _space;
    private AudioSource _source;
    private GameObject _audioObject;
    private GameObject _cueObject;
    private AudioSource _cueSource;
    private AudioClip _openingCue, _closingCue;
    private bool _starting;
    private double _voiceStartAt;
    private AudioHighPassFilter _highPass;
    private AudioLowPassFilter _lowPass;
    private AudioDistortionFilter _distortion;
    private bool _radioFiltered;
    private float _distortionLevel = -1;
    private AudioClip _clip;
    private UnityWebRequest _request;
    private Coroutine _loading;
    private Button _play, _stop, _replay;
    private TMP_Text _status, _time;
    private Slider _progress;
    private string _questId;
    private bool _unlocked, _enabled, _autoPlay, _wasPlaying;
    private bool _hasLayoutSnapshot;
    private Vector2 _originalSize;
    private Vector2 _originalPosition;
    private TextAlignmentOptions _originalAlignment;
    private float _lastTextHeight = -1;
    private readonly Color _ink = new Color32(197, 195, 178, 255);

    private BriefingRecording _recording;
    private bool UseRadioCues => BriefingSettings.RadioCues.Value && _recording?.RadioCues != false;

    public static void StopActive()
    {
        if (_active) _active.Clear();
    }

    public static void SuspendActive()
    {
        if (_active) _active.SuspendPlayback();
    }

    public void Initialize(TMP_Text description)
    {
        _description = description;
        
        _audioObject = new GameObject("QuestBriefingAudio");
        _audioObject.transform.SetParent(transform, false);
        
        _source = _audioObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0;
        _source.volume = BriefingSettings.Volume.Value;
        
        _cueObject = new GameObject("QuestBriefingRadioCues");
        _cueObject.transform.SetParent(transform, false);
        
        _cueSource = _cueObject.AddComponent<AudioSource>();
        _cueSource.playOnAwake = false;
        _cueSource.loop = false;
        _cueSource.spatialBlend = 0;
        
        _openingCue = CreateCue(true);
        _closingCue = CreateCue(false);
        
        _highPass = _audioObject.AddComponent<AudioHighPassFilter>();
        _highPass.cutoffFrequency = 300f;
        _highPass.highpassResonanceQ = 1f;
        
        _distortion = _audioObject.AddComponent<AudioDistortionFilter>();
        
        _lowPass = _audioObject.AddComponent<AudioLowPassFilter>();
        _lowPass.cutoffFrequency = 3400f;
        _lowPass.lowpassResonanceQ = 1f;
        
        ApplyRadioFilter(true);
        
        _enabled = BriefingSettings.Enabled.Value;
        _autoPlay = BriefingSettings.AutoPlay.Value;
        
        BuildControls();
        ShowControls(false);
    }

    internal void Select(string questId, bool unlocked, BriefingRecording recording)
    {
        if (_active && _active != this) { _active.Clear(); }
        _active = this;
        var changed = _questId != questId || _unlocked != unlocked || _recording != recording;
        _recording = recording;
        _questId = questId;
        _unlocked = unlocked;
        
        if (!changed) { return; }
        
        ResetAudio();
        _selection.Clear();
        if (!_enabled || !unlocked) 
        { 
            ShowControls(false);
            return; 
        }
        
        _selection.Select(questId, _autoPlay);
        ShowControls(true);
        
        LoadRecording();
    }

    private void Update()
    {
        if (!_source) return;
        _source.volume = BriefingSettings.Volume.Value;
        _cueSource.volume = BriefingSettings.Volume.Value * 0.55f;
        if (!UseRadioCues) _cueSource.Stop();
        ApplyRadioFilter();
        var isEnabled = BriefingSettings.Enabled.Value;
        var autoPlay = BriefingSettings.AutoPlay.Value;
        if (_autoPlay && !autoPlay) StopPlayback(false);
        _autoPlay = autoPlay;
        if (_enabled != isEnabled)
        {
            _enabled = isEnabled;
            ResetAudio();
            _selection.Clear();
            ShowControls(isEnabled && _questId != null && _unlocked);
            if (isEnabled && _questId != null && _unlocked)
            {
                // Re-enabling the feature doesn't unexpectedly start talking.
                _selection.Select(_questId, false);
                LoadRecording();
            }
        }
        if (!_bar || !_bar.gameObject.activeSelf || !_clip) return;
        if (!_focus.Update(IsForeground(), Time.unscaledTime))
        {
            PauseForForeground();
            return;
        }
        if (_paused && _selection.WantsPlayback)
        {
            _paused = false;
            _source.UnPause();
            _wasPlaying = true;
            _status.text = "Playing";
        }
        else if (_selection.WantsPlayback && !_starting && !_wasPlaying && !_source.isPlaying)
        {
            StartPlayback();
        }
        if (_starting && (AudioSettings.dspTime >= _voiceStartAt || !UseRadioCues))
        {
            _starting = false;
            _source.Play();
            _wasPlaying = true;
            _status.text = "Playing";
        }
        if (_source.isPlaying)
        {
            _wasPlaying = true;
            _progress.SetValueWithoutNotify(_source.time / _clip.length);
            SetTime(_source.time);
        }
        else if (_wasPlaying)
        {
            _wasPlaying = false;
            _selection.Stop();
            _progress.SetValueWithoutNotify(1);
            SetTime(_clip.length);
            _status.text = "Finished";
            PlayCue(_closingCue);
        }
    }

    private bool IsForeground()
    {
        if (!Application.isFocused || !isActiveAndEnabled || !_description
            || !_description.gameObject.activeInHierarchy || !_bar.gameObject.activeInHierarchy
            || BriefingForeground.HasForegroundWindow()) return false;
        
        _description.GetComponentsInParent(false, _canvasGroups);
        
        foreach (var group in _canvasGroups)
        {
            if (group.enabled && (group.alpha <= 0.01f || !group.interactable)) { return false; }
        }
        
        return true;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) SuspendPlayback();
    }

    private void SuspendPlayback()
    {
        _focus.Reset();
        PauseForForeground();
    }

    private void PauseForForeground()
    {
        if (!_source) { return; }
        
        if (_source.isPlaying || _wasPlaying)
        {
            _source.Pause();
            _paused = true;
            _wasPlaying = false;
        }
        _starting = false;
        
        if (_cueSource) _cueSource.Stop();
        if (_clip && _selection.WantsPlayback)
        {
            _status.text = _paused ? "Paused" : "Waiting for quest view...";
        }
    }

    private void ApplyRadioFilter(bool force = false)
    {
        var isEnabled = BriefingSettings.RadioFilter.Value && _recording?.RadioFilter != false;
        var distortion = BriefingSettings.RadioDistortion.Value;
        if (!force && _radioFiltered == isEnabled && Mathf.Approximately(_distortionLevel, distortion)) { return; }
        
        _radioFiltered = isEnabled;
        _distortionLevel = distortion;
        _highPass.enabled = isEnabled;
        _lowPass.enabled = isEnabled;
        _distortion.distortionLevel = distortion;
        _distortion.enabled = isEnabled && distortion > 0;
    }

    private void LateUpdate()
    {
        if (!_bar || !_bar.gameObject.activeSelf) return;
        
        var width = _description.rectTransform.rect.width;
        if (width <= 1) return;
        
        var height = _description.GetPreferredValues(_description.text, width, Mathf.Infinity).y;
        _bar.anchoredPosition = new Vector2(0, -height - 14);
        if (Mathf.Abs(height - _lastTextHeight) < 0.5f) return;
        _lastTextHeight = height;
        _space.minHeight = _space.preferredHeight = height + 86;
        var fitter = _description.GetComponent<ContentSizeFitter>();
        var parentLayout = _description.transform.parent.GetComponent<LayoutGroup>();
        if ((!fitter || !fitter.enabled || fitter.verticalFit == ContentSizeFitter.FitMode.Unconstrained)
            && (!parentLayout || !parentLayout.enabled))
        {
            var rect = _description.rectTransform;
            var oldHeight = rect.rect.height;
            var baselineHeight = oldHeight - rect.sizeDelta.y + _originalSize.y;
            var newHeight = Mathf.Max(baselineHeight, _space.preferredHeight);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newHeight);

            rect.anchoredPosition -= new Vector2(0, (newHeight - oldHeight) * (1 - rect.pivot.y));
        }
        LayoutRebuilder.MarkLayoutForRebuild(_description.rectTransform);
    }

    private void LoadRecording()
    {
        if (_loading != null || !_selection.IsCurrent(_selection.Revision)) return;
        try
        {
            var path = _recording?.ResolvePath();
            if (path == null)
            {
                _status.text = "Recording not installed";
                _time.text = "--:-- / --:--";
                SetButtons(true, false, false); // Play retries after a file is added.
                return;
            }
            _status.text = "Loading...";
            _time.text = "--:-- / --:--";
            SetButtons(false, true, false);
            _loading = StartCoroutine(LoadClip(path, _selection.Revision));
        }
        catch (Exception ex) { LoadFailed(ex.Message); }
    }

    private IEnumerator LoadClip(string path, int revision)
    {
        yield return null;
        
        var type = path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? AudioType.WAV
            : path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? AudioType.OGGVORBIS : AudioType.MPEG;
        UnityWebRequestAsyncOperation operation;

        try
        {
            _request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type);
            _request.timeout = 30;
            operation = _request.SendWebRequest();
        }
        catch (Exception ex)
        {
            LoadFailed(ex.Message); 
            yield break;
        }
        
        yield return operation;

        if (!_selection.IsCurrent(revision))
        {
            DisposeRequest(); 
            yield break;
        }
        
        try
        {
            if (_request.result != UnityWebRequest.Result.Success)
            {
                throw new IOException(_request.error);
            }
            _clip = DownloadHandlerAudioClip.GetContent(_request);
            if (!_clip || _clip.length <= 0) throw new IOException("Recording has no decodable audio.");
            _source.clip = _clip;
            _status.text = "Briefing audio";
            _progress.interactable = true;
            SetButtons(true, true, true);
            SetTime(0);
            DisposeRequest();
            _loading = null;
            if (_autoPlay && !BriefingSettings.AutoPlay.Value) _selection.Stop();
            if (_selection.WantsPlayback && BriefingSettings.Enabled.Value)
            {
                Play();
            }
        }
        catch (Exception ex) { LoadFailed(ex.Message); }
    }

    private void LoadFailed(string message)
    {
        DisposeRequest();
        _loading = null;
        _selection.Stop();
        if (_clip) Destroy(_clip);
        _clip = null;
        _source.clip = null;
        _progress.interactable = false;
        _status.text = "Unable to load recording";
        _time.text = "--:-- / --:--";
        SetButtons(true, false, false);
        Plugin.LogSource.LogWarning($"[Briefings] {_questId}: {message}");
    }

    private void Play()
    {
        if (!_enabled || !_unlocked) return;
        
        _selection.Play();
        
        if (!_clip)
        {
            LoadRecording();
        }
    }

    private void StartPlayback()
    {
        if (_progress.value >= 0.999f) _source.time = 0;
        if (Singleton<GUISounds>.Instantiated && Singleton<GUISounds>.Instance.MasterMixer != null)
        {
            var groups = Singleton<GUISounds>.Instance.MasterMixer.FindMatchingGroups("UI");
            if (groups.Length > 0) _source.outputAudioMixerGroup = groups[0];
        }
        _cueSource.Stop();
        if (UseRadioCues)
        {
            PlayCue(_openingCue);
            _voiceStartAt = AudioSettings.dspTime + _openingCue.length;
            _starting = true;
            _status.text = "Connecting...";
        }
        else
        {
            _source.Play();
            _wasPlaying = true;
            _status.text = "Playing";
        }
    }

    private static AudioClip CreateCue(bool opening)
    {
        var samples = RadioCueSamples.Create(opening);
        var clip = AudioClip.Create(opening ? "Radio connect" : "Radio disconnect",
            samples.Length, 1, RadioCueSamples.SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void PlayCue(AudioClip clip)
    {
        if (!_cueSource || !UseRadioCues || !BriefingSettings.Enabled.Value) return;
        _cueSource.outputAudioMixerGroup = _source.outputAudioMixerGroup;
        _cueSource.volume = BriefingSettings.Volume.Value * 0.55f;
        _cueSource.clip = clip;
        _cueSource.Play();
    }

    private void StopPlayback() => StopPlayback(true);

    private void StopPlayback(bool disconnect)
    {
        _selection.Stop();
        if (!_source) return;
        
        var hadTransmission = _starting || _wasPlaying || _source.isPlaying;
        
        _paused = false;
        _starting = false;
        _source.Stop();
        
        if (_cueSource) { _cueSource.Stop(); }
        if (disconnect && hadTransmission && IsForeground()) { PlayCue(_closingCue); }
        
        _wasPlaying = false;
        if (_progress) _progress.SetValueWithoutNotify(0);

        if (!_clip) { return; }

        SetTime(0); 
        _status.text = "Stopped";
    }

    private void Replay() { StopPlayback(false); Play(); }

    private void Seek(float value)
    {
        if (!_clip) return;
        _source.time = Mathf.Min(value * _clip.length, Mathf.Max(0, _clip.length - 0.05f));
        SetTime(_source.time);
    }

    private void SetTime(float seconds) => _time.text =
        BriefingSelection.FormatTime(seconds) + " / " + BriefingSelection.FormatTime(_clip.length);

    private void DisposeRequest()
    {
        if (_request == null) return;
        _request.Dispose();
        _request = null;
    }

    private void ResetAudio()
    {
        StopPlayback(false);
        _focus.Reset();
        if (_loading != null) { StopCoroutine(_loading); _loading = null; }
        if (_request != null) { _request.Abort(); DisposeRequest(); }
        if (_source) _source.clip = null;
        if (_clip) Destroy(_clip);
        _clip = null;
        if (_progress) _progress.interactable = false;
    }

    public void Clear()
    {
        ResetAudio();
        _selection.Clear();
        _questId = null;
        _recording = null;
        ShowControls(false);
        if (_active == this) _active = null;
    }

    private void OnDisable() => Clear();
    private void OnDestroy()
    {
        Clear();
        if (_bar) Destroy(_bar.gameObject);
        if (_space) Destroy(_space);
        if (_audioObject) Destroy(_audioObject);
        if (_cueObject) Destroy(_cueObject);
        if (_openingCue) Destroy(_openingCue);
        if (_closingCue) Destroy(_closingCue);
    }

    private void ShowControls(bool show)
    {
        if (!_bar) { return; }
        
        if (show && !_hasLayoutSnapshot)
        {
            _originalSize = _description.rectTransform.sizeDelta;
            _originalPosition = _description.rectTransform.anchoredPosition;
            _originalAlignment = _description.alignment;
            _hasLayoutSnapshot = true;
            _description.alignment = TextAlignmentOptions.TopLeft;
            _lastTextHeight = -1;
        }
        
        _space.enabled = show;
        _bar.gameObject.SetActive(show);
        
        if (show || !_hasLayoutSnapshot) { return; }

        _description.rectTransform.sizeDelta = _originalSize;
        _description.rectTransform.anchoredPosition = _originalPosition;
        _description.alignment = _originalAlignment;
        _hasLayoutSnapshot = false;
        
        LayoutRebuilder.MarkLayoutForRebuild(_description.rectTransform);
    }

    private void SetButtons(bool play, bool stop, bool replay)
    {
        _play.interactable = play;
        _stop.interactable = stop;
        _replay.interactable = replay;
    }

    private void BuildControls()
    {
        _space = _description.gameObject.AddComponent<LayoutElement>();
        _space.layoutPriority = 100;
        
        _bar = NewRect("QuestBriefingPlayer", _description.transform);
        _bar.anchorMin = new Vector2(0, 1);
        _bar.anchorMax = Vector2.one;
        _bar.pivot = new Vector2(0, 1);
        _bar.sizeDelta = new Vector2(0, 64);
        
        var ignore = _bar.gameObject.AddComponent<LayoutElement>();
        ignore.ignoreLayout = true;

        var row = NewRect("Controls", _bar);
        TopRow(row, 24, 32);
        
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleLeft;
        
        _play = MakeButton(row, "Play", BriefingControlIcon.Symbol.Play, Play);
        _stop = MakeButton(row, "Stop", BriefingControlIcon.Symbol.Stop, StopPlayback);
        _replay = MakeButton(row, "Replay", BriefingControlIcon.Symbol.Replay, Replay);
        _status = MakeText("Status", row, "Briefing audio", 18);
        _status.color = new Color32(145, 148, 140, 255);
        
        var statusLayout = _status.gameObject.AddComponent<LayoutElement>();
        statusLayout.minWidth = 0;
        statusLayout.preferredWidth = 80;
        statusLayout.flexibleWidth = 1;
        statusLayout.preferredHeight = 32;
        
        _time = MakeText("Duration", row, "--:-- / --:--", 18);
        _time.alignment = TextAlignmentOptions.MidlineRight;
        
        var timeLayout = _time.gameObject.AddComponent<LayoutElement>();
        timeLayout.minWidth = timeLayout.preferredWidth = 150;
        timeLayout.preferredHeight = 32;

        var track = NewRect("Progress", _bar);
        TopRow(track, 0, 16);
        
        var hitArea = track.gameObject.AddComponent<Image>();
        hitArea.color = Color.clear;
        
        var rail = NewRect("Rail", track);
        Stretch(rail, new Vector2(0, 7), new Vector2(0, -7));
        
        var railImage = rail.gameObject.AddComponent<Image>();
        railImage.color = new Color32(65, 70, 66, 255);
        railImage.raycastTarget = false;
        
        var fill = NewRect("Fill", rail);
        Stretch(fill, Vector2.zero, Vector2.zero);
        
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = _ink;
        fillImage.raycastTarget = false;
        
        _progress = track.gameObject.AddComponent<Slider>();
        _progress.fillRect = fill;
        _progress.transition = Selectable.Transition.None;
        _progress.navigation = new Navigation { mode = Navigation.Mode.None };
        _progress.onValueChanged.AddListener(Seek);
        _progress.interactable = false;
    }

    private Button MakeButton(Transform parent, string label, BriefingControlIcon.Symbol symbol,
        UnityEngine.Events.UnityAction action)
    {
        var rect = NewRect(label, parent);
        
        var element = rect.gameObject.AddComponent<LayoutElement>();
        element.minWidth = element.preferredWidth = 32;
        element.minHeight = element.preferredHeight = 32;
        
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Color.clear;
        
        var iconRect = NewRect("Icon", rect);
        Stretch(iconRect, new Vector2(5, 5), new Vector2(-5, -5));
        
        var icon = iconRect.gameObject.AddComponent<BriefingControlIcon>();
        icon.Shape = symbol;
        icon.raycastTarget = false;
        
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = icon;
        
        var colors = button.colors;
        colors.normalColor = _ink;
        colors.highlightedColor = colors.selectedColor = new Color32(244, 241, 218, 255);
        colors.pressedColor = new Color32(158, 155, 131, 255);
        colors.disabledColor = new Color32(75, 80, 73, 255);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(action);
        
        return button;
    }

    private static void TopRow(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0, 1);
        rect.sizeDelta = new Vector2(0, height);
        rect.anchoredPosition = new Vector2(0, -top);
    }

    private TMP_Text MakeText(string rectName, Transform parent, string value, float size)
    {
        var rect = NewRect(rectName, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _description.font;
        text.fontSharedMaterial = _description.fontSharedMaterial;
        text.fontSize = size;
        text.color = _ink;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.gameObject.layer = parent.gameObject.layer;
        return rect;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }
}