using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace VTBL.Restrict.Loader.Application.Observability
{
    /// <summary>
    /// Скоупы для корреляционного логирования (correlationId / listType / operation).
    /// </summary>
    public static class OperationLogScope
    {
        public const string KeyOperation = "operation";
        public const string KeyCorrelationId = "correlationId";
        public const string KeyListType = "listType";
        public const string KeyErrorCode = "errorCode";

        public static IDisposable BeginUpload(ILogger logger, string listType, Guid? correlationId = null)
        {
            var state = new Dictionary<string, object>
            {
                [KeyOperation] = "upload",
                [KeyListType] = listType ?? string.Empty
            };
            if (correlationId.HasValue)
            {
                state[KeyCorrelationId] = correlationId.Value;
            }

            return logger.BeginScope(state);
        }

        public static IDisposable BeginPublish(ILogger logger, Guid correlationId, string listType)
        {
            return logger.BeginScope(new Dictionary<string, object>
            {
                [KeyOperation] = "publish",
                [KeyCorrelationId] = correlationId,
                [KeyListType] = listType ?? string.Empty
            });
        }
    }
}
