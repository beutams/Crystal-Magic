namespace CrystalMagic.Core
{
    public readonly struct CommonGameEvent : IGameEvent
    {
        public CommonGameEvent(string eventName, object data = null)
        {
            EventName = eventName;
            Data = data;
        }

        public string EventName { get; }
        public object Data { get; }

        public T GetData<T>()
        {
            if (Data is T typedData)
                return typedData;

            return default;
        }
    }
}
