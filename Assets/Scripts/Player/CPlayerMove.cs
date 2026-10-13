using System;
using UnityEngine;

public enum EMoveState
{
    Idle = 0,
    Walk,
    Run,
    Jump,
    Fall
}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(CPlayerInput))]
public class CPlayerMove : MonoBehaviour
{
    #region 인스펙터
    [Header("이동")]
    [SerializeField] private float _walkSpeed = 2f;
    [SerializeField] private float _runSpeed = 5.5f;
    [SerializeField] private float _acceleration = 20f;         // 초당 속도 변화량 (가감속)
    [SerializeField] private float _turnSpeed = 720f;           // 초당 회전 각도
    [SerializeField] private float _airControl = 0.4f;          // 공중에서의 가속 비율 (0 ~ 1)
    [SerializeField] private Transform _cameraTransform;        // 비어 있으면 Camera.main

    [Header("점프")]
    [SerializeField] private float _jumpHeight = 1.2f;          // 점프 높이 (m)
    [SerializeField] private float _groundIgnoreTime = 0.15f;   // 점프 직후 지면 판정을 잠깐 끈다

    [Header("지면 판정")]
    [SerializeField] private LayerMask _groundMask = ~0;
    [SerializeField] private float _groundCheckRadius = 0.2f;
    [SerializeField] private float _groundCheckOffset = 0.15f;  // 발 위치에서 구 중심까지의 높이

    [Header("디버그")]
    [SerializeField] private bool _logState = true;
    [SerializeField] private bool _showDebugRay = true;
    #endregion

    #region 내부 변수
    private const int GROUND_BUFFER_SIZE = 8;

    private Rigidbody _rb;
    private CPlayerInput _input;
    private readonly Collider[] _groundHits = new Collider[GROUND_BUFFER_SIZE];

    private EMoveState _state = EMoveState.Idle;
    private bool _isGrounded;
    private float _groundIgnoreTimer;
    private Vector3 _moveDirection;
    #endregion

    public event Action<EMoveState, EMoveState> OnStateChanged;
    public event Action OnJump;

    public EMoveState State => _state;
    public bool IsGrounded => _isGrounded;
    public float WalkSpeed => _walkSpeed;
    public float RunSpeed => _runSpeed;
    public float PlanarSpeed
    {
        get
        {
            Vector3 v = _rb.velocity;
            v.y = 0f;
            return v.magnitude;
        }
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _input = GetComponent<CPlayerInput>();

        // 회전은 코드로만 한다. → 물리 충돌로 넘어지지 않게
        _rb.freezeRotation = true;
        _rb.useGravity = true;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void Start()
    {
        if (_cameraTransform == null && Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }

        _isGrounded = CheckGround();
    }

    private void Update()
    {
        SetState(DecideState());
    }

    private void FixedUpdate()
    {
        if (_rb == null)
        {
            return;
        }

        TickGround();
        TickJump();
        TickMove();
        TickRotate();
    }

    private void TickGround()
    {
        // 점프 직후에는 아직 발이 땅에 붙어 있다. → 바로 다시 지상 판정이 나지 않게 잠깐 무시
        if (_groundIgnoreTimer > 0f)
        {
            _groundIgnoreTimer -= Time.fixedDeltaTime;
            _isGrounded = false;
            return;
        }

        _isGrounded = CheckGround();
    }

    private bool CheckGround()
    {
        Vector3 center = transform.position + Vector3.up * _groundCheckOffset;
        int count = Physics.OverlapSphereNonAlloc(center, _groundCheckRadius, _groundHits, _groundMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = _groundHits[i];

            if (col == null)
            {
                continue;
            }

            // 내 콜라이더는 제외
            if (col.attachedRigidbody == _rb)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void TickJump()
    {
        // 공중에서 누른 입력도 여기서 소비된다. → 착지 순간 자동 점프 방지
        if (!_input.TryConsumeJump())
        {
            return;
        }

        if (!_isGrounded)
        {
            return;
        }

        float jumpVelocity = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * _jumpHeight);

        Vector3 v = _rb.velocity;
        v.y = jumpVelocity;
        _rb.velocity = v;

        _isGrounded = false;
        _groundIgnoreTimer = _groundIgnoreTime;

        OnJump?.Invoke();
    }

    private void TickMove()
    {
        _moveDirection = GetCameraRelativeDirection(_input.MoveInput);

        float targetSpeed = _input.IsRunHeld ? _runSpeed : _walkSpeed;
        Vector3 targetVelocity = _moveDirection * targetSpeed;

        Vector3 current = _rb.velocity;
        Vector3 planar = new Vector3(current.x, 0f, current.z);

        // 공중에서는 방향 전환이 둔하게
        float accel = _isGrounded ? _acceleration : _acceleration * _airControl;
        planar = Vector3.MoveTowards(planar, targetVelocity, accel * Time.fixedDeltaTime);

        // y 는 그대로 → 중력 / 점프 속도 유지
        _rb.velocity = new Vector3(planar.x, current.y, planar.z);

        if (_showDebugRay)
        {
            Debug.DrawRay(transform.position + Vector3.up, _moveDirection, Color.cyan);
        }
    }

    private void TickRotate()
    {
        if (_moveDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion target = Quaternion.LookRotation(_moveDirection, Vector3.up);
        Quaternion next = Quaternion.RotateTowards(_rb.rotation, target, _turnSpeed * Time.fixedDeltaTime);
        _rb.MoveRotation(next);
    }

    // 입력 (x, y) → 카메라 기준 월드 방향 (x, 0, z)
    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        if (input.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        if (_cameraTransform == null)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        // 카메라가 아래를 내려다봐도 수평 방향만 쓴다.
        Vector3 forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up).normalized;

        return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
    }

    private EMoveState DecideState()
    {
        if (!_isGrounded)
        {
            return _rb.velocity.y > 0.1f ? EMoveState.Jump : EMoveState.Fall;
        }

        if (!_input.HasMoveInput)
        {
            return EMoveState.Idle;
        }

        return _input.IsRunHeld ? EMoveState.Run : EMoveState.Walk;
    }

    private void SetState(EMoveState next)
    {
        if (_state == next)
        {
            return;
        }

        EMoveState prev = _state;
        _state = next;

        if (_logState)
        {
            Debug.Log($"[CPlayerMove] {prev} → {_state}");
        }

        OnStateChanged?.Invoke(prev, _state);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * _groundCheckOffset, _groundCheckRadius);
    }
}
