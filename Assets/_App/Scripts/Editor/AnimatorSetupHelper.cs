#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Linq;

public class AnimatorSetupHelper
{
    private const string CONTROLLER_PATH = "Assets/_App/Scripts/NPC/NPCAnimatorController.controller";
    private const string GATHER_CLIP_PATH = "Assets/Animation/Crafting Mecanim Animation Pack/Animations/Crafter@Gather-Kneeling.FBX";
    private const string SICKLE_CLIP_PATH = "Assets/Animation/Crafting Mecanim Animation Pack/Animations/Crafter@Item-Sickle-Use.FBX";

    [MenuItem("Tools/Setup Mushroom Animator States")]
    public static void SetupMushroomStates()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
        if (controller == null)
        {
            Debug.LogError($"[AnimatorSetup] Animator Controller not found at {CONTROLLER_PATH}");
            return;
        }

        // 1. パラメーターの追加
        AddTriggerParameter(controller, "GatherTrigger");
        AddTriggerParameter(controller, "SickleTrigger");

        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

        // 2. ステートとモーションの追加
        AnimationClip gatherClip = LoadClip(GATHER_CLIP_PATH);
        AnimatorState gatherState = SetupState(rootStateMachine, "GatherFood", gatherClip);

        AnimationClip sickleClip = LoadClip(SICKLE_CLIP_PATH);
        AnimatorState sickleState = SetupState(rootStateMachine, "SickleUse", sickleClip);

        // 3. トランジションの設定 (Any State -> State)
        SetupAnyStateTransition(rootStateMachine, gatherState, "GatherTrigger");
        SetupAnyStateTransition(rootStateMachine, sickleState, "SickleTrigger");

        // 4. トランジションの設定 (State -> Idle)
        AnimatorState idleState = FindState(rootStateMachine, "Idle");
        if (idleState != null)
        {
            SetupExitTransition(gatherState, idleState);
            SetupExitTransition(sickleState, idleState);
        }
        else
        {
            Debug.LogWarning("[AnimatorSetup] 'Idle' state not found. Could not create return transitions.");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();

        Debug.Log("[AnimatorSetup] Mushroom Animator states setup completed successfully!");
        // EditorUtility.DisplayDialog("Success", "Mushroom Animator states added successfully!\n- GatherFood\n- SickleUse", "OK");
    }

    private static void AddTriggerParameter(AnimatorController controller, string paramName)
    {
        if (!controller.parameters.Any(p => p.name == paramName))
        {
            controller.AddParameter(paramName, AnimatorControllerParameterType.Trigger);
            Debug.Log($"[AnimatorSetup] Added parameter: {paramName}");
        }
    }

    private static AnimationClip LoadClip(string fbxPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (Object asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                return clip;
            }
        }
        Debug.LogError($"[AnimatorSetup] Could not find valid AnimationClip inside {fbxPath}");
        return null;
    }

    private static AnimatorState SetupState(AnimatorStateMachine sm, string stateName, AnimationClip clip)
    {
        AnimatorState state = FindState(sm, stateName);
        if (state == null)
        {
            state = sm.AddState(stateName);
            Debug.Log($"[AnimatorSetup] Created state: {stateName}");
        }
        state.motion = clip;
        return state;
    }

    private static AnimatorState FindState(AnimatorStateMachine sm, string stateName)
    {
        foreach (ChildAnimatorState child in sm.states)
        {
            if (child.state.name == stateName)
            {
                return child.state;
            }
        }
        return null;
    }

    private static void SetupAnyStateTransition(AnimatorStateMachine sm, AnimatorState dstState, string triggerName)
    {
        // 既存のトランジションがあるかチェック
        foreach (var t in sm.anyStateTransitions)
        {
            if (t.destinationState == dstState)
            {
                // すでにある場合は更新
                t.conditions = new AnimatorCondition[] { new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = triggerName } };
                return;
            }
        }

        // 新規作成
        AnimatorStateTransition transition = sm.AddAnyStateTransition(dstState);
        transition.AddCondition(AnimatorConditionMode.If, 0, triggerName);
        transition.hasFixedDuration = true;
        transition.duration = 0.1f;
        transition.canTransitionToSelf = false;
        Debug.Log($"[AnimatorSetup] Added AnyState transition to {dstState.name}");
    }

    private static void SetupExitTransition(AnimatorState srcState, AnimatorState dstState)
    {
        foreach (var t in srcState.transitions)
        {
            if (t.destinationState == dstState) return; // すでにある
        }

        AnimatorStateTransition transition = srcState.AddTransition(dstState);
        transition.hasExitTime = true;
        transition.exitTime = 0.9f;
        transition.hasFixedDuration = true;
        transition.duration = 0.1f;
        Debug.Log($"[AnimatorSetup] Added Exit transition from {srcState.name} to {dstState.name}");
    }
}
#endif
