using System;
using System.IO;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using NUnit.Framework;

public sealed class ClientAccountIdentityTests
{
    private const ulong SteamId = 76561198000000001UL;

    [Test]
    public void ForcedLocalAccountSkipsSteamEvenWhenSteamIsAvailable()
    {
        var config = new LobbyAccountConfig { accountId = 2, username = "Tester2", useLocalTestAccount = true };
        Assert.That(ClientAccountPolicy.ShouldSkipSteam(config, true), Is.True);
        var account = ClientAccountPolicy.Resolve(config, true, SteamId, "SteamName");
        Assert.That(account.IsLocalTestAccount, Is.True);
        Assert.That(account.AccountId, Is.EqualTo(2));
        Assert.That(account.Name, Is.EqualTo("Tester2"));
    }

    [Test]
    public void ReleasePolicyIgnoresBothLocalAccountSwitches()
    {
        var config = new LobbyAccountConfig { useLocalTestAccount = true, allowDevelopmentFallback = true };
        Assert.That(ClientAccountPolicy.ShouldSkipSteam(config, false), Is.False);
        var account = ClientAccountPolicy.Resolve(config, false, SteamId, "SteamName");
        Assert.That(account.IsLocalTestAccount, Is.False);
        Assert.That(account.AccountId, Is.EqualTo(SteamId));
        var offline = ClientAccountPolicy.Resolve(config, false, 0, string.Empty);
        Assert.That(offline.IsValid, Is.False);
        Assert.That(offline.IsLocalTestAccount, Is.False);
    }

    [Test]
    public void DefaultModeStillPrefersSteamAndOnlyFallsBackOnFailure()
    {
        var config = new LobbyAccountConfig();
        Assert.That(ClientAccountPolicy.ShouldSkipSteam(config, true), Is.False);
        Assert.That(ClientAccountPolicy.Resolve(config, true, SteamId, "SteamName").IsLocalTestAccount, Is.False);
        Assert.That(ClientAccountPolicy.Resolve(config, true, 0, string.Empty).IsLocalTestAccount, Is.True);
    }

    [Test]
    public void FallbackCanBeDisabledWithoutDisablingExplicitLocalMode()
    {
        var config = new LobbyAccountConfig { allowDevelopmentFallback = false };
        Assert.That(ClientAccountPolicy.Resolve(config, true, 0, string.Empty).IsValid, Is.False);
        config.useLocalTestAccount = true;
        Assert.That(ClientAccountPolicy.Resolve(config, true, 0, string.Empty).IsValid, Is.True);
    }

    [Test]
    public void LocalSavesAreIsolatedFromSteamAndFromEachOther()
    {
        var local1 = new ClientAccountIdentity(1, "One", true);
        var local2 = new ClientAccountIdentity(2, "Two", true);
        var steam = new ClientAccountIdentity(SteamId, "Steam", false);
        const string root = "test-root";
        Assert.That(steam.GetSaveFolder(root), Is.EqualTo(Path.Combine(root, "SaveData")));
        Assert.That(local1.GetSaveFolder(root), Is.EqualTo(Path.Combine(root, "SaveData", "LocalTest", "1")));
        Assert.That(local2.GetSaveFolder(root), Is.EqualTo(Path.Combine(root, "SaveData", "LocalTest", "2")));
    }

    [Test]
    public void ZeroLocalIdCannotAccessTheSteamSaveDirectory()
    {
        var config = new LobbyAccountConfig { useLocalTestAccount = true, accountId = 0 };
        var account = ClientAccountPolicy.Resolve(config, true, SteamId, "SteamName");
        Assert.That(account.IsLocalTestAccount, Is.True);
        Assert.That(account.IsValid, Is.False);
        Assert.Throws<InvalidOperationException>(() => account.GetSaveFolder("test-root"));
    }

    [Test]
    public void AccountNameCannotEscapeItsNumericSaveDirectory()
    {
        var config = new LobbyAccountConfig { useLocalTestAccount = true, username = "../Other", accountId = 7 };
        var account = ClientAccountPolicy.Resolve(config, true, 0, null);
        Assert.That(account.GetSaveFolder("test-root"), Is.EqualTo(Path.Combine("test-root", "SaveData", "LocalTest", "7")));
    }

    [Test]
    public void StartupIdentityIsImmutableWhenConfigurationChangesLater()
    {
        var config = new LobbyAccountConfig { useLocalTestAccount = true, accountId = 1, username = "One" };
        var account = ClientAccountPolicy.Resolve(config, true, 0, null);
        config.accountId = 2; config.username = "Two"; config.useLocalTestAccount = false;
        Assert.That(account.AccountId, Is.EqualTo(1));
        Assert.That(account.Name, Is.EqualTo("One"));
        Assert.That(account.IsLocalTestAccount, Is.True);
    }

    [Test]
    public void BlankLocalNameUsesReadableDefault()
    {
        var config = new LobbyAccountConfig { useLocalTestAccount = true, username = " " };
        var account = ClientAccountPolicy.Resolve(config, true, 0, null);
        Assert.That(account.Name, Is.EqualTo("TestPlayer"));
        Assert.That(account.IsValid, Is.True);
    }

    [Test]
    public void MissingConfigurationDoesNotInventATestIdentity()
    {
        Assert.That(ClientAccountPolicy.ShouldSkipSteam(null, true), Is.False);
        Assert.That(ClientAccountPolicy.Resolve(null, true, 0, null).IsValid, Is.False);
    }

    [Test]
    public void LocalAndSteamAccountsHaveDisjointWireIdsWithoutChangingSaveFolders()
    {
        var local = new ClientAccountIdentity(7, "local", true);
        var steam = new ClientAccountIdentity(7, "steam", false);
        Assert.That(local.NetworkAccountId, Is.Not.EqualTo(steam.NetworkAccountId));
        Assert.That(ClientAccountIdentity.IsLocalNetworkId(local.NetworkAccountId), Is.True);
        Assert.That(ClientAccountIdentity.IsLocalNetworkId(steam.NetworkAccountId), Is.False);
        Assert.That(local.GetSaveFolder("test"), Is.EqualTo(Path.Combine("test", "SaveData", "LocalTest", "7")));
        Assert.That(new ClientAccountIdentity(ClientAccountIdentity.LocalNamespace, "invalid", true).IsValid, Is.False);
    }

    [Test]
    public void LaunchArgumentsAllowParallelInstancesWithoutEditingSharedConfig()
    {
        var shared = new LobbyAccountConfig { useLocalTestAccount = true };
        var local = ClientAccountPolicy.ForLaunch(shared, new[] { "-localAccount", "2", "-battlePort", "13000", "-lobbyAddress", "192.168.1.5" }, true);
        var steam = ClientAccountPolicy.ForLaunch(shared, new[] { "-steamAccount" }, true);
        Assert.That(local.accountId, Is.EqualTo(2));
        Assert.That(local.hostedTcpPort, Is.EqualTo(13000));
        Assert.That(local.lobbyAddress, Is.EqualTo("192.168.1.5"));
        Assert.That(steam.useLocalTestAccount, Is.False);
        Assert.That(steam.allowDevelopmentFallback, Is.False);
        Assert.That(shared.accountId, Is.EqualTo(1));
        Assert.That(shared.useLocalTestAccount, Is.True);
    }

    [Test]
    public void LocalLaunchSwitchCannotEnableTestIdentityInRelease()
    {
        var config = ClientAccountPolicy.ForLaunch(new LobbyAccountConfig(), new[] { "-localAccount", "2" }, false);
        Assert.That(ClientAccountPolicy.Resolve(config, false, 0, null).IsValid, Is.False);
        Assert.Throws<ArgumentException>(() => ClientAccountPolicy.ForLaunch(config, new[] { "-localAccount", "0" }, true));
        Assert.Throws<ArgumentException>(() => ClientAccountPolicy.ForLaunch(config, new[] { "-battlePort", "65536" }, true));
    }
}
