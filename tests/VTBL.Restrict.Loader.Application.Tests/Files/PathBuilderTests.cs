using System;
using System.IO;
using VTBL.Restrict.Loader.Infrastructure.Files;
using Xunit;

namespace VTBL.Restrict.Loader.Application.Tests.Files
{
    /// <summary>
    /// Построение пути выкладки.
    /// </summary>
    public sealed class PathBuilderTests
    {
        [Fact]
        public void BuildTargetPath_Mvk_ProducesExpectedSegments()
        {
            var correlationId = Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6");

            var path = PathBuilder.BuildTargetPath(
                @"C:\inbox",
                correlationId,
                "list.xlsx");

            Assert.EndsWith(
                Path.Combine("3fa85f6457174562b3fc2c963f66afa6_list.xlsx"),
                path);
        }

        [Fact]
        public void BuildTargetPath_SanitizesPathTraversalInFileName()
        {
            var path = PathBuilder.BuildTargetPath(
                @"C:\inbox",
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                @"..\..\evil.xlsx");

            Assert.Contains("evil.xlsx", path);
            Assert.DoesNotContain("..", path);
        }
    }
}
