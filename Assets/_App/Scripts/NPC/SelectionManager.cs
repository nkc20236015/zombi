using System.Collections.Generic;
using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; private set; }

    [SerializeField] private LayerMask npcLayer;
    private Camera mainCamera;

    private List<NPCController> selectedNPCs = new List<NPCController>();
    public IReadOnlyList<NPCController> SelectedNPCs => selectedNPCs;

    // --- 追加: 詳細情報用の汎用選択オブジェクト ---
    public ISelectable CurrentSelected { get; private set; }

    public delegate void SelectionChangedHandler(ISelectable newSelection);
    public event SelectionChangedHandler OnSelectionChanged;
    // ---------------------------------------------

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
        if (GameManager.Instance != null && 
            (GameManager.Instance.CurrentPlayerMode == PlayerMode.Building ||
             GameManager.Instance.CurrentPlayerMode == PlayerMode.Gathering ||
             GameManager.Instance.CurrentPlayerMode == PlayerMode.Cancelling ||
             GameManager.Instance.CurrentPlayerMode == PlayerMode.StockpileZoning ||
             GameManager.Instance.CurrentPlayerMode == PlayerMode.Cutting ||
             GameManager.Instance.CurrentPlayerMode == PlayerMode.Picking))
            return; // 建築・伐採・キャンセル・ゾーニング・切る・採取モード中は選択無効

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
        
        // 全てのレイヤーを対象にレイキャストを行い、ISelectableを探す
        if (Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            ISelectable selectable = hit.collider.GetComponentInParent<ISelectable>();
            if (selectable != null)
            {
                // 選択対象がNPCControllerの場合
                if (selectable is NPCController npc)
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
                        DeselectAllExcept(npc);
                        Select(npc);
                    }
                }
                else
                {
                    // NPC以外のISelectable（ResourceNodeなど）をクリックした場合
                    // 既存のNPC選択は解除
                    DeselectAllNPCs();
                    SetCurrentSelected(selectable);
                }
                return;
            }
        }

        // 何もないところをクリックしたら選択解除
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
        {
            DeselectAllNPCs();
            SetCurrentSelected(null);
        }
    }

    private void SetCurrentSelected(ISelectable newSelection)
    {
        if (CurrentSelected != newSelection)
        {
            CurrentSelected = newSelection;
            OnSelectionChanged?.Invoke(CurrentSelected);
        }
    }

    private void Select(NPCController npc)
    {
        if (!selectedNPCs.Contains(npc))
        {
            selectedNPCs.Add(npc);
            npc.SetSelected(true);
            SetCurrentSelected(npc); // 最後に選択したNPCを詳細対象にする
        }
    }

    private void Deselect(NPCController npc)
    {
        if (selectedNPCs.Contains(npc))
        {
            selectedNPCs.Remove(npc);
            npc.SetSelected(false);
            
            // 現在の詳細選択オブジェクトが解除されたNPCだった場合、他の選択中NPCがあればそれに切り替える
            if (CurrentSelected == npc)
            {
                if (selectedNPCs.Count > 0)
                    SetCurrentSelected(selectedNPCs[selectedNPCs.Count - 1]);
                else
                    SetCurrentSelected(null);
            }
        }
    }

    public void DeselectAll()
    {
        DeselectAllNPCs();
        SetCurrentSelected(null);
    }

    private void DeselectAllNPCs()
    {
        foreach (var npc in selectedNPCs)
        {
            if (npc != null) npc.SetSelected(false);
        }
        selectedNPCs.Clear();
    }

    private void DeselectAllExcept(NPCController keepNpc)
    {
        // keepNpc以外のNPCの選択を解除する
        for (int i = selectedNPCs.Count - 1; i >= 0; i--)
        {
            NPCController npc = selectedNPCs[i];
            if (npc != keepNpc)
            {
                Deselect(npc);
            }
        }
    }
}