using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

public class AddIdleBoredState
{
    [MenuItem("Tools/Add Idle-Bored1 to NPC Animator")]
    public static void AddState()
    {
        string controllerPath = "Assets/_App/Scripts/NPC/NPCAnimatorController.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

        if (controller == null)
        {
            Debug.LogError($"[AddIdleBoredState] Could not find AnimatorController at {controllerPath}");
            return;
        }

        string fbxPath = "Assets/Animation/Crafting Mecanim Animation Pack/Animations/Crafter@Idle-Bored1.FBX";
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        AnimationClip idleBoredClip = null;

        foreach (var asset in assets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                idleBoredClip = clip;
                break;
            }
        }

        if (idleBoredClip == null)
        {
            Debug.LogError($"[AddIdleBoredState] Could not find AnimationClip in {fbxPath}");
            return;
        }

        AnimatorStateMachine rootStateMachine = controller.layers[0].stateMachine;

        // Check if state already exists
        bool stateExists = false;
        foreach (var childState in rootStateMachine.states)
        {
            if (childState.state.name == "Idle-Bored1")
            {
                stateExists = true;
                break;
            }
        }

        if (!stateExists)
        {
            AnimatorState newState = rootStateMachine.AddState("Idle-Bored1");
            newState.motion = idleBoredClip;
            Debug.Log("[AddIdleBoredState] Added Idle-Bored1 state successfully!");
            AssetDatabase.SaveAssets();
        }
        else
        {
            Debug.Log("[AddIdleBoredState] State 'Idle-Bored1' already exists.");
        }
    }
}
