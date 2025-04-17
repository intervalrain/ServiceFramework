using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.Exceptions
{
    public enum NatsErrorCode
    {
        OK = 0,
        UrlEmpty = 30001,
        CredFileEmpty = 30002,
        ConnectionError = 30003,
        Timeout = 30004,
        AuthenticationFailed = 30005,
        InvalidMessageFormat = 30006,
        SubscriptionError = 30007,
        PublishError = 30008,
        UnknownError = 39999
    }

    public class NatsConnException: NatsException
    {
        
        public NatsErrorCode ErrorCode { get; set;} = NatsErrorCode.OK;
        public string ErrorMessage { get; set; } = string.Empty;

        public NatsConnException(string message, NatsErrorCode code): base(message)
        {
            ErrorCode = code;
            ErrorMessage = message;
        }
    }

}