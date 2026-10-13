using UnityEngine;

[RequireComponent(typeof(CPlayerMove))]
public class CPlayerAnimator : MonoBehaviour
{
    #region 인스펙터
    [SerializeField] private Animator _animator;                 // 비어 있으면 자식에서 찾는다
    [SerializeField] private float _speedDampTime = 0.1f;        // Speed 값 보간 시간
    [SerializeField] private string _speedParam = "Speed";
    [SerializeField] private string _groundedParam = "IsGrounded";
    #endregion

    #region 내부 변수
    private const float WALK_BLEND = 0.5f;
    private const float RUN_BLEND = 1f;

    private CPlayerMove _move;
    private int _speedHash;
    private int _groundedHash;
    #endregion

    private void Awake()
    {
        _move = GetComponent<CPlayerMove>();

        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }

        if (_animator == null)
        {
            Debug.LogWarning("[CPlayerAnimator] Animator 가 없습니다.");
            return;
        }

        // 이동은 Rigidbody 가 담당 → 애니메이션이 위치를 움직이지 않게
        _animator.applyRootMotion = false;

        _speedHash = Animator.StringToHash(_speedParam);
        _groundedHash = Animator.StringToHash(_groundedParam);
    }

    private void Update()
    {
        if (_animator == null)
        {
            return;
        }

        _animator.SetFloat(_speedHash, CalcSpeedBlend(), _speedDampTime, Time.deltaTime);
        _animator.SetBool(_groundedHash, _move.IsGrounded);
    }

    // 실제 수평 속도 → 블렌드 값
    //  ㄴ 0 ~ walkSpeed        : 0   ~ 0.5
    //  ㄴ walkSpeed ~ runSpeed : 0.5 ~ 1
    private float CalcSpeedBlend()
    {
        float speed = _move.PlanarSpeed;
        float walk = _move.WalkSpeed;
        float run = _move.RunSpeed;

        if (speed <= walk)
        {
            return walk > 0f ? Mathf.Lerp(0f, WALK_BLEND, speed / walk) : 0f;
        }

        if (run <= walk)
        {
            return WALK_BLEND;
        }

        return Mathf.Lerp(WALK_BLEND, RUN_BLEND, (speed - walk) / (run - walk));
    }
}
