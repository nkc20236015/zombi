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
    private float totalDaySeconds = 1080f;

    [Header("UI")]
    [SerializeField] private TMPro.TextMeshProUGUI watchText;


    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (phases == null || phases.Length == 0) return;

        totalDaySeconds = 0f;
        foreach (var p in phases)
        {
            totalDaySeconds += p.durationSeconds;
        }

        if (watchText == null)
        {
            GameObject watchObj = GameObject.Find("watch");
            if (watchObj != null) watchText = watchObj.GetComponent<TMPro.TextMeshProUGUI>();
        }

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

        UpdateClockUI();
    }

    private void UpdateClockUI()
    {
        if (watchText == null || totalDaySeconds <= 0f) return;

        float totalSecondsToday = 0f;
        for (int i = 0; i < currentPhaseIndex; i++)
        {
            totalSecondsToday += phases[i].durationSeconds;
        }
        totalSecondsToday += timeInCurrentPhase;

        // 1仮想日 = 24時間 = 1440分
        float simulatedMinutesToday = (totalSecondsToday / totalDaySeconds) * 1440f;
        
        // 朝 05:00 からスタートするため 5時間分(300分) を足す
        simulatedMinutesToday += 300f;
        if (simulatedMinutesToday >= 1440f)
        {
            simulatedMinutesToday -= 1440f;
        }

        int hours = Mathf.FloorToInt(simulatedMinutesToday / 60f);
        int minutes = Mathf.FloorToInt(simulatedMinutesToday % 60f);

        watchText.text = $"{hours:D2}:{minutes:D2}";
    }
}
