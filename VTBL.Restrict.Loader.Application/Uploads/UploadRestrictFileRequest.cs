using System.IO;

namespace VTBL.Restrict.Loader.Application.Uploads
{
    /// <summary>
    /// Входной запрос сценария загрузки рестриктивного файла
    /// (<see cref="UploadRestrictFileCommand"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Собирается на UI из формы и Windows Authentication, затем передаётся в application-слой.
    /// Содержимое файла читается только как поток для записи на шару — разбор Excel/CSV здесь не выполняется.
    /// </para>
    /// </remarks>
    public sealed class UploadRestrictFileRequest
    {
        /// <summary>
        /// Код типа списка (ListType), выбранный оператором на форме.
        /// </summary>
        public string ListTypeCode { get; set; }

        /// <summary>
        /// Исходное имя загружаемого файла (как пришло от клиента).
        /// </summary>
        public string OriginalFileName { get; set; }

        /// <summary>
        /// Размер содержимого в байтах (для shell-валидации лимита).
        /// </summary>
        public long ContentLength { get; set; }

        /// <summary>
        /// Поток содержимого файла; не должен быть null при успешной клиентской валидации.
        /// </summary>
        public Stream Content { get; set; }

        /// <summary>
        /// Кто инициировал загрузку (как правило Windows login, дублирует <see cref="UserName"/>).
        /// </summary>
        public string UploadedBy { get; set; }

        /// <summary>
        /// Идентификатор пользователя: Windows SID или login без домена.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Отображаемое имя пользователя Windows (<c>DOMAIN\user</c> / UPN).
        /// </summary>
        public string UserName { get; set; }
    }
}
