using UnityEngine;
using System;

[Serializable]
public class DayPhase
{
    public string phaseName;
    public GameState gameState;
    public float durationSeconds;
    
    [Header("Skybox & Lighting")]
    public Material skyboxMaterial;
    public Color directionalLightColor = Color.white;
    public float lightIntensity = 1f;
    public Vector3 lightRotation; // Euler angles
}

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }

    [SerializeField] private Light directionalLight;
    [SerializeField] private DayPhase[] phases;

    private int currentPhaseIndex = 0;
    private float timeInCurrentPhase = 0f;


    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (phases == null || phases.Length == 0) return;

        // Set initial skybox
        if (phases[0].skyboxMaterial != null)
        {
            RenderSettings.skybox = phases[0].skyboxMaterial;
            DynamicGI.UpdateEnvironment();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetGameState(phases[0].gameState);
        }
    }

    void Update()
    {
        if (phases == null || phases.Length == 0 || directionalLight == null) return;

        timeInCurrentPhase += Time.deltaTime;
        
        DayPhase currentPhase = phases[currentPhaseIndex];
        DayPhase nextPhase = phases[(currentPhaseIndex + 1) % phases.Length];

        // Phase Transition
        if (timeInCurrentPhase >= currentPhase.durationSeconds)
        {
            timeInCurrentPhase -= currentPhase.durationSeconds;
            currentPhaseIndex = (currentPhaseIndex + 1) % phases.Length;
            
            // Check if day advanced (looped back to Dawn)
            if (currentPhaseIndex == 0 && GameManager.Instance != null)
            {
                GameManager.Instance.AdvanceDay();
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(phases[currentPhaseIndex].gameState);
            }

            currentPhase = phases[currentPhaseIndex];
            nextPhase = phases[(currentPhaseIndex + 1) % phases.Length];
        }

        // Interpolation factor
        float t = timeInCurrentPhase / currentPhase.durationSeconds;

        // Update Skybox at the start of the phase
        if (currentPhase.skyboxMaterial != null && RenderSettings.skybox != currentPhase.skyboxMaterial)
        {
            RenderSettings.skybox = currentPhase.skyboxMaterial;
            DynamicGI.UpdateEnvironment();
        }

        // Smooth transition for Directional Light
        directionalLight.color = Color.Lerp(currentPhase.directionalLightColor, nextPhase.directionalLightColor, t);
        directionalLight.intensity = Mathf.Lerp(currentPhase.lightIntensity, nextPhase.lightIntensity, t);
        
        Quaternion currentRot = Quaternion.Euler(currentPhase.lightRotation);
        Quaternion nextRot = Quaternion.Euler(nextPhase.lightRotation);
        directionalLight.transform.rotation = Quaternion.Slerp(currentRot, nextRot, t);
    }
}
