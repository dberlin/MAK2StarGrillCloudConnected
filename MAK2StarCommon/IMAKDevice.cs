namespace MAK2StarCommon
{
    using GrillInfo;

    public interface IMAKDevice
    {
        string DeviceId { get; }
        string GrillId { get; }
        string GrillName { get; }
        string Description { get; }
        string Manufacturer { get; }
        string Model { get; }
        string DeviceType { get; }
        string DeviceSubType { get; }
        void SetConnectionStatus(bool connected);
        void SetGrillName(string name);
        void RefreshGrillDataHandler(GrillInfoJson grillInfo);
    }
}