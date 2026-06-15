using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.Windows;

public class PlayerMoveTest : MonoBehaviour
{
    #region Class Variables
    Animator animator;
    [Tooltip("How far in degrees can you move the camera up")]
    public float TopClamp = 40.0f;
    [Tooltip("How far in degrees can you move the camera down")]
    public float BottomClamp = -30.0f;
    [Tooltip("For locking the camera position on all axis")]
    public bool LockCameraPosition = false;
    [Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
    public float CameraAngleOverride = 0.0f;
    bool armedRifle;
    public GameObject rifleOnBack;
    public GameObject rifleInHand;
    public GameObject CinemachineCameraTarget;
    public TwoBoneIKConstraint rightHandConstraint;
    public TwoBoneIKConstraint leftHandConstraint;
    CharacterController characterController;
    Transform tr;
    Transform cameraTransform;

    // cinemachine
    private float _cinemachineTargetYaw;
    private float _cinemachineTargetPitch;
    private const float _threshold = 0.01f;
    [SerializeField] private float mouseSensitivity = 5.0f;
    [SerializeField] private LayerMask aimColliderLayerMask = new LayerMask();
    [SerializeField] private Transform debugTransform;
    [Tooltip("独立摄像机追踪目标：不随角色旋转，只同步位置")]
    public Transform cameraFollowTarget;
    public enum PlayerPosture
    {
        Crouch,
        Stand,
        Midair
    };
    [HideInInspector]
    public PlayerPosture playerPosture = PlayerPosture.Stand;

    float crouchThreshold = 0f;
    float standThreshold = 1f;
    float midairThreshold = 2.1f;

    public enum LocomotionState
    {
        Idle,
        Walk,
        Run
    };
    [HideInInspector]
    public LocomotionState locomotionState = LocomotionState.Idle;

    public enum ArmState
    {
        Normal,
        Aim
    };
    [HideInInspector]
    public ArmState armState = ArmState.Normal;

    float crouchSpeed = 1.5f;
    float walkSpeed = 2.5f;
    float runSpeed = 5.5f;

    Vector2 moveInput;
    bool isRunning;
    bool isCrouch;
    bool isAiming;
    bool isJumping;
    Vector2 playerLook;

    int postureHash = Animator.StringToHash("Blend");
    int horizontalSpeedHash = Animator.StringToHash("Horizontal Speed");
    int moveSpeedHash = Animator.StringToHash("Vertical Speed");
    int turnSpeedHash = Animator.StringToHash("Turn Speed");
    int verticalSpeedHash = Animator.StringToHash("Jump Speed");
    int feetTweenHash = Animator.StringToHash("Feet");

    Vector3 playerMovement = Vector3.zero;

    public float gravity = -9.8f;

    float VerticalVelocity;

    // �������߶�
    public float maxHeight = 1f;

    // �Ϳ����ҽ�״̬
    float feetTween;
    Vector3 lastVelOnGround;

    static readonly int CACHE_SIZE = 3;
    int currentCacheIndex = 0;
    Vector3[] velCache = new Vector3[CACHE_SIZE];
    Vector3 averageVel = Vector3.zero;

    float fallMutiplier = 1.5f;

    bool isGrounded;
    float groundCheckOffset = 0.5f;

    public PlayerInput _playerInput;
    private bool IsCurrentDeviceMouse
    {
        get
        {
            return _playerInput.currentControlScheme == "KeyboardMouse";
        }
    }

    #endregion

    #region Start
    void Start()
    {
        _playerInput = GetComponent<PlayerInput>();
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        _cinemachineTargetYaw = CinemachineCameraTarget.transform.rotation.eulerAngles.y;
        tr = transform;
        cameraTransform = Camera.main.transform;
        Cursor.lockState = CursorLockMode.Locked;
    }
    #endregion

    #region Update
    void Update()
    {
        CheckGround();
        CalculateGravity();
        Jump();
        CalculateInputDirection();
        AimRayCast();
        SwitchPlayerState();
        SetupAnimator();
        SetTwoHandsWeight();
    }
    #endregion

    #region LateUpdate
    private void LateUpdate()
    {
        CameraRotation();

        // 同步 CameraFollowTarget 到玩家位置（只追踪位置，不跟旋转）
        // 确保摄像机追踪目标始终在玩家位置但保持世界朝向
        if (cameraFollowTarget != null)
        {
            cameraFollowTarget.position = tr.position;
        }
    }
    #endregion

    #region ���������ת
    private void CameraRotation()
    {
        // if there is an input and camera position is not fixed
        if (playerLook.sqrMagnitude >= _threshold && !LockCameraPosition)
        {
            //Don't multiply mouse input by Time.deltaTime;
            float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

            _cinemachineTargetYaw += playerLook.x * deltaTimeMultiplier * mouseSensitivity;
            _cinemachineTargetPitch -= playerLook.y * deltaTimeMultiplier * mouseSensitivity;
        }

        // clamp our rotations so our values are limited 360 degrees
        _cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
        _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

        // Cinemachine will follow this target
        CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride,
            _cinemachineTargetYaw, 0.0f);
    }

