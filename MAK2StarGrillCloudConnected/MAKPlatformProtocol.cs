namespace MAK2StarGrillCloudConnected
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using Crestron.RAD.Common.Transports;
    using Crestron.RAD.DeviceTypes.Gateway;
    using Flurl;
    using Flurl.Http;
    using MAK2StarCommon;
    using MAK2StarCommon.GrillInfo;
    using MAK2StarCommon.GrillList;
    using MAK2StarGrillSingleDevice;
    using Newtonsoft.Json;

    public class MAKPlatformProtocol : AGatewayProtocol, IMAKPlatformProtocol
    {
        public MAKPlatformProtocol(string username, string password, ISerialTransport transport, byte id) : base(
            transport, id)
        {
            this.MAKUsername = username;
            this.MAKPassword = password;
            this.isAuthenticatedToMAK = false; }

        public void QueueSetPointChange(IMAKDevice device, int intVal)
        {
            this.makSetPointChanges.Enqueue((device, intVal));
        }

        public void Start()
        {
            this.EnableAutoPolling = true;
        }

        public void Stop()
        {
            this.makCookies = null;
            this.EnableAutoPolling = false;
        }

        /// <summary>
        ///     Connection changed event handler. Updates the status of child devices.
        /// </summary>
        /// <param name="connected">Whether the platform is connected or not.</param>
        protected override void ConnectionChangedEvent(bool connected)
        {
            // Make a copy since it may get updated
            MAKLogging.TraceMessage(this.EnableLogging, $"connection:{connected}");
            base.ConnectionChangedEvent(connected);
            // Return early if connection is valid. It tells us nothing about the lower level devices.
            if (connected) return;

            // We could reduce lock contention even further by only using the lock to make a copy of the device list
            // but this is not worth it for a list of 1-2 devices.
            lock (this.deviceUpdateLock)
            {
                MAKLogging.TraceMessage(this.EnableLogging, "acquired device update lock");
                foreach (var device in this.makDevices.Values)
                    device.SetConnectionStatus(false);
            }
        }

        /// <summary>
        ///     Poll event. Used to refresh grill information from the website.
        /// </summary>
        protected override void Poll()
        {
            MAKLogging.TraceMessage(this.EnableLogging);
            // Handle device management
            var authed = this.Reauthenticate();
            if (!authed)
            {
                this.ConnectionChanged(false);
                MAKLogging.TraceMessage(this.EnableLogging, "Authentication for polling failed");
                return;
            }

            this.ConnectionChanged(true);
            var grillList = this.RefreshGrillList();
            this.ProcessDeviceChanges(grillList);

            // Process any pending setpoint changes.  This has copies of devices and so doesn't need a lock
            var finalSetPoints = new Dictionary<IMAKDevice, int>();
            // Consolidate any setpoint changes.
            while (this.makSetPointChanges.TryDequeue(out var queuePair))
                finalSetPoints[queuePair.device] = queuePair.temperature;

            foreach (var devicePair in finalSetPoints)
                this.SetGrillTemperatureInternal(devicePair.Key, devicePair.Value);


            // We could reduce lock contention even further by only using the lock to make a copy of the device list
            // but this is not worth it for a list of 1-2 devices.
            lock (this.deviceUpdateLock)
            {
                MAKLogging.TraceMessage(this.EnableLogging, "acquired device update lock");
                // We could use events here, but as we are doing synchronous polling, for 1-2 devices, this seems
                // pointless.
                foreach (var device in this.makDevices.Values) this.SingleDeviceRefresh(device);
            }
        }


        /// <summary>
        ///     Change the setpoint for a given grill.
        /// </summary>
        /// <param name="grillDevice">The grill to change.</param>
        /// <param name="temperature">New setpoint temperature.</param>
        /// <returns>Whether the request was successful</returns>
        private bool SetGrillTemperatureInternal(IMAKDevice grillDevice, int temperature)
        {
            var grillId = grillDevice.GrillId;
            MAKLogging.TraceMessage(this.EnableLogging,
                $"Setting grill temperature for Grill ID {grillId} to temperature {temperature}");
            if (!this.isAuthenticatedToMAK)
            {
                var authed = this.Reauthenticate();
                if (!authed)
                    return false;
            }

            var result = MAKMobileGrillSetTempUrl.AppendPathSegment(grillId).WithCookies(this.makCookies)
                .OnRedirect(call =>
                {
                    // When we get redirected it means authentication failed.
                    call.Redirect.Follow = false;
                    // This is not perfectly thread safe, someone else could reauthenticate and set this to true
                    // after our authenticate check above and we will force a reauthenticate that is pointless.
                    this.isAuthenticatedToMAK = false;
                }).PostUrlEncodedAsync(new {SetPoint = temperature}).Result;
            return result.ResponseMessage.IsSuccessStatusCode;
        }


        /// <summary>
        ///     Refresh the grill information for a single device.
        /// </summary>
        /// <param name="device">Device to refresh.</param>
        private void SingleDeviceRefresh(in IMAKDevice device)
        {
            try
            {
                var grillInfo = this.RefreshSingleGrillInfo(device.GrillId);
                MAKLogging.TraceMessage(this.EnableLogging,
                    $"Received grill info:{grillInfo.ToPrettyJsonString()}");
                device.SetConnectionStatus(grillInfo.Connected);
                device.RefreshGrillDataHandler(grillInfo);
            }
            catch (Exception e)
            {
                MAKLogging.TraceMessage(this.EnableLogging, $"Error while refreshing grill: ${e}");
            }
        }

        /// <summary>
        ///     Process devices changes between the current list of devices and a new list of grills.
        /// </summary>
        /// <param name="grillList">New list of devices.</param>
        private void ProcessDeviceChanges(GrillListJson grillList)
        {
            MAKLogging.TraceMessage(this.EnableLogging);

            try
            {
                var grillListDiff = DifferenceOfGrillLists.GenerateDiff(this.lastPollGrillList, grillList.Data);
                MAKLogging.TraceMessage(this.EnableLogging,
                    $"Old grill list:{JsonConvert.SerializeObject(this.lastPollGrillList)}");
                MAKLogging.TraceMessage(this.EnableLogging,
                    $"New grill list:{JsonConvert.SerializeObject(grillList.Data)}");
                MAKLogging.TraceMessage(this.EnableLogging,
                    $"Added grill list:{JsonConvert.SerializeObject(grillListDiff.Added)}");
                MAKLogging.TraceMessage(this.EnableLogging,
                    $"Removed grill list:{JsonConvert.SerializeObject(grillListDiff.Removed)}");
                MAKLogging.TraceMessage(this.EnableLogging,
                    $"Changed grill list:{JsonConvert.SerializeObject(grillListDiff.Changed)}");
                lock (this.deviceUpdateLock)
                {
                    MAKLogging.TraceMessage(this.EnableLogging, "acquired device update lock");
                    foreach (var entry in grillListDiff.Removed)
                    {
                        var deviceId = MAKUtilities.FormatDeviceId(entry.GrillId);
                        MAKLogging.TraceMessage(this.EnableLogging, $"Removing paired device for ID: {deviceId}");
                        if (!this.makDevices.ContainsKey(deviceId))
                        {
                            MAKLogging.TraceMessage(this.EnableLogging,
                                $"When trying to remove: missing key {deviceId} in the device dictionary!");
                            continue;
                        }

                        this.makDevices.Remove(deviceId);
                        this.RemovePairedDevice(deviceId);
                    }

                    foreach (var entry in grillListDiff.Added)
                    {
                        var deviceId = MAKUtilities.FormatDeviceId(entry.GrillId);
                        MAKLogging.TraceMessage(this.EnableLogging, $"Adding paired device for ID: {deviceId}");
                        if (this.makDevices.ContainsKey(deviceId))
                        {
                            MAKLogging.TraceMessage(this.EnableLogging,
                                $"When trying to add: Already have key {deviceId} in the device dictionary!");
                            continue;
                        }

                        var deviceInstance = new MAK2StarGrillSingleDevice(entry.GrillId, entry.Name, this);
                        var pairedDeviceInfo = new GatewayPairedDeviceInformation(deviceId,
                            deviceInstance.GrillName, deviceInstance.Description, deviceInstance.Manufacturer,
                            deviceInstance.Model, deviceInstance.DeviceType,
                            deviceInstance.DeviceSubType);
                        this.makDevices.Add(deviceId, deviceInstance);
                        this.AddPairedDevice(pairedDeviceInfo, deviceInstance);
                        deviceInstance.SetConnectionStatus(this.IsConnected);
                    }

                    foreach (var entry in grillListDiff.Changed)
                    {
                        var beforeDeviceId = MAKUtilities.FormatDeviceId(entry.Before.GrillId);

                        if (!this.makDevices.ContainsKey(beforeDeviceId))
                        {
                            MAKLogging.TraceMessage(this.EnableLogging,
                                $"When trying to change: missing key {beforeDeviceId} in the device dictionary!");
                            continue;
                        }

                        var deviceInstance = this.makDevices[beforeDeviceId];
                        var pairedDeviceInfo = new GatewayPairedDeviceInformation(beforeDeviceId,
                            entry.After.Name, deviceInstance.Description, deviceInstance.Manufacturer,
                            deviceInstance.Model, deviceInstance.DeviceType,
                            deviceInstance.DeviceSubType);
                        deviceInstance.SetGrillName(entry.After.Name);
                        this.UpdatePairedDevice(beforeDeviceId, pairedDeviceInfo);
                    }

                    this.lastPollGrillList = grillList.Data;
                }
            }
            catch (Exception e)
            {
                MAKLogging.TraceMessage(this.EnableLogging, $"Exception processing devices:{e}");
            }
        }

        /// <summary>
        ///     Reauthenticate to the MAK Mobile platform.
        /// </summary>
        /// <returns>True if we successfully authenticated, false otherwise.</returns>
        private bool Reauthenticate()
        {
            lock (this.authenticationLock)
            {
                if (this.isAuthenticatedToMAK) return true;
                try
                {
                    MAKMobileLoginUrl.WithCookies(out this.makCookies).OnRedirect(call =>
                    {
                        call.Redirect.Follow = false;
                        this.isAuthenticatedToMAK = true;
                        MAKLogging.TraceMessage(this.EnableLogging, "Logged in");
                    }).PostUrlEncodedAsync(new
                    {
                        Username = this.MAKUsername,
                        Password = this.MAKPassword,
                        RememberMe = "false",
                    }).Wait();
                }
                catch (AggregateException e)
                {
                    MAKLogging.TraceMessage(this.EnableLogging, $"Error authenticating:{e}");
                }

                return this.isAuthenticatedToMAK;
            }
        }

        /// <summary>
        ///     Refresh the list of grills associated with a given user account.
        /// </summary>
        /// <returns>Grill list JSON</returns>
        private GrillListJson RefreshGrillList()
        {
            if (!this.isAuthenticatedToMAK)
            {
                var authed = this.Reauthenticate();
                if (!authed)
                    return null;
            }

            return MAKMobileGrillListUrl.WithCookies(this.makCookies).OnRedirect(call =>
            {
                // When we get redirected it means authentication failed.
                call.Redirect.Follow = false;
                // This is not perfectly thread safe, someone else could reauthenticate and set this to true, and we
                // will force a reauthenticate that is pointless
                this.isAuthenticatedToMAK = false;
            }).PostUrlEncodedAsync(new
            {
                group = string.Empty,
                filter = string.Empty,
                sort = string.Empty,
            }).ReceiveJson<GrillListJson>().Result;
        }

        /// <summary>
        ///     Refresh a single grill's information from the MAK Mobile website.
        /// </summary>
        /// <param name="grillId">The grill to refresh.</param>
        /// <returns>GrillInfoJSON from the website</returns>
        private GrillInfoJson RefreshSingleGrillInfo(string grillId)
        {
            if (!this.isAuthenticatedToMAK)
            {
                var authed = this.Reauthenticate();
                if (!authed)
                    return null;
            }

            return MAKMobileGrillDataUrl.AppendPathSegment(grillId).WithCookies(this.makCookies)
                .OnRedirect(call =>
                {
                    // When we get redirected it means authentication failed - we don't need to follow the call.
                    call.Redirect.Follow = false;
                    // This is not perfectly thread safe, someone else could reauthenticate and set this to true
                    // after our authenticate check above and we will force a reauthenticate that is pointless.
                    this.isAuthenticatedToMAK = false;
                }).PostAsync().ReceiveJson<GrillInfoJson>().Result;
        }

        #region Constants

        private const string MAKMobileUrl = "http://makgrillsmobile.com";
        private static readonly string MAKMobileHomeUrl = MAKMobileUrl.AppendPathSegment("Home");
        private static readonly string MAKMobileLoginUrl = MAKMobileHomeUrl.AppendPathSegment("Login");
        private static readonly string MAKMobileGrillListUrl = MAKMobileHomeUrl.AppendPathSegment("GrillsRead");
        private static readonly string MAKMobileGrillUrl = MAKMobileUrl.AppendPathSegment("Grill");
        private static readonly string MAKMobileGrillDataUrl = MAKMobileGrillUrl.AppendPathSegment("GetAjaxGrillData");
        private static readonly string MAKMobileGrillSetTempUrl = MAKMobileGrillUrl.AppendPathSegment("SetGrillTemp");

        #endregion Constants

        #region Fields

        internal string MAKUsername { get; set; }
        internal string MAKPassword { get; set; }

        // Set of cookies from the MAK website
        private CookieJar makCookies;

        // Whether we are authenticated to the MAK platform
        private bool isAuthenticatedToMAK;

        // List of instantiated devices
        private readonly Dictionary<string, IMAKDevice> makDevices = new Dictionary<string, IMAKDevice>();

        // Queue of setpoint changes requested by devices
        private readonly ConcurrentQueue<(IMAKDevice device, int temperature)> makSetPointChanges =
            new ConcurrentQueue<(IMAKDevice device, int temperature)>();

        // This is the grill list that we received at the last poll.
        // It is used to track added/changed/removed devices
        private List<GrillListEntry> lastPollGrillList;

        // This lock ensures only one thing tries to authenticate to the website at once
        private readonly object authenticationLock = new object();

        // This lock is locked by things reading or writing the list of devices.  
        // There is not enough contention to be worth using a ReaderWriterLockSlim
        private readonly object deviceUpdateLock = new object();

        #endregion Fields
    }
}