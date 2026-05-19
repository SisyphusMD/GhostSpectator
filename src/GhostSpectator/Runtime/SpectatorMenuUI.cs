using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zorro.Core;

namespace GhostSpectator.Runtime;

// Spectator toggle (power button, airport only) + always-visible player list
// showing who's in the room with a per-row icon indicating live vs spectator
// status. Top-left corner, mouse-only.
//
// Two procedural icons, both drawn from C# pixel data so we don't ship any
// image assets:
//   - climber silhouette (head circle + trapezoidal body) -- shown on live
//     rows in the airport AND on live rows during a run when the player's
//     Character is alive.
//   - ghost silhouette (rounded head, scallop bottom, two eyes) -- shown on
//     spectator rows always, and on live rows during a run when the player's
//     Character is currently in vanilla mid-run dead/passedOut state (they
//     stay in the LIVE list visually, just the icon changes to communicate
//     "temporarily a vanilla ghost waiting to be revived").
//
// Both icons are tinted to the player's chosen character skin color
// (read from PEAK's PersistentPlayerDataService, which the game already
// syncs across the room) so a player keeps the same color identity whether
// they're showing as a climber or a ghost.
internal class SpectatorMenuUI : MonoBehaviour
{
    private GUIStyle? _titleStyle;
    private GUIStyle? _sectionHeaderStyle;
    private GUIStyle? _rowStyle;
    private GUIStyle? _boxStyle;
    private GUIStyle? _powerButtonStyle;
    private Texture2D? _ghostTexture;
    private Texture2D? _climberTexture;
    private Texture2D? _powerTexture;
    private Texture2D? _panelBackground;

    private const int PanelWidth = 240;
    private const int ScreenMargin = 20;
    private const int PanelPaddingX = 12;
    private const int PanelPaddingY = 10;
    private const int RowHeight = 24;
    private const int TitleHeight = 28;
    // Section header rect height. Same descender-clearance reasoning as the
    // old subtitle: font size + ~6px keeps "p"/"g" from clipping against the
    // rect bottom.
    private const int SectionHeaderHeight = 20;
    private const int SectionGap = 4;
    private const int IconSize = 18;
    private const int IconPadding = 2;
    private const int PowerButtonSize = 26;

    // Fallback if we can't read the player's actual chosen skin color (services
    // not yet initialized, customization data not yet synced, etc.).
    private static readonly Color FallbackGhostColor = new(0.78f, 0.66f, 1.00f, 1f);
    private static readonly Color PowerOnColor = new(0.30f, 0.95f, 0.40f, 1f);
    private static readonly Color PowerOffColor = new(0.62f, 0.62f, 0.68f, 1f);
    private static readonly Color EmptySlotColor = new(0.60f, 0.60f, 0.65f, 0.45f);

