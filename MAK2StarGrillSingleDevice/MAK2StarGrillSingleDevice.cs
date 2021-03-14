// <copyright file="MAK2StarGrillCloudConnected.cs" company="Daniel Berlin">
// Copyright (c) Daniel Berlin. All rights reserved.
// </copyright>

namespace MAK2StarGrillSingleDevice
{
    using System;
    using System.Threading.Tasks;
    using Crestron.RAD.Common.Enums;
    using Crestron.RAD.Common.Interfaces;
    using Crestron.RAD.Common.Interfaces.ExtensionDevice;
    using Crestron.RAD.DeviceTypes.ExtensionDevice;
    using MAK2StarCommon;
    using MAK2StarCommon.GrillInfo;

    /// <summary>
    ///     Main driver for MAK 2 Star Grill.
    /// </summary>
    public sealed class MAK2StarGrillSingleDevice : AExtensionDevice, ICloudConnected, IMAKDevice
    {
        #region Private driver fields

        private readonly IMAKPlatformProtocol protocol;

        #endregion Private driver fields

        /// <summary>
        ///     Initializes a new instance of the <see cref="MAK2StarGrillSingleDevice" /> class.
        /// </summary>
        public MAK2StarGrillSingleDevice(string grillId, string grillName, IMAKPlatformProtocol protocol)
        {
            this.DeviceId = MAKUtilities.FormatDeviceId(grillId);
            this.GrillId = grillId;
            this.GrillName = grillName;
            this.protocol = protocol;
            this.CreateDeviceDefinition();
            this.Initialize();
        }

        /// <summary>
        ///     Change the connection status of this device.
        ///     This is usually set from either the platform protocol (if it can't connect)
        ///     or locally when the device status is retrieved from the platform.
        /// </summary>
        /// <param name="connected">Whether the device is connected to the platform</param>
        public void SetConnectionStatus(bool connected)
        {
            if (this.Connected == connected) return;
            this.Connected = connected;
        }

        public void SetGrillName(string name)
        {
            this.GrillName = name;
        }

        /// <summary>
        ///     This routine is called by the platform protocol to refresh data about a grill.
        /// </summary>
        /// <param name="grillInfo">Grill information.</param>
        public void RefreshGrillDataHandler(GrillInfoJson grillInfo)
        {
            MAKLogging.TraceMessage(this.EnableLogging,
                $"Handling refresh for grill {this.GrillId}");

            // Fire off a thread to update the values
            // It is easier to be non-blocking by doing this, than trying to have the platform driver start threads
            // for the refreshes.
            // In theory this thread could still be running by the time the next refresh happens.
            // In practice we don't do anything but set properties so it doesn't matter
            // If there was real work being done it might.

            // Also note: This will ignore exceptions that aren't caught as part of Refresh because we never call Wait
            // on the task. 
            Task.Run(() => { this.Refresh(grillInfo); });
        }

        private void Refresh(in GrillInfoJson grillInfo)
        {
            this.SetConnectionStatus(grillInfo.Connected);
            if (!grillInfo.Connected)
            {
                this.SetGrillPropsDisconnected();
            }
            else
            {
                if (grillInfo.GrillData.Power == GrillConstants.JsonState.Cooldown)
                {
                    this.SetGrillPropsCooldown(grillInfo);
                }
                else if (grillInfo.GrillData.Power == GrillConstants.JsonState.Off)
                {
                    this.SetGrillPropsOff();
                }
                else if (grillInfo.GrillData.Power == GrillConstants.JsonState.Start
                         || grillInfo.GrillData.Power == GrillConstants.JsonState.On)
                {
                    switch (grillInfo.GrillData.Power)
                    {
                        case GrillConstants.JsonState.On:
                        {
                            this.grillStateTextProperty.Value = "COOKING";
                            this.grillTileStatusTextProperty.Value = $"On:\tTemp={grillInfo.GrillData.Temp:D}°F";
                            break;
                        }
                        case GrillConstants.JsonState.Start:
                        {
                            this.grillStateTextProperty.Value = "IGNITING";
                            this.grillTileStatusTextProperty.Value =
                                $"Igniting:\tTemp={grillInfo.GrillData.Temp:D}°F";
                            break;
                        }
                    }

                    this.grillStateIconProperty.Value = "icFireOn";
                    this.grillCurrentTempProperty.Value = $"{grillInfo.GrillData.Temp:D}°F";
                    this.grillSetPointProperty.Value = GetTemperatureString(grillInfo.SessionData.SetPoint);
                    this.grillSetPointRLEnabledProperty.Value = true;
                    this.grillSetPointRLValueProperty.Value = grillInfo.SessionData.SetPoint;
                    this.grillRadialValueProperty.Value = ComputeRadialValue(grillInfo);
                }
            }

            this.grillProbe1StatusProperty.Value =
                GetProbeTextFromJson(1, grillInfo.GrillData);
            this.grillProbe2StatusProperty.Value =
                GetProbeTextFromJson(2, grillInfo.GrillData);
            this.grillProbe3StatusProperty.Value =
                GetProbeTextFromJson(3, grillInfo.GrillData);
            this.Commit();
        }

