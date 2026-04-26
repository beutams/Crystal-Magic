using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class UISelectableListGroup : MonoBehaviour
{
    private readonly List<UISelectableListItem> _items = new();

    public UISelectableListItem SelectedItem { get; private set; }

    public void Register(UISelectableListItem item)
    {
        if (item == null || _items.Contains(item))
            return;

        _items.Add(item);

        if (item.IsSelected)
        {
            Select(item);
        }
        else
        {
            item.SetSelectedInternal(SelectedItem == item);
        }
    }

    public void Unregister(UISelectableListItem item)
    {
        if (item == null)
            return;

        _items.Remove(item);

        if (SelectedItem == item)
            SelectedItem = null;
    }

    public void Select(UISelectableListItem item)
    {
        if (item == null)
            return;

        if (!_items.Contains(item))
            Register(item);

        SelectedItem = item;

        for (int i = 0; i < _items.Count; i++)
        {
            UISelectableListItem currentItem = _items[i];
            if (currentItem == null)
                continue;

            currentItem.SetSelectedInternal(currentItem == item);
        }
    }

    public void ClearSelection()
    {
        SelectedItem = null;

        for (int i = 0; i < _items.Count; i++)
        {
            UISelectableListItem item = _items[i];
            if (item == null)
                continue;

            item.SetSelectedInternal(false);
        }
    }
}
