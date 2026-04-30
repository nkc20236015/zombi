using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class NPCAnimationController : MonoBehaviour
{
    private Animator animator;
    private NavMeshAgent agent;

    // Parameters for Animator
    private readonly int isMovingHash = Animator.StringToHash("IsMoving");
    private readonly int velocityHash = Animator.StringToHash("Velocity");
    private readonly int actionTriggerHash = Animator.StringToHash("ActionTrigger");
    private readonly int actionTypeHash = Animator.StringToHash("ActionType"); // 0=chop, 1=mine

    private bool isPerformingAction;

    void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        // Calculate velocity magnitude based on NavMeshAgent
        float speed = agent.velocity.magnitude;
        bool isMoving = speed > 0.1f && !agent.isStopped;

        // Update animator parameters
        animator.SetBool(isMovingHash, isMoving);
        animator.SetFloat(velocityHash, speed);

        // 移動開始したらアクションを中断
        if (isMoving && isPerformingAction)
        {
            StopAction();
        }
    }

    /// <summary>
    /// Play a specific action animation.
    /// 0 = Chop (伐採), 1 = Mine (採掘)
    /// </summary>
    public void PlayAction(int actionType)
    {
        isPerformingAction = true;
        animator.SetInteger(actionTypeHash, actionType);
        animator.SetTrigger(actionTriggerHash);
    }

    /// <summary>
    /// Stops the current action and resets triggers.
    /// </summary>
    public void StopAction()
    {
        isPerformingAction = false;
        animator.ResetTrigger(actionTriggerHash);
        // ActionType を -1 にリセットすることで、Chop/Mine → Idle 遷移を発火
        animator.SetInteger(actionTypeHash, -1);
    }

    // Animation Event Receivers
    public void FootR() { }
    public void FootL() { }
    public void Hit() { } // Chop/Mine アニメーションのヒットイベント用
}
