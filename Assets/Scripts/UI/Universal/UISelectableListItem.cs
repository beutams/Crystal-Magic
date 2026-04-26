using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class UISelectableListItem : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] private GameObject _defaultRoot;
    [SerializeField] private GameObject _selectedRoot;
    [SerializeField] private GameObject _activeRoot;

    private UISelectableListGroup _group;
    private bool _isPressed;

    public bool IsSelected { get; private set; }

    private void OnEnable()
    {
        RegisterToGroup();
        ApplyVisualState();
    }

    private void OnDisable()
    {
        _isPressed = false;

        if (_group != null)
            _group.Unregister(this);
    }

    private void OnTransformParentChanged()
    {
        RegisterToGroup();
        ApplyVisualState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_group != null)
        {
            _group.Select(this);
        }
        else
        {
            SetSelectedInternal(true);
        }

        _isPressed = true;
        ApplyVisualState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        ApplyVisualState();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_group == null)
            SetSelectedInternal(true);
    }

    public void SetSelected(bool selected)
    {
        if (selected)
        {
            if (_group != null)
                _group.Select(this);
            else
                SetSelectedInternal(true);
        }
        else
        {
            SetSelectedInternal(false);
        }
    }

    public void SetSelectedInternal(bool selected)
    {
        IsSelected = selected;
        ApplyVisualState();
    }

    private void RegisterToGroup()
    {
        UISelectableListGroup newGroup = GetComponentInParent<UISelectableListGroup>();
        if (_group == newGroup)
            return;

        if (_group != null)
            _group.Unregister(this);

        _group = newGroup;

        if (_group != null && isActiveAndEnabled)
            _group.Register(this);
    }

    private void ApplyVisualState()
    {
        bool showActive = _isPressed && _activeRoot != null;
        bool showSelected = !showActive && IsSelected && _selectedRoot != null;
        bool showDefault = !showActive && !showSelected;

        SetRootActive(_defaultRoot, showDefault);
        SetRootActive(_selectedRoot, showSelected);
        SetRootActive(_activeRoot, showActive);
    }

    private static void SetRootActive(GameObject root, bool active)
    {
        if (root == null)
            return;

        if (root.activeSelf != active)
            root.SetActive(active);
    }
}
