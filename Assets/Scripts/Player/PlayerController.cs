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
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Движение")]
    [SerializeField] private float moveSpeed = 7f;
    [Tooltip("Вертикальная скорость в момент прыжка")]
    [SerializeField] private float jumpForce = 14f;

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

    private float moveInput;        // -1 влево, 0 стоим, 1 вправо
    private bool jumpRequested;     // нажали прыжок, ждём FixedUpdate
    private bool isGrounded;        // стоим ли на земле
    private bool controlsEnabled = true;
    private float invulnerableUntil;
    private MovingPlatform currentPlatform;
    private Coroutine blinkRoutine;

    /// <summary>Игрок мигает после урона и временно неуязвим.</summary>
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    // В Unity 6 свойство velocity переименовали в linearVelocity.
    // #if выбирает нужный вариант в зависимости от версии Unity.
    private Vector2 Velocity
    {
#if UNITY_6000_0_OR_NEWER
        get => rb.linearVelocity;
        set => rb.linearVelocity = value;
#else
        get => rb.velocity;
        set => rb.velocity = value;
#endif
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        renderers = GetComponentsInChildren<SpriteRenderer>();

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
            UpdateAnimation();
            return;
        }

        // "Horizontal" — стандартная ось: A/D и стрелки ←/→.
        moveInput = Input.GetAxisRaw("Horizontal");

        // "Jump" — стандартная кнопка: Пробел. Плюс W и стрелка вверх.
        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            jumpRequested = true;
        }

        // Поворачиваем картинку в сторону движения.
        if (visual != null && moveInput != 0f)
        {
            visual.localScale = new Vector3(Mathf.Sign(moveInput), 1f, 1f);
        }

        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        // Спрашиваем у физики: касаемся ли мы чего-то снизу?
        isGrounded = rb.IsTouching(groundFilter);

        Vector2 velocity = Velocity;

        // Горизонтальная скорость = ввод * скорость (+ скорость платформы, если стоим на ней).
        float platformSpeed = currentPlatform != null ? currentPlatform.Velocity.x : 0f;
        velocity.x = moveInput * moveSpeed + platformSpeed;

        // Прыгнуть можно ТОЛЬКО стоя на земле — так требует задание.
        if (jumpRequested && isGrounded)
        {
            velocity.y = jumpForce;
        }
        jumpRequested = false;

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

    // --- Методы, которые вызывает GameManager --------------------------

    /// <summary>Переносит игрока в точку возрождения и даёт время неуязвимости.</summary>
    public void Respawn(Vector3 position, float invulnerabilityTime)
    {
        rb.position = position;
        transform.position = position;
        Velocity = Vector2.zero;
        currentPlatform = null;
        jumpRequested = false;

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
