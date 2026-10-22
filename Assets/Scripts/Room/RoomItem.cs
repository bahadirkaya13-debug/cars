using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class RoomItem : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    static RoomItem selected;

    RectTransform rectTransform;
    Canvas canvas;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    void Update()
    {
        if (selected != this)
            return;

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            RotateClockwise();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        BringToFront();
    }

    public void OnDrag(PointerEventData eventData)
    {
        BringToFront();

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        float scale = canvas != null ? canvas.scaleFactor : 1f;
        rectTransform.anchoredPosition += eventData.delta / scale;
    }

    void BringToFront()
    {
        selected = this;
        transform.SetAsLastSibling();
    }

    void RotateClockwise()
    {
        rectTransform.Rotate(0f, 0f, -90f);
    }
}
