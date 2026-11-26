using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// РЕДАКТОРНЫЙ скрипт (лежит в папке Editor, в саму игру не попадает).
/// Он один раз "собирает" игру за тебя: рисует спрайты, создаёт анимации,
/// префабы, три сцены (MainMenu, Level1, Level2) и прописывает их в Build Settings.
///
/// Запуск: верхнее меню Unity → Platformer → Собрать игру.
/// ВНИМАНИЕ: повторный запуск ПЕРЕЗАПИШЕТ сцены. Если ты правил сцену руками
/// и хочешь сохранить правки — не запускай сборку снова (или меняй LevelMaps).
/// </summary>
public static class PlatformerBuilder
{
    private const string ArtFolder = "Assets/Art";
    private const string AnimFolder = "Assets/Animations";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string SceneFolder = "Assets/Scenes";

    // Палитра игры
    private static readonly Color SkyColor = new Color(0.55f, 0.80f, 0.98f);
    private static readonly Color GroundColor = new Color(0.55f, 0.36f, 0.22f);
    private static readonly Color GrassColor = new Color(0.33f, 0.75f, 0.30f);
    private static readonly Color PlayerColor = new Color(0.95f, 0.45f, 0.20f);
    private static readonly Color CoinColor = new Color(1.00f, 0.80f, 0.10f);
    private static readonly Color CoinShineColor = new Color(1.00f, 0.93f, 0.55f);
    private static readonly Color SpikeColor = new Color(0.62f, 0.64f, 0.70f);
    private static readonly Color PlatformColor = new Color(0.42f, 0.45f, 0.80f);
    private static readonly Color HeartColor = new Color(0.92f, 0.20f, 0.30f);
    private static readonly Color PoleColor = new Color(0.85f, 0.85f, 0.85f);
    private static readonly Color FinishFlagColor = new Color(0.90f, 0.15f, 0.15f);
    private static readonly Color ButtonColor = new Color(0.20f, 0.62f, 0.32f);
    private static readonly Color EnemyColor = new Color(0.55f, 0.30f, 0.75f);
    private static readonly Color FarHillColor = new Color(0.62f, 0.82f, 0.78f);
    private static readonly Color NearHillColor = new Color(0.47f, 0.74f, 0.45f);

    private static Sprite square, rounded, circle, triangle;
    private static Font font;

    private class Prefabs
    {
        public GameObject Player, Coin, Heart, Spike, Checkpoint, Finish, Enemy;
    }

    [MenuItem("Platformer/Собрать игру (сцены, префабы, анимации)", priority = 0)]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        try
        {
            EditorUtility.DisplayProgressBar("Собиратель монет", "Готовлю ресурсы...", 0.1f);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureFolder(ArtFolder);
            EnsureFolder(AnimFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(SceneFolder);

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateSprites();
            PhysicsMaterial2D noFriction = CreateNoFrictionMaterial();
            AnimatorController playerAnimator = CreatePlayerAnimator();

            EditorUtility.DisplayProgressBar("Собиратель монет", "Создаю префабы...", 0.3f);
            Prefabs prefabs = CreatePrefabs(playerAnimator, noFriction);

            EditorUtility.DisplayProgressBar("Собиратель монет", "Создаю сцены...", 0.6f);
            BuildMainMenu(prefabs);
            BuildLevel("Level1", "Уровень 1", LevelMaps.Level1, "Level2", prefabs);
            BuildLevel("Level2", "Уровень 2", LevelMaps.Level2, "", prefabs);

            // Сцены должны быть в Build Settings, иначе SceneManager.LoadScene их не найдёт.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath("MainMenu"), true),
                new EditorBuildSettingsScene(ScenePath("Level1"), true),
                new EditorBuildSettingsScene(ScenePath("Level2"), true),
            };

            AssetDatabase.SaveAssets();
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorSceneManager.OpenScene(ScenePath("MainMenu"));
        if (Application.isBatchMode) return; // в фоновом режиме окна показывать нельзя
        EditorUtility.DisplayDialog("Собиратель монет",
            "Готово! Открыта сцена MainMenu.\nНажми ▶ Play вверху редактора, чтобы играть.", "Ура");
    }

    // =====================================================================
    //  Ресурсы: спрайты, материал, анимации
    // =====================================================================

    private static void CreateSprites()
    {
        // Рисуем простые фигуры попиксельно и сохраняем в PNG.
        // alpha(u, v) возвращает непрозрачность точки (u, v от 0 до 1).
        const float edge = 64f; // для сглаживания краёв
        square = MakeSprite("Square", (u, v) => 1f);
        circle = MakeSprite("Circle", (u, v) =>
        {
            float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f));
            return (0.5f - d) * edge;
        });
        triangle = MakeSprite("Triangle", (u, v) => (0.5f * (1f - v) - Mathf.Abs(u - 0.5f)) * edge);
        rounded = MakeSprite("RoundedSquare", (u, v) =>
        {
            const float r = 0.22f;
            float dx = Mathf.Max(Mathf.Abs(u - 0.5f) - (0.5f - r), 0f);
            float dy = Mathf.Max(Mathf.Abs(v - 0.5f) - (0.5f - r), 0f);
            return (r - Mathf.Sqrt(dx * dx + dy * dy)) * edge;
        });
    }

    private static Sprite MakeSprite(string name, Func<float, float, float> alpha)
    {
        const int size = 64;
        string path = $"{ArtFolder}/{name}.png";

        if (!File.Exists(path))
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        // Настройки импорта: это спрайт, 64 пикселя = 1 клетка (unit).
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = size;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static PhysicsMaterial2D CreateNoFrictionMaterial()
    {
        // Без трения игрок не "прилипает" к стенам, если прыгнуть в них боком.
        string path = $"{ArtFolder}/NoFriction.physicsMaterial2D";
        var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
        if (material == null)
        {
            material = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    private struct PoseKey
    {
        public float Time, ScaleX, ScaleY, PosY;
        public PoseKey(float time, float scaleX, float scaleY, float posY)
        {
            Time = time; ScaleX = scaleX; ScaleY = scaleY; PosY = posY;
        }
    }

    private static AnimatorController CreatePlayerAnimator()
    {
        // Анимации меняют масштаб и позицию объекта Visual/Body:
        // сплющивание, подпрыгивание при беге, вытягивание в прыжке.
        AnimationClip idle = CreateClip("Idle", true,
            new PoseKey(0f, 1f, 1f, 0f),
            new PoseKey(0.6f, 1.04f, 0.95f, -0.022f),
            new PoseKey(1.2f, 1f, 1f, 0f));
        AnimationClip run = CreateClip("Run", true,
            new PoseKey(0f, 1.05f, 0.95f, -0.02f),
            new PoseKey(0.15f, 0.95f, 1.07f, 0.1f),
            new PoseKey(0.3f, 1.05f, 0.95f, -0.02f));
        AnimationClip jump = CreateClip("Jump", true,
            new PoseKey(0f, 0.85f, 1.15f, 0.05f),
            new PoseKey(0.2f, 0.85f, 1.15f, 0.05f));
        AnimationClip fall = CreateClip("Fall", true,
            new PoseKey(0f, 0.92f, 1.08f, 0f),
            new PoseKey(0.2f, 0.92f, 1.08f, 0f));

        string path = $"{AnimFolder}/Player.controller";
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("VelocityY", AnimatorControllerParameterType.Float);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idleState = sm.AddState("Idle", new Vector3(300, 0));
        AnimatorState runState = sm.AddState("Run", new Vector3(300, 120));
        AnimatorState jumpState = sm.AddState("Jump", new Vector3(600, 0));
        AnimatorState fallState = sm.AddState("Fall", new Vector3(600, 120));
        idleState.motion = idle;
        runState.motion = run;
        jumpState.motion = jump;
        fallState.motion = fall;
        sm.defaultState = idleState;

        // Переходы между состояниями (видно в окне Window → Animation → Animator).
        AddTransition(idleState, runState, (AnimatorConditionMode.Greater, 0.1f, "Speed"), (AnimatorConditionMode.If, 0f, "IsGrounded"));
        AddTransition(runState, idleState, (AnimatorConditionMode.Less, 0.1f, "Speed"));
        foreach (AnimatorState ground in new[] { idleState, runState })
        {
            AddTransition(ground, jumpState, (AnimatorConditionMode.IfNot, 0f, "IsGrounded"), (AnimatorConditionMode.Greater, 0.1f, "VelocityY"));
            AddTransition(ground, fallState, (AnimatorConditionMode.IfNot, 0f, "IsGrounded"), (AnimatorConditionMode.Less, -0.1f, "VelocityY"));
        }
        AddTransition(jumpState, fallState, (AnimatorConditionMode.Less, 0f, "VelocityY"));
        AddTransition(jumpState, idleState, (AnimatorConditionMode.If, 0f, "IsGrounded"));
        AddTransition(fallState, idleState, (AnimatorConditionMode.If, 0f, "IsGrounded"));

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimationClip CreateClip(string name, bool loop, params PoseKey[] keys)
    {
        const string bodyPath = "Visual/Body";
        var clip = new AnimationClip { name = name, frameRate = 60 };

        var scaleX = new AnimationCurve();
        var scaleY = new AnimationCurve();
        var posY = new AnimationCurve();
        foreach (PoseKey k in keys)
        {
            scaleX.AddKey(k.Time, k.ScaleX);
            scaleY.AddKey(k.Time, k.ScaleY);
            posY.AddKey(k.Time, k.PosY);
        }
        float length = keys[keys.Length - 1].Time;

        SetCurve(clip, bodyPath, "m_LocalScale.x", scaleX);
        SetCurve(clip, bodyPath, "m_LocalScale.y", scaleY);
        SetCurve(clip, bodyPath, "m_LocalScale.z", AnimationCurve.Constant(0f, length, 1f));
        SetCurve(clip, bodyPath, "m_LocalPosition.x", AnimationCurve.Constant(0f, length, 0f));
        SetCurve(clip, bodyPath, "m_LocalPosition.y", posY);
        SetCurve(clip, bodyPath, "m_LocalPosition.z", AnimationCurve.Constant(0f, length, 0f));

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string path = $"{AnimFolder}/{name}.anim";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static void SetCurve(AnimationClip clip, string path, string property, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
    }

    private static void AddTransition(AnimatorState from, AnimatorState to,
        params (AnimatorConditionMode mode, float threshold, string parameter)[] conditions)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.duration = 0.05f;
        foreach (var c in conditions)
        {
            transition.AddCondition(c.mode, c.threshold, c.parameter);
        }
    }

    // =====================================================================
    //  Префабы (заготовки объектов, лежат в Assets/Prefabs)
    // =====================================================================

    private static Prefabs CreatePrefabs(AnimatorController playerAnimator, PhysicsMaterial2D noFriction)
    {
        return new Prefabs
        {
            Player = CreatePlayerPrefab(playerAnimator, noFriction),
            Coin = CreateCoinPrefab(),
            Heart = CreateHeartPrefab(),
            Spike = CreateSpikePrefab(),
            Checkpoint = CreateCheckpointPrefab(),
            Finish = CreateFinishPrefab(),
            Enemy = CreateEnemyPrefab(noFriction),
        };
    }

    private static GameObject CreateEnemyPrefab(PhysicsMaterial2D noFriction)
    {
        var root = new GameObject("Enemy");

        var rb = root.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var collider = root.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.75f, 0.55f);
        collider.edgeRadius = 0.03f; // чуть скруглённые углы — не цепляется за стыки
        collider.sharedMaterial = noFriction;

        // Слизень: фиолетовое тело и глаз (смотрит вправо, скрипт разворачивает Visual).
        Transform visual = CreateChild("Visual", root.transform);
        AddSprite("Body", visual, rounded, EnemyColor, Vector2.zero, new Vector2(0.85f, 0.62f), 8);
        AddSprite("Eye", visual, circle, Color.white, new Vector2(0.18f, 0.08f), new Vector2(0.22f, 0.22f), 9);
        AddSprite("Pupil", visual, circle, new Color(0.1f, 0.1f, 0.15f), new Vector2(0.22f, 0.08f), new Vector2(0.1f, 0.1f), 10);

        var enemy = root.AddComponent<Enemy>();
        SetRef(enemy, "visual", visual);
        return SavePrefab(root, "Enemy");
    }

    private static GameObject CreatePlayerPrefab(AnimatorController animatorController, PhysicsMaterial2D noFriction)
    {
        var root = new GameObject("Player");
        root.tag = "Player";

        // Физика: Rigidbody2D (масса, гравитация, скорость) + коллайдер (форма тела).
        var rb = root.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation; // чтобы не катился как мяч
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // плавное движение
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var collider = root.AddComponent<CapsuleCollider2D>();
        collider.direction = CapsuleDirection2D.Vertical;
        collider.size = new Vector2(0.75f, 0.9f);
        collider.sharedMaterial = noFriction;

        // Visual — для разворота влево/вправо, Body — для анимаций.
        Transform visual = CreateChild("Visual", root.transform);
        Transform body = CreateChild("Body", visual);
        AddSprite("Sprite", body, rounded, PlayerColor, Vector2.zero, new Vector2(0.8f, 0.9f), 10);
        AddSprite("Eye", body, circle, Color.white, new Vector2(0.17f, 0.14f), new Vector2(0.26f, 0.26f), 11);
        AddSprite("Pupil", body, circle, new Color(0.1f, 0.1f, 0.15f), new Vector2(0.21f, 0.14f), new Vector2(0.12f, 0.12f), 12);

        var animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = animatorController;

        var player = root.AddComponent<PlayerController>();
        SetRef(player, "visual", visual);
        SetRef(player, "animator", animator);

        return SavePrefab(root, "Player");
    }

    private static GameObject CreateCoinPrefab()
    {
        var root = new GameObject("Coin");
        var collider = root.AddComponent<CircleCollider2D>();
        collider.isTrigger = true; // триггер: сквозь монету можно пройти, но касание засчитается
        collider.radius = 0.3f;

        Transform visual = CreateChild("Visual", root.transform);
        AddSprite("Sprite", visual, circle, CoinColor, Vector2.zero, new Vector2(0.6f, 0.6f), 5);
        AddSprite("Shine", visual, circle, CoinShineColor, new Vector2(-0.05f, 0.05f), new Vector2(0.3f, 0.3f), 6);

        var coin = root.AddComponent<Coin>();
        SetRef(coin, "visual", visual);
        return SavePrefab(root, "Coin");
    }

    private static GameObject CreateHeartPrefab()
    {
        var root = new GameObject("Heart");
        var collider = root.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.3f;

        // Сердечко из двух кружков и перевёрнутого треугольника.
        Transform visual = CreateChild("Visual", root.transform);
        AddSprite("Left", visual, circle, HeartColor, new Vector2(-0.12f, 0.06f), new Vector2(0.27f, 0.27f), 5);
        AddSprite("Right", visual, circle, HeartColor, new Vector2(0.12f, 0.06f), new Vector2(0.27f, 0.27f), 5);
        SpriteRenderer bottom = AddSprite("Bottom", visual, triangle, HeartColor, new Vector2(0f, -0.1f), new Vector2(0.52f, 0.34f), 5);
        bottom.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);

        var heart = root.AddComponent<Heart>();
        SetRef(heart, "visual", visual);
        return SavePrefab(root, "Heart");
    }

    private static GameObject CreateSpikePrefab()
    {
        var root = new GameObject("Spike");
        var collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(0.7f, 0.35f);   // хитбокс чуть меньше картинки — так честнее
        collider.offset = new Vector2(0f, -0.3f);

        AddSprite("SpikeL", root.transform, triangle, SpikeColor, new Vector2(-0.23f, -0.2f), new Vector2(0.46f, 0.6f), 4);
        AddSprite("SpikeR", root.transform, triangle, SpikeColor, new Vector2(0.23f, -0.2f), new Vector2(0.46f, 0.6f), 4);

        root.AddComponent<Hazard>();
        return SavePrefab(root, "Spike");
    }

    private static GameObject CreateCheckpointPrefab()
    {
        var root = new GameObject("Checkpoint");
        var collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(0.8f, 2f);
        collider.offset = new Vector2(0f, 0.5f);

        AddSprite("Pole", root.transform, square, PoleColor, new Vector2(0f, 0.3f), new Vector2(0.1f, 1.6f), 3);
        SpriteRenderer flag = AddSprite("Flag", root.transform, square, Color.gray, new Vector2(0.28f, 0.9f), new Vector2(0.45f, 0.32f), 3);

        var checkpoint = root.AddComponent<Checkpoint>();
        SetRef(checkpoint, "flag", flag);
        return SavePrefab(root, "Checkpoint");
    }

    private static GameObject CreateFinishPrefab()
    {
        var root = new GameObject("Finish");
        var collider = root.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(1f, 3f);
        collider.offset = new Vector2(0f, 1f);

        AddSprite("Pole", root.transform, square, PoleColor, new Vector2(0f, 0.8f), new Vector2(0.12f, 2.6f), 3);
        AddSprite("Flag", root.transform, square, FinishFlagColor, new Vector2(0.42f, 1.75f), new Vector2(0.7f, 0.5f), 3);
        AddSprite("Base", root.transform, square, new Color(0.3f, 0.3f, 0.35f), new Vector2(0f, -0.4f), new Vector2(0.5f, 0.2f), 3);

        root.AddComponent<Finish>();
        return SavePrefab(root, "Finish");
    }

    private static GameObject SavePrefab(GameObject root, string name)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    // =====================================================================
    //  Сцены
    // =====================================================================

    private static void BuildLevel(string sceneName, string title, string[] map, string nextScene, Prefabs prefabs)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        int rows = map.Length;
        int cols = 0;
        foreach (string line in map) cols = Mathf.Max(cols, line.Length);

        // Символ карты в клетке (r — строка сверху вниз, c — колонка слева направо).
        char At(int r, int c) => r >= 0 && r < rows && c >= 0 && c < map[r].Length ? map[r][c] : ' ';

        Camera cam = CreateCamera();

        Transform level = new GameObject("Level").transform;
        Transform ground = CreateChild("Ground", level);
        Transform platforms = CreateChild("MovingPlatforms", level);
        Transform items = CreateChild("Items", level);
        Transform hazards = CreateChild("Hazards", level);
        Transform decor = CreateChild("Decor", level);

        // 1) Длинные объекты: соседние клетки одного типа сливаем в один объект.
        //    Один длинный коллайдер вместо десяти маленьких — игрок не "спотыкается" на стыках.
        for (int r = 0; r < rows; r++)
        {
            int row = r;
            ForEachRun(cols, c => At(row, c) == '#', (start, end) => CreateGround(ground, start, end, row));
            ForEachRun(cols, c => At(row, c) == '#' && At(row - 1, c) != '#', (start, end) => CreateGrass(ground, start, end, row));
            ForEachRun(cols, c => At(row, c) == '=', (start, end) => CreateMovingPlatform(platforms, start, end, row, new Vector2(4f, 0f)));
            ForEachRun(cols, c => At(row, c) == '~', (start, end) => CreateMovingPlatform(platforms, start, end, row, new Vector2(0f, 4f)));
        }

        // 2) Одиночные объекты.
        Vector3 playerStart = new Vector3(1f, -rows + 3f, 0f);
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                Vector3 pos = Cell(c, r);
                switch (At(r, c))
                {
                    case 'P': playerStart = pos; break;
                    case 'C': Place(prefabs.Coin, items, pos); break;
                    case 'H': Place(prefabs.Heart, items, pos); break;
                    case 'K': Place(prefabs.Checkpoint, items, pos); break;
                    case 'F': Place(prefabs.Finish, items, pos); break;
                    case '^': Place(prefabs.Spike, hazards, pos); break;
                    case 'E': Place(prefabs.Enemy, hazards, pos); break;
                }
            }
        }

        // 3) Яма: невидимая зона-триггер под всем уровнем. Упал — потерял жизнь.
        var killZone = new GameObject("KillZone (яма)");
        killZone.transform.SetParent(hazards, false);
        killZone.transform.position = new Vector3(cols / 2f, -rows - 2f, 0f);
        var killCollider = killZone.AddComponent<BoxCollider2D>();
        killCollider.isTrigger = true;
        killCollider.size = new Vector2(cols + 40f, 2f);
        Hazard killHazard = killZone.AddComponent<Hazard>();
        SetBool(killHazard, "ignoreInvulnerability", true);

        // Невидимые стены по краям, чтобы не убежать за уровень.
        CreateWall(level, -1f, rows);
        CreateWall(level, cols, rows);

        CreateBackground(decor, cols, rows);

        // 4) Игрок, камера, GameManager и интерфейс.
        GameObject player = Place(prefabs.Player, null, playerStart);
        cam.transform.position = new Vector3(playerStart.x, playerStart.y, -10f);

        var follow = cam.gameObject.AddComponent<CameraFollow>();
        SetRef(follow, "target", player.transform);
        SetVector2(follow, "minBounds", new Vector2(-0.5f, -rows + 0.5f));
        SetVector2(follow, "maxBounds", new Vector2(cols - 0.5f, 4f));

        var gameManager = new GameObject("GameManager").AddComponent<GameManager>();
        GameUI ui = BuildLevelUI(gameManager);
        SetRef(gameManager, "player", player.GetComponent<PlayerController>());
        SetRef(gameManager, "ui", ui);
        SetString(gameManager, "levelName", title);
        SetString(gameManager, "nextLevelScene", nextScene);

        EditorSceneManager.SaveScene(scene, ScenePath(sceneName));
    }

    private static void BuildMainMenu(Prefabs prefabs)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Camera cam = CreateCamera();
        cam.transform.position = new Vector3(0f, 0f, -10f);

        // Немного декораций: земля, облака, крутящиеся монеты.
        Transform decor = new GameObject("Decor").transform;
        AddSprite("Ground", decor, square, GroundColor, new Vector2(0f, -5.5f), new Vector2(40f, 2f), 0);
        AddSprite("Grass", decor, square, GrassColor, new Vector2(0f, -4.6f), new Vector2(40f, 0.2f), 1);
        for (int i = -4; i <= 4; i++)
        {
            Place(prefabs.Coin, decor, new Vector3(i * 2f, -3.6f, 0f));
        }
        CreateClouds(decor, 24, 3, -12f);

        var menu = new GameObject("MainMenu").AddComponent<MainMenu>();

        Canvas canvas = CreateCanvas();
        Transform ct = canvas.transform;
        Text records = CreateText(ct, "RecordsText", "", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
            new Vector2(0f, -230f), new Vector2(1400f, 100f), CoinShineColor);
        SetRef(menu, "recordsText", records);
        CreateText(ct, "Title", "Собиратель монет", 120, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 280f), new Vector2(1600f, 180f), CoinColor);
        CreateText(ct, "Subtitle", "2D-платформер", 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 170f), new Vector2(1200f, 70f), Color.white);
        CreateButton(ct, "PlayButton", "Играть", new Vector2(0f, 20f), menu.Play);
        CreateButton(ct, "QuitButton", "Выход", new Vector2(0f, -100f), menu.Quit);
        CreateText(ct, "Controls", "A/D или ←/→ — бег     Пробел / W — прыжок (держи — выше)     Esc — пауза", 30,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1800f, 50f), Color.white);

        EditorSceneManager.SaveScene(scene, ScenePath("MainMenu"));
    }

    private static GameUI BuildLevelUI(GameManager gm)
    {
        Canvas canvas = CreateCanvas();
        Transform ct = canvas.transform;
        var ui = canvas.gameObject.AddComponent<GameUI>();

        // HUD — счётчики в углу экрана.
        Text coins = CreateText(ct, "CoinsText", "Монеты: 0", 48, TextAnchor.UpperLeft, new Vector2(0f, 1f),
            new Vector2(40f, -30f), new Vector2(800f, 70f), CoinColor);
        Text lives = CreateText(ct, "LivesText", "Жизни: 3", 48, TextAnchor.UpperLeft, new Vector2(0f, 1f),
            new Vector2(40f, -95f), new Vector2(800f, 70f), new Color(1f, 0.45f, 0.5f));
        Text levelName = CreateText(ct, "LevelText", "Уровень", 44, TextAnchor.UpperCenter, new Vector2(0.5f, 1f),
            new Vector2(0f, -30f), new Vector2(800f, 70f), Color.white);
        Text timer = CreateText(ct, "TimerText", "Время: 0:00.0", 44, TextAnchor.UpperRight, new Vector2(1f, 1f),
            new Vector2(-40f, -30f), new Vector2(600f, 70f), Color.white);
        CreateText(ct, "HintText", "A/D — бег   Пробел — прыжок   Esc — пауза", 28, TextAnchor.LowerCenter,
            new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1600f, 50f), new Color(1f, 1f, 1f, 0.8f));

        // Экран паузы.
        GameObject pause = CreatePanel(ct, "PausePanel", new Color(0f, 0f, 0.1f, 0.7f));
        CreateText(pause.transform, "Title", "Пауза", 110, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 230f), new Vector2(1400f, 160f), Color.white);
        CreateButton(pause.transform, "ResumeButton", "Продолжить", new Vector2(0f, 60f), gm.Resume);
        CreateButton(pause.transform, "RestartButton", "Начать заново", new Vector2(0f, -60f), gm.RestartLevel);
        CreateButton(pause.transform, "MenuButton", "В главное меню", new Vector2(0f, -180f), gm.GoToMainMenu);

        // Экран Game Over.
        GameObject gameOver = CreatePanel(ct, "GameOverPanel", new Color(0.15f, 0f, 0f, 0.8f));
        CreateText(gameOver.transform, "Title", "GAME OVER", 120, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 230f), new Vector2(1400f, 160f), new Color(1f, 0.35f, 0.35f));
        Text gameOverInfo = CreateText(gameOver.transform, "Info", "", 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 90f), new Vector2(1400f, 140f), Color.white);
        CreateButton(gameOver.transform, "RestartButton", "Начать заново", new Vector2(0f, -50f), gm.RestartLevel);
        CreateButton(gameOver.transform, "MenuButton", "В главное меню", new Vector2(0f, -170f), gm.GoToMainMenu);

        // Экран победы.
        GameObject win = CreatePanel(ct, "WinPanel", new Color(0f, 0.12f, 0.05f, 0.8f));
        Text winTitle = CreateText(win.transform, "Title", "Победа!", 110, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 230f), new Vector2(1400f, 160f), CoinColor);
        Text winInfo = CreateText(win.transform, "Info", "", 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
            new Vector2(0f, 90f), new Vector2(1400f, 140f), Color.white);
        Button next = CreateButton(win.transform, "NextLevelButton", "Следующий уровень", new Vector2(0f, -50f), gm.LoadNextLevel);
        Button again = CreateButton(win.transform, "PlayAgainButton", "Играть снова", new Vector2(0f, -50f), gm.PlayAgain);
        CreateButton(win.transform, "MenuButton", "В главное меню", new Vector2(0f, -170f), gm.GoToMainMenu);

        SetRef(ui, "coinsText", coins);
        SetRef(ui, "livesText", lives);
        SetRef(ui, "levelText", levelName);
        SetRef(ui, "gameOverPanel", gameOver);
        SetRef(ui, "gameOverInfoText", gameOverInfo);
        SetRef(ui, "winPanel", win);
        SetRef(ui, "winTitleText", winTitle);
        SetRef(ui, "winInfoText", winInfo);
        SetRef(ui, "nextLevelButton", next.gameObject);
        SetRef(ui, "playAgainButton", again.gameObject);
        SetRef(ui, "timerText", timer);
        SetRef(ui, "pausePanel", pause);

        pause.SetActive(false);
        gameOver.SetActive(false);
        win.SetActive(false);
        return ui;
    }

    // =====================================================================
    //  Строительные блоки уровня
    // =====================================================================

    // Клетка карты → координаты в мире. Колонка = X, строка идёт вниз, поэтому Y = -строка.
    private static Vector3 Cell(int c, int r) => new Vector3(c, -r, 0f);

    private static void ForEachRun(int cols, Func<int, bool> match, Action<int, int> onRun)
    {
        int c = 0;
        while (c < cols)
        {
            if (!match(c)) { c++; continue; }
            int start = c;
            while (c < cols && match(c)) c++;
            onRun(start, c - 1);
        }
    }

    private static void CreateGround(Transform parent, int start, int end, int row)
    {
        float width = end - start + 1;
        SpriteRenderer sr = AddSprite("Ground", parent, square, GroundColor,
            new Vector2((start + end) / 2f, -row), new Vector2(width, 1f), 0);
        var collider = sr.gameObject.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one; // коллайдер растягивается вместе с масштабом объекта
    }

    private static void CreateGrass(Transform parent, int start, int end, int row)
    {
        float width = end - start + 1;
        AddSprite("Grass", parent, square, GrassColor,
            new Vector2((start + end) / 2f, -row + 0.4f), new Vector2(width, 0.2f), 1);
    }

    private static void CreateMovingPlatform(Transform parent, int start, int end, int row, Vector2 offset)
    {
        float width = end - start + 1;
        var go = new GameObject("MovingPlatform");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((start + end) / 2f, -row + 0.25f, 0f);

        AddSprite("Sprite", go.transform, square, PlatformColor, Vector2.zero, new Vector2(width, 0.5f), 2);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var collider = go.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(width, 0.5f);

        MovingPlatform platform = go.AddComponent<MovingPlatform>();
        SetVector2(platform, "moveOffset", offset);
    }

    private static void CreateWall(Transform parent, float x, int rows)
    {
        var wall = new GameObject("InvisibleWall");
        wall.transform.SetParent(parent, false);
        wall.transform.position = new Vector3(x, 0f, 0f);
        var collider = wall.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1f, rows * 4f);
    }

    // Фон из трёх слоёв с параллаксом: дальние холмы, ближние холмы, облака.
    private static void CreateBackground(Transform parent, int cols, int rows)
    {
        float groundTop = -rows + 2.5f; // верхний край земли (нижние 2 ряда карты — земля)
        var random = new System.Random(cols);

        Transform far = CreateParallaxLayer("FarHills (параллакс 0.8)", parent, 0.8f);
        for (float x = -15f; x < cols + 15f; x += 7 + random.Next(5))
        {
            float size = 9 + random.Next(6);
            AddSprite("Hill", far, circle, FarHillColor, new Vector2(x, groundTop - 1f), new Vector2(size, size * 0.75f), -30);
        }

        Transform near = CreateParallaxLayer("NearHills (параллакс 0.5)", parent, 0.5f);
        for (float x = -15f; x < cols + 15f; x += 5 + random.Next(4))
        {
            float size = 5 + random.Next(4);
            AddSprite("Hill", near, circle, NearHillColor, new Vector2(x, groundTop - 1.5f), new Vector2(size, size * 0.8f), -20);
        }

        Transform clouds = CreateParallaxLayer("Clouds (параллакс 0.9)", parent, 0.9f);
        CreateClouds(clouds, cols + 20, 7 + cols, -10f);
    }

    private static Transform CreateParallaxLayer(string name, Transform parent, float factor)
    {
        Transform layer = CreateChild(name, parent);
        Parallax parallax = layer.gameObject.AddComponent<Parallax>();
        SetFloat(parallax, "factor", factor);
        return layer;
    }

    private static void CreateClouds(Transform parent, int width, int seed, float startX = 2f)
    {
        var random = new System.Random(seed);
        var cloudColor = new Color(1f, 1f, 1f, 0.9f);
        for (float x = startX; x < startX + width; x += 8 + random.Next(6))
        {
            float y = 2f - random.Next(5);
            Transform cloud = CreateChild("Cloud", parent);
            cloud.position = new Vector3(x, y, 0f);
            AddSprite("Part", cloud, circle, cloudColor, Vector2.zero, new Vector2(1.6f, 1.2f), -10);
            AddSprite("Part", cloud, circle, cloudColor, new Vector2(-0.8f, -0.2f), new Vector2(1.1f, 0.9f), -10);
            AddSprite("Part", cloud, circle, cloudColor, new Vector2(0.85f, -0.15f), new Vector2(1.2f, 0.95f), -10);
        }
    }

    private static Camera CreateCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;      // 2D-камера без перспективы
        cam.orthographicSize = 6f;    // видно 12 клеток по высоте
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = SkyColor;
        go.AddComponent<AudioListener>();
        return cam;
    }

    // =====================================================================
    //  UI-помощники
    // =====================================================================

    private static Canvas CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; // UI одинаковый на любом разрешении
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>(); // чтобы по кнопкам можно было кликать

        // EventSystem обрабатывает клики мышью/клавиатуру для UI.
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        return canvas;
    }

    private static Text CreateText(Transform parent, string name, string text, int fontSize, TextAnchor alignment,
        Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        var label = go.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(2f, -2f);
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 position, UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(480f, 95f);

        var image = go.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = ButtonColor;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        // Persistent listener — то же самое, что вручную добавить функцию в On Click () в Inspector.
        UnityEventTools.AddPersistentListener(button.onClick, onClick);

        Text text = CreateText(go.transform, "Text", label, 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
            Vector2.zero, rt.sizeDelta, Color.white);
        text.raycastTarget = false;
        return button;
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = color;
        return go;
    }

    // =====================================================================
    //  Общие помощники
    // =====================================================================

    private static Transform CreateChild(string name, Transform parent)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static SpriteRenderer AddSprite(string name, Transform parent, Sprite sprite, Color color,
        Vector2 localPosition, Vector2 scale, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder; // кто рисуется поверх: больше = ближе к нам
        return sr;
    }

    private static GameObject Place(GameObject prefab, Transform parent, Vector3 position)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.position = position;
        return instance;
    }

    private static string ScenePath(string sceneName) => $"{SceneFolder}/{sceneName}.unity";

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // Наши поля помечены [SerializeField] private — из другого класса их не изменить напрямую.
    // SerializedObject — это "как Inspector": меняет сохраняемые поля любого компонента.
    private static void SetRef(Object target, string field, Object value) => Modify(target, field, p => p.objectReferenceValue = value);
    private static void SetString(Object target, string field, string value) => Modify(target, field, p => p.stringValue = value);
    private static void SetBool(Object target, string field, bool value) => Modify(target, field, p => p.boolValue = value);
    private static void SetFloat(Object target, string field, float value) => Modify(target, field, p => p.floatValue = value);
    private static void SetVector2(Object target, string field, Vector2 value) => Modify(target, field, p => p.vector2Value = value);

    private static void Modify(Object target, string field, Action<SerializedProperty> apply)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"[PlatformerBuilder] Поле '{field}' не найдено в {target.GetType().Name}");
            return;
        }
        apply(property);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

