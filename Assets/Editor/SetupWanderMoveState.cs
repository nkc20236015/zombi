using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class SetupWanderMoveState
{
    [MenuItem("Tools/Add WanderMove to NPC Animator")]
    public static void AddState()
    {
        string controllerPath = "Assets/_App/Scripts/NPC/NPCAnimatorController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (controller == null)
        {
            Debug.LogError($"[SetupWanderMoveState] Could not find AnimatorController at {controllerPath}");
            return;
        }

        string fbxPath = "Assets/Animation/Crafting Mecanim Animation Pack/Animations/Crafter@Carry-WalkForward.FBX";
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        AnimationClip walkClip = null;

        foreach (var asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                walkClip = clip;
                break;
            }
        }

        if (walkClip == null)
        {
            Debug.LogError($"[SetupWanderMoveState] Could not find AnimationClip in {fbxPath}");
            return;
        }

        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

        // Check if state already exists
        bool stateExists = false;
        foreach (var childState in rootStateMachine.states)
        {
            if (childState.state.name == "WanderMove")
            {
                stateExists = true;
                break;
            }
        }

        if (!stateExists)
        {
            AnimatorState newState = rootStateMachine.AddState("WanderMove");
            newState.motion = walkClip;
            Debug.Log("[SetupWanderMoveState] Added WanderMove state successfully!");
            AssetDatabase.SaveAssets();
        }
        else
        {
            Debug.Log("[SetupWanderMoveState] State 'WanderMove' already exists.");
        }
    }
}
