using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Infrastructure.Stub;
using Xunit;

namespace VTBL.Restrict.Application.Tests.ListTypes
{
    /// <summary>
    /// TC-UNIT-03: ListType store returns only IsActive=1 (lightweight in-memory).
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
            Assert.DoesNotContain(active, x => x.Code == "OTHER");
            Assert.All(active, x => Assert.True(x.IsActive));
        }

        [Fact]
        public async Task GetByCodeAsync_IgnoresInactive()
        {
            var store = new InMemoryListTypeReadStore();
            var other = await store.GetByCodeAsync("OTHER", CancellationToken.None);
            Assert.Null(other);

            var mvk = await store.GetByCodeAsync("MVK", CancellationToken.None);
            Assert.NotNull(mvk);
            Assert.Equal("mvk", mvk.FolderSegment);
        }
    }
}
