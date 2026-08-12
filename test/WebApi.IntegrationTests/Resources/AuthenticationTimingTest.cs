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
/// Timing tests are noisy by nature, so this one is built to be stable: it runs in-process where BCrypt
/// dominates everything else in the request, discards a warm-up round, and compares medians rather than means
/// so that one scheduling hiccup cannot decide the result. The tolerance is well inside both defects it
/// guards against.
/// </para>
/// </summary>
[Trait("Category", "IntegrationTests")]
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

        await Measure(client, "warm-up-user", "warm-up-password");

        // Act
        var unknownUsernameTimings = await MeasureMany(client, sample => ($"no-such-user-{sample}", "any-password"));
        var knownUsernameTimings = await MeasureMany(client, sample => (TestCredentials.Username, $"wrong-password-{sample}"));

        // Assert
        var unknown = Median(unknownUsernameTimings);
        var known = Median(knownUsernameTimings);
        var ratio = Math.Max(unknown, known) / Math.Min(unknown, known);

        ratio.Should().BeLessThan(MaximumRatio,
            "an unknown username must cost the same as a known one, but the medians were {0:F1} ms unknown "
            + "and {1:F1} ms known, which is an enumeration oracle", unknown, known);
    }

    private static async Task<List<double>> MeasureMany(HttpClient client, Func<int, (string Username, string Password)> credentials)
    {
        var timings = new List<double>(Samples);
        for (var sample = 0; sample < Samples; sample++)
        {
            var (username, password) = credentials(sample);
            timings.Add(await Measure(client, username, password));
        }

        return timings;
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
