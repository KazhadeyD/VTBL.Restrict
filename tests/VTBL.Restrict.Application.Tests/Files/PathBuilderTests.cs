using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VTBL.Restrict.Infrastructure.Files;
using Xunit;

namespace VTBL.Restrict.Application.Tests.Files
{
    /// <summary>
    /// TC-UNIT-01: PathBuilder.
    /// </summary>
    public sealed class PathBuilderTests
    {
        [Fact]
        public void BuildTargetPath_Mvk_ProducesExpectedSegments()
        {
            var correlationId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");
            var utc = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

            var path = PathBuilder.BuildTargetPath(
                @"C:\inbox",
                "mvk",
                utc,
                correlationId,
                "list.xlsx");

            Assert.EndsWith(
                Path.Combine("mvk", "2026", "07", "14", "3fa85f6457174562b3fc2c963f66afa6_list.xlsx"),
                path);
        }

        [Fact]
        public void BuildTargetPath_SanitizesPathTraversalInFileName()
        {
            var path = PathBuilder.BuildTargetPath(
                @"C:\inbox",
                "terrorists",
                new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                @"..\..\evil.xlsx");

            Assert.Contains("evil.xlsx", path);
            Assert.DoesNotContain("..", path);
        }
    }
}