    private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
    {
        if (lfAngle < -360f) lfAngle += 360f;
        if (lfAngle > 360f) lfAngle -= 360f;
        return Mathf.Clamp(lfAngle, lfMin, lfMax);
    }
    #endregion

    #region ƽ�����
    Vector3 AverageVel(Vector3 newVel)
    {
        velCache[currentCacheIndex] = newVel;
        currentCacheIndex++;
        currentCacheIndex %= CACHE_SIZE;
        Vector3 average = Vector3.zero;
        foreach (Vector3 vel in velCache)
        {
            average += vel;
        }
        return average / CACHE_SIZE;
    }
    #endregion

    #region �������˶�
    private void OnAnimatorMove()
    {
        if (playerPosture != PlayerPosture.Midair)
        {
            Vector3 playerDeltaMovement = animator.deltaPosition;
            playerDeltaMovement.y = VerticalVelocity * Time.deltaTime;
            characterController.Move(playerDeltaMovement);
            averageVel = AverageVel(animator.velocity);
        }
        else
        {
            // �������ǰ��֡��ƽ���ٶ� ��ΪdeltaPosition��֡��Ӱ�죬����Ծǰ��֡�ʲ�һ���������Ծ����ʱƫ�����
            averageVel.y = VerticalVelocity;
            Vector3 playerDeltaMovement = averageVel * Time.deltaTime;
            characterController.Move(playerDeltaMovement);
        }
    }
    #endregion

    #region �����������
    public void PlayerMove(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }
    public void PlayerRun(InputAction.CallbackContext ctx)
    {
        isRunning = ctx.ReadValueAsButton();
    }
    public void PlayerAiming(InputAction.CallbackContext ctx)
    {
        if (ctx.ReadValue<float>() == 0)
        {
            isAiming = !isAiming;
            animator.SetBool("isAiming", isAiming);
        }
    }
    public void PlayerarmedRifle(InputAction.CallbackContext ctx)
    {
        if (ctx.ReadValue<float>() == 0)
        {
            armedRifle = !armedRifle;
            animator.SetBool("Rifle", armedRifle);
        }
    }
    public void PlayerLook(InputAction.CallbackContext ctx)
    {
        playerLook = ctx.ReadValue<Vector2>();
    }

    public void PlayerCrouch(InputAction.CallbackContext ctx)
    {
        isCrouch = ctx.ReadValueAsButton();
    }

    public void PlayerJump(InputAction.CallbackContext ctx)
    {
        isJumping = ctx.ReadValueAsButton();
    }
    #endregion

    #region ���״̬ת��
    void SwitchPlayerState()
    {
        if (!isGrounded)
        {
            playerPosture = PlayerPosture.Midair;
        }
        else if (isCrouch)
        {
            playerPosture = PlayerPosture.Crouch;
        }
        else
        {
            playerPosture = PlayerPosture.Stand;
        }
        if (moveInput.magnitude == 0)
        {
            locomotionState = LocomotionState.Idle;
        }
        else if (!isRunning)
        {
            locomotionState = LocomotionState.Walk;
        }
        else
        {
            locomotionState = LocomotionState.Run;
        }
        if (isAiming)
        {
            armState = ArmState.Aim;
        }
        else
        {
            armState = ArmState.Normal;
        }
    }
    #endregion

    #region ��ؼ��
    void CheckGround()
    {
        if (Physics.SphereCast
            (tr.position + (Vector3.up * groundCheckOffset),
            characterController.radius,
            Vector3.down,
            out RaycastHit hit, groundCheckOffset - characterController.radius + 2 * characterController.skinWidth))
        {
            isGrounded = true;
        }
        else
        {
            isGrounded = false;
        }
    }
    #endregion

    #region ��������
    void CalculateGravity()
    {
        if (isGrounded)
        {
            // isGrounded �жϱ���Ҫ��һ�����µ��ٶ�
            VerticalVelocity = gravity * Time.deltaTime;
            return;
        }
        else
        {
            if (VerticalVelocity <= 0)
            {
                VerticalVelocity += gravity * fallMutiplier * Time.deltaTime;
            }
            else
            {
                VerticalVelocity += gravity * Time.deltaTime;
            }

        }
    }
    #endregion

    #region ��Ծ
    void Jump()
    {
        if (isJumping && isGrounded)
        {
            VerticalVelocity = Mathf.Sqrt(-2 * gravity * maxHeight);
            feetTween = UnityEngine.Random.Range(-1f, 1f);
        }
    }

