using CrystalMagic.Core;
using UnityEngine.UI;

public class ShopItemInfoUI : UIBase<ShopItemInfoUIData>
{
    private CrystalMagic.UI.ShopItemInfoUIModel _model;
    private bool _isOpened;
    private bool _isModelEventSubscribed;

    public void BindModel(CrystalMagic.UI.ShopItemInfoUIModel model)
    {
        if (_model == model)
            return;

        if (_model != null && _isOpened)
            UnsubscribeModelEvents();

        _model = model;

        if (_model != null && _isOpened)
        {
            SubscribeModelEvents();
            Render();
        }
    }

    protected override void OnInit()
    {
        base.OnInit();
    }

    public override void OnOpen()
    {
        _isOpened = true;
        SubscribeModelEvents();

        if (_model != null)
            Render();
    }

    public override void OnClose()
    {
        UnsubscribeModelEvents();
        _isOpened = false;
    }

    private void Render()
    {
        UI.Name.TextMeshProUGUI.text = _model != null ? _model.Name : string.Empty;
        UI.HaveCount.TextMeshProUGUI.text = _model != null ? _model.HaveCount.ToString() : "0";
        UI.Description.TextMeshProUGUI.text = _model != null ? _model.Description : string.Empty;
        UI.Price.TextMeshProUGUI.text = _model != null ? _model.Price.ToString() : string.Empty;
        UI.Icon.Image.sprite = LoadIcon(_model != null ? _model.IconPath : string.Empty);
    }

    private void OnModelChanged(CommonGameEvent gameEvent)
    {
        CrystalMagic.UI.ShopItemInfoUIModel eventModel = gameEvent.GetData<CrystalMagic.UI.ShopItemInfoUIModel>();
        if (eventModel != _model)
            return;

        Render();
    }

    private void SubscribeModelEvents()
    {
        if (_isModelEventSubscribed || _model == null)
            return;

        EventComponent.Instance.Subscribe(new CommonGameEvent(CrystalMagic.UI.ShopItemInfoUIModel.DataChangedEventName), OnModelChanged);
        _isModelEventSubscribed = true;
    }

    private void UnsubscribeModelEvents()
    {
        if (!_isModelEventSubscribed)
            return;

        EventComponent.Instance.Unsubscribe(new CommonGameEvent(CrystalMagic.UI.ShopItemInfoUIModel.DataChangedEventName), OnModelChanged);
        _isModelEventSubscribed = false;
    }


    private UnityEngine.Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return ResourceComponent.Instance.Load<UnityEngine.Sprite>(iconPath);
    }
}
