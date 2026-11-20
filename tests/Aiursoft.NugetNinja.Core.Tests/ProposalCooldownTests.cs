using Aiursoft.CSTools.Services;
using Aiursoft.NugetNinja.Core.Services.Extractor;
using Aiursoft.NugetNinja.PrBot.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aiursoft.NugetNinja.Core.Tests;

[TestClass]
public class ProposalCooldownTests
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "ninja-proposals-" + Guid.NewGuid().ToString("N"));
    private readonly CommandService _commands = new();
    private readonly DateTimeOffset _now = DateTimeOffset.Parse("2026-11-20T12:00:00Z");

    [TestInitialize]
    public void CreateWorkspace() => Directory.CreateDirectory(_path);

    [TestCleanup]
    public void DeleteWorkspace() => Directory.Delete(_path, recursive: true);

    [TestMethod]
    public async Task ReadsPerRepositoryPolicyAndLeavesOldConfigurationsOptional()
    {
        var extractor = new Extractor(NullLogger<Extractor>.Instance);
        var path = Path.Combine(_path, "ninja.yaml");
        await File.WriteAllTextAsync(path, "npm:\n  updateDependencies: true\n");
        Assert.IsNull((await extractor.ReadConfigAsync(_path)).ProposalCooldowns);

        await File.AppendAllTextAsync(path, "proposalCooldowns:\n  nuget: 1d\n  npm: 7d\n  translation: 0\n");
        var cooldowns = (await extractor.ReadConfigAsync(_path)).ProposalCooldowns!;
        Assert.AreEqual(TimeSpan.FromDays(1), cooldowns.NugetInterval);
        Assert.AreEqual(TimeSpan.FromDays(7), cooldowns.NpmInterval);
        Assert.AreEqual(TimeSpan.Zero, cooldowns.TranslationInterval);
    }

    [TestMethod]
    [DataRow("-1d")]
    [DataRow("1.5d")]
    [DataRow("1w")]
    [DataRow("999999999d")]
    public async Task RejectsInvalidIntervals(string value)
    {
        var path = Path.Combine(_path, "ninja.yaml");
        await File.WriteAllTextAsync(path, $"proposalCooldowns:\n  npm: {value}\n");
        var extractor = new Extractor(NullLogger<Extractor>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(() => extractor.ReadConfigAsync(_path));
    }

    [TestMethod]
    public void CooldownsSurviveARestartAndAdvanceOnlyForTheKindsPublished()
    {
        var store = new ProposalCooldownStore(_path, "repository");
        using var handle = store.Lock();
        var publication = store.Load();
        Assert.IsTrue(publication.IsReady(ProposalCategory.Npm, TimeSpan.FromDays(7), _now));
        publication.Pending = new PendingProposal("npm-commit", null, _now, [ProposalCategory.Npm]);
        store.Save(publication);

        publication = store.Load();
        publication.Recover("npm-commit");
        Assert.IsTrue(publication.Pending!.Pushed);
        Assert.IsTrue(publication.IsReady(ProposalCategory.Nuget, TimeSpan.FromDays(1), _now));

        var tomorrow = _now.AddDays(1);
        publication.Pending = new PendingProposal("other-changes", "npm-commit", tomorrow,
            [ProposalCategory.Nuget, ProposalCategory.Translation]);
        publication.ConfirmPush(tomorrow);
        publication.Pending = null;
        store.Save(publication);

        publication = store.Load();
        Assert.IsTrue(publication.IsReady(ProposalCategory.Translation, TimeSpan.Zero, tomorrow));
        Assert.IsFalse(publication.IsReady(ProposalCategory.Nuget, TimeSpan.FromDays(1), tomorrow.AddHours(23)));
        Assert.IsTrue(publication.IsReady(ProposalCategory.Nuget, TimeSpan.FromDays(1), tomorrow.AddDays(1)));
        Assert.IsFalse(publication.IsReady(ProposalCategory.Npm, TimeSpan.FromDays(7), _now.AddDays(7).AddSeconds(-1)));
        Assert.IsTrue(publication.IsReady(ProposalCategory.Npm, TimeSpan.FromDays(7), _now.AddDays(7)));
        Assert.IsTrue(new ProposalCooldownStore(_path, "another-repository").Load()
            .IsReady(ProposalCategory.Npm, TimeSpan.FromDays(7), tomorrow));
    }

    [TestMethod]
    public void RejectedPushCanBeRetriedWithoutAdvancingCooldown()
    {
        var publication = new ProposalPublication
        {
            Pending = new PendingProposal("proposed-head", "before-push", _now, [ProposalCategory.Npm])
        };
        publication.Recover("before-push");
        Assert.IsNull(publication.Pending);
        Assert.HasCount(0, publication.LastPublished);
        Assert.IsTrue(publication.IsReady(ProposalCategory.Npm, TimeSpan.FromDays(7), _now.AddSeconds(1)));
    }

    [TestMethod]
    public async Task ExistingProposalSurvivesTranslationRefreshAndStalePushIsRejected()
    {
        var repo = Path.Combine(_path, "work");
        var fork = Path.Combine(_path, "remote.git");
        await Git(fork, "init --bare --initial-branch=main");
        await Git(repo, "init --initial-branch=main");
        await Git(repo, "config user.name Test");
        await Git(repo, "config user.email test@example.invalid");
        await Git(repo, "config commit.gpgsign false");
        var npm = Path.Combine(repo, "package-lock.json");
        await File.WriteAllTextAsync(npm, "original lock");
        await Git(repo, "add .");
        await Git(repo, "commit -m Main");
        var original = await Git(repo, "rev-parse HEAD");

        var branch = new ProposalBranch(_commands, repo, "users/ninja/evergreen", fork);
        Assert.IsNull(await branch.ReadRemoteRevision());
        await File.WriteAllTextAsync(npm, "published update");
        await branch.Snapshot();
        await Git(repo, "commit -m Npm");
        await branch.Push(null);
        var prior = await branch.ReadRemoteRevision();
        Assert.IsNotNull(prior);

        await Git(repo, $"reset --hard {original}");
        var businessChange = Path.Combine(repo, "business.txt");
        await File.WriteAllTextAsync(businessChange, "new default branch change");
        await Git(repo, "add .");
        await Git(repo, "commit -m Business");
        await branch.PreserveExistingProposal(prior);
        Assert.AreEqual("published update", await File.ReadAllTextAsync(npm));
        Assert.IsTrue(File.Exists(businessChange));

        var existing = await branch.Snapshot();
        Assert.AreEqual(existing, await branch.Snapshot(), "The same candidate keeps the existing commit.");
        await File.WriteAllTextAsync(Path.Combine(repo, "strings.resx"), "new translation");
        Assert.AreNotEqual(existing, await branch.Snapshot());
        await Git(repo, "commit -m Translation");
        await Assert.ThrowsAsync<InvalidOperationException>(() => branch.Push(null));
        Assert.AreEqual(prior, await branch.ReadRemoteRevision());
        await branch.Push(prior);

        var current = await branch.ReadRemoteRevision();
        Assert.AreEqual(await branch.CurrentRevision(), current);
        Assert.AreEqual("published update", await Git(fork, $"show {current}:package-lock.json"));
        Assert.AreEqual("new translation", await Git(fork, $"show {current}:strings.resx"));
    }

    private async Task<string> Git(string path, string args)
    {
        var (exitCode, output, error) = await _commands.RunCommandAsync("git", args, path);
        Assert.AreEqual(0, exitCode, error);
        return output.Trim();
    }
}
