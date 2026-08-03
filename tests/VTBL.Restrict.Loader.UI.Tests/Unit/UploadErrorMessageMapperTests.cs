using VTBL.Restrict.Loader.Application.Uploads;
using VTBL.Restrict.Loader.UI.Uploads;
using Xunit;

namespace VTBL.Restrict.Loader.UI.Tests.Unit
{
    /// <summary>
    /// Различимые тексты и коды ошибок загрузки.
    /// </summary>
    public sealed class UploadErrorMessageMapperTests
    {
        [Fact]
        public void Map_Share_Vs_Rmq_Vs_Validation_AreDistinct()
        {
            var validation = UploadErrorMessageMapper.Map(UploadErrorCodes.Validation, null);
            var share = UploadErrorMessageMapper.Map(UploadErrorCodes.Share, null);
            var rmq = UploadErrorMessageMapper.Map(UploadErrorCodes.Rmq, null);

            Assert.NotEqual(validation, share);
            Assert.NotEqual(share, rmq);
            Assert.NotEqual(validation, rmq);
            Assert.Contains("тип", validation.ToLowerInvariant());
            Assert.Contains("файл", share.ToLowerInvariant());
            Assert.Contains("уведомлен", rmq.ToLowerInvariant());
        }

        [Fact]
        public void ShouldShowRetry_OnlyForRmqWithCorrelationId()
        {
            Assert.True(UploadErrorMessageMapper.ShouldShowRetry(false, UploadErrorCodes.Rmq, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
            Assert.False(UploadErrorMessageMapper.ShouldShowRetry(true, UploadErrorCodes.Rmq, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
            Assert.False(UploadErrorMessageMapper.ShouldShowRetry(false, UploadErrorCodes.Share, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
            Assert.False(UploadErrorMessageMapper.ShouldShowRetry(false, UploadErrorCodes.Rmq, null));
            Assert.False(UploadErrorMessageMapper.ShouldShowRetry(false, UploadErrorCodes.Validation, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        }

        [Fact]
        public void Map_DoesNotContainStackTraceMarkers()
        {
            var text = UploadErrorMessageMapper.Map(UploadErrorCodes.Unexpected, "boom");
            Assert.DoesNotContain("at ", text);
            Assert.DoesNotContain("Exception", text);
            Assert.DoesNotContain("Stack", text);
        }
    }
}
