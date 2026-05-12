using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// ゲーム全体の資源を管理するシングルトン。
/// 資源の増減とイベント通知を担当する。
/// </summary>
public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }

    /// <summary>資源量が変化したとき (ResourceType, 現在量)</summary>
    public event Action<ResourceType, int> OnResourceChanged;

    // 各資源の現在量
    private Dictionary<ResourceType, int> resources = new Dictionary<ResourceType, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 全ResourceTypeを0で初期化
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            resources[type] = 0;
        }
    }

    /// <summary>
    /// 指定した資源を追加する。
    /// </summary>
    public void AddResource(ResourceType type, int amount)
    {
        if (amount <= 0) return;

        resources[type] += amount;
        Debug.Log($"[ResourceManager] +{amount} {type} (Total: {resources[type]})");
        OnResourceChanged?.Invoke(type, resources[type]);
    }

    /// <summary>
    /// 指定した資源を消費する。足りない場合はfalseを返す。
    /// </summary>
    public bool SpendResource(ResourceType type, int amount)
    {
        if (amount <= 0) return true;
        if (resources[type] < amount) return false;

        resources[type] -= amount;
        Debug.Log($"[ResourceManager] -{amount} {type} (Total: {resources[type]})");
        OnResourceChanged?.Invoke(type, resources[type]);
        return true;
    }

    /// <summary>
    /// 指定した資源の現在量を取得する。
    /// </summary>
    public int GetResource(ResourceType type)
    {
        return resources.ContainsKey(type) ? resources[type] : 0;
    }

    /// <summary>
    /// 指定した資源が十分にあるか確認する。
    /// </summary>
    public bool HasEnough(ResourceType type, int amount)
    {
        return resources.ContainsKey(type) && resources[type] >= amount;
    }
}
