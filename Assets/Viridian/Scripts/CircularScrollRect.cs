using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[ExecuteAlways]
public class CircularScrollRect : MonoBehaviour, IDragHandler, IScrollHandler
{
    [Header("Settings")]
    public RectTransform content;
    public float radius = 250f;
    public float angleSpacing = 20f;   // degrees between items
    public float scrollSpeed = 1f;
    public bool clockwise = true;
    public bool centerSelected = false;

    private float currentAngle = 0f;
    private List<RectTransform> items = new();

    void Start()
    {
        Refresh();
    }

    void OnValidate()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (!content) return;

        items.Clear();
        for (int i = 0; i < content.childCount; i++)
        {
            if (content.GetChild(i) is RectTransform rt)
                items.Add(rt);
        }

        Layout();
    }

    private void Layout()
    {
        float startAngle = currentAngle;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            float angle = startAngle + i * angleSpacing * (clockwise ? -1f : 1f);
            float rad = angle * Mathf.Deg2Rad;

            Vector2 pos = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * radius;
            item.anchoredPosition = pos;

            float rotZ = -angle; // optional rotation for facing center
            item.localRotation = Quaternion.Euler(0, 0, rotZ);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        currentAngle += eventData.delta.x * scrollSpeed * (clockwise ? 1f : -1f);
        Layout();
    }

    public void OnScroll(PointerEventData eventData)
    {
        currentAngle += eventData.scrollDelta.y * 10f * (clockwise ? 1f : -1f);
        Layout();
    }
}
