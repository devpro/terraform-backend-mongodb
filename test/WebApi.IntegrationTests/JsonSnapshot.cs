using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests;

/// <summary>
/// Compares a JSON document with a snapshot committed under <c>Snapshots/</c>.
/// <para>
/// Both sides are reindented the same way before comparing, so a difference in whitespace or escaping alone never fails the test, while a change in property order does.
/// On a mismatch the actual document is written next to the snapshot as <c>{name}.received.json</c>, ignored by git, so accepting a change is a rename.
/// The directory comes from the source path of this file rather than the build output, so the received file lands where it can be reviewed and committed.
/// </para>
/// </summary>
public static class JsonSnapshot
{
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static void Match(string? actualJson, string name)
    {
        var directory = SnapshotDirectory();
        var snapshotPath = Path.Combine(directory, $"{name}.json");
        var receivedPath = Path.Combine(directory, $"{name}.received.json");

        var actual = Normalize(actualJson ?? "null");
        var expected = File.Exists(snapshotPath) ? Normalize(File.ReadAllText(snapshotPath)) : null;

        if (actual == expected)
        {
            File.Delete(receivedPath);
            return;
        }

        File.WriteAllText(receivedPath, actual);
        expected.Should().Be(actual, $"the snapshot {snapshotPath} must match, and the actual document was written to {receivedPath}");
    }

    private static string SnapshotDirectory([CallerFilePath] string sourceFilePath = "") =>
        Path.Combine(Path.GetDirectoryName(sourceFilePath)!, "Snapshots");

    private static string Normalize(string json) =>
        (JsonNode.Parse(json)?.ToJsonString(Indented) ?? "null").ReplaceLineEndings("\n") + "\n";
}
