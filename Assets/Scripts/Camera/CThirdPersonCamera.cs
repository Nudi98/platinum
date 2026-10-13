using UnityEngine;

public class CThirdPersonCamera : MonoBehaviour
{
    #region 인스펙터
    [Header("대상")]
    [SerializeField] private Transform _target;
    [SerializeField] private string _playerTag = "Player";        // _target 이 비어 있으면 이 태그로 찾는다
    [SerializeField] private Vector3 _pivotOffset = new Vector3(0f, 1.4f, 0f);

    [Header("회전")]
    [SerializeField] private float _mouseSensitivity = 3f;
    [SerializeField] private float _minPitch = -30f;
    [SerializeField] private float _maxPitch = 70f;
    [SerializeField] private bool _invertY = false;

    [Header("거리")]
    [SerializeField] private float _distance = 4f;
    [SerializeField] private float _minDistance = 1.5f;
    [SerializeField] private float _maxDistance = 8f;
    [SerializeField] private float _zoomSpeed = 4f;
    [SerializeField] private float _followSmooth = 15f;           // 클수록 바로 따라간다

    [Header("충돌")]
    [SerializeField] private LayerMask _collisionMask = ~0;
    [SerializeField] private float _collisionRadius = 0.2f;

    [Header("커서")]
    [SerializeField] private bool _lockCursor = true;
    #endregion

    #region 내부 변수
    private const int HIT_BUFFER_SIZE = 8;

    private float _yaw;
    private float _pitch = 15f;
    private Vector3 _pivot;
    private readonly RaycastHit[] _hits = new RaycastHit[HIT_BUFFER_SIZE];
    #endregion

    private void Start()
    {
        if (_target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag(_playerTag);

            if (player != null)
            {
                _target = player.transform;
            }
        }

        if (_target == null)
        {
            Debug.LogWarning("[CThirdPersonCamera] 추적할 대상이 없습니다.");
            return;
        }

        // 시작 시 캐릭터 뒤에서 보도록
        _yaw = _target.eulerAngles.y;
        _pivot = _target.position + _pivotOffset;

        SetCursorLock(_lockCursor);
    }

    private void Update()
    {
        TickCursor();
    }

    private void LateUpdate()
    {
        if (_target == null)
        {
            return;
        }

        TickRotate();
        TickZoom();
        TickPosition();
    }

    private void TickCursor()
    {
        if (!_lockCursor)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorLock(false);
        }

        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            SetCursorLock(true);
        }
    }

    private void TickRotate()
    {
        // 커서가 풀려 있으면 UI 조작 중 → 시점 고정
        if (_lockCursor && Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        float mx = Input.GetAxis("Mouse X") * _mouseSensitivity;
        float my = Input.GetAxis("Mouse Y") * _mouseSensitivity;

        _yaw += mx;
        _pitch += _invertY ? my : -my;
        _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
    }

    private void TickZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Approximately(scroll, 0f))
        {
            return;
        }

        _distance = Mathf.Clamp(_distance - scroll * _zoomSpeed, _minDistance, _maxDistance);
    }

    private void TickPosition()
    {
        // 피벗은 부드럽게 따라간다. → 점프 / 착지 때 화면이 덜 흔들린다.
        Vector3 targetPivot = _target.position + _pivotOffset;
        _pivot = Vector3.Lerp(_pivot, targetPivot, 1f - Mathf.Exp(-_followSmooth * Time.deltaTime));

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 back = rotation * Vector3.back;
        float distance = GetBlockedDistance(_pivot, back, _distance);

        transform.SetPositionAndRotation(_pivot + back * distance, rotation);
    }

    // 피벗 → 카메라 사이에 벽이 있으면 그 앞까지 당긴다. (플레이어 자신은 무시)
    private float GetBlockedDistance(Vector3 origin, Vector3 dir, float maxDistance)
    {
        int count = Physics.SphereCastNonAlloc(origin, _collisionRadius, dir, _hits, maxDistance, _collisionMask, QueryTriggerInteraction.Ignore);
        float result = maxDistance;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _hits[i];

            if (hit.collider == null)
            {
                continue;
            }

            if (hit.collider.transform.IsChildOf(_target))
            {
                continue;
            }

            // 시작부터 겹친 경우 distance 가 0 → 무시
            if (hit.distance <= 0f)
            {
                continue;
            }

            if (hit.distance < result)
            {
                result = hit.distance;
            }
        }

        return Mathf.Max(result, 0.1f);
    }

    private void SetCursorLock(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }

    private void OnDisable()
    {
        SetCursorLock(false);
    }
}
