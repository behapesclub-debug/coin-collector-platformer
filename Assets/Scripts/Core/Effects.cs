using UnityEngine;

/// <summary>
/// Простые визуальные эффекты: пыль при приземлении, искры, след от рывка.
///
/// Как и звуки в SoundFX, картинка для частиц не хранится в проекте —
/// маленький мягкий кружок рисуется кодом один раз (Texture2D → Sprite).
/// Каждая частица — крошечный объект со SpriteRenderer и скриптом EffectParticle,
/// который двигает её, уменьшает, делает прозрачной и удаляет.
///
/// Использование из любого скрипта:
///   Effects.Dust(позиция, количество, цвет);   // пыль в стороны
///   Effects.Burst(позиция, количество, цвет);  // искры во все стороны
/// </summary>
public static class Effects
{
    private static Sprite dot;

    private static Sprite Dot
    {
        get
        {
            if (dot == null) dot = CreateDot(16);
            return dot;
        }
    }

    /// <summary>Облачко пыли: частицы разлетаются влево и вправо по земле.</summary>
    public static void Dust(Vector2 position, int count, Color color)
    {
        for (int i = 0; i < count; i++)
        {
            float side = i % 2 == 0 ? 1f : -1f;
            var velocity = new Vector2(side * Random.Range(1f, 3.2f), Random.Range(0.3f, 1.6f));
            var offset = new Vector2(Random.Range(-0.2f, 0.2f), 0f);
            Spawn(position + offset, velocity, Random.Range(0.15f, 0.3f), Random.Range(0.25f, 0.45f), 2f, color);
        }
    }

    /// <summary>Вспышка искр во все стороны (подбор звезды, гибель врага).</summary>
    public static void Burst(Vector2 position, int count, Color color, float speed = 4f)
    {
        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            Spawn(position, direction * Random.Range(speed * 0.5f, speed),
                Random.Range(0.12f, 0.25f), Random.Range(0.3f, 0.6f), 6f, color);
        }
    }

    /// <summary>Одна неподвижная частица, которая тает на месте (след от рывка).</summary>
    public static void Puff(Vector2 position, Color color, float size, float life)
    {
        Spawn(position, Vector2.zero, size, life, 0f, color);
    }

    private static void Spawn(Vector2 position, Vector2 velocity, float size, float life, float gravity, Color color)
    {
        var go = new GameObject("Particle");
        go.transform.position = position;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Dot;
        sr.sortingOrder = 20; // поверх всего уровня
        go.AddComponent<EffectParticle>().Init(velocity, life, gravity, color, size);
    }

    // Мягкий белый кружок size×size пикселей. Цвет частице задаёт SpriteRenderer.color.
    private static Sprite CreateDot(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                float a = Mathf.Clamp01((1f - d) * 3f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;
        // Последний параметр — пикселей на клетку: кружок займёт ровно 1×1 клетку.
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
