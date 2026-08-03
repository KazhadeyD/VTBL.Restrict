using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace VTBL.Restrict.Loader.Application.Observability
{
    /// <summary>
    /// Скоупы для корреляционного логирования (correlationId / caseId / listType).
    /// </summary>
    public static class OperationLogScope
    {
        public const string KeyOperation = "operation";
        public const string KeyCorrelationId = "correlationId";
        public const string KeyCaseId = "caseId";
        public const string KeyListType = "listType";
        public const string KeyErrorCode = "errorCode";
        public const string KeyTokenPresent = "tokenPresent";

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

        public static IDisposable BeginRetry(ILogger logger, Guid correlationId, string listType = null)
        {
            var state = new Dictionary<string, object>
            {
                [KeyOperation] = "retry",
                [KeyCorrelationId] = correlationId
            };
            if (!string.IsNullOrEmpty(listType))
            {
                state[KeyListType] = listType;
            }

            return logger.BeginScope(state);
        }

        public static IDisposable BeginOpen(ILogger logger, Guid caseId, string listType = null)
        {
            var state = new Dictionary<string, object>
            {
                [KeyOperation] = "open",
                [KeyCaseId] = caseId
            };
            if (!string.IsNullOrEmpty(listType))
            {
                state[KeyListType] = listType;
            }

            return logger.BeginScope(state);
        }

        public static IDisposable BeginSave(ILogger logger, Guid caseId, string listType = null)
        {
            var state = new Dictionary<string, object>
            {
                [KeyOperation] = "save",
                [KeyCaseId] = caseId
            };
            if (!string.IsNullOrEmpty(listType))
            {
                state[KeyListType] = listType;
            }

            return logger.BeginScope(state);
        }

        /// <summary>
        /// Скоуп списка Pending Error Processing (без caseId; count — в обычном логе success).
        /// </summary>
        public static IDisposable BeginList(ILogger logger)
        {
            return logger.BeginScope(new Dictionary<string, object>
            {
                [KeyOperation] = "list"
            });
        }
    }

    /// <summary>
    /// Политика: сырой token в логи не пишется (security epic deferred; маскирование — best-effort).
    /// </summary>
    public static class SensitiveLog
    {
        public static bool HasToken(string rawToken) => !string.IsNullOrEmpty(rawToken);

        /// <summary>
        /// Никогда не возвращает исходный token — только индикатор наличия.
        /// </summary>
        public static string DescribeTokenPresence(string rawToken) =>
            HasToken(rawToken) ? "present" : "absent";
    }
}
