using UnityEngine;

public class HoverHighlight : MonoBehaviour
{
    private Renderer[] renderers;
    private MaterialPropertyBlock[] propBlocks;
    private Color highlightTint = new Color(1.8f, 1.8f, 1.8f, 1f); // より明確に明るくする

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        propBlocks = new MaterialPropertyBlock[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            propBlocks[i] = new MaterialPropertyBlock();
            renderers[i].GetPropertyBlock(propBlocks[i]);
        }
    }

    void OnMouseEnter()
    {
        // ゴースト表示等の建築モード中は無視
        if (GameManager.Instance != null && GameManager.Instance.CurrentPlayerMode == PlayerMode.Building) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].GetPropertyBlock(propBlocks[i]);
                propBlocks[i].SetColor("_Color", highlightTint);
                propBlocks[i].SetColor("_BaseColor", highlightTint);
                renderers[i].SetPropertyBlock(propBlocks[i]);
            }
        }
    }

    void OnMouseExit()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].GetPropertyBlock(propBlocks[i]);
                propBlocks[i].SetColor("_Color", Color.white);
                propBlocks[i].SetColor("_BaseColor", Color.white);
                renderers[i].SetPropertyBlock(propBlocks[i]);
            }
        }
    }
}
