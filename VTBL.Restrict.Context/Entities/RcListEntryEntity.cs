using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace VTBL.Restrict.Context.Entities
{
    /// <summary>
    /// Зеркало Excel листа RC ([restrict].[RcListEntry]).
    /// </summary>
    [Table("RcListEntry", Schema = "restrict")]
    public sealed class RcListEntryEntity
    {
        public Guid RcListEntryId { get; set; }

        [Column("Дата")]
        public DateTime Date { get; set; }

        [Column("Название")]
        public string Name { get; set; }

        [Column("ИНН")]
        public string Inn { get; set; }

        [Column("Адрес")]
        public string Address { get; set; }

        [Column("Сайт")]
        public string Website { get; set; }

        [Column("Признаки, установленные Банком России")]
        public string BankOfRussiaSigns { get; set; }

        [Column("Регионы")]
        public string Regions { get; set; }

        [Column("Деятельность прекращена согласно ЕГРЮЛ")]
        public bool ActivityStoppedPerEgrul { get; set; }

        [Column("Дополнительно")]
        public string AdditionalInfo { get; set; }

        [Column("Внутренний идентификатор записи")]
        public int ExternalRecordId { get; set; }

        [Column("Дата последнего обновления данных")]
        public DateTime LastDataUpdateAt { get; set; }

        [Column("Тип записи")]
        public string RecordType { get; set; }

        public DateTime LoadedAtUtc { get; set; }
    }
}
