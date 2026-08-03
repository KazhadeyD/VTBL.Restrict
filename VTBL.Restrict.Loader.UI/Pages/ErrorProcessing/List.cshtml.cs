using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using VTBL.Restrict.Loader.Application.ErrorProcessing;

namespace VTBL.Restrict.Loader.UI.Pages.ErrorProcessing
{
    /// <summary>
    /// Список Pending-кейсов Error Processing для оператора (маршрут /error-processing).
    /// </summary>
    public class ListModel : PageModel
    {
        private readonly ListPendingErrorProcessingCasesQuery _listQuery;
        private readonly ILogger<ListModel> _logger;

        public ListModel(
            ListPendingErrorProcessingCasesQuery listQuery,
            ILogger<ListModel> logger)
        {
            _listQuery = listQuery;
            _logger = logger;
        }

        /// <summary>Строки Pending summary из List-query (CreatedAt DESC).</summary>
        public IReadOnlyList<ErrorProcessingCaseSummaryDto> Items { get; private set; }

        /// <summary>Сообщение об ошибке загрузки списка (без стека).</summary>
        public string ListError { get; private set; }

        /// <summary>True, если List-query вернул Failed.</summary>
        public bool HasError => !string.IsNullOrEmpty(ListError);

        /// <summary>
        /// Загружает Pending-кейсы через <see cref="ListPendingErrorProcessingCasesQuery"/>.
        /// Success → Items; Fail → ListError и пустой Items.
        /// </summary>
        public async Task OnGetAsync(CancellationToken cancellationToken)
        {
            var result = await _listQuery.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                ListError = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Не удалось загрузить список кейсов."
                    : result.ErrorMessage;
                Items = System.Array.Empty<ErrorProcessingCaseSummaryDto>();
                _logger.LogWarning(
                    "ErrorProcessing UI list failed code={Code}",
                    result.ErrorCode);
                return;
            }

            ListError = null;
            Items = result.Items ?? System.Array.Empty<ErrorProcessingCaseSummaryDto>();
        }
    }
}
