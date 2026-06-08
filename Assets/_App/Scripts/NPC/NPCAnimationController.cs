using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
public class NPCAnimationController : MonoBehaviour
{
    private Animator animator;
    private NavMeshAgent agent;
    private NPCController npcController;

    // Parameters for Animator
    private readonly int isMovingHash = Animator.StringToHash("IsMoving");
    private readonly int velocityHash = Animator.StringToHash("Velocity");
    private readonly int actionTriggerHash = Animator.StringToHash("ActionTrigger");
    private readonly int actionTypeHash = Animator.StringToHash("ActionType"); // 0=chop, 1=mine
    private readonly int gatherTriggerHash = Animator.StringToHash("GatherTrigger"); // 完熟キノコ等の採取専用トリガー
    private readonly int sickleTriggerHash = Animator.StringToHash("SickleTrigger"); // 未完熟キノコ等の切る専用トリガー

    private bool isPerformingAction;

    void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        npcController = GetComponent<NPCController>();
    }

    void Update()
    {
        // Calculate velocity magnitude based on NavMeshAgent
        float speed = agent.velocity.magnitude;
        bool isMoving = speed > 0.1f && !agent.isStopped;
        
        bool isWandering = npcController.CurrentState == NPCState.Wandering;
        bool isCarrying = npcController.CurrentState == NPCState.Carrying;

        // もし徘徊中または運搬中なら、通常のMoveへの遷移を防ぐため IsMoving を false にする
        animator.SetBool(isMovingHash, (isWandering || isCarrying) ? false : isMoving);
        animator.SetFloat(velocityHash, speed);

        // 移動開始したらアクションを中断
        if (isMoving && isPerformingAction)
        {
            StopAction();
        }

        var state = animator.GetCurrentAnimatorStateInfo(0);

        // 徘徊中または運搬中の移動アニメーション（Carry-WalkForward）を強制再生
        if ((isWandering || isCarrying) && isMoving)
        {
            if (!state.IsName("WanderMove") && !animator.IsInTransition(0))
            {
                animator.CrossFade("WanderMove", 0.25f);
            }
        }
        else if (!isMoving && state.IsName("WanderMove") && !animator.IsInTransition(0))
        {
            animator.CrossFade("Idle", 0.25f);
        }
        
        // Idle-Bored1が終わったら自動的にIdleに戻る
        if (!isMoving && state.IsName("Idle-Bored1") && state.normalizedTime >= 0.95f && !animator.IsInTransition(0))
        {
            animator.CrossFade("Idle", 0.25f);
        }
    }

    public bool IsPlayingBoredIdle()
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        var nextState = animator.GetNextAnimatorStateInfo(0);
        if (animator.IsInTransition(0) && nextState.IsName("Idle-Bored1")) return true;
        return state.IsName("Idle-Bored1") && state.normalizedTime < 0.95f;
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
    /// Play gathering animation (e.g., picking up ripe mushrooms).
    /// </summary>
    public void PlayGather()
    {
        isPerformingAction = true;
        animator.SetTrigger(gatherTriggerHash);
    }

    /// <summary>
    /// Play sickle animation (e.g., cutting unripe mushrooms).
    /// </summary>
    public void PlaySickle()
    {
        isPerformingAction = true;
        animator.SetTrigger(sickleTriggerHash);
    }

    /// <summary>
    /// Stops the current action and resets triggers.
    /// CrossFadeで確実にIdleへ遷移させる。
    /// </summary>
    public void StopAction()
    {
        isPerformingAction = false;
        animator.ResetTrigger(actionTriggerHash);
        animator.SetInteger(actionTypeHash, -1);
        // Animator Controllerの遷移条件に依存せず、確実にIdleへ戻す
        animator.CrossFade("Idle", 0.25f);
    }

    /// <summary>
    /// たまに再生する特殊なIdleアニメーション
    /// </summary>
    public void PlayBoredIdle()
    {
        isPerformingAction = false;
        animator.ResetTrigger(actionTriggerHash);
        animator.SetInteger(actionTypeHash, -1);
        animator.CrossFade("Idle-Bored1", 0.25f);
    }

    /// <summary>
    /// 通常のIdleアニメーションを明示的に再生
    /// </summary>
    public void PlayIdle()
    {
        isPerformingAction = false;
        animator.ResetTrigger(actionTriggerHash);
        animator.SetInteger(actionTypeHash, -1);
        animator.CrossFade("Idle", 0.25f);
    }

    /// <summary>
    /// ツールをしまうアニメーション（PutItem）を再生する。
    /// PuttingAwayステートの開始時に呼ばれる。
    /// </summary>
    public void PlayPutAway()
    {
        isPerformingAction = false;
        animator.ResetTrigger(actionTriggerHash);
        animator.SetInteger(actionTypeHash, -1);
        animator.CrossFade("PutItem", 0.25f);
    }

    // Animation Event Receivers
    public void FootR() { }
    public void FootL() { }
    public void Hit() { OnStrikeOrHit(); }    // Chop/Mine アニメーションのヒットイベント用
    public void Strike() { OnStrikeOrHit(); } // Chop-Horizontal/Chop-Vertical-Upper のヒットイベント用

    /// <summary>
    /// 斧/ツールが当たった瞬間の共通処理。NPCControllerに通知して木を揺らす。
    /// </summary>
    private void OnStrikeOrHit()
    {
        if (npcController != null)
        {
            npcController.OnStrikeHit();
        }
    }
}
