using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using VTBL.Restrict.Loader.Domain.ListTypes;

namespace VTBL.Restrict.Loader.UI.Configuration
{
    /// <summary>
    /// Валидирует ListTypesOptions (обязательные поля и уникальность code).
    /// </summary>
    public sealed class ListTypesOptionsValidator : IValidateOptions<ListTypesOptions>
    {
        public ValidateOptionsResult Validate(string name, ListTypesOptions options)
        {
            var items = options?.Items ?? Array.Empty<ListTypeInfo>();
            var errors = new List<string>();

            if (items.Count == 0)
            {
                errors.Add("Configuration key 'ListTypes' must contain at least one item.");
            }

            var duplicateCodes = items
                .Where(x => !string.IsNullOrWhiteSpace(x.Code))
                .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateCodes.Count > 0)
            {
                errors.Add("ListTypes contains duplicated 'code': " + string.Join(", ", duplicateCodes));
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (string.IsNullOrWhiteSpace(item.Code))
                {
                    errors.Add($"ListTypes[{i}] has empty 'code'.");
                }
                else if (!IsCodeValid(item.Code))
                {
                    errors.Add(
                        $"ListTypes[{i}] has invalid 'code'='{item.Code}'. Allowed chars: A-Z, a-z, 0-9, '.', '_', '-'.");
                }

                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    errors.Add($"ListTypes[{i}] has empty 'name'.");
                }

                if (string.IsNullOrWhiteSpace(item.RemoteRoot))
                {
                    errors.Add($"ListTypes[{i}] has empty 'remoteRoot'.");
                }
            }

            return errors.Count == 0
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(errors);
        }

        private static bool IsCodeValid(string code)
        {
            return code.All(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-');
        }
    }
}

