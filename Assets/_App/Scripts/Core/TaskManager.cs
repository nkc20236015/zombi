using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 伐採タスクの登録・自動割り当て・キャンセルを管理するシングルトン。
/// AxePanel でモードに入り、木をクリックするとタスクが登録される。
/// Idle状態のNPCが自動的に最寄りのタスクを取得して作業を開始する。
/// </summary>
public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance { get; private set; }

    /// <summary>登録されている全タスクのリスト</summary>
    private List<GatherTask> gatherTasks = new List<GatherTask>();

    /// <summary>外部から読み取り用</summary>
    public IReadOnlyList<GatherTask> GatherTasks => gatherTasks;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        // 未割り当てタスクに空きNPCを自動アサイン
        AssignIdleNPCsToTasks();

        // 完了・無効化したタスクをクリーンアップ
        CleanupTasks();
    }

    // ==================== Public API ====================

    /// <summary>
    /// 伐採タスクを登録する。既に同じノードのタスクがあれば無視。
    /// </summary>
    public bool RegisterGatherTask(ResourceNode node)
    {
        if (node == null || !node.HasResources) return false;

        // 重複チェック
        if (gatherTasks.Any(t => t.TargetNode == node && !t.IsCancelled))
        {
            Debug.Log($"[TaskManager] タスク重複: {node.gameObject.name} は既に登録済み");
            return false;
        }

        GatherTask task = new GatherTask
        {
            TargetNode = node,
            AssignedNPC = null,
            IsActive = false,
            IsCancelled = false
        };

        gatherTasks.Add(task);
        Debug.Log($"[TaskManager] タスク登録: {node.gameObject.name} ({node.Type})");
        return true;
    }

    /// <summary>
    /// 指定ノードのタスクをキャンセルする。
    /// 採取中のNPCがいれば作業を中断し、次のタスクに移る。
    /// </summary>
    public bool CancelGatherTask(ResourceNode node)
    {
        if (node == null) return false;

        GatherTask task = gatherTasks.FirstOrDefault(t => t.TargetNode == node && !t.IsCancelled);
        if (task == null)
        {
            Debug.Log($"[TaskManager] キャンセル対象なし: {node.gameObject.name}");
            return false;
        }

        task.IsCancelled = true;

        // 割り当て済みNPCがいれば作業中断
        if (task.AssignedNPC != null)
        {
            Debug.Log($"[TaskManager] タスクキャンセル: {node.gameObject.name} — NPC {task.AssignedNPC.gameObject.name} の作業を中断");
            task.AssignedNPC.CancelGathering();
            task.AssignedNPC = null;
        }
        else
        {
            Debug.Log($"[TaskManager] タスクキャンセル: {node.gameObject.name} (未割り当て)");
        }

        return true;
    }

    /// <summary>
    /// NPCがタスク完了を報告する。タスクリストから削除し、
    /// そのNPCに次の未割り当てタスクがあれば自動で割り当てる。
    /// </summary>
    public void ReportTaskComplete(NPCController npc)
    {
        GatherTask task = gatherTasks.FirstOrDefault(t => t.AssignedNPC == npc);
        if (task != null)
        {
            Debug.Log($"[TaskManager] タスク完了: {task.TargetNode?.gameObject.name ?? "null"}");
            gatherTasks.Remove(task);
        }

        // 完了したNPCに次のタスクを割り当て
        TryAssignTaskToNPC(npc);
    }

    /// <summary>
    /// 指定ノードが現在タスクに登録されているかチェック
    /// </summary>
    public bool HasTaskForNode(ResourceNode node)
    {
        return gatherTasks.Any(t => t.TargetNode == node && !t.IsCancelled);
    }

    // ==================== Internal ====================

    /// <summary>
    /// 未割り当てタスクにIdle状態のNPCを割り当てる。
    /// 最寄りのNPCを優先的にアサイン。
    /// </summary>
    private void AssignIdleNPCsToTasks()
    {
        // 未割り当てタスクを取得
        var unassignedTasks = gatherTasks
            .Where(t => !t.IsActive && !t.IsCancelled && t.TargetNode != null && t.TargetNode.HasResources)
            .ToList();

        if (unassignedTasks.Count == 0) return;

        // Idle状態のNPCを取得
        if (GameManager.Instance == null) return;
        var idleNPCs = GameManager.Instance.NPCs
            .Where(npc => npc != null && npc.CurrentState == NPCState.Idle)
            .ToList();

        if (idleNPCs.Count == 0) return;

        foreach (var task in unassignedTasks)
        {
            if (idleNPCs.Count == 0) break;

            // 最寄りのNPCを探す
            NPCController nearestNPC = null;
            float nearestDist = float.MaxValue;

            foreach (var npc in idleNPCs)
            {
                float dist = Vector3.Distance(npc.transform.position, task.TargetNode.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestNPC = npc;
                }
            }

            if (nearestNPC != null)
            {
                task.AssignedNPC = nearestNPC;
                task.IsActive = true;
                nearestNPC.GatherResource(task.TargetNode);
                idleNPCs.Remove(nearestNPC);
                Debug.Log($"[TaskManager] タスク割り当て: {task.TargetNode.gameObject.name} → {nearestNPC.gameObject.name}");
            }
        }
    }

    /// <summary>
    /// 指定NPCに未割り当てタスクがあれば割り当てる（タスク完了後の連続作業用）
    /// </summary>
    private void TryAssignTaskToNPC(NPCController npc)
    {
        if (npc == null || npc.CurrentState != NPCState.Idle) return;

        var unassignedTask = gatherTasks
            .Where(t => !t.IsActive && !t.IsCancelled && t.TargetNode != null && t.TargetNode.HasResources)
            .OrderBy(t => Vector3.Distance(npc.transform.position, t.TargetNode.transform.position))
            .FirstOrDefault();

        if (unassignedTask != null)
        {
            unassignedTask.AssignedNPC = npc;
            unassignedTask.IsActive = true;
            npc.GatherResource(unassignedTask.TargetNode);
            Debug.Log($"[TaskManager] 次タスク割り当て: {unassignedTask.TargetNode.gameObject.name} → {npc.gameObject.name}");
        }
    }

    /// <summary>
    /// キャンセル済みや無効になったタスクをリストから削除
    /// </summary>
    private void CleanupTasks()
    {
        gatherTasks.RemoveAll(t =>
            t.IsCancelled ||
            t.TargetNode == null ||
            (!t.TargetNode.gameObject.activeInHierarchy && t.AssignedNPC == null)
        );
    }
}

/// <summary>
/// 伐採タスクのデータクラス
/// </summary>
public class GatherTask
{
    public ResourceNode TargetNode;
    public NPCController AssignedNPC;
    public bool IsActive;      // NPC割り当て済みかどうか
    public bool IsCancelled;   // キャンセルされたか
}
