using System.Collections.Generic;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    [SerializeField] private LayerMask npcLayer;
    private Camera mainCamera;

    private List<NPCController> selectedNPCs = new List<NPCController>();
    public IReadOnlyList<NPCController> SelectedNPCs => selectedNPCs;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentPlayerMode == PlayerMode.Building)
            return; // 建築モード中は選択無効

        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            HandleSelection();
        }
    }

    private void HandleSelection()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, npcLayer))
        {
            NPCController npc = hit.collider.GetComponentInParent<NPCController>();
            if (npc != null)
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    // 追加選択 / 解除
                    if (selectedNPCs.Contains(npc))
                    {
                        Deselect(npc);
                    }
                    else
                    {
                        Select(npc);
                    }
                }
                else
                {
                    // 単体選択
                    DeselectAll();
                    Select(npc);
                }
                return;
            }
        }

        // 何もないところをクリックしたら選択解除
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
        {
            DeselectAll();
        }
    }

    private void Select(NPCController npc)
    {
        if (!selectedNPCs.Contains(npc))
        {
            selectedNPCs.Add(npc);
            npc.SetSelected(true);
        }
    }

    private void Deselect(NPCController npc)
    {
        if (selectedNPCs.Contains(npc))
        {
            selectedNPCs.Remove(npc);
            npc.SetSelected(false);
        }
    }

    public void DeselectAll()
    {
        foreach (var npc in selectedNPCs)
        {
            if (npc != null) npc.SetSelected(false);
        }
        selectedNPCs.Clear();
    }
}
