using UnityEngine;

public class CPlayerInput : MonoBehaviour
{
    #region 인스펙터
    [SerializeField] private string _horizontalAxis = "Horizontal";   // A / D
    [SerializeField] private string _verticalAxis = "Vertical";       // W / S
    [SerializeField] private KeyCode _runKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode _jumpKey = KeyCode.Space;
    [SerializeField] private bool _logInput = false;                  // 디버그 로그 토글
    #endregion

    #region 내부 변수
    private Vector2 _moveInput;
    private bool _isRunHeld;
    private bool _jumpRequested;
    #endregion

    public Vector2 MoveInput => _moveInput;
    public bool IsRunHeld => _isRunHeld;
    public bool HasMoveInput => _moveInput.sqrMagnitude > 0.01f;

    private void Update()
    {
        // GetAxisRaw → 키를 떼면 바로 0 (가감속은 CPlayerMove 에서 직접 처리)
        float h = Input.GetAxisRaw(_horizontalAxis);
        float v = Input.GetAxisRaw(_verticalAxis);

        // 대각선 입력이 더 빨라지지 않도록 길이를 1 로 제한
        _moveInput = Vector2.ClampMagnitude(new Vector2(h, v), 1f);
        _isRunHeld = Input.GetKey(_runKey);

        if (Input.GetKeyDown(_jumpKey))
        {
            _jumpRequested = true;

            if (_logInput)
            {
                Debug.Log("[CPlayerInput] Jump 요청");
            }
        }
    }

    // 점프 요청은 한 번 꺼내면 사라진다. → 한 번 누르면 한 번만 처리
    public bool TryConsumeJump()
    {
        if (!_jumpRequested)
        {
            return false;
        }

        _jumpRequested = false;
        return true;
    }
}
