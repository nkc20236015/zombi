using UnityEngine;
using UnityEngine.EventSystems;

namespace Zombi.UI
{
    /// <summary>
    /// UI要素にアタッチし、マウスホバー時にTooltipManagerを呼び出す。
    /// </summary>
    public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [TextArea]
        public string tooltipText = "ボタンの説明";

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.ShowTooltip(tooltipText);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.HideTooltip();
            }
        }

        void OnDisable()
        {
            // UIが非表示になったら確実にツールチップも消す
            if (TooltipManager.Instance != null && TooltipManager.Instance.gameObject.activeInHierarchy)
            {
                TooltipManager.Instance.HideTooltip();
            }
        }
    }
}
