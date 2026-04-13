using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class ButtonPlus : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float _clickStateDuration = 0.2f;
    [SerializeField] private bool _canClickInDuration = true;

    [SerializeField] private Transform defaultTransforms;
    [SerializeField] private Transform enterTransforms;
    [SerializeField] private Transform clickTransforms;

    public UnityEvent onClick;

    private ButtonState _state = ButtonState.Default;
    private bool _pointerInside;
    private Coroutine _clickRoutine;

    public ButtonState State => _state;

    private void OnEnable()
    {
        _pointerInside = false;
        SetState(ButtonState.Default);
    }

    private void OnDisable()
    {
        if (_clickRoutine != null)
        {
            StopCoroutine(_clickRoutine);
            _clickRoutine = null;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_canClickInDuration && _clickRoutine != null)
            return;

        onClick?.Invoke();

        if (_clickRoutine != null)
            StopCoroutine(_clickRoutine);
        _clickRoutine = StartCoroutine(ClickStateRoutine());
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerInside = true;
        if (_clickRoutine != null)
            return;
        SetState(ButtonState.Enter);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerInside = false;
        if (_clickRoutine != null)
            return;
        SetState(ButtonState.Default);
    }

    private IEnumerator ClickStateRoutine()
    {
        SetState(ButtonState.Click);
        yield return new WaitForSeconds(_clickStateDuration);
        _clickRoutine = null;
        SetState(_pointerInside ? ButtonState.Enter : ButtonState.Default);
    }

    private void SetState(ButtonState newState)
    {
        _state = newState;
        ApplyStateRoots();
    }

    private void ApplyStateRoots()
    {
        SetRootActive(defaultTransforms, _state == ButtonState.Default);
        SetRootActive(enterTransforms, _state == ButtonState.Enter);
        SetRootActive(clickTransforms, _state == ButtonState.Click);
    }

    private static void SetRootActive(Transform root, bool active)
    {
        if (root == null)
            return;
        if (root.gameObject.activeSelf != active)
            root.gameObject.SetActive(active);
    }
}

public enum ButtonState
{
    Default,
    Enter,
    Click
}
