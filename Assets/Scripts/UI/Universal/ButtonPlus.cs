using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class ButtonPlus : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float _clickStateDuration = 0.2f;
    [SerializeField] private bool _canClickInDuration = true;
    [SerializeField, Min(0f)] private float _hoverScaleMultiplier = 1f;

    [SerializeField] private Transform defaultTransforms;
    [SerializeField] private Transform enterTransforms;
    [SerializeField] private Transform clickTransforms;

    public UnityEvent onClick;

    private ButtonState _state = ButtonState.Default;
    private bool _pointerInside;
    private bool _clickAccepted;
    private Coroutine _clickRoutine;
    private Vector3 _defaultScale;

    public ButtonState State => _state;

    private void OnEnable()
    {
        _defaultScale = transform.localScale;
        _pointerInside = false;
        ApplyHoverScale();
        SetState(ButtonState.Default);
    }

    private void OnDisable()
    {
        if (_clickRoutine != null)
        {
            StopCoroutine(_clickRoutine);
            _clickRoutine = null;
        }

        _clickAccepted = false;
        transform.localScale = _defaultScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_canClickInDuration && _clickRoutine != null)
        {
            _clickAccepted = false;
            return;
        }

        _clickAccepted = true;

        if (_clickRoutine != null)
        {
            StopCoroutine(_clickRoutine);
            _clickRoutine = null;
        }

        if (clickTransforms == null)
            return;

        SetState(ButtonState.Click);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_clickAccepted)
            return;

        _clickAccepted = false;

        if (clickTransforms != null)
            _clickRoutine = StartCoroutine(ClickStateRoutine());

        onClick?.Invoke();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _pointerInside = true;
        ApplyHoverScale();
        if (_clickRoutine != null)
            return;
        SetState(ButtonState.Enter);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _pointerInside = false;
        ApplyHoverScale();
        if (_clickRoutine != null)
            return;
        SetState(ButtonState.Default);
    }

    private IEnumerator ClickStateRoutine()
    {
        yield return new WaitForSeconds(_clickStateDuration);
        _clickRoutine = null;
        SetState(_pointerInside && enterTransforms != null ? ButtonState.Enter : ButtonState.Default);
    }

    private bool SetState(ButtonState newState)
    {
        if (GetStateRoot(newState) == null)
            return false;

        _state = newState;
        ApplyStateRoots();
        return true;
    }

    private void ApplyStateRoots()
    {
        SetRootActive(defaultTransforms, _state == ButtonState.Default);
        SetRootActive(enterTransforms, _state == ButtonState.Enter);
        SetRootActive(clickTransforms, _state == ButtonState.Click);
    }

    private void ApplyHoverScale()
    {
        float multiplier = _pointerInside ? _hoverScaleMultiplier : 1f;
        transform.localScale = _defaultScale * multiplier;
    }

    private Transform GetStateRoot(ButtonState state)
    {
        return state switch
        {
            ButtonState.Default => defaultTransforms,
            ButtonState.Enter => enterTransforms,
            ButtonState.Click => clickTransforms,
            _ => null,
        };
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
