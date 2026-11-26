using System.Collections;
using UnityEngine;

/// <summary>
/// Управление игроком: бег влево/вправо, прыжок (только с земли),
/// анимации, возрождение и мигание после урона.
///
/// Как это устроено:
///   Update()      — каждый кадр читаем клавиатуру и запоминаем, что нажато;
///   FixedUpdate() — с постоянным шагом физики меняем скорость Rigidbody2D.
/// Делить так правильно: ввод "ловится" каждый кадр (иначе нажатия теряются),
/// а физика считается стабильно независимо от FPS.
///
/// Три приёма, которые делают прыжок "приятным" (так делают почти во всех платформерах):
///   1) Coyote time — ещё 0.1 сек после схода с края можно прыгнуть
///      (как койот из мультика, который бежит по воздуху, пока не посмотрит вниз).
///   2) Jump buffer — если нажал прыжок чуть-чуть ДО приземления, прыжок всё равно засчитается.
///   3) Переменная высота — отпустил пробел раньше → прыжок ниже. Держишь → выше.
/// Важно: прыгнуть по-прежнему можно ТОЛЬКО от земли — двойного прыжка нет.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    [SerializeField] private float moveSpeed = 7f;
    [Tooltip("Вертикальная скорость в момент прыжка")]
    [SerializeField] private float jumpForce = 15f;

    [Header("Удобство прыжка")]
    [Tooltip("Сколько секунд после схода с края ещё можно прыгнуть")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("За сколько секунд до приземления можно нажать прыжок заранее")]
    [SerializeField] private float jumpBufferTime = 0.1f;
    [Tooltip("Во сколько раз гасится скорость вверх, если отпустить прыжок раньше")]
    [SerializeField] private float jumpCutMultiplier = 0.5f;
    [Tooltip("Во сколько раз сильнее гравитация при падении — прыжок менее 'ватный'")]
    [SerializeField] private float fallGravityMultiplier = 1.4f;

    [Header("Части персонажа")]
    [Tooltip("Объект с картинкой — его разворачиваем влево/вправо")]
    [SerializeField] private Transform visual;
    [SerializeField] private Animator animator;

    // Имена параметров в Animator Controller (Assets/Animations/Player.controller).
    // StringToHash переводит строку в число — так Unity работает быстрее.
    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int GroundedParam = Animator.StringToHash("IsGrounded");
    private static readonly int VelocityYParam = Animator.StringToHash("VelocityY");

    private Rigidbody2D rb;
    private SpriteRenderer[] renderers;
    private ContactFilter2D groundFilter;
    private float baseGravity;

    private float moveInput;        // -1 влево, 0 стоим, 1 вправо
    private bool jumpHeld;          // кнопка прыжка сейчас зажата
    private float jumpBufferTimer;  // > 0 — недавно нажали прыжок
    private float coyoteTimer;      // > 0 — недавно стояли на земле
    private bool canCutJump;        // можно "срезать" текущий прыжок, отпустив кнопку
    private bool isGrounded;
    private bool controlsEnabled = true;
    private float invulnerableUntil;
    private MovingPlatform currentPlatform;
    private Coroutine blinkRoutine;

    /// <summary>Игрок мигает после урона и временно неуязвим.</summary>
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    /// <summary>Скорость игрока (её читают враги, чтобы понять, прыгнули ли на них сверху).</summary>
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
        renderers = GetComponentsInChildren<SpriteRenderer>();
        baseGravity = rb.gravityScale;

        // Фильтр "земли": касания твёрдых коллайдеров (не триггеров),
        // у которых нормаль смотрит вверх (угол 45°..135°).
        // Т.е. мы стоим НА чём-то, а не трёмся боком о стену.
        groundFilter.useTriggers = false;
        groundFilter.SetNormalAngle(45f, 135f);
    }

    private void Update()
    {
        if (!controlsEnabled)
        {
            moveInput = 0f;
            jumpHeld = false;
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

        // Поворачиваем картинку в сторону движения.
        if (visual != null && moveInput != 0f)
        {
            visual.localScale = new Vector3(Mathf.Sign(moveInput), 1f, 1f);
        }

        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        Vector2 velocity = Velocity;
        Vector2 platformVelocity = currentPlatform != null ? currentPlatform.Velocity : Vector2.zero;

        // 1) Стоим ли на земле? (и не летим вверх после прыжка — тогда скорость вверх большая)
        isGrounded = rb.IsTouching(groundFilter) && velocity.y - platformVelocity.y <= 0.5f;
        coyoteTimer = isGrounded ? coyoteTime : coyoteTimer - dt;
        jumpBufferTimer -= dt;

        // 2) Горизонтальная скорость = ввод * скорость (+ скорость платформы, если стоим на ней).
        velocity.x = moveInput * moveSpeed + platformVelocity.x;

        // 3) Прыжок: нажат недавно И недавно стояли на земле.
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            velocity.y = jumpForce;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;   // второй раз в воздухе не прыгнешь
            canCutJump = true;
            SoundFX.Play(Sfx.Jump);
        }

        // 4) Отпустил кнопку, пока летим вверх → прыжок ниже.
        if (canCutJump && !jumpHeld && velocity.y > 0f)
        {
            velocity.y *= jumpCutMultiplier;
            canCutJump = false;
        }
        if (velocity.y <= 0f) canCutJump = false;

        // 5) При падении гравитация сильнее — так прыжок ощущается "плотнее".
        rb.gravityScale = velocity.y < 0f ? baseGravity * fallGravityMultiplier : baseGravity;

        Velocity = velocity;
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
    }

    /// <summary>Переносит игрока в точку возрождения и даёт время неуязвимости.</summary>
    public void Respawn(Vector3 position, float invulnerabilityTime)
    {
        rb.position = position;
        transform.position = position;
        Velocity = Vector2.zero;
        currentPlatform = null;
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;

        invulnerableUntil = Time.time + invulnerabilityTime;
        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        blinkRoutine = StartCoroutine(Blink(invulnerabilityTime));
    }

    public void SetControlsEnabled(bool isEnabled)
    {
        controlsEnabled = isEnabled;
        if (!isEnabled) Velocity = new Vector2(0f, Velocity.y);
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

    private void SetVisible(bool visible)
    {
        foreach (SpriteRenderer sr in renderers)
        {
            sr.enabled = visible;
        }
    }
}
