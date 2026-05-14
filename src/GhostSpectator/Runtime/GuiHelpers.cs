using UnityEngine;

namespace GhostSpectator.Runtime;

// Shared IMGUI utilities for the mod's two MonoBehaviour-based UIs
// (SpectatorMenuUI and MidRunJoinPopup). Both build 1x1 solid-color
// textures for box backgrounds and define text labels with the same
// constructor pattern; extracting here avoids the byte-identical
// `BuildSolidTexture` and ~5x-repeated GUIStyle factory boilerplate.
internal static class GuiHelpers
{
    // Single-pixel solid texture, suitable for stretching as a box
    // background via GUI.skin.box.normal.background. HideAndDontSave so
    // Unity doesn't unload it across scene transitions.
    internal static Texture2D BuildSolidTexture(Color color)
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    // Label-style factory. Replaces the repeated
    //   var s = new GUIStyle(GUI.skin.label) { fontSize=..., fontStyle=..., alignment=... };
    //   s.normal.textColor = ...;
    // pattern.
    internal static GUIStyle MakeLabelStyle(int fontSize, FontStyle fontStyle, TextAnchor alignment, Color textColor, bool wordWrap = false)
    {
        var s = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = fontStyle,
            alignment = alignment,
            wordWrap = wordWrap,
        };
        s.normal.textColor = textColor;
        return s;
    }
}
