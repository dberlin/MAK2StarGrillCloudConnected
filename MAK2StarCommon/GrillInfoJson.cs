﻿// <auto-generated />
//
// To parse this JSON data, add NuGet 'Newtonsoft.Json' then do:
//

namespace MAK2StarCommon
{
    namespace GrillInfo
    {
        using System;
        using System.Collections.Generic;
        using Newtonsoft.Json;

        public sealed class GrillData
        {
            [JsonProperty("Date")] public string Date { get; set; }

            [JsonProperty("Flame")] public object Flame { get; set; }

            [JsonProperty("GrillId")] public string GrillId { get; set; }

            [JsonProperty("Power")] public string Power { get; set; }

            [JsonProperty("Probe1")] public string Probe1 { get; set; }

            [JsonProperty("Probe2")] public string Probe2 { get; set; }

            [JsonProperty("Probe3")] public string Probe3 { get; set; }

            [JsonProperty("Probe4")] public object Probe4 { get; set; }

            [JsonProperty("Probe5")] public object Probe5 { get; set; }

            [JsonProperty("Probe6")] public object Probe6 { get; set; }

            [JsonProperty("Probe7")] public object Probe7 { get; set; }

            [JsonProperty("Probe8")] public object Probe8 { get; set; }

            [JsonProperty("SessionId")] public Guid SessionId { get; set; }
            [JsonProperty("Temp")] public int Temp { get; set; }
        }

        public sealed class SessionData
        {
            [JsonProperty("CookingProgram")] public object CookingProgram { get; set; }

            [JsonProperty("CookMode")] public int CookMode { get; set; }

            [JsonProperty("ElapsedPaused")] public bool ElapsedPaused { get; set; }

            [JsonProperty("ElapsedPausedDuration")]
            public long ElapsedPausedDuration { get; set; }

            [JsonProperty("ElapsedStart")] public long ElapsedStart { get; set; }

            [JsonProperty("PotStatus")] public long PotStatus { get; set; }

            [JsonProperty("SessionId")] public Guid SessionId { get; set; }

            [JsonProperty("SessionName")] public object SessionName { get; set; }

            [JsonProperty("SetPoint")] public int SetPoint { get; set; }

            [JsonProperty("StartTime")] public long StartTime { get; set; }
            [JsonProperty("ZoneProbe")] public long ZoneProbe { get; set; }
        }

        public sealed class GrillInfoJson
        {
            [JsonProperty("Connected")] public bool Connected { get; set; }

            [JsonProperty("GrillData")] public GrillData GrillData { get; set; }

            [JsonProperty("ProbeAlarms")] public List<object> ProbeAlarms { get; set; }

            [JsonProperty("SessionData")] public SessionData SessionData { get; set; }

            [JsonProperty("Timers")] public List<Timer> Timers { get; set; }

            public string ToPrettyJsonString()
            {
                return JsonConvert.SerializeObject(this, Formatting.Indented);
            }
        }

        public sealed class Timer
        {
            [JsonProperty("Duration")] public long Duration { get; set; }

            [JsonProperty("Paused")] public bool Paused { get; set; }

            [JsonProperty("StartTime")] public long StartTime { get; set; }
        }
    }
}