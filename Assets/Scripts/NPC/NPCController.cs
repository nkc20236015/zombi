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
    private Material[][] selectionMaterials;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        CacheRenderers();

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

    private void UpdateVisuals()
    {
        if (modelRenderers == null) return;
        
        bool isGhost = GameManager.Instance != null && GameManager.Instance.CurrentPlayerMode == PlayerMode.Building;
        
        for (int i = 0; i < modelRenderers.Length; i++)
        {
            if (isGhost)
            {
                modelRenderers[i].materials = ghostMaterials[i];
            }
            else if (IsSelected)
            {
                modelRenderers[i].materials = selectionMaterials[i];
            }
            else
            {
                modelRenderers[i].materials = originalMaterials[i];
            }
        }
    }

    private void CacheRenderers()
    {
        modelRenderers = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[modelRenderers.Length][];
        ghostMaterials = new Material[modelRenderers.Length][];
        selectionMaterials = new Material[modelRenderers.Length][];

        for (int i = 0; i < modelRenderers.Length; i++)
        {
            Material[] origMats = modelRenderers[i].sharedMaterials;
            originalMaterials[i] = origMats;

            Material[] ghosts = new Material[origMats.Length];
            Material[] selections = new Material[origMats.Length];
            
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

                // 選択時マテリアル
                Material sel = new Material(origMats[j]);
                if (sel.HasProperty("_BaseColor"))
                {
                    Color baseC = sel.GetColor("_BaseColor");
                    sel.SetColor("_BaseColor", baseC * selectionColor);
                }
                else if (sel.HasProperty("_Color"))
                {
                    sel.color = sel.color * selectionColor;
                }
                selections[j] = sel;
            }
            ghostMaterials[i] = ghosts;
            selectionMaterials[i] = selections;
        }
    }


}
