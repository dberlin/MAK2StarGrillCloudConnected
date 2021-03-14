namespace MAK2StarCommon
{
    public interface IMAKPlatformProtocol
    {
        void QueueSetPointChange(IMAKDevice device, int intVal);
    }
}