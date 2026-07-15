namespace VTBL.Restrict.Domain.Enums
{
    /// <summary>
    /// Статус кейса обработки ошибок (ErrorProcessingCase.Status).
    /// </summary>
    public enum ErrorProcessingStatus
    {
        Pending = 0,
        ResolvedByUser = 1,
        Expired = 2,
        Cancelled = 3
    }
}
