namespace VTBL.Restrict.Loader.Domain.Uploads
{
    /// <summary>
    /// Результат проверки оболочки.
    /// </summary>
    public sealed class ShellValidationResult
    {
        public bool IsValid { get; private set; }
        public string ErrorCode { get; private set; }
        public string Message { get; private set; }

        public static ShellValidationResult Ok()
        {
            return new ShellValidationResult { IsValid = true };
        }

        public static ShellValidationResult Fail(string errorCode, string message)
        {
            return new ShellValidationResult
            {
                IsValid = false,
                ErrorCode = errorCode,
                Message = message
            };
        }
    }
}

