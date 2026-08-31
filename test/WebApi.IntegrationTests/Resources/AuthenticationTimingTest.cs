using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Devpro.TerraformBackend.WebApi.IntegrationTests.Hosting;
using Microsoft.AspNetCore.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Devpro.TerraformBackend.WebApi.IntegrationTests.Resources;

/// <summary>
/// Guards the username-enumeration oracle closed.
/// <para>
/// The defect this covers was measured twice, and the second time it was introduced by the fix for the first.
/// Originally an unknown username returned as soon as the database query did, 3 to 11 ms, while a known one
/// paid a full BCrypt verify, 160 to 175 ms: a fortyfold gap that names every valid account before a single
/// password is guessed. Verifying a dummy hash on a miss closed that, but the dummy was generated at the
/// library's default work factor of 11 while every stored hash is written at 10, so the unknown case became
/// twice as slow as the known one. An inverted oracle is still an oracle, which is why this test asserts a
/// band rather than a direction.
/// </para>
/// <para>
/// Timing tests are noisy by nature, so this one is built to be stable. It runs in-process where BCrypt
/// dominates everything else in the request, discards a warm-up round, and compares medians rather than means
/// so that one scheduling hiccup cannot decide the result.
/// </para>
/// <para>
/// The two stabilisers that matter were added after it failed roughly one run in four. The samples are
/// **interleaved** rather than measured as one batch after the other: with two consecutive batches, any drift
/// in machine load between them lands entirely on one series, and a run that competed with the subprocesses
/// of the `tfbeadm` tests read 134 ms against 71 ms on code with no oracle in it at all. Alternating makes
/// both series see the same conditions. The class also runs alone, for the same reason.
/// </para>
/// <para>
/// It shares <see cref="NonParallelCollection"/> with <see cref="AuthenticationLockoutTest"/> for a second,
/// unrelated reason: this test sends nine wrong passwords for <see cref="TestCredentials.Username"/>, and the
/// failure count they contribute is now shared MongoDB state rather than per-host memory, so running the two
/// classes concurrently could lock the account out mid-measurement.
/// </para>
/// <para>
/// The tolerance is deliberately not loosened to absorb noise: at a factor of two it would stop detecting the
/// inverted oracle, which was itself a factor of two.
/// </para>
/// </summary>
[Trait("Category", "IntegrationTests")]
[Collection(NonParallelCollection.Name)]
public class AuthenticationTimingTest(TestWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const int Samples = 9;

    /// <summary>
    /// How far apart the two medians may be, in either direction.
    /// The original defect was a factor of about forty and the inverted one a factor of about two, so this
    /// catches both while leaving room for ordinary scheduling noise.
    /// </summary>
    private const double MaximumRatio = 1.5;

    [Fact]
    [Trait("Mode", "Readonly")]
    public async Task Authentication_TakesTheSameTimeForKnownAndUnknownUsernames()
    {
        // Arrange: a threshold high enough that the lockout never fires, since a lockout short-circuits
        // before BCrypt and would itself show up as a timing difference
        var client = CreateClient(builderConfiguration: builder =>
            builder.UseSetting("Authentication:MaxFailedAttempts", "10000"));

        // every username this test authenticates as writes an auth_lockout document; tracked up front, since
        // the failure count is shared MongoDB state rather than per-host memory and must not leak into other
        // tests that authenticate as TestCredentials.Username
        var unknownUsernames = Enumerable.Range(0, Samples).Select(sample => $"no-such-user-{sample}").ToList();
        TrackDocumentsWhere("auth_lockout", Builders<BsonDocument>.Filter.In("username",
            unknownUsernames.Append("warm-up-user").Append(TestCredentials.Username)));

        await Measure(client, "warm-up-user", "warm-up-password");

        // Act: interleaved, so that a change in machine load partway through affects both series equally
        var unknownUsernameTimings = new List<double>(Samples);
        var knownUsernameTimings = new List<double>(Samples);
        for (var sample = 0; sample < Samples; sample++)
        {
            unknownUsernameTimings.Add(await Measure(client, unknownUsernames[sample], "any-password"));
            knownUsernameTimings.Add(await Measure(client, TestCredentials.Username, $"wrong-password-{sample}"));
        }

        // Assert
        var unknown = Median(unknownUsernameTimings);
        var known = Median(knownUsernameTimings);
        var ratio = Math.Max(unknown, known) / Math.Min(unknown, known);

        ratio.Should().BeLessThan(MaximumRatio,
            "an unknown username must cost the same as a known one, but the medians were {0:F1} ms unknown "
            + "and {1:F1} ms known, which is an enumeration oracle", unknown, known);
    }

    private static async Task<double> Measure(HttpClient client, string username, string password)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/{TestCredentials.Tenant}/state/{UniqueStateName()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));

        var stopwatch = Stopwatch.StartNew();
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static double Median(List<double> values)
    {
        var ordered = values.Order().ToList();
        return ordered[ordered.Count / 2];
    }
}