        /* Clamp a value to a range */
        private static T Clamp<T>(T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            if (val.CompareTo(max) > 0) return max;
            return val;
        }

        /* Return a number between 0 and 100 representing the percentage the temperature is of the setpoint. */
        private static int ComputeRadialValue(in GrillInfoJson grillInfo)
        {
            if (grillInfo.GrillData.Temp > grillInfo.SessionData.SetPoint)
                return 100;
            if (grillInfo.SessionData.SetPoint == 0)
                return 100;
            var percentVal = grillInfo.GrillData.Temp / (double) grillInfo.SessionData.SetPoint;
            var result = (int) Math.Round(percentVal);
            return Clamp(result, 0, 100);
        }

        private static string GetProbeTextFromJson(int probeNum, in GrillData grillData)
        {
            var propValue = string.Empty;
            if (grillData != null)
            {
                propValue = (string) grillData.GetType().GetProperty($"Probe{probeNum}")?.GetValue(grillData);
                propValue = propValue ?? string.Empty;
            }

            return GetProbeText(probeNum, propValue == string.Empty
                ? "N/A"
                : propValue.Trim() + "°F");
        }

        private static string GetTemperatureString(long temperature)
        {
            if (temperature <= 175)
                return GrillConstants.Temp.Smoke;
            if (temperature >= 450)
                return GrillConstants.Temp.High;
            return $"{temperature:D}°F";
        }

        private void SetGrillPropsOff()
        {
            this.grillStateIconProperty.Value = "icFireOff";
            this.grillStateTextProperty.Value = "OFF";
            this.grillTileStatusTextProperty.Value = "Off";
            this.grillSetPointProperty.Value = "N/A";
            this.grillCurrentTempProperty.Value = "N/A";
            this.grillSetPointRLEnabledProperty.Value = false;
            this.grillRadialValueProperty.Value = 0;
        }

        private static string GetProbeText(int probeNum, string text)
        {
            return $"Probe {probeNum}: {text}";
        }

        private void SetGrillPropsCooldown(in GrillInfoJson grillInfo)
        {
            this.grillStateIconProperty.Value = "icFireOff";
            this.grillStateTextProperty.Value = "COOLING DOWN";
            this.grillTileStatusTextProperty.Value = "Cooling down";
            this.grillSetPointProperty.Value = "N/A";
            this.grillSetPointRLEnabledProperty.Value = false;
            this.grillRadialValueProperty.Value = ComputeRadialValue(grillInfo);
        }

        private void CreateDeviceDefinition()
        {
            this.grillTileStatusTextProperty =
                this.CreateProperty<string>(new PropertyDefinition(GrillTileStatusTextKey, null,
                    DevicePropertyType.String));
            this.grillStateIconProperty =
                this.CreateProperty<string>(new PropertyDefinition(GrillStateIconKey, null, DevicePropertyType.String));
            this.grillStateTextProperty =
                this.CreateProperty<string>(new PropertyDefinition(GrillStateTextKey, null, DevicePropertyType.String));
            this.grillCurrentTempProperty =
                this.CreateProperty<string>(
                    new PropertyDefinition(GrillCurrentTempKey, null, DevicePropertyType.String));
            this.grillSetPointProperty =
                this.CreateProperty<string>(new PropertyDefinition(GrillSetPointKey, null, DevicePropertyType.String));
            this.grillRadialValueProperty =
                this.CreateProperty<int>(new PropertyDefinition(GrillRadialValueKey, null, DevicePropertyType.Int32, 0,
                    100, 1));
            this.grillSetPointRLValueProperty =
                this.CreateProperty<int>(new PropertyDefinition(GrillSetPointRLValueKey, null,
                    DevicePropertyType.Int32, 175.0, 455.0, 5.0));
            this.grillRadialLabelProperty =
                this.CreateProperty<string>(new PropertyDefinition(GrillRadialLabelKey, null,
                    DevicePropertyType.String));
            this.grillRadialLabelProperty.Value = "Grill";
            this.grillSetPointRLEnabledProperty = this.CreateProperty<bool>(
                new PropertyDefinition(GrillSetPointRLEnabledKey, null, DevicePropertyType.Boolean));
            this.grillProbe1StatusProperty =
                this.grillProbe1StatusProperty =
                    this.CreateProperty<string>(new PropertyDefinition(GrillProbe1StatusKey, null,
                        DevicePropertyType.String));
            this.grillProbe2StatusProperty =
                this.CreateProperty<string>(new PropertyDefinition(GrillProbe2StatusKey, null,
                    DevicePropertyType.String));
            this.grillProbe3StatusProperty =
                this.CreateProperty<string>(new PropertyDefinition(GrillProbe3StatusKey, null,
                    DevicePropertyType.String));
        }

