using System.Collections;
using UnityEngine;

/// <summary>
/// Управление игроком: бег с разгоном, прыжок (только с земли), рывок,
/// анимации, эффекты, возрождение, мигание после урона и сила звезды.
///
/// Как это устроено:
///   Update()      — каждый кадр читаем клавиатуру и запоминаем, что нажато;
///   FixedUpdate() — с постоянным шагом физики меняем скорость Rigidbody2D.
/// Делить так правильно: ввод "ловится" каждый кадр (иначе нажатия теряются),
/// а физика считается стабильно независимо от FPS.
///
/// Приёмы, которые делают управление "приятным" (так делают почти во всех платформерах):
///   1) Разгон и торможение — скорость меняется не мгновенно, а быстро нарастает.
///      В воздухе разгон слабее, чем на земле, а разворот — быстрее обычного разгона.
///   2) Coyote time — ещё 0.1 сек после схода с края можно прыгнуть.
///   3) Jump buffer — если нажал прыжок чуть-чуть ДО приземления, прыжок всё равно засчитается.
///   4) Переменная высота — отпустил пробел раньше → прыжок ниже. Держишь → выше.
///   5) "Зависание" в верхней точке прыжка — там гравитация слабее, легче прицелиться.
///   6) Предел скорости падения — с большой высоты не разгоняешься бесконечно.
///   7) Коррекция углов — задел головой край платформы? Игрока чуть сдвигает вбок,
///      и прыжок не обрывается.
///   8) Инерция платформы — прыгнул с едущей платформы и полетел вместе с её скоростью.
/// Важно: прыгнуть по-прежнему можно ТОЛЬКО от земли — двойного прыжка нет.
/// Рывок (Shift) — это не прыжок: он только двигает вбок, один раз до следующего касания земли.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Бег")]
    [SerializeField] private float moveSpeed = 7f;
    [Tooltip("Разгон на земле (клеток/сек за секунду)")]
    [SerializeField] private float groundAcceleration = 70f;
    [Tooltip("Торможение на земле, когда клавиши отпущены")]
    [SerializeField] private float groundDeceleration = 90f;
    [Tooltip("Разгон в воздухе — меньше, чем на земле: в полёте сложнее менять направление")]
    [SerializeField] private float airAcceleration = 45f;
    [SerializeField] private float airDeceleration = 20f;
    [Tooltip("Во сколько раз быстрее меняется скорость при развороте")]
    [SerializeField] private float turnMultiplier = 1.6f;

    [Header("Прыжок")]
    [Tooltip("Вертикальная скорость в момент прыжка")]
    [SerializeField] private float jumpForce = 15f;
    [Tooltip("Сколько секунд после схода с края ещё можно прыгнуть")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("За сколько секунд до приземления можно нажать прыжок заранее")]
    [SerializeField] private float jumpBufferTime = 0.12f;
    [Tooltip("Во сколько раз гасится скорость вверх, если отпустить прыжок раньше")]
    [SerializeField] private float jumpCutMultiplier = 0.5f;
    [Tooltip("Во сколько раз сильнее гравитация при падении — прыжок менее 'ватный'")]
    [SerializeField] private float fallGravityMultiplier = 1.4f;
    [Tooltip("Гравитация около верхней точки прыжка (пока прыжок зажат) — короткое 'зависание'")]
    [SerializeField] private float apexGravityMultiplier = 0.6f;
    [Tooltip("Насколько близко к нулю должна быть скорость Y, чтобы считать это верхней точкой")]
    [SerializeField] private float apexThreshold = 2f;
    [Tooltip("Максимальная скорость падения")]
    [SerializeField] private float maxFallSpeed = 18f;
    [Tooltip("На сколько клеток можно сдвинуть игрока вбок, если он задел головой угол")]
    [SerializeField] private float cornerCorrection = 0.3f;

    [Header("Рывок (Shift)")]
    [SerializeField] private float dashSpeed = 17f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 0.35f;
    [SerializeField] private Color dashTrailColor = new Color(1f, 0.6f, 0.3f, 0.5f);

    [Header("Эффекты")]
    [SerializeField] private Color dustColor = new Color(0.95f, 0.92f, 0.85f, 0.9f);

    [Header("Части персонажа")]
    [Tooltip("Объект с картинкой — его разворачиваем влево/вправо и сплющиваем")]
    [SerializeField] private Transform visual;
    [SerializeField] private Animator animator;

    // Имена параметров в Animator Controller (Assets/Animations/Player.controller).
    // StringToHash переводит строку в число — так Unity работает быстрее.
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int GroundedParam = Animator.StringToHash("IsGrounded");
    private static readonly int VelocityYParam = Animator.StringToHash("VelocityY");

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private ContactFilter2D groundFilter;
    private ContactFilter2D solidFilter;
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[8];
    private readonly Collider2D[] overlapHits = new Collider2D[8];
    private float baseGravity;

    private float moveInput;          // -1 влево, 0 стоим, 1 вправо
    private float facing = 1f;        // куда смотрит игрок: -1 или 1
    private bool jumpHeld;            // кнопка прыжка сейчас зажата
    private float jumpBufferTimer;    // > 0 — недавно нажали прыжок
    private float coyoteTimer;        // > 0 — недавно стояли на земле
    private bool canCutJump;          // можно "срезать" текущий прыжок, отпустив кнопку
    private bool isGrounded;
    private float lowestAirSpeed;     // самая большая скорость падения за полёт (для силы приземления)
    private bool controlsEnabled = true;

    private bool dashRequested;
    private bool canDash = true;      // рывок восстанавливается при касании земли
    private float dashTimer;          // > 0 — сейчас идёт рывок
    private float dashCooldownTimer;
    private float dashDirection;

    private float squash;             // > 0 сплющен (приземление), < 0 вытянут (прыжок)
    private float invulnerableUntil;
    private float starUntil;
    private MovingPlatform currentPlatform;
    private float carriedPlatformX;   // скорость платформы, которую прибавили на прошлом шаге
    private Coroutine blinkRoutine;
    private Coroutine starRoutine;

    /// <summary>Игрок неуязвим: мигает после урона или под действием звезды.</summary>
    public bool IsInvulnerable => Time.time < invulnerableUntil || IsStarPowered;

    /// <summary>Действует звезда: шипы не страшны, враги погибают от касания.</summary>
    public bool IsStarPowered => Time.time < starUntil;

    /// <summary>Сколько секунд осталось действовать звезде (0 — не действует).</summary>
    public float StarTimeLeft => Mathf.Max(0f, starUntil - Time.time);

    /// <summary>Скорость игрока (её читают враги и батуты).</summary>
    public Vector2 Velocity
    {
        // В Unity 6 свойство velocity переименовали в linearVelocity.
        // #if выбирает нужный вариант в зависимости от версии Unity.
#if UNITY_6000_0_OR_NEWER
        get => rb.linearVelocity;
        private set => rb.linearVelocity = value;
#else
        get => rb.velocity;
        private set => rb.velocity = value;
#endif
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) originalColors[i] = renderers[i].color;
        baseGravity = rb.gravityScale;

        // Фильтр "земли": касания твёрдых коллайдеров (не триггеров),
        // у которых нормаль смотрит вверх (угол 45°..135°).
        // Т.е. мы стоим НА чём-то, а не трёмся боком о стену.
        groundFilter.useTriggers = false;
        groundFilter.SetNormalAngle(45f, 135f);

        // Фильтр для проверок "есть ли что-то твёрдое рядом" (коррекция углов).
        solidFilter.useTriggers = false;
    }

    private void Update()
    {
        if (!controlsEnabled)
        {
            moveInput = 0f;
            jumpHeld = false;
            UpdateVisual();
            UpdateAnimation();
            return;
        }

        // "Horizontal" — стандартная ось: A/D и стрелки ←/→.
        moveInput = Input.GetAxisRaw("Horizontal");

        // "Jump" — стандартная кнопка: Пробел. Плюс W и стрелка вверх.
        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            jumpBufferTimer = jumpBufferTime; // запоминаем нажатие на короткое время
        }
        jumpHeld = Input.GetButton("Jump") || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);

        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift) || Input.GetKeyDown(KeyCode.X))
        {
            dashRequested = true;
        }

        // Поворачиваемся в сторону движения (во время рывка — не крутимся).
        if (moveInput != 0f && dashTimer <= 0f) facing = Mathf.Sign(moveInput);

        UpdateVisual();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector2 velocity = Velocity;
        Vector2 platformVelocity = currentPlatform != null ? currentPlatform.Velocity : Vector2.zero;

        // 1) Стоим ли на земле? (и не летим вверх после прыжка — тогда скорость вверх большая)
        bool wasGrounded = isGrounded;
        isGrounded = rb.IsTouching(groundFilter) && velocity.y - platformVelocity.y <= 0.5f;
        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
            canDash = true;
            if (!wasGrounded) OnLanded(-lowestAirSpeed);
            lowestAirSpeed = 0f;
        }
        else
        {
            coyoteTimer -= dt;
            lowestAirSpeed = Mathf.Min(lowestAirSpeed, velocity.y);
        }
        jumpBufferTimer -= dt;
        dashCooldownTimer -= dt;

        // 2) "Своя" горизонтальная скорость — без скорости платформы.
        //    Пока стоим на платформе, вычитаем то, что она добавила на прошлом шаге.
        //    Сошли или спрыгнули — её скорость остаётся нашей (инерция).
        float ownX = currentPlatform != null ? velocity.x - carriedPlatformX : velocity.x;

        // 3) Рывок: короткий быстрый полёт по прямой, гравитация на это время выключена.
        if (dashRequested)
        {
            dashRequested = false;
            if (canDash && dashCooldownTimer <= 0f && controlsEnabled) StartDash();
        }

        bool dashing = dashTimer > 0f;
        if (dashing)
        {
            dashTimer -= dt;
            ownX = dashDirection * dashSpeed;
            velocity.y = 0f;
            Effects.Puff(rb.position, dashTrailColor, 0.8f, 0.2f);
        }
        else
        {
            // Разгон к нужной скорости. MoveTowards меняет значение не больше чем на шаг.
            float target = moveInput * moveSpeed;
            bool hasInput = Mathf.Abs(target) > 0.01f;
            float accel = isGrounded
                ? (hasInput ? groundAcceleration : groundDeceleration)
                : (hasInput ? airAcceleration : airDeceleration);
            bool turning = hasInput && Mathf.Abs(ownX) > 0.1f && Mathf.Sign(target) != Mathf.Sign(ownX);
            if (turning) accel *= turnMultiplier;
            ownX = Mathf.MoveTowards(ownX, target, accel * dt);
        }

        // 4) Прыжок: нажат недавно И недавно стояли на земле. Прыжок прерывает рывок.
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            velocity.y = jumpForce + Mathf.Max(platformVelocity.y, 0f);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;   // второй раз в воздухе не прыгнешь
            canCutJump = true;
            dashTimer = 0f;
            dashing = false;
            squash = -0.2f;     // вытягиваемся вверх
            Effects.Dust(FeetPosition, 4, dustColor);
            SoundFX.Play(Sfx.Jump);
        }

        velocity.x = ownX + platformVelocity.x;
        carriedPlatformX = platformVelocity.x;

        // 5) Отпустил кнопку, пока летим вверх → прыжок ниже.
        if (canCutJump && !jumpHeld && velocity.y > 0f)
        {
            velocity.y *= jumpCutMultiplier;
            canCutJump = false;
        }
        if (velocity.y <= 0f) canCutJump = false;

        // 6) Гравитация: во время рывка нет, в верхней точке слабее, при падении сильнее.
        if (dashing) rb.gravityScale = 0f;
        else if (!isGrounded && jumpHeld && Mathf.Abs(velocity.y) < apexThreshold) rb.gravityScale = baseGravity * apexGravityMultiplier;
        else if (velocity.y < 0f) rb.gravityScale = baseGravity * fallGravityMultiplier;
        else rb.gravityScale = baseGravity;

        // 7) Предел скорости падения.
        if (velocity.y < -maxFallSpeed) velocity.y = -maxFallSpeed;

        // 8) Летим вверх и вот-вот ударимся головой об угол → чуть сдвигаемся вбок.
        if (velocity.y > 0f && !isGrounded) TryCornerCorrection(velocity.y * dt);

        Velocity = velocity;
    }

    private Vector2 FeetPosition => new Vector2(rb.position.x, bodyCollider.bounds.min.y);

    private void StartDash()
    {
        dashDirection = moveInput != 0f ? Mathf.Sign(moveInput) : facing;
        facing = dashDirection;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
        canDash = false;   // следующий рывок — только после касания земли
        canCutJump = false;
        SoundFX.Play(Sfx.Dash);
    }

    // Приземление: чем сильнее упали, тем сильнее сплющивает и больше пыли.
    private void OnLanded(float impactSpeed)
    {
        if (impactSpeed < 4f) return;
        float strength = Mathf.InverseLerp(4f, maxFallSpeed, impactSpeed);
        squash = Mathf.Lerp(0.1f, 0.3f, strength);
        Effects.Dust(FeetPosition, 3 + Mathf.RoundToInt(strength * 6f), dustColor);
    }

    // --- Коррекция углов -------------------------------------------------

    private void TryCornerCorrection(float distance)
    {
        Bounds b = bodyCollider.bounds;
        var stripSize = new Vector2(b.size.x * 0.9f, 0.05f);       // тонкая полоска на макушке
        var stripCenter = new Vector2(b.center.x, b.max.y - 0.03f);
        float castDistance = distance + 0.03f;

        if (!SolidAbove(stripCenter, stripSize, castDistance)) return; // над головой пусто

        // Пробуем сдвинуться вбок понемногу: 0.05, 0.10 ... до cornerCorrection.
        for (float shift = 0.05f; shift <= cornerCorrection + 0.001f; shift += 0.05f)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var offset = new Vector2(side * shift, 0f);
                if (SolidAbove(stripCenter + offset, stripSize, castDistance)) continue; // всё ещё ударимся
                if (BodyBlocked(b, offset)) continue;                                   // сдвинемся в стену
                rb.position += offset;
                return;
            }
        }
    }

    private bool SolidAbove(Vector2 center, Vector2 size, float distance)
    {
        int count = Physics2D.BoxCast(center, size, 0f, Vector2.up, solidFilter, castHits, distance);
        for (int i = 0; i < count; i++)
        {
            if (IsLevelGeometry(castHits[i].collider)) return true;
        }
        return false;
    }

    private bool BodyBlocked(Bounds b, Vector2 offset)
    {
        int count = Physics2D.OverlapBox((Vector2)b.center + offset, (Vector2)b.size * 0.95f, 0f, solidFilter, overlapHits);
        for (int i = 0; i < count; i++)
        {
            if (IsLevelGeometry(overlapHits[i])) return true;
        }
        return false;
    }

    // Земля и платформы — да. Сам игрок и враги — нет.
    private bool IsLevelGeometry(Collider2D other)
    {
        if (other == bodyCollider) return false;
        if (other.GetComponent<EnemyBase>() != null) return false;
        Rigidbody2D body = other.attachedRigidbody;
        return body == null || body.bodyType != RigidbodyType2D.Dynamic;
    }

    // --- Внешний вид ----------------------------------------------------

    private void UpdateVisual()
    {
        // Сплющивание плавно возвращается к нормальной форме.
        squash = Mathf.Lerp(squash, 0f, 12f * Time.deltaTime);
        if (visual == null) return;

        // Сплющиваемся относительно ног: опускаем картинку на половину потерянной высоты.
        visual.localScale = new Vector3(facing * (1f + squash), 1f - squash, 1f);
        visual.localPosition = new Vector3(0f, -squash * 0.45f, 0f);
    }

    private void UpdateAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        animator.SetFloat(SpeedParam, Mathf.Abs(moveInput));
        animator.SetBool(GroundedParam, isGrounded);
        animator.SetFloat(VelocityYParam, Velocity.y);
    }

    // --- Движущиеся платформы -------------------------------------------
    // OnCollisionEnter2D / Stay2D / Exit2D вызывает Unity, когда наш
    // твёрдый коллайдер начинает / продолжает / перестаёт касаться другого.

    private void OnCollisionEnter2D(Collision2D collision) => CheckPlatform(collision);

    private void OnCollisionStay2D(Collision2D collision) => CheckPlatform(collision);

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (currentPlatform != null && collision.gameObject == currentPlatform.gameObject)
        {
            currentPlatform = null;
        }
    }

    private void CheckPlatform(Collision2D collision)
    {
        if (!collision.gameObject.TryGetComponent(out MovingPlatform platform)) return;

        // Нормаль контакта смотрит вверх → мы стоим СВЕРХУ на платформе.
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                currentPlatform = platform;
                return;
            }
        }
    }

    // --- Методы, которые вызывают другие скрипты ------------------------

    /// <summary>Подбросить игрока вверх (когда он прыгнул на врага).</summary>
    public void Bounce(float force)
    {
        Velocity = new Vector2(Velocity.x, force);
        canCutJump = true; // держишь прыжок — отскок выше
        canDash = true;    // после отскока снова можно сделать рывок
        dashTimer = 0f;
        squash = -0.2f;
    }

    /// <summary>Батут: подбрасывает на фиксированную высоту (отпускание прыжка не срезает).</summary>
    public void Launch(float force)
    {
        Velocity = new Vector2(Velocity.x, force);
        canCutJump = false;
        canDash = true;
        dashTimer = 0f;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        squash = -0.3f;
        Effects.Dust(FeetPosition, 6, dustColor);
    }

    /// <summary>Звезда: на duration секунд игрок неуязвим и побеждает врагов касанием.</summary>
    public void GiveStarPower(float duration)
    {
        starUntil = Time.time + duration;
        if (starRoutine != null) StopCoroutine(starRoutine);
        starRoutine = StartCoroutine(StarGlow());
    }

    /// <summary>Переносит игрока в точку возрождения и даёт время неуязвимости.</summary>
    public void Respawn(Vector3 position, float invulnerabilityTime)
    {
        rb.position = position;
        transform.position = position;
        Velocity = Vector2.zero;
        currentPlatform = null;
        carriedPlatformX = 0f;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        dashTimer = 0f;
        canDash = true;
        squash = 0f;

        invulnerableUntil = Time.time + invulnerabilityTime;
        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        blinkRoutine = StartCoroutine(Blink(invulnerabilityTime));
    }

    public void SetControlsEnabled(bool isEnabled)
    {
        controlsEnabled = isEnabled;
        if (!isEnabled)
        {
            dashTimer = 0f;
            dashRequested = false;
            Velocity = new Vector2(0f, Velocity.y);
        }
    }

    // Корутина — метод, который может "ставить себя на паузу" (yield)
    // и продолжаться в следующих кадрах. Удобно для эффектов во времени.
    private IEnumerator Blink(float duration)
    {
        float endTime = Time.time + duration;
        bool visible = true;
        while (Time.time < endTime)
        {
            visible = !visible;
            SetVisible(visible);
            yield return new WaitForSeconds(0.1f);
        }
        SetVisible(true);
        blinkRoutine = null;
    }

    // Пока действует звезда, игрок переливается всеми цветами радуги.
    // В последние 1.5 секунды мерцает — предупреждение, что сила кончается.
    private IEnumerator StarGlow()
    {
        while (IsStarPowered)
        {
            bool flicker = StarTimeLeft < 1.5f && Mathf.FloorToInt(Time.time * 10f) % 2 == 0;
            Color rainbow = Color.HSVToRGB(Mathf.Repeat(Time.time * 2f, 1f), 0.6f, 1f);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].color = flicker ? originalColors[i] : Color.Lerp(originalColors[i], rainbow, 0.55f);
            }
            yield return null; // ждём следующего кадра
        }
        for (int i = 0; i < renderers.Length; i++) renderers[i].color = originalColors[i];
        starRoutine = null;
    }

    private void SetVisible(bool visible)
    {
        foreach (SpriteRenderer sr in renderers)
        {
            sr.enabled = visible;
        }
    }
}
