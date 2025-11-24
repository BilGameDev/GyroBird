using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonSelector : MonoBehaviour
{
    [SerializeField] Image[] uiImages;
    [SerializeField] RawImage[] graphics;
    [SerializeField] TextMeshProUGUI[] texts;

    [Header("Settings")]
    [SerializeField] Color selectedColor;
    [SerializeField] Color unselectedColor;

    public void SelectButton()
    {
        foreach (var uiImage in uiImages)
        {
            uiImage.color = selectedColor;
        }

        foreach (var graphic in graphics)
        {
            graphic.color = selectedColor;
        }

        foreach (var text in texts)
        {
            text.color = selectedColor;
        }
    }

    public void DeselectButton()
    {
       foreach (var uiImage in uiImages)
        {
            uiImage.color = unselectedColor;
        }

        foreach (var graphic in graphics)
        {
            graphic.color = unselectedColor;
        }

        foreach (var text in texts)
        {
            text.color = unselectedColor;
        }
    }
}
