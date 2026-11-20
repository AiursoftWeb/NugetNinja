using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aiursoft.NugetNinja.PrBot.Services;

internal enum ProposalCategory
{
    Nuget,
    Npm,
    Translation
}

internal sealed class ProposalCooldownStore(string workspace, string identity)
{
    private readonly string _path = Path.Combine(workspace, ".proposal-cooldowns",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))) + ".json");

    public FileStream Lock()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        return new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public ProposalPublication Load() => File.Exists(_path)
        ? JsonSerializer.Deserialize<ProposalPublication>(File.ReadAllText(_path))
            ?? throw new InvalidDataException("Invalid proposal cooldown state.")
        : new ProposalPublication();

    public void Save(ProposalPublication publication)
    {
        var temp = _path + ".tmp";
        using (var stream = File.Create(temp))
        {
            JsonSerializer.Serialize(stream, publication);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, _path, overwrite: true);
    }
}

internal sealed class ProposalPublication
{
    public Dictionary<ProposalCategory, DateTimeOffset> LastPublished { get; init; } = [];

    public PendingProposal? Pending { get; set; }

    public bool IsReady(ProposalCategory category, TimeSpan interval, DateTimeOffset now) =>
        interval == TimeSpan.Zero || !LastPublished.TryGetValue(category, out var last) || now - last >= interval;

    public void ConfirmPush(DateTimeOffset pushedAt)
    {
        var pending = Pending ?? throw new InvalidOperationException("There is no prepared proposal.");
        foreach (var category in pending.Categories)
        {
            LastPublished[category] = pushedAt;
        }

        Pending = pending with { Pushed = true };
    }

    public void Recover(string? revision)
    {
        if (Pending is not { Pushed: false } pending)
        {
            return;
        }

        if (revision == pending.Revision)
        {
            ConfirmPush(pending.PreparedAt);
        }
        else if (revision == pending.PreviousRevision)
        {
            Pending = null;
        }
        else
        {
            throw new InvalidOperationException("The proposed branch changed during an unconfirmed push.");
        }
    }
}

internal sealed record PendingProposal(
    string Revision, string? PreviousRevision, DateTimeOffset PreparedAt, ProposalCategory[] Categories, bool Pushed = false);
