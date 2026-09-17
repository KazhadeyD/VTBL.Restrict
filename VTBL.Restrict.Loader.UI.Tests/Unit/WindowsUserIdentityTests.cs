using System.Security.Claims;
using VTBL.Restrict.Loader.UI.Security;
using Xunit;

namespace VTBL.Restrict.Loader.UI.Tests.Unit
{
    public sealed class WindowsUserIdentityTests
    {
        [Fact]
        public void GetUserId_PrefersPrimarySidClaim()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, @"CONTOSO\ivanov"),
                new Claim(ClaimTypes.PrimarySid, "S-1-5-21-1-2-3-1001")
            }, authenticationType: "Windows");
            var principal = new ClaimsPrincipal(identity);

            Assert.Equal("S-1-5-21-1-2-3-1001", WindowsUserIdentity.GetUserId(principal));
            Assert.Equal(@"CONTOSO\ivanov", WindowsUserIdentity.GetUserName(principal));
        }

        [Fact]
        public void GetUserId_FallsBackToLoginWithoutDomain()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, @"CONTOSO\ivanov")
            }, authenticationType: "Windows");
            var principal = new ClaimsPrincipal(identity);

            Assert.Equal("ivanov", WindowsUserIdentity.GetUserId(principal));
        }

        [Theory]
        [InlineData(@"DOMAIN\user", "user")]
        [InlineData("user@corp.local", "user")]
        [InlineData("plain", "plain")]
        public void ExtractLoginWithoutDomain_SupportsCommonFormats(string input, string expected)
        {
            Assert.Equal(expected, WindowsUserIdentity.ExtractLoginWithoutDomain(input));
        }
    }
}
