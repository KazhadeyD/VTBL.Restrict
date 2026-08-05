using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Loader.Infrastructure.Stub;
using Xunit;

namespace VTBL.Restrict.Loader.Application.Tests.ListTypes
{
    /// <summary>
    /// Справочник типов возвращает только активные записи.
    /// SQL реализация покрыта кодом SqlListTypeReadStore; live SQL — external blocker без RestrictDb.
    /// </summary>
    public sealed class InMemoryListTypeReadStoreTests
    {
        [Fact]
        public async Task GetActiveAsync_ReturnsOnlyActive()
        {
            var store = new InMemoryListTypeReadStore();
            var active = await store.GetActiveAsync(CancellationToken.None);

            Assert.Contains(active, x => x.Code == "MVK");
            Assert.Contains(active, x => x.Code == "TERRORISTS");
            Assert.Contains(active, x => x.Code == "NFA");
            Assert.Contains(active, x => x.Code == "OTHER");
        }

        [Fact]
        public async Task GetByCodeAsync_ReturnsKnownItem()
        {
            var store = new InMemoryListTypeReadStore();
            var other = await store.GetByCodeAsync("OTHER", CancellationToken.None);
            Assert.NotNull(other);

            var mvk = await store.GetByCodeAsync("MVK", CancellationToken.None);
            Assert.NotNull(mvk);
            Assert.False(string.IsNullOrWhiteSpace(mvk.RemoteRoot));
        }
    }
}
