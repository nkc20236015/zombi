using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCController : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private float buildModeAlpha = 0.3f;
    [SerializeField] private Color selectionColor = new Color(0.5f, 1f, 0.5f, 1f);

    public NPCState CurrentState { get; private set; } = NPCState.Idle;
    public bool IsSelected { get; private set; }

    private NavMeshAgent agent;
    private Renderer[] modelRenderers;
    private Material[][] originalMaterials;
    private Material[][] ghostMaterials;
    private GameObject selectionRing;

    [Header("Movement Marker")]
    [SerializeField] private GameObject targetMarkerPrefab;
    private GameObject targetMarkerInstance;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

#if UNITY_EDITOR
        if (targetMarkerPrefab == null)
        {
            targetMarkerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Map track markers VFX/Prefabs/Marker 6 Arrows Loop.prefab");
        }
#endif
    }

    void Start()
    {
        CacheRenderers();
        CreateSelectionRing();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerModeChanged += OnPlayerModeChanged;
            GameManager.Instance.RegisterNPC(this);
            SyncMode(GameManager.Instance.CurrentPlayerMode);
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPlayerModeChanged -= OnPlayerModeChanged;
            GameManager.Instance.UnregisterNPC(this);
        }
    }

    void Update()
    {
        // Simple state machine update
        if (CurrentState == NPCState.Moving)
        {
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
                {
                    CurrentState = NPCState.Idle;
                    if (targetMarkerInstance != null)
                    {
                        targetMarkerInstance.SetActive(false);
                    }
                }
            }
        }
    }

    public void MoveTo(Vector3 destination)
    {
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(destination);
            CurrentState = NPCState.Moving;

            if (targetMarkerPrefab != null)
            {
                if (targetMarkerInstance == null)
                {
                    targetMarkerInstance = Instantiate(targetMarkerPrefab);
                }
                targetMarkerInstance.transform.position = destination;
                targetMarkerInstance.SetActive(true);
            }
        }
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        UpdateVisuals();
    }

    private void OnPlayerModeChanged(PlayerMode mode)
    {
        UpdateVisuals();
    }

    private void SyncMode(PlayerMode mode)
    {
        UpdateVisuals();
    }

    private Material outlineMaterial;

    private void UpdateVisuals()
    {
        if (modelRenderers == null) return;
        
        bool isGhost = GameManager.Instance != null && GameManager.Instance.CurrentPlayerMode == PlayerMode.Building;
        bool shouldOutline = IsSelected && !isGhost;
        
        for (int i = 0; i < modelRenderers.Length; i++)
        {
            Material[] baseMats = isGhost ? ghostMaterials[i] : originalMaterials[i];
            
            if (shouldOutline && outlineMaterial != null)
            {
                Material[] newMats = new Material[baseMats.Length + 1];
                for (int j = 0; j < baseMats.Length; j++) newMats[j] = baseMats[j];
                newMats[baseMats.Length] = outlineMaterial;
                modelRenderers[i].materials = newMats;
            }
            else
            {
                modelRenderers[i].materials = baseMats;
            }
        }

        if (selectionRing != null)
        {
            selectionRing.SetActive(IsSelected && !isGhost);
        }
    }

    private void CacheRenderers()
    {
        modelRenderers = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[modelRenderers.Length][];
        ghostMaterials = new Material[modelRenderers.Length][];

        Shader outlineShader = Shader.Find("Custom/Outline");
        if (outlineShader != null)
        {
            outlineMaterial = new Material(outlineShader);
            outlineMaterial.SetColor("_OutlineColor", selectionColor);
            outlineMaterial.SetFloat("_OutlineWidth", 0.015f);
        }

        for (int i = 0; i < modelRenderers.Length; i++)
        {
            Material[] origMats = modelRenderers[i].sharedMaterials;
            originalMaterials[i] = origMats;

            Material[] ghosts = new Material[origMats.Length];
            
            for (int j = 0; j < origMats.Length; j++)
            {
                // ゴーストマテリアル
                Material ghost = new Material(origMats[j]);
                ghost.SetFloat("_Surface", 1); // Transparent
                ghost.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                ghost.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                ghost.SetInt("_ZWrite", 0);
                ghost.renderQueue = 3000;
                ghost.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                
                if (ghost.HasProperty("_BaseColor"))
                {
                    Color c = ghost.GetColor("_BaseColor");
                    c.a = buildModeAlpha;
                    ghost.SetColor("_BaseColor", c);
                }
                else if (ghost.HasProperty("_Color"))
                {
                    Color c = ghost.color;
                    c.a = buildModeAlpha;
                    ghost.color = c;
                }
                ghosts[j] = ghost;
            }
            ghostMaterials[i] = ghosts;
        }
    }

    private void CreateSelectionRing()
    {
        selectionRing = new GameObject("SelectionRing");
        selectionRing.transform.SetParent(transform);
        selectionRing.transform.localPosition = new Vector3(0, 0.05f, 0); // 地面から少しだけ浮かす
        selectionRing.transform.localRotation = Quaternion.Euler(90, 0, 0);
        
        var line = selectionRing.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        line.positionCount = 31;
        line.loop = true;
        
        // スプライト用のデフォルトマテリアルを利用して緑の円を描く
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = selectionColor;
        line.endColor = selectionColor;

        float radius = 0.6f;
        for (int i = 0; i <= 30; i++)
        {
            float angle = i * Mathf.PI * 2 / 30f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0));
        }

        selectionRing.SetActive(false);
    }


}