    private void OnGUI()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null) return;
        EnsureTextures();
        EnsureStyles();
        // Re-bind the background every frame in case a scene transition
        // unloaded the texture and left the style holding a destroyed Unity
        // Object. Single alpha value across all scenes -- airport notifications
        // and island scenery both need to show through.
        if (_boxStyle != null && _panelBackground != null)
        {
            _boxStyle.normal.background = _panelBackground;
        }
        DrawPlayerPanel(ScreenMargin);
        DrawKioskRefusedMessage();
    }

    // Brief on-screen banner shown when AirportCheckInKiosk.StartGame refuses
    // the run because every player is a spectator. Timestamp is set by the
    // refusing prefix; we render for KioskRefusedDisplayDuration seconds then
    // disappear. Without this the kiosk just silently does nothing and the
    // user has no idea why.
    private GUIStyle? _kioskBannerStyle;
    private Texture2D? _kioskBannerBackground;

    private void DrawKioskRefusedMessage()
    {
        if (Patches.SpectatorState.KioskRefusedTimestamp < 0f) return;
        float age = Time.realtimeSinceStartup - Patches.SpectatorState.KioskRefusedTimestamp;
        if (age > Patches.SpectatorState.KioskRefusedDisplayDuration) return;

        if (_kioskBannerStyle == null)
        {
            _kioskBannerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(20, 20, 14, 14),
            };
            _kioskBannerStyle.normal.textColor = Color.white;
        }
        if (_kioskBannerBackground == null)
        {
            _kioskBannerBackground = GuiHelpers.BuildSolidTexture(new Color(0.20f, 0.06f, 0.06f, 0.92f));
            _kioskBannerStyle.normal.background = _kioskBannerBackground;
        }
        else if (_kioskBannerStyle.normal.background == null)
        {
            _kioskBannerStyle.normal.background = _kioskBannerBackground;
        }

        const int bannerW = 540;
        var msg = string.IsNullOrEmpty(Patches.SpectatorState.KioskRefusedMessage)
            ? "Can't start the run."
            : Patches.SpectatorState.KioskRefusedMessage;
        var content = new GUIContent(msg);
        // Auto-size to the actual wrapped height at this width so the box
        // grows for any future message length and never clips the text.
        float bannerH = _kioskBannerStyle.CalcHeight(content, bannerW);
        var rect = new Rect(
            (Screen.width - bannerW) / 2f,
            Screen.height * 0.18f,
            bannerW,
            bannerH);
        GUI.Label(rect, content, _kioskBannerStyle);
    }

    private void DrawPlayerPanel(float topY)
    {
        bool inAirport = SceneManager.GetActiveScene().name == "Airport";

        // Cached snapshot rebuilt on OnJoinedRoom / OnPlayerEntered / OnPlayerLeft.
        // OnGUI fires ~120 Hz, so reading a cached array avoids ~120 LINQ
        // allocations/sec for a list that only changes on join/leave events.
        var players = RoomCallbackHandler.SortedPlayers;
        var labels = RoomCallbackHandler.SortedPlayerLabels;

        // Bucket players into live vs spectator while counting. Manual loop
        // (no LINQ) to avoid the closure allocation cost on every OnGUI tick.
        // The live section always renders all LiveCap slots, filling empties
        // with placeholders, so the player can see at a glance "Live 3/4"
        // means one open slot. The spectator section only renders occupied
        // rows because rendering 16 empty rows when spec count is 0 would
        // overwhelm the panel; the header alone conveys the cap.
        int liveCount = 0;
        int specCount = 0;
        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null) continue;
            if (Patches.SpectatorState.ClaimsSpectator(p)) specCount++;
            else liveCount++;
        }

        int liveRows = Patches.SpectatorState.LiveCap;
        int specRows = specCount;
        int panelHeight =
            PanelPaddingY * 2
            + TitleHeight
            + SectionHeaderHeight + liveRows * RowHeight
            + SectionGap
            + SectionHeaderHeight + specRows * RowHeight;

        var panelRect = new Rect(ScreenMargin, topY, PanelWidth, panelHeight);
        GUI.Box(panelRect, GUIContent.none, _boxStyle);

        float cursorY = panelRect.y + PanelPaddingY;

        // Title row with optional power-toggle to the right (airport only).
        var headerRect = new Rect(panelRect.x + PanelPaddingX, cursorY, panelRect.width - PanelPaddingX * 2, TitleHeight);
        var titleRect = inAirport
            ? new Rect(headerRect.x, headerRect.y, headerRect.width - PowerButtonSize - 6, headerRect.height)
            : headerRect;
        GUI.Label(titleRect, "Ghost Spectator", _titleStyle);
        if (inAirport)
        {
            var powerRect = new Rect(
                headerRect.xMax - PowerButtonSize,
                headerRect.y + (TitleHeight - PowerButtonSize) / 2f,
                PowerButtonSize,
                PowerButtonSize);
            DrawPowerToggle(powerRect);
        }
        cursorY += TitleHeight;

        // Live section.
        DrawSectionHeader(panelRect, cursorY,
            $"LIVE {liveCount}/{Patches.SpectatorState.LiveCap}");
        cursorY += SectionHeaderHeight;
        int liveDrawn = 0;
        for (int i = 0; i < players.Length && liveDrawn < liveRows; i++)
        {
            var p = players[i];
            if (p == null) continue;
            if (Patches.SpectatorState.ClaimsSpectator(p)) continue;
            string label = i < labels.Length ? labels[i] : (string.IsNullOrEmpty(p.NickName) ? "(unnamed)" : p.NickName);
            // Live-section icon picker:
            //   - Airport: always climber (no run state to read from).
            //   - During run: climber if this player's character is alive,
            //     ghost if it's currently in vanilla mid-run dead/passedOut
            //     state. The player stays in the LIVE list either way --
            //     only the icon changes to communicate they're temporarily
            //     a vanilla ghost waiting to be revived.
            var liveIcon = inAirport
                ? _climberTexture
                : (Patches.SpectatorState.IsPlayerCharacterDead(p) ? _ghostTexture : _climberTexture);
            DrawPlayerRow(p, label, new Rect(
                panelRect.x + PanelPaddingX, cursorY,
                panelRect.width - PanelPaddingX * 2, RowHeight),
                icon: liveIcon);
            cursorY += RowHeight;
            liveDrawn++;
        }
        // Fill remaining live slots with placeholders so the cap is always
        // visually represented (empty seat = recruit-someone hint).
        while (liveDrawn < liveRows)
        {
            DrawEmptySlot(new Rect(
                panelRect.x + PanelPaddingX, cursorY,
                panelRect.width - PanelPaddingX * 2, RowHeight));
            cursorY += RowHeight;
            liveDrawn++;
        }

        cursorY += SectionGap;

        // Spectator section.
        DrawSectionHeader(panelRect, cursorY,
            $"SPECTATORS {specCount}/{Patches.SpectatorState.SpectatorCap}");
        cursorY += SectionHeaderHeight;
        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            // SortedPlayers is a snapshot rebuilt only on join/leave callbacks,
            // but each Player object inside it is the live PUN object. A
            // remote player who just left can have their fields nulled out
            // mid-OnGUI-tick; skip the row rather than NRE inside
            // ClaimsSpectator / NickName accessors.
            if (p == null) continue;
            if (!Patches.SpectatorState.ClaimsSpectator(p)) continue;
            string label = i < labels.Length ? labels[i] : (string.IsNullOrEmpty(p.NickName) ? "(unnamed)" : p.NickName);
            // Spectator-section rows always render the ghost icon (this
            // section is by definition permanent spectators -- their
            // character is permanently in ghost state for the whole run).
            DrawPlayerRow(p, label, new Rect(
                panelRect.x + PanelPaddingX, cursorY,
                panelRect.width - PanelPaddingX * 2, RowHeight),
                icon: _ghostTexture);
            cursorY += RowHeight;
        }
    }

    private void DrawSectionHeader(Rect panelRect, float y, string text)
    {
        var rect = new Rect(
            panelRect.x + PanelPaddingX, y,
            panelRect.width - PanelPaddingX * 2, SectionHeaderHeight);
        GUI.Label(rect, text, _sectionHeaderStyle);
    }

    private void DrawEmptySlot(Rect row)
    {
        var prevColor = GUI.color;
        GUI.color = EmptySlotColor;
        GUI.Label(row, "(open)", _rowStyle);
        GUI.color = prevColor;
    }

    // Power-button toggle in the panel header. Only drawn in the airport (the
    // caller gates on scene); on the island the spectator setting is locked for
    // the duration of the run, so showing a control would be misleading.
    private void DrawPowerToggle(Rect rect)
    {
        bool on = Plugin.SpectatorEnabled.Value;
        Color iconColor = on ? PowerOnColor : PowerOffColor;

        if (GUI.Button(rect, GUIContent.none, _powerButtonStyle))
        {
            Plugin.SpectatorEnabled.Value = !on;
            Plugin.Trace($"GhostSpectator: toggled via menu to {Plugin.SpectatorEnabled.Value}");
        }

        const float iconInset = 4f;
        var iconRect = new Rect(
            rect.x + iconInset,
            rect.y + iconInset,
            rect.width - iconInset * 2,
            rect.height - iconInset * 2);

        var prevColor = GUI.color;
        GUI.color = iconColor;
        if (_powerTexture != null) GUI.DrawTexture(iconRect, _powerTexture, ScaleMode.ScaleToFit);
        GUI.color = prevColor;
    }

    // Row layout: [name label .................. icon].  Caller passes the
    // texture to draw on the right side; null = no icon (text-only row).
    // The icon is tinted by GUI.color to the player's chosen character skin
    // color so live/ghost icons share the same color identity per player.
    private void DrawPlayerRow(Photon.Realtime.Player player, string label, Rect row, Texture2D? icon)
    {
        float labelW = icon != null ? row.width - IconSize - IconPadding : row.width;
        var nameRect = new Rect(row.x, row.y, labelW, row.height);
        GUI.Label(nameRect, label, _rowStyle);
        if (icon == null) return;

        var iconRect = new Rect(
            row.xMax - IconSize,
            row.y + (row.height - IconSize) / 2f,
            IconSize,
            IconSize);

        var prevColor = GUI.color;
        GUI.color = GetPlayerSkinColor(player) ?? FallbackGhostColor;
        GUI.DrawTexture(iconRect, icon);
        GUI.color = prevColor;
    }

    // Pulls the player's chosen skin color out of PEAK's PersistentPlayerDataService,
    // which the game itself syncs across the room (see PersistentPlayerDataService
    // SyncPersistentPlayerDataPackage). Returns null if any link in the chain isn't
    // ready yet so the caller can fall back to a default tint.
    private static Color? GetPlayerSkinColor(Photon.Realtime.Player player)
    {
        try
        {
            var service = GameHandler.GetService<PersistentPlayerDataService>();
            if (service == null) return null;
            var data = service.GetPlayerData(player);
            if (data?.customizationData == null) return null;
            var customization = Singleton<Customization>.Instance;
            if (customization == null || customization.skins == null || customization.skins.Length == 0) return null;
            int idx = data.customizationData.currentSkin;
            if (idx < 0 || idx >= customization.skins.Length) return null;
            var option = customization.skins[idx];
            return option != null ? option.color : (Color?)null;
        }
        // Narrowed from bare catch to the specific exceptions we expect from
        // service-lookup races and index access. Anything else (stack overflow,
        // access violation) should propagate rather than be silently masked.
        catch (System.NullReferenceException)
        {
            return null;
        }
        catch (System.IndexOutOfRangeException)
        {
            return null;
        }
    }

    private void EnsureStyles()
    {
        if (_titleStyle != null) return;
        _titleStyle = GuiHelpers.MakeLabelStyle(
            fontSize: 18, fontStyle: FontStyle.Bold, alignment: TextAnchor.MiddleLeft, textColor: Color.white);
        // Slight letter-spacing illusion via a softer gray so the section
        // headers read as labels rather than competing with the title.
        _sectionHeaderStyle = GuiHelpers.MakeLabelStyle(
            fontSize: 11, fontStyle: FontStyle.Bold, alignment: TextAnchor.MiddleLeft,
            textColor: new Color(0.72f, 0.74f, 0.80f));
        _rowStyle = GuiHelpers.MakeLabelStyle(
            fontSize: 14, fontStyle: FontStyle.Normal, alignment: TextAnchor.MiddleLeft, textColor: Color.white);
        _boxStyle = new GUIStyle(GUI.skin.box);
        // Background is re-bound per-frame in OnGUI based on scene.
        _powerButtonStyle = new GUIStyle(GUI.skin.button)
        {
            padding = new RectOffset(2, 2, 2, 2),
        };
    }

    private void EnsureTextures()
    {
        if (_ghostTexture == null) _ghostTexture = GuiHelpers.BuildGhostTexture(40);
        if (_climberTexture == null) _climberTexture = BuildClimberTexture(40);
        if (_powerTexture == null) _powerTexture = BuildPowerTexture(40);
        if (_panelBackground == null) _panelBackground = GuiHelpers.BuildSolidTexture(new Color(0.06f, 0.06f, 0.09f, 0.45f));
    }

    // Procedural climber/person silhouette: head circle on top + trapezoidal
    // torso that widens at the shoulders and tapers to a narrower waist. Pure
    // white so the caller can tint via GUI.color (player's skin color).
    // Same 40x40 footprint as the ghost icon for visual parity between live
    // and spectator rows.
    private static Texture2D BuildClimberTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        float cx = size / 2f;
        float headCy = size * 0.22f;     // head center, y from top
        float headR = size * 0.16f;
        float bodyTop = size * 0.40f;
        float bodyBottom = size * 0.92f;
        float shoulderHalfW = size * 0.34f;
        float waistHalfW = size * 0.24f;

        for (int row = 0; row < size; row++)
        {
            float fy = row + 0.5f;
            int writeRow = size - 1 - row;
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f;
                float dx = fx - cx;
                bool fill = false;

                // Head: circle.
                float hdy = fy - headCy;
                if (dx * dx + hdy * hdy <= headR * headR) fill = true;

                // Body: trapezoid that lerps shoulder->waist width.
                else if (fy >= bodyTop && fy <= bodyBottom)
                {
                    float t = (fy - bodyTop) / (bodyBottom - bodyTop);
                    float halfW = Mathf.Lerp(shoulderHalfW, waistHalfW, t);
                    if (Mathf.Abs(dx) <= halfW) fill = true;
                }

                if (fill) pixels[writeRow * size + x] = Color.white;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // Procedural power icon: a ring with a gap at the top and a vertical bar
    // piercing the gap. Pure white so the caller can tint via GUI.color
    // (green when ON, gray when OFF).
    private static Texture2D BuildPowerTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        float cx = size / 2f;
        float cy = size / 2f;
        float rOuter = size * 0.42f;
        float rInner = size * 0.30f;
        float lineHalfW = size * 0.07f;
        float lineTop = size * 0.10f;
        float lineBottom = size * 0.52f;
        float gapHalfRad = 18f * Mathf.Deg2Rad;

        for (int row = 0; row < size; row++)
        {
            float fy = row + 0.5f;
            int writeRow = size - 1 - row;
            float dy = fy - cy;
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f;
                float dx = fx - cx;
                bool fill = false;

                float r2 = dx * dx + dy * dy;
                if (r2 >= rInner * rInner && r2 <= rOuter * rOuter)
                {
                    bool inGap = false;
                    if (dy < 0)
                    {
                        float angleFromTop = Mathf.Atan2(Mathf.Abs(dx), -dy);
                        if (angleFromTop < gapHalfRad) inGap = true;
                    }
                    if (!inGap) fill = true;
                }

                if (fy >= lineTop && fy <= lineBottom && Mathf.Abs(dx) <= lineHalfW)
                    fill = true;

                if (fill) pixels[writeRow * size + x] = Color.white;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

}
