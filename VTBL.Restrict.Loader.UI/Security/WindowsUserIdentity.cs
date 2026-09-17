using System;
using System.Security.Claims;
using System.Security.Principal;

namespace VTBL.Restrict.Loader.UI.Security
{
    /// <summary>
    /// Хелпер для извлечения идентификатора и отображаемого имени текущего пользователя
    /// из Windows Authentication (IIS / IIS Express → <see cref="ClaimsPrincipal"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Используется на UI-слое при сборке запроса загрузки,
    /// чтобы Infrastructure/RabbitMQ не зависели от <c>HttpContext</c>.
    /// </para>
    /// <para>
    /// Ожидаемый источник: Windows Authentication. При запуске через Kestrel
    /// без Negotiate/Windows Auth поля обычно пустые (дальше в Rabbit уйдут stubs).
    /// </para>
    /// </remarks>
    public static class WindowsUserIdentity
    {
        private const string PrimarySidClaimType =
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/primarysid";

        /// <summary>
        /// Возвращает имя пользователя Windows в виде, который отдаёт IIS
        /// (обычно <c>DOMAIN\login</c> или UPN <c>login@domain</c>).
        /// </summary>
        /// <param name="user">Текущий principal из <c>HttpContext.User</c>; может быть null.</param>
        /// <returns>
        /// Обрезанное имя пользователя или <c>null</c>, если имя отсутствует/пустое.
        /// </returns>
        public static string GetUserName(ClaimsPrincipal user)
        {
            var name = user?.Identity?.Name;
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        }

        /// <summary>
        /// Возвращает стабильный идентификатор пользователя для поля <c>UserId</c> в RabbitMQ.
        /// </summary>
        /// <remarks>
        /// Порядок выбора:
        /// <list type="number">
        /// <item><description>Claim <see cref="ClaimTypes.PrimarySid"/> или URI primarysid;</description></item>
        /// <item><description>на Windows — SID из <see cref="WindowsIdentity.User"/>;</description></item>
        /// <item><description>иначе логин без домена (см. <see cref="ExtractLoginWithoutDomain"/>).</description></item>
        /// </list>
        /// </remarks>
        /// <param name="user">Текущий principal из <c>HttpContext.User</c>; может быть null.</param>
        /// <returns>SID, либо login без домена, либо <c>null</c>, если пользователя определить нельзя.</returns>
        public static string GetUserId(ClaimsPrincipal user)
        {
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
            {
                var anonymousName = GetUserName(user);
                return string.IsNullOrWhiteSpace(anonymousName)
                    ? null
                    : ExtractLoginWithoutDomain(anonymousName);
            }

            var sid = user.FindFirst(ClaimTypes.PrimarySid)?.Value
                      ?? user.FindFirst(PrimarySidClaimType)?.Value;

            if (string.IsNullOrWhiteSpace(sid)
                && OperatingSystem.IsWindows()
                && user.Identity is WindowsIdentity windowsIdentity)
            {
                sid = windowsIdentity.User?.Value;
            }

            if (!string.IsNullOrWhiteSpace(sid))
            {
                return sid.Trim();
            }

            var userName = GetUserName(user);
            return string.IsNullOrWhiteSpace(userName) ? null : ExtractLoginWithoutDomain(userName);
        }

        /// <summary>
        /// Нормализует имя Windows-пользователя к «голому» логину без домена.
        /// </summary>
        /// <param name="userName">
        /// Строка вида <c>DOMAIN\user</c>, <c>user@corp.local</c> или уже простой login.
        /// </param>
        /// <returns>
        /// Login без доменной части; для <c>DOMAIN\user</c> → <c>user</c>, для UPN → часть до <c>@</c>.
        /// </returns>
        public static string ExtractLoginWithoutDomain(string userName)
        {
            var value = userName.Trim();
            var slash = value.LastIndexOf('\\');
            if (slash >= 0 && slash < value.Length - 1)
            {
                return value.Substring(slash + 1);
            }

            var at = value.IndexOf('@');
            if (at > 0)
            {
                return value.Substring(0, at);
            }

            return value;
        }
    }
}
