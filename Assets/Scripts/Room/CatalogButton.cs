using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class CatalogButton : MonoBehaviour
{
    [SerializeField] RoomItem itemPrefab;
    [SerializeField] RectTransform roomParent;

    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(SpawnItem);
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(SpawnItem);
    }

    public void SpawnItem()
    {
        if (itemPrefab == null)
        {
            Debug.LogWarning("CatalogButton: Item Prefab atanmamış.", this);
            return;
        }

        RectTransform parent = roomParent != null
            ? roomParent
            : GetComponentInParent<Canvas>()?.transform as RectTransform;

        if (parent == null)
        {
            Debug.LogWarning("CatalogButton: Eşyanın oluşacağı bir Canvas veya Room Parent yok.", this);
            return;
        }

        RoomItem item = Instantiate(itemPrefab, parent);
        RectTransform rect = item.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        item.transform.SetAsLastSibling();
    }
}
