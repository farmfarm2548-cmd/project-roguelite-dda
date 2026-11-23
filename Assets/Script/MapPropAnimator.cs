using UnityEngine;

public class MapPropAnimator : MonoBehaviour
{
    public enum Mode { FlameFlicker, CrystalPulse, WindSway, AltarPulse, FloatingEmber }
    public Mode mode = Mode.FlameFlicker;

    public float speed = 3f;
    public float amplitude = 0.08f;
    public Color glowColor = new Color(1f, 0.55f, 0.15f, 0.45f);
    public float glowScale = 2.6f;

    private Vector3 basePos;
    private Vector3 baseScale;
    private float phase;
    private SpriteRenderer sr;
    private SpriteRenderer haloSr;
    private Transform haloTr;
    private static Sprite radialGlowSprite;

    void Start()
    {
        basePos = transform.localPosition;
        baseScale = transform.localScale;
        phase = Random.Range(0f, Mathf.PI * 2f);
        sr = GetComponent<SpriteRenderer>();

        if (mode == Mode.FlameFlicker || mode == Mode.CrystalPulse || mode == Mode.AltarPulse)
        {
            CreateGlowHalo();
        }
    }

    private void CreateGlowHalo()
    {
        if (radialGlowSprite == null)
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(1f - d, 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            radialGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
        }

        GameObject halo = new GameObject("GlowHalo");
        halo.transform.SetParent(transform, false);
        halo.transform.localPosition = new Vector3(0f, mode == Mode.FlameFlicker ? 0.15f : 0f, 0f);
        halo.transform.localScale = Vector3.one * glowScale;
        haloTr = halo.transform;

        haloSr = halo.AddComponent<SpriteRenderer>();
        haloSr.sprite = radialGlowSprite;
        haloSr.color = glowColor;
        haloSr.sortingOrder = (sr != null ? sr.sortingOrder : 5000) - 1;
    }

    void Update()
    {
        float t = Time.time * speed + phase;

        switch (mode)
        {
            case Mode.FlameFlicker:
            {
                float flick = Mathf.Sin(t * 2.1f) * 0.5f + Mathf.Sin(t * 5.3f) * 0.3f + Mathf.Cos(t * 8.7f) * 0.2f;
                float s = 1f + flick * amplitude;
                transform.localScale = new Vector3(baseScale.x * (1f + flick * 0.03f), baseScale.y * s, baseScale.z);
                if (haloTr != null && haloSr != null)
                {
                    haloTr.localScale = Vector3.one * (glowScale * (1f + flick * 0.12f));
                    Color c = glowColor;
                    c.a = Mathf.Clamp01(glowColor.a + flick * 0.12f);
                    haloSr.color = c;
                }
                break;
            }
            case Mode.CrystalPulse:
            {
                float wave = Mathf.Sin(t);
                transform.localPosition = basePos + new Vector3(0f, wave * 0.06f, 0f);
                if (haloTr != null && haloSr != null)
                {
                    haloTr.localScale = Vector3.one * (glowScale * (1f + wave * 0.15f));
                    Color c = glowColor;
                    c.a = Mathf.Clamp01(glowColor.a * (0.75f + 0.35f * wave));
                    haloSr.color = c;
                }
                break;
            }
            case Mode.WindSway:
            {
                float angle = Mathf.Sin(t) * (amplitude * 45f);
                transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                break;
            }
            case Mode.AltarPulse:
            {
                float wave = 0.5f + 0.5f * Mathf.Sin(t);
                if (haloTr != null && haloSr != null)
                {
                    haloTr.localScale = Vector3.one * (glowScale * (0.9f + wave * 0.25f));
                    Color c = glowColor;
                    c.a = Mathf.Clamp01(glowColor.a * (0.5f + 0.6f * wave));
                    haloSr.color = c;
                }
                break;
            }
            case Mode.FloatingEmber:
            {
                float cycle = Mathf.Repeat(Time.time * 0.28f + phase, 1f);
                float xDrift = Mathf.Sin(Time.time * 1.6f + phase * 3f) * 0.55f;
                transform.localPosition = basePos + new Vector3(xDrift, cycle * 4.5f, 0f);
                if (sr != null)
                {
                    Color c = glowColor;
                    c.a = Mathf.Sin(cycle * Mathf.PI) * 0.85f;
                    sr.color = c;
                }
                break;
            }
        }
    }
}
