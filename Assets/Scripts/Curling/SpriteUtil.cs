using UnityEngine;

public static class SpriteUtil
{
    public static Sprite Circle(int size, Color color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r = (size - 1) * 0.5f;
        float r2 = r * r;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r;
                float dy = y - r;
                float d2 = dx * dx + dy * dy;
                float a = Mathf.Clamp01(r - Mathf.Sqrt(d2));
                Color c = color;
                c.a *= a > 0.75f ? 1f : Mathf.SmoothStep(0f, 1f, a / 0.75f);
                if (d2 > r2)
                    c.a = 0f;
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    public static Sprite Rect(int w, int h, Color color, float corner = 0.18f)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float rad = Mathf.Min(w, h) * corner;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float ax = Mathf.Min(x, w - 1 - x);
                float ay = Mathf.Min(y, h - 1 - y);
                Color c = color;
                if (ax < rad && ay < rad)
                {
                    float dx = rad - ax;
                    float dy = rad - ay;
                    if (dx * dx + dy * dy > rad * rad)
                        c.a = 0f;
                }

                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
    }
}
