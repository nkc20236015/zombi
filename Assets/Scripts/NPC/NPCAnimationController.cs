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
    private readonly int actionTypeHash = Animator.StringToHash("ActionType"); // e.g. 0=chop, 1=mine

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
    }

    /// <summary>
    /// Play a specific action animation (for future phases like chopping wood).
    /// </summary>
    /// <param name="actionType">The integer ID of the action to perform.</param>
    public void PlayAction(int actionType)
    {
        animator.SetInteger(actionTypeHash, actionType);
        animator.SetTrigger(actionTriggerHash);
    }

    /// <summary>
    /// Stops the current action and resets triggers.
    /// </summary>
    public void StopAction()
    {
        animator.ResetTrigger(actionTriggerHash);
    }

    // Animation Event Receivers
    public void FootR() { }
    public void FootL() { }
}
