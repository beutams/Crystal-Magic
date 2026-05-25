using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;

namespace CrystalMagic.UI
{
    public readonly struct InteractionSelectUIOpenData
    {
        public InteractionSelectUIOpenData(
            IReadOnlyList<NPCSelectOptionData> options,
            Action<NPCSelectOptionData> selectAction)
        {
            Options = options;
            SelectAction = selectAction;
        }

        public IReadOnlyList<NPCSelectOptionData> Options { get; }
        public Action<NPCSelectOptionData> SelectAction { get; }
    }

    public sealed class InteractionSelectOptionDisplayData
    {
        public NPCSelectOptionData Option { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class InteractionSelectUIModel : UIModelBase, IUIOpenDataReceiver<InteractionSelectUIOpenData>
    {
        public const string DataChangedEventName = "InteractionSelectUIModel.DataChanged";
        public override string ChangedEventName => DataChangedEventName;

        private readonly List<InteractionSelectOptionDisplayData> _options = new();

        public IReadOnlyList<InteractionSelectOptionDisplayData> Options => _options;
        public Action<NPCSelectOptionData> SelectAction { get; private set; }

        public void SetOpenData(InteractionSelectUIOpenData data)
        {
            SelectAction = data.SelectAction;
            RebuildOptions(data.Options);
        }

        public void ConfirmSelection(InteractionSelectOptionDisplayData option)
        {
            if (option?.Option == null)
                return;

            SelectAction?.Invoke(option.Option);
        }

        public override void Dispose()
        {
            SelectAction = null;
            _options.Clear();
        }

        private void RebuildOptions(IReadOnlyList<NPCSelectOptionData> options)
        {
            _options.Clear();

            if (options != null)
            {
                for (int i = 0; i < options.Count; i++)
                {
                    NPCSelectOptionData option = options[i];
                    if (option == null)
                        continue;

                    _options.Add(new InteractionSelectOptionDisplayData
                    {
                        Option = option,
                        DisplayName = option.DisplayName ?? string.Empty,
                    });
                }
            }

            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }
    }
}
