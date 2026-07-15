using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Infrastructure.Files;
using Xunit;

namespace VTBL.Restrict.Application.Tests.Files
{
    /// <summary>
    /// TC-UNIT-02/03: UncFileShareStore byte copy + temp cleanup on failure.
    /// </summary>
    public sealed class UncFileShareStoreTests
    {
        [Fact]
        public async Task WriteAsIsAsync_CopiesStreamByteIdentical()
        {
            var root = CreateTempDirectory();
            var target = Path.Combine(root, "out", "data.bin");
            var expected = new byte[] { 0, 1, 2, 255, 128, 64 };

            var store = new UncFileShareStore();
            await using (var input = new MemoryStream(expected))
            {
                var stored = await store.WriteAsIsAsync(input, target, CancellationToken.None);
                Assert.Equal(target, stored);
            }

            Assert.True(File.Exists(target));
            Assert.False(File.Exists(target + ".tmp"));
            Assert.Equal(expected, await File.ReadAllBytesAsync(target));
        }

        [Fact]
        public async Task WriteAsIsAsync_MidWriteFailure_RemovesTemp_NoFinalFile()
        {
            var root = CreateTempDirectory();
            var target = Path.Combine(root, "fail", "data.bin");
            var store = new UncFileShareStore();

            await Assert.ThrowsAsync<IOException>(async () =>
            {
                await using var input = new ThrowingReadStream(new byte[] { 1, 2, 3, 4, 5 }, throwAfterBytes: 2);
                await store.WriteAsIsAsync(input, target, CancellationToken.None);
            });

            Assert.False(File.Exists(target));
            Assert.False(File.Exists(target + ".tmp"));
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "VTBL.Restrict.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private sealed class ThrowingReadStream : MemoryStream
        {
            private readonly int _throwAfterBytes;
            private int _totalRead;

            public ThrowingReadStream(byte[] buffer, int throwAfterBytes)
                : base(buffer)
            {
                _throwAfterBytes = throwAfterBytes;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = base.Read(buffer, offset, count);
                _totalRead += read;
                if (_totalRead >= _throwAfterBytes)
                {
                    throw new IOException("Simulated mid-write failure.");
                }

                return read;
            }
        }
    }
}