/// <summary>
/// Помощник при открытии проекта:
///   - если сцены уже есть, а открыта пустая "Untitled", открывает главное меню;
///   - если сцен ещё нет, предлагает собрать их автоматически.
/// [InitializeOnLoad] = этот код выполняется каждый раз, когда Unity загружает скрипты.
/// </summary>
[InitializeOnLoad]
internal static class PlatformerAutoBuild
{
    private const string CheckedKey = "Platformer.AutoBuildChecked";
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";

    static PlatformerAutoBuild()
    {
        if (Application.isBatchMode) return;
        if (SessionState.GetBool(CheckedKey, false)) return;
        SessionState.SetBool(CheckedKey, true);

        EditorApplication.delayCall += () =>
        {
            if (File.Exists(MainMenuPath))
            {
                var active = EditorSceneManager.GetActiveScene();
                bool isUntitled = string.IsNullOrEmpty(active.path);
                if (isUntitled && !active.isDirty && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorSceneManager.OpenScene(MainMenuPath);
                }
                return;
            }
            if (EditorUtility.DisplayDialog("Собиратель монет",
                    "Сцены игры ещё не созданы.\nСобрать их сейчас? (это займёт несколько секунд)",
                    "Да, собрать", "Позже"))
            {
                PlatformerBuilder.BuildAll();
            }
        };
    }
}
