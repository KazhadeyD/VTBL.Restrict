using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using VTBL.Restrict.Loader.Domain.ListTypes;

namespace VTBL.Restrict.Loader.UI.Configuration
{
    /// <summary>
    /// Заполняет ListTypesOptions из секции "ListTypes" в appsettings.
    /// </summary>
    public sealed class ListTypesOptionsSetup : IConfigureOptions<ListTypesOptions>
    {
        private readonly IConfiguration _configuration;

        public ListTypesOptionsSetup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void Configure(ListTypesOptions options)
        {
            var items = _configuration.GetSection("ListTypes")
                .GetChildren()
                .Select(section => new ListTypeInfo
                {
                    RemoteRoot = section["remoteRoot"]?.Trim(),
                    Code = section["code"]?.Trim(),
                    Name = section["name"]?.Trim()
                })
                .ToList();

            options.Items = items;
        }
    }
}

