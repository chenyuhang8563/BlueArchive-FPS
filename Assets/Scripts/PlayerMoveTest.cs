using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

public class PlayerMoveTest : MonoBehaviour
{
    #region Class Variables
    Animator animator;
    bool armedRifle;
    public GameObject rifleOnBack;
    public GameObject rifleInHand;
    public TwoBoneIKConstraint rightHandConstraint;
    public TwoBoneIKConstraint leftHandConstraint;
    CharacterController characterController;
    Transform tr;
    Transform cameraTransform;

    [SerializeField] private LayerMask aimColliderLayerMask = new LayerMask();
    [SerializeField] private Transform debugTransform;
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

    int postureHash = Animator.StringToHash("Blend");
    int horizontalSpeedHash = Animator.StringToHash("Horizontal Speed");
    int moveSpeedHash = Animator.StringToHash("Vertical Speed");
    int turnSpeedHash = Animator.StringToHash("Turn Speed");
    int verticalSpeedHash = Animator.StringToHash("Jump Speed");
    int feetTweenHash = Animator.StringToHash("Feet");

    Vector3 playerMovement = Vector3.zero;

    public float gravity = -9.8f;

    float VerticalVelocity;

    // 最大跳跃高度
    public float maxHeight = 1f;

    // 脚落地动画混合状态
    float feetTween;
    Vector3 lastVelOnGround;

    static readonly int CACHE_SIZE = 3;
    int currentCacheIndex = 0;
    Vector3[] velCache = new Vector3[CACHE_SIZE];
    Vector3 averageVel = Vector3.zero;

    float fallMutiplier = 1.5f;

    bool isGrounded;
    float groundCheckOffset = 0.5f;
    #endregion

    #region Start
    void Start()
    {
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
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
        SwitchPlayerState();
        SetupAnimator();
        SetTwoHandsWeight();
    }
    #endregion

    #region 平均速度
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

    #region 角色运动
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
            // 使用落地前几帧的平均速度，因为 deltaPosition 受帧率影响，落地前帧率不一致会导致跳跃落地时偏移
            averageVel.y = VerticalVelocity;
            Vector3 playerDeltaMovement = averageVel * Time.deltaTime;
            characterController.Move(playerDeltaMovement);
        }
    }
    #endregion

    #region 角色旋转
    private void PlayerRotate()
    {
        if (!isAiming)
        {
            if (playerMovement.magnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(playerMovement);
                tr.rotation = Quaternion.Slerp(tr.rotation, targetRotation, Time.deltaTime * 10f);
            }
        }
        else
        {
            Vector3 playerTargetPosition;
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            Plane hitPlane = new Plane(Vector3.up, tr.position);
            float distance;
            if(hitPlane.Raycast(ray, out distance))
            {
                playerTargetPosition = ray.GetPoint(distance);
                tr.LookAt(playerTargetPosition);
            }
        }
    }
    #endregion

    #region 玩家输入处理
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
    public void PlayerCrouch(InputAction.CallbackContext ctx)
    {
        isCrouch = ctx.ReadValueAsButton();
    }

    public void PlayerJump(InputAction.CallbackContext ctx)
    {
        isJumping = ctx.ReadValueAsButton();
    }
    #endregion

    #region 玩家状态切换
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

    #region 落地检测
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

    #region 重力计算
    void CalculateGravity()
    {
        if (isGrounded)
        {
            // isGrounded 判断需要保持一个向下的速度
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

    #region 跳跃
    void Jump()
    {
        if (isJumping && isGrounded)
        {
            VerticalVelocity = Mathf.Sqrt(-2 * gravity * maxHeight);
            feetTween = UnityEngine.Random.Range(-1f, 1f);
        }
    }

    #endregion

    #region 计算输入方向
    void CalculateInputDirection()
    {
        Vector3 cameraForward = cameraTransform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        Vector3 cameraRight = cameraTransform.right;
        cameraRight.y = 0;
        cameraRight.Normalize();

        playerMovement = (cameraForward * moveInput.y + cameraRight * moveInput.x).normalized;
    }
    #endregion

    #region 设置动画参数
    void SetupAnimator()
    {
        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        if(!isAiming)
        {
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

        }
        else
        {
            animator.SetFloat(postureHash, midairThreshold);
            animator.SetFloat(horizontalSpeedHash, playerMovement.x * targetSpeed, 0.1f, Time.deltaTime);
            animator.SetFloat(moveSpeedHash, playerMovement.z * targetSpeed, 0.1f, Time.deltaTime);
        }

        PlayerRotate();
    }
    #endregion

    #region 收枪和持枪切换
    /// <summary>
    /// 切换枪在背上/在手上
    /// </summary>
    /// <param name="index">参数为1时枪背在背上，参数为0时枪握在手上</param>
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

    #region 设置双手IK权重
    void SetTwoHandsWeight()
    {
        rightHandConstraint.weight = animator.GetFloat("Right Hand Weight");
        leftHandConstraint.weight = animator.GetFloat("Left Hand Weight");
    }
    #endregion


}