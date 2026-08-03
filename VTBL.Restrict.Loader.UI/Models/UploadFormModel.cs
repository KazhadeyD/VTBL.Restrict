using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace VTBL.Restrict.Loader.UI.Models
{
    /// <summary>
    /// Модель формы загрузки файла рестриктивного списка.
    /// </summary>
    public sealed class UploadFormModel
    {
        [Required(ErrorMessage = "Выберите тип списка")]
        [Display(Name = "Тип списка")]
        public string ListTypeCode { get; set; }

        [Display(Name = "Файл")]
        public IFormFile File { get; set; }
    }
}
