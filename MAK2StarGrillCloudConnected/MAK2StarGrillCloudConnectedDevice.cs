namespace MAK2StarGrillCloudConnected
{
    using System.Globalization;
    using Crestron.RAD.Common.Interfaces;
    using Crestron.RAD.DeviceTypes.Gateway;
    using Crestron.SimplSharp;
    using Flurl.Http;
    using Flurl.Http.Configuration;
    using MAK2StarCommon;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class MAK2StarGrillCloudConnectedDevice : AGateway, ICloudConnected
    {
        public MAK2StarGrillCloudConnectedDevice()
        {
            FlurlHttp.Configure(settings =>
            {
                var jsonSettings = new JsonSerializerSettings
                {
                    Converters =
                    {
                        new IsoDateTimeConverter {DateTimeStyles = DateTimeStyles.AssumeUniversal},
                    },
                };
                settings.JsonSerializer = new NewtonsoftJsonSerializer(jsonSettings);
            });
            /*this.EnableLogging = true;
            this.EnableRxDebug = true;
            this.EnableTxDebug = true;*/
        }

        public void Initialize()
        {
            MAKLogging.TraceMessage(this.EnableLogging, "Initializing MAK driver");
            this.ConnectionTransport = new MAK2StarGrillTransport();
            var makPlatformProtocol = new MAKPlatformProtocol(this.driverUsername, this.driverPassword,
                this.ConnectionTransport,
                this.Id);
            this.Protocol = makPlatformProtocol;
            this.Protocol.Initialize(this.DriverData);
        }

        public override void Connect()
        {
            base.Connect();
            if (this.Protocol == null)
            {
                MAKLogging.TraceMessage(this.EnableLogging, "MAK Platform Protocol was null when Connect was called");
                ErrorLog.Error("MAK Platform Protocol was null when Connect was called");

                return;
            }

            ((MAKPlatformProtocol) this.Protocol).Start();
        }

        public override void Disconnect()
        {
            base.Disconnect();
            if (this.Protocol == null)
            {
                MAKLogging.TraceMessage(this.EnableLogging,
                    "MAK Platform Protocol was null when Disconnect was called");
                ErrorLog.Error("MAK Platform Protocol was null when Disconnect was called");
                return;
            }

            ((MAKPlatformProtocol) this.Protocol).Stop();
        }

        public override void OverridePassword(string password)
        {
            this.driverPassword = password;
            if (this.Protocol != null)
                ((MAKPlatformProtocol) this.Protocol).MAKPassword = password;
        }

        public override void OverrideUsername(string username)
        {
            this.driverUsername = username;
            if (this.Protocol != null)
                ((MAKPlatformProtocol) this.Protocol).MAKUsername = username;
        }

        #region Private fields

        private string driverPassword;
        private string driverUsername;

        #endregion Private fields

        #region Driver overrides

        public override bool SupportsDisconnect => true;
        public override bool SupportsReconnect => true;

        #endregion Driver overrides
    }
}