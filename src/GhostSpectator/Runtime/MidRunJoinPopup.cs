using UnityEngine;

namespace GhostSpectator.Runtime;

// Modal popup shown when joining a friend's lobby mid-run (host is on a
// non-Airport scene). Offers a [Play] vs [Spectate] choice so the joiner
// commits to a role BEFORE Character.Start fires, avoiding a spawn-then-
// revert flash. Rendered via OnGUI on top of the loading screen so the
// user never sees the island until they've picked.
//
// State machine:
//   - Show() : flips IsShowing = true (OnGUI starts rendering)
//             clears HasChoice / Choice fields
//   - User clicks a button : sets Choice + HasChoice = true,
//             writes SpectatorEnabled.Value (which fires PublishSpectatorStatus)
//   - The gate coroutine (MidRunJoinGate.WaitForMidRunChoice) polls HasChoice
//   - On wait release, Hide() flips IsShowing = false
//
// Scene-load is blocked by the gate coroutine (which is injected ahead of the
// LoadSceneProcess in LoadingScreenHandler.Load's processes[] array), so the
// loading screen stays visible while we wait for input.
internal class MidRunJoinPopup : MonoBehaviour
{
    internal static MidRunJoinPopup? Instance { get; private set; }

    private bool _isShowing;
    private bool _hasChoice;
    private bool _choiceIsSpectate;
    private float _shownAtUnscaledTime;

    // Fail-safe self-hide if the waiter coroutine that opened us is killed
    // before the user clicks (force-quit lobby join, Photon disconnect,
    // LoadingScreenHandler torn down mid-load, etc.). Without this, the
    // popup would remain visible for the rest of the session, blocking
    // input behind the scrim. 5 minutes is well above any realistic
    // decision time and well below "the user gave up and walked away."
    private const float SelfHideTimeoutSeconds = 300f;

    internal bool IsShowing => _isShowing;
    internal bool HasChoice => _hasChoice;
    internal bool ChoiceIsSpectate => _choiceIsSpectate;

    private GUIStyle? _titleStyle;
    private GUIStyle? _bodyStyle;
    private GUIStyle? _buttonStyle;
    private GUIStyle? _boxStyle;
    private Texture2D? _backgroundTexture;
    private Texture2D? _scrimTexture;

    private const int PanelWidth = 520;
    private const int PanelHeight = 240;
    private const int ButtonHeight = 56;
    private const int ButtonGap = 16;
    private const int PaddingX = 32;
    private const int PaddingY = 28;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    internal void Show()
    {
        _hasChoice = false;
        _choiceIsSpectate = false;
        _isShowing = true;
        _shownAtUnscaledTime = Time.realtimeSinceStartup;
        Plugin.TraceDebug("[trace] MidRunJoinPopup shown");
    }

    private void Update()
    {
        if (!_isShowing) return;
        // Self-hide if we've been visible far past any reasonable click time.
        // Indicates the waiter coroutine died without calling Hide.
        if (Time.realtimeSinceStartup - _shownAtUnscaledTime > SelfHideTimeoutSeconds)
        {
            Plugin.TraceWarn($"[trace] MidRunJoinPopup self-hiding after {SelfHideTimeoutSeconds:0}s timeout (waiter coroutine likely orphaned)");
            Hide();
            return;
        }
        // Self-hide if we've left the Photon room while the popup is up
        // (lobby exit, disconnect). No room = no run to join = popup is
        // showing for a destination that no longer applies.
        if (!Photon.Pun.PhotonNetwork.InRoom)
        {
            Plugin.TraceWarn("[trace] MidRunJoinPopup self-hiding because PhotonNetwork.InRoom flipped to false (lobby exit?)");
            Hide();
        }
    }

    internal void Hide()
    {
        _isShowing = false;
        Plugin.TraceDebug("[trace] MidRunJoinPopup hidden.");
    }

    private void OnGUI()
    {
        if (!_isShowing) return;
        EnsureStyles();
        // Re-bind the box-style background every frame in case a scene
        // transition unloaded the texture and left the style holding a
        // destroyed Unity Object reference. Mirrors SpectatorMenuUI's
        // defensive rebind; matters here too because the popup is shown
        // exactly across a scene transition (loading screen -> island).
        if (_boxStyle != null && _backgroundTexture != null)
        {
            _boxStyle.normal.background = _backgroundTexture;
        }

        // Full-screen scrim so the loading screen's parallax / artwork doesn't
        // visually compete with the modal. Drawn at a low depth so the panel
        // sits on top.
        var prevDepth = GUI.depth;
        GUI.depth = -1000;
        if (_scrimTexture != null)
        {
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _scrimTexture);
        }

        var panelRect = new Rect(
            (Screen.width - PanelWidth) / 2f,
            (Screen.height - PanelHeight) / 2f,
            PanelWidth,
            PanelHeight);
        GUI.Box(panelRect, GUIContent.none, _boxStyle);

        float cursorY = panelRect.y + PaddingY;
        var titleRect = new Rect(panelRect.x + PaddingX, cursorY, panelRect.width - PaddingX * 2, 30);
        GUI.Label(titleRect, "Join this run as…", _titleStyle);
        cursorY += 38;

        var bodyRect = new Rect(panelRect.x + PaddingX, cursorY, panelRect.width - PaddingX * 2, 50);
        GUI.Label(bodyRect, "Pick how to join. Live climbers play the run; spectators watch.", _bodyStyle);
        cursorY += 58;

        float buttonRowY = cursorY;
        float buttonW = (panelRect.width - PaddingX * 2 - ButtonGap) / 2f;

        var playRect = new Rect(panelRect.x + PaddingX, buttonRowY, buttonW, ButtonHeight);
        var specRect = new Rect(panelRect.x + PaddingX + buttonW + ButtonGap, buttonRowY, buttonW, ButtonHeight);

        if (GUI.Button(playRect, "Play", _buttonStyle))
        {
            Commit(spectate: false);
        }
        if (GUI.Button(specRect, "Spectate", _buttonStyle))
        {
            Commit(spectate: true);
        }

        GUI.depth = prevDepth;
    }

    private void Commit(bool spectate)
    {
        _choiceIsSpectate = spectate;
        _hasChoice = true;
        // Writing the config value fires SpectatorEnabled.SettingChanged which
        // RoomCallbackHandler subscribed to in Awake; that calls
        // PublishSpectatorStatus, broadcasting the property change to the room
        // (the broadcast is buffered until LoadingScreenHandler restores the
        // message queue after this coroutine resolves).
        Plugin.SpectatorEnabled.Value = spectate;
        Plugin.Trace($"[trace] MidRunJoinPopup committed: spectate={spectate}");
    }

    private void EnsureStyles()
    {
        if (_titleStyle != null) return;
        _titleStyle = GuiHelpers.MakeLabelStyle(
            fontSize: 22, fontStyle: FontStyle.Bold, alignment: TextAnchor.MiddleLeft, textColor: Color.white);
        _bodyStyle = GuiHelpers.MakeLabelStyle(
            fontSize: 14, fontStyle: FontStyle.Normal, alignment: TextAnchor.UpperLeft,
            textColor: new Color(0.88f, 0.88f, 0.92f), wordWrap: true);
        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };

        _backgroundTexture = GuiHelpers.BuildSolidTexture(new Color(0.08f, 0.08f, 0.12f, 0.96f));
        _scrimTexture = GuiHelpers.BuildSolidTexture(new Color(0f, 0f, 0f, 0.65f));
        _boxStyle = new GUIStyle(GUI.skin.box);
        _boxStyle.normal.background = _backgroundTexture;
    }
}
