using System.Collections.Generic;
using System.Linq;
using VTBL.Restrict.Loader.Domain.Uploads;
using Xunit;

namespace VTBL.Restrict.Loader.Application.Tests.Domain
{
    /// <summary>
    /// Проверка метаданных загрузки.
    /// </summary>
    public sealed class UploadShellValidatorTests
    {
        private static readonly string[] Allowed = { ".xlsx", ".xls", ".csv" };

        [Fact]
        public void Validate_Rejects_EmptyType()
        {
            var result = UploadShellValidator.Validate("", "a.xlsx", 10, Allowed, 100);
            Assert.False(result.IsValid);
            Assert.Equal("Validation", result.ErrorCode);
        }

        [Fact]
        public void Validate_Rejects_EmptyFile()
        {
            var result = UploadShellValidator.Validate("MVK", "a.xlsx", 0, Allowed, 100);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_Rejects_BadExtension()
        {
            var result = UploadShellValidator.Validate("MVK", "a.exe", 10, Allowed, 100);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_Rejects_Oversize()
        {
            var result = UploadShellValidator.Validate("MVK", "a.xlsx", 101, Allowed, 100);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Validate_Accepts_ValidShell()
        {
            var result = UploadShellValidator.Validate("MVK", "list.XLSX", 50, Allowed, 100);
            Assert.True(result.IsValid);
        }
    }

    /// <summary>
    /// Санитизация имени файла.
    /// </summary>
    public sealed class FileNameSanitizerTests
    {
        [Theory]
        [InlineData(@"..\secret.xlsx", "secret.xlsx")]
        [InlineData(@"folder/list.xlsx", "list.xlsx")]
        [InlineData(@"a\b\c.csv", "c.csv")]
        [InlineData("plain.xlsx", "plain.xlsx")]
        public void Sanitize_Strips_PathSegments(string input, string expected)
        {
            Assert.Equal(expected, FileNameSanitizer.Sanitize(input));
        }
    }
}
