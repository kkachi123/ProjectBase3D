using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIActionBindingButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private BoolReactiveProperty _isPressed = new BoolReactiveProperty(false);
    public IReadOnlyReactiveProperty<bool> IsPressed => _isPressed;

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed.Value = true;
    }
    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed.Value = false;
    }
}
