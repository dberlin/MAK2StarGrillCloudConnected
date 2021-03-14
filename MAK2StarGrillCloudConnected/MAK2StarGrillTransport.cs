// <copyright file="MAK2StarGrillTransport.cs" company="Daniel Berlin">
// Copyright (c) Daniel Berlin. All rights reserved.
// </copyright>

namespace MAK2StarGrillCloudConnected
{
    using Crestron.RAD.Common.Transports;
    using MAK2StarCommon;

    public sealed class MAK2StarGrillTransport : ATransportDriver
    {
        public MAK2StarGrillTransport()
        {
            this.IsConnected = true;
            this.IsEthernetTransport = true;
        }

        /// <inheritdoc />
        public override void SendMethod(string message, object[] parameters)
        {
            MAKLogging.TraceMessage(this.EnableLogging);
        }

        /// <inheritdoc />
        public override void Start()
        {
            MAKLogging.TraceMessage(this.EnableLogging);
        }

        /// <inheritdoc />
        public override void Stop()
        {
            MAKLogging.TraceMessage(this.EnableLogging);
        }
    }
}