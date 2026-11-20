using Aiursoft.CSTools.Services;

namespace Aiursoft.NugetNinja.PrBot.Services;

internal sealed class ProposalBranch(CommandService commands, string path, string branch, string endpoint)
{
    private const string Remote = "ninja-proposal";
    private const string RemoteEnvironment = "NINJA_PROPOSAL_REMOTE";
    private readonly string _reference = "refs/heads/" + branch;

    private async Task<string> Run(string args, bool useRemote = false)
    {
        // Keep the authenticated URL out of command arguments and the repository's Git config.
        var (code, output, error) = useRemote
            ? await commands.RunCommandAsync("git", $"--config-env=remote.{Remote}.url={RemoteEnvironment} {args}", path,
                environmentVariables: new Dictionary<string, string?> { [RemoteEnvironment] = endpoint })
            : await commands.RunCommandAsync("git", args, path);
        if (code != 0)
        {
            var details = useRemote ? $"Git exited with code {code}." : error.Trim();
            throw new InvalidOperationException($"Unable to run Git for the proposal branch ({args}). {details}");
        }

        return output.Trim();
    }

    public async Task<string?> ReadRemoteRevision()
    {
        await Run($"check-ref-format {Quote(_reference)}");
        var reply = await Run($"ls-remote --refs {Remote} {Quote(_reference)}", useRemote: true);
        return reply.Split('\n')
            .Select(line => line.TrimEnd('\r').Split('\t'))
            .Where(fields => fields.Length == 2 && fields[1] == _reference)
            .Select(fields => fields[0])
            .SingleOrDefault();
    }

    public async Task PreserveExistingProposal(string expectedRevision)
    {
        // Use the merged tree as a baseline without committing a rebase-only change.
        var combinedTree = await ReadCombinedTree(expectedRevision);
        await Run($"read-tree --reset -u {combinedTree}");
    }

    public async Task<bool> HasUnmergedChanges(string expectedRevision) =>
        await ReadCombinedTree(expectedRevision) != await Run("rev-parse HEAD^{tree}");

    private async Task<string> ReadCombinedTree(string expectedRevision)
    {
        await Run($"fetch --quiet --no-tags {Remote} {Quote(_reference)}", useRemote: true);
        var observedRevision = await CurrentRevision("FETCH_HEAD");
        if (observedRevision != expectedRevision)
        {
            throw new InvalidOperationException("The proposed branch changed while it was being read.");
        }

        return (await Run($"merge-tree --write-tree HEAD {observedRevision}"))
            .Split('\n')[0].Trim();
    }

    public async Task<string> Snapshot()
    {
        await Run("add -A");
        return await Run("write-tree");
    }

    public Task<string> CurrentRevision(string reference = "HEAD") => Run($"rev-parse {Quote(reference)}");

    public Task Push(string? expectedRevision) =>
        Run($"push {Quote($"--force-with-lease={_reference}:{expectedRevision}")} {Remote} {Quote($"HEAD:{_reference}")}",
            useRemote: true);

    private static string Quote(string argument) =>
        "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