        #region Gateway device info

        public string GrillId { get; }
        public string GrillName { get; private set; }
        public string Model => "2 Star";
        public string DeviceType => "Grill";
        public string DeviceSubType => string.Empty;
        public string DeviceId { get; }
        
        #endregion Gateway device info

        #region Property keys

        /* Define all of the keys for properties.
           These are meant to match the properties used in the UI definition. */
        private const string GrillTileStatusTextKey = "GrillTileStatusText";
        private const string GrillRadialLabelKey = "GrillRadialLabel";
        private const string GrillStateIconKey = "GrillStateIcon";
        private const string GrillStateTextKey = "GrillStateText";
        private const string GrillRadialValueKey = "GrillRadialValue";
        private const string GrillCurrentTempKey = "GrillCurrentTemp";
        private const string GrillSetPointKey = "GrillSetPoint";
        private const string GrillSetPointRLValueKey = "GrillSetPointRLValue";
        private const string GrillSetPointRLEnabledKey = "GrillSetPointRLEnabled";
        private const string GrillProbe1StatusKey = "GrillProbe1Status";
        private const string GrillProbe2StatusKey = "GrillProbe2Status";
        private const string GrillProbe3StatusKey = "GrillProbe3Status";

        #endregion Property keys

        #region UI properties

        private PropertyValue<string> grillTileStatusTextProperty;
        private PropertyValue<string> grillStateIconProperty;
        private PropertyValue<string> grillStateTextProperty;
        private PropertyValue<string> grillCurrentTempProperty;
        private PropertyValue<string> grillSetPointProperty;
        private PropertyValue<int> grillRadialValueProperty;
        private PropertyValue<int> grillSetPointRLValueProperty;
        private PropertyValue<string> grillRadialLabelProperty;
        private PropertyValue<bool> grillSetPointRLEnabledProperty;
        private PropertyValue<string> grillProbe1StatusProperty;
        private PropertyValue<string> grillProbe2StatusProperty;
        private PropertyValue<string> grillProbe3StatusProperty;

        #endregion UI properties

        #region ICloudConnected members

        /// <inheritdoc />
        public void Initialize()
        {
            this.SetGrillPropsDisconnected();
            this.grillProbe1StatusProperty.Value =
                GetProbeTextFromJson(1, null);
            this.grillProbe2StatusProperty.Value =
                GetProbeTextFromJson(2, null);
            this.grillProbe3StatusProperty.Value =
                GetProbeTextFromJson(3, null);
            MAKLogging.TraceMessage(this.EnableLogging);

            this.Commit();
        }

        private void SetGrillPropsDisconnected()
        {
            this.grillTileStatusTextProperty.Value = "Connecting to Grill";
            this.grillStateIconProperty.Value = "icFireOff";
            this.grillStateTextProperty.Value = "CONNECTING";
            this.grillCurrentTempProperty.Value = "N/A";
            this.grillSetPointProperty.Value = "N/A";
            this.grillRadialValueProperty.Value = 0;
            this.grillSetPointRLEnabledProperty.Value = false;
            this.grillSetPointRLValueProperty.Value = 175;
        }

        #endregion ICloudConnected members

        #region AExtensionDevice members

        /// <inheritdoc />
        protected override IOperationResult DoCommand(string command, string[] parameters)
        {
            this.Commit();
            return new OperationResult(OperationResultCode.Success);
        }

        /// <inheritdoc />
        protected override IOperationResult SetDriverPropertyValue<T>(string propertyKey, T value)
        {
            switch (propertyKey)
            {
                case GrillSetPointRLValueKey:
                {
                    if (value is int intVal)
                    {
                        this.grillSetPointRLValueProperty.Value = intVal;
                        this.protocol.QueueSetPointChange(this, intVal);
                    }

                    break;
                }
            }

            this.Commit();
            return new OperationResult(OperationResultCode.Success);
        }

        /// <inheritdoc />
        protected override IOperationResult SetDriverPropertyValue<T>(string objectId, string propertyKey, T value)
        {
            this.Commit();
            return new OperationResult(OperationResultCode.Success);
        }

        #endregion AExtensionDevice members
    }
}