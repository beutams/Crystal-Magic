namespace CrystalMagic.UI
{
    public sealed class UnitHealthBarUIModel : UIModelBase
    {
        public const string DataChangedEventName = "UnitHealthBarUIModel.DataChanged";
        public override string ChangedEventName => DataChangedEventName;

        public float CurrentHealth { get; private set; }
        public float MaxHealth { get; private set; }
        public UnityEngine.Vector2 AnchoredPosition { get; private set; }
        public bool Visible { get; private set; }

        public void UpdateDisplay(float currentHealth, float maxHealth, UnityEngine.Vector2 anchoredPosition, bool visible)
        {
            bool changed = false;

            if (!UnityEngine.Mathf.Approximately(CurrentHealth, currentHealth))
            {
                CurrentHealth = currentHealth;
                changed = true;
            }

            if (!UnityEngine.Mathf.Approximately(MaxHealth, maxHealth))
            {
                MaxHealth = maxHealth;
                changed = true;
            }

            if (AnchoredPosition != anchoredPosition)
            {
                AnchoredPosition = anchoredPosition;
                changed = true;
            }

            if (Visible != visible)
            {
                Visible = visible;
                changed = true;
            }

            if (changed)
                CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.CommonGameEvent(DataChangedEventName, this));
        }

        public void SetVisible(bool visible)
        {
            if (Visible == visible)
                return;

            Visible = visible;
            CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.CommonGameEvent(DataChangedEventName, this));
        }

        public void SetHealth(float currentHealth, float maxHealth)
        {
            bool changed = false;

            if (!UnityEngine.Mathf.Approximately(CurrentHealth, currentHealth))
            {
                CurrentHealth = currentHealth;
                changed = true;
            }

            if (!UnityEngine.Mathf.Approximately(MaxHealth, maxHealth))
            {
                MaxHealth = maxHealth;
                changed = true;
            }

            if (changed)
                CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.CommonGameEvent(DataChangedEventName, this));
        }
    }
}
