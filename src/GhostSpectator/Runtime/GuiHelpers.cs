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

    // Procedural ghost silhouette: rounded head, straight body, three scallops
    // at the bottom, two black eye dots. Pure white so the caller can tint via
    // GUI.color (player's skin color). Used by:
    //   - SpectatorMenuUI airport panel (Texture2D for IMGUI)
    //   - Patch_WaitingForPlayersUI_Update postfix (wrapped as Sprite for
    //     UnityEngine.UI.Image override on the end-game portrait strip)
    internal static Texture2D BuildGhostTexture(int size)
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
        float halfW = size * 0.42f;
        float headBottom = size * 0.45f;
        float bodyBottom = size * 0.82f;
        float scallopR = halfW / 2.6f;

        for (int row = 0; row < size; row++)
        {
            float fy = row + 0.5f;
            int writeRow = size - 1 - row;
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f;
                float dx = fx - cx;
                bool fill = false;

                if (fy < headBottom)
                {
                    float ady = (fy - headBottom) / headBottom;
                    float adx = dx / halfW;
                    if (adx * adx + ady * ady <= 1f) fill = true;
                }
                else if (fy < bodyBottom)
                {
                    if (Mathf.Abs(dx) <= halfW) fill = true;
                }
                else
                {
                    if (Mathf.Abs(dx) <= halfW)
                    {
                        int scallops = 3;
                        float step = halfW * 2 / scallops;
                        for (int s = 0; s < scallops; s++)
                        {
                            float scx = -halfW + (s + 0.5f) * step;
                            float sdx = dx - scx;
                            float sdy = fy - bodyBottom;
                            if (sdx * sdx + sdy * sdy <= scallopR * scallopR)
                            {
                                fill = true;
                                break;
                            }
                        }
                    }
                }

                if (fill) pixels[writeRow * size + x] = Color.white;
            }
        }

        DrawDisk(pixels, size, (int)(size * 0.34f), (int)(size * 0.42f), Mathf.Max(2, size / 14), Color.black);
        DrawDisk(pixels, size, (int)(size * 0.66f), (int)(size * 0.42f), Mathf.Max(2, size / 14), Color.black);

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static void DrawDisk(Color[] pixels, int size, int cx, int cy, int r, Color color)
    {
        int writeRow0 = size - 1 - cy;
        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dy * dy > r * r) continue;
                int px = cx + dx;
                int py = writeRow0 - dy;
                if (px < 0 || px >= size || py < 0 || py >= size) continue;
                pixels[py * size + px] = color;
            }
        }
    }
}