    #endregion

    #region ������ҷ���
    void CalculateInputDirection()
    {
        playerMovement = new Vector3(moveInput.x, 0, moveInput.y).normalized;
        playerMovement = tr.InverseTransformVector(playerMovement);
    }
    #endregion

    #region ������Ҷ���
    void SetupAnimator()
    {
        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        if (playerPosture == PlayerPosture.Stand)
        {
            animator.SetFloat(postureHash, standThreshold, 0.1f, Time.deltaTime);
            switch (locomotionState)
            {
                case LocomotionState.Idle:
                    animator.SetFloat(moveSpeedHash, 0, 0.1f, Time.deltaTime);
                    break;
                case LocomotionState.Walk:
                    animator.SetFloat(moveSpeedHash, playerMovement.magnitude * walkSpeed, 0.1f, Time.deltaTime);
                    break;
                case LocomotionState.Run:
                    animator.SetFloat(moveSpeedHash, playerMovement.magnitude * runSpeed, 0.1f, Time.deltaTime);
                    break;
            }
        }
        else if (playerPosture == PlayerPosture.Crouch)
        {
            animator.SetFloat(postureHash, crouchThreshold, 0.1f, Time.deltaTime);
            switch (locomotionState)
            {
                case LocomotionState.Idle:
                    animator.SetFloat(moveSpeedHash, 0, 0.1f, Time.deltaTime);
                    break;
                default:
                    animator.SetFloat(moveSpeedHash, playerMovement.magnitude * crouchSpeed, 0.1f, Time.deltaTime);
                    break;
            }
        }
        else if (playerPosture == PlayerPosture.Midair)
        {
            animator.SetFloat(postureHash, midairThreshold);
            animator.SetFloat(verticalSpeedHash, VerticalVelocity);
            animator.SetFloat(feetTweenHash, feetTween);
        }
        float rad = Mathf.Atan2(playerMovement.x, playerMovement.z);
        animator.SetFloat(turnSpeedHash, rad, 0.1f, Time.deltaTime);
        tr.Rotate(0, rad * 180 * Time.deltaTime, 0f);
     
    }
    #endregion

    #region ��ǹ�ͱ�ǹת��
    /// <summary>
    /// 
    /// </summary>
    /// <param name="index">����Ϊ1ʱǹ�ڱ��ϣ�����Ϊ0ʱǹ������</param>
    public void PutGrabRifle(int index)
    {
        if (index == 1)
        {
            rifleOnBack.SetActive(true);
            rifleInHand.SetActive(false);
        }
        else if (index == 0)
        {
            rifleOnBack.SetActive(false);
            rifleInHand.SetActive(true);
        }
    }
    #endregion

    #region ��˫��IKȨ��
    void SetTwoHandsWeight()
    {
        rightHandConstraint.weight = animator.GetFloat("Right Hand Weight");
        leftHandConstraint.weight = animator.GetFloat("Left Hand Weight");
    }
    #endregion

    #region ��׼�����߼�
    private void AimRayCast()
    {
        Vector3 mouseWorldPosition = Vector3.zero;
        Vector2 screenPointCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
        Ray ray = Camera.main.ScreenPointToRay(screenPointCenter);
        if (Physics.Raycast(ray, out RaycastHit raycastHit, 999f, aimColliderLayerMask))
        {
            debugTransform.position = raycastHit.point;
            mouseWorldPosition = raycastHit.point;
        }
    //     if (isAiming)
    //     {
    //         Vector3 worldAimTarget = mouseWorldPosition;
    //         worldAimTarget.y = transform.position.y;
    //         Vector3 aimDirection = (worldAimTarget - transform.position).normalized;
    //         transform.forward = Vector3.Lerp(transform.forward, aimDirection, Time.deltaTime * 20f);
    //     }
    }
    #endregion
    //private void GetPlayerMoving()
    //{
    //    var targetSpeed = isRunning ? runSpeed : walkSpeed;
    //    // ��׼ʱ������λ����Ϊ��׼����
    //    if (isAiming)
    //    {
    //        Vector3 dir;
    //        dir = new Vector3(moveInput.x, 0f, moveInput.y);
    //        //dir = tr.InverseTransformVector(dir);
    //        float rad = Mathf.Atan2(dir.x, dir.z);
    //        animator.SetFloat("Vertical Speed", dir.z * targetSpeed, 0.1f, Time.deltaTime);
    //        animator.SetFloat("Horizontal Speed", dir.x * targetSpeed, 0.1f, Time.deltaTime);
    //    }
    //    else
    //    {
    //        animator.SetFloat("Vertical Speed", moveInput.magnitude * targetSpeed, 0.1f, Time.deltaTime);
    //    }
    //}

}