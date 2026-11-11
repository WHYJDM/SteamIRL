namespace SteamIRL.Api.IntegrationTests;

internal static class TestCredentials
{
    // Shared only inside this test process; no usable fixed password in Git.
    internal static readonly string Password = "Aa1" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
}
