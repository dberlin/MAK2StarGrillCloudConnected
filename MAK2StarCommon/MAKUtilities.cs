namespace MAK2StarCommon
{
    public static class MAKUtilities
    {
        public static string FormatDeviceId(string grillId)
        {
            return $"MAK2StarGrillSingleDevice-{grillId}";
        }
    }
}