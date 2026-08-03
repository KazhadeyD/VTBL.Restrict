namespace VTBL.Restrict.Loader.Infrastructure.Options
{
    /// <summary>
    /// Настройки RabbitMQ для publish RestrictFileUploaded.
    /// </summary>
    public sealed class RabbitMqOptions
    {
        public const string SectionName = "RabbitMq";

        public string Host { get; set; }

        public string VirtualHost { get; set; } = "/";

        public string Exchange { get; set; } = "restrict.uploads";

        public string Username { get; set; }

        public string Password { get; set; }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
    }
}
