using UnityEngine;

/// <summary>
/// Procedural rounded-rect sprite factory. Rendering corners without a pre-made
/// asset would otherwise require a custom shader; instead we bake a small
/// white-on-transparent texture with rounded corners and 9-slice it.
///
/// The returned sprite can be assigned to any UnityEngine.UI.Image and stretched
/// to arbitrary sizes (set Image.type = Sliced). The border equals the radius,
/// so the corners never distort.
///
/// We cache per-(size,radius) so spawning many panels doesn't allocate many
/// textures.
/// </summary>
public static class UIRoundedSprite
{
    // Default shared sprite — 64px box with 20px rounding. Good for cards.
    private static Sprite _default;

    public static Sprite Default
    {
        get
        {
            if (_default == null) _default = Create(size: 64, radius: 20);
            return _default;
        }
    }

    /// <summary>
    /// Bakes a new rounded-rect sprite. Pixels are white (so Image.color tints
    /// it cleanly); outside-the-corner pixels are fully transparent.
    /// </summary>
    public static Sprite Create(int size = 64, int radius = 20)
    {
        size   = Mathf.Max(8, size);
        radius = Mathf.Clamp(radius, 1, size / 2 - 1);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp,
            hideFlags  = HideFlags.HideAndDontSave
        };

        Color32 on  = new Color32(255, 255, 255, 255);
        Color32 off = new Color32(255, 255, 255, 0);

        Color32[] px = new Color32[size * size];
        float r2 = radius * radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance from the nearest corner center.
                // If we're in the straight edges, the "corner center" clamps to
                // the actual pixel and the check trivially passes.
                int cx = x < radius ? radius
                       : x >= size - radius ? size - 1 - radius
                       : x;
                int cy = y < radius ? radius
                       : y >= size - radius ? size - 1 - radius
                       : y;

                float dx = x - cx;
                float dy = y - cy;
                px[y * size + x] = (dx * dx + dy * dy) <= r2 ? on : off;
            }
        }

        tex.SetPixels32(px);
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        // 9-slice border = radius; pixelsPerUnit = 100 is Unity UI's default.
        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit: 100f,
            extrude: 0,
            meshType: SpriteMeshType.FullRect,
            border: new Vector4(radius, radius, radius, radius));
    }
}
