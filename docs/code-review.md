# Code review

Assessment of the repository as of 2026-08-11 (version 1.2.3, commit `d380eaa`), extended with a dedicated security pass on 2026-08-12, and again on 2026-08-31 with a configuration-validation, scaling, and coverage pass.
It supersedes the review of 2026-07-10 (commit `a6de57c`): every earlier finding was re-checked against the current code, and the outcome is recorded in the [status of the previous review](#status-of-the-previous-review).
The review covers source code, tests, CI/CD workflows, container packaging, and operational tooling.

## Method

Findings are marked **Proven** when a command reproduced them against a running instance, and **Observed** when they come from reading the code alone.
The proven findings were reproduced against `dotnet run --project src/WebApi` backed by a freshly seeded MongoDB 8.2 database.
Reproduction commands are given inline so that each one can be turned into a regression test.

## Overall assessment

The codebase is small, clean, and focused.
The three-project layering (`WebApi` > `Infrastructure.MongoDb` > `Domain`) is respected with one-way dependencies.
The Terraform HTTP backend protocol is implemented faithfully at the level of status codes and lock semantics: raw `text/plain` state responses, `423 Locked`, `409 Conflict` with the current lock as body, and upsert on POST.
Multi-tenancy is enforced consistently through the tenant claim and the `TenantAuthorizationFilter`, and every MongoDB query filters on tenant.
CI is mature for a project of this size: Sonar, FOSSA, container CVE scanning with a zero-CVE budget, markdown and YAML linting, and scenario tests that run the real `terraform` CLI against a Kestrel-hosted instance.

The three high-severity findings of the previous review were fixed and the fixes hold up under inspection.
The theme of this round is different: the protocol layer is correct, but the **conversion between the request payload and the stored document is unguarded**.

The security pass added on 2026-08-12 found a second theme, and it is now the more urgent of the two.
Tenant isolation is sound, and the multi-tenancy model holds under inspection.
What is missing is everything around the credential: **there is no rate limiting, no lockout, no attributable record of a failed authentication, and the cost of rejecting a bad password is paid in full by the server on every attempt**.
The application is in daily production use, so the [security](#security) section is ranked ahead of the medium findings.

The design intent is not in question here.
Storing the state as a queryable BSON document is the premise of the project, stated in the first line of the README, fixed in `AGENTS.md`, and relied on by other applications that read `tf_state` directly.
Every finding below takes that constraint as given.
The defect is that the conversion has no guard rails: values that BSON cannot represent natively are either rejected with a 500 or silently reshaped on the way out, and a document that exceeds the BSON size limit is rejected outright.
Both produce a failed `terraform apply` rather than a degraded experience, which is why they are ranked high.

## Verification of the NuGet upgrade

The package bump in `d380eaa` was verified, since it had not been tested when it was committed.

- `dotnet build` succeeds with 0 warnings and 0 errors.
- `dotnet test` passes 13 of 13 tests, including the `terraform init/plan/apply` scenario against a Kestrel-hosted instance.

No action is needed on the upgrade itself.
The first test run did fail 6 of 13 tests, for an environmental reason that is a finding in its own right: see [T1](#t1-the-test-suite-fails-confusingly-against-a-drifted-local-database).

## High severity

### H1. A state larger than 16 MB is rejected with an unhandled 500

**Fixed on 2026-08-12**, covered by `StateFidelityTest`.
The ceiling is unchanged and permanent, as argued below; what changed is how it is reported.
An oversized state now answers `413` naming the limit rather than letting a driver `FormatException` escape as a 500:

```text
{"message":"The state exceeds the maximum document size of 16777216 bytes (16 MB), which is the limit of the underlying storage."}
```

The limit is taken from MongoDB's documented value rather than from `BsonDefaults.MaxDocumentSize`, which is `int.MaxValue` until a connection reports otherwise and would have named a limit no operator can act on.

The finding as originally proven follows.

**Proven.**
`StateRepository.CreateAsync` stores the state as a parsed `BsonDocument`, so a state document is bound by the MongoDB maximum BSON document size of 16 MB.

```bash
# builds an 18 MB state document
python3 -c "import json;open('big.json','w').write(json.dumps({'version':4,'serial':1,'resources':[{'name':'r%d'%i,'blob':'x'*900} for i in range(20000)]}))"
curl -u admin:admin123 -X POST http://localhost:5293/dummy/state/big \
  -H "Content-Type: application/json" --data-binary @big.json
```

The response is `500` with `System.FormatException: Size 18777785 is larger than MaxDocumentSize 16793600`.

Two details make this worse than a plain size limit.
The failure surfaces during `terraform apply`, after the lock has been taken and after the real infrastructure has been changed, which is the worst possible moment to lose a state write.
The history patch in `tf_state_history` is stored as a JSON *string* in a single document, so it is bound by the same 16 MB limit while being far less compact than the BSON it describes, and on an update it is written first.
The effective ceiling for updating a large state is therefore lower than 16 MB, and which of the two writes fails depends on how much the state changed.

Terraform states of this size are not exotic for a large workspace, and nothing in the documentation mentions the limit.

The 16 MB ceiling is a permanent property of this design, and it should be treated as one.
The shape of a `tf_state` document is a published contract: other applications query it by resource attribute, and that shape will not change.
Every way out of the limit therefore breaks something that matters more than the limit does.
GridFS and compressed `BsonBinaryData` raise the ceiling by making the state opaque.
Splitting the `resources` array into separate documents keeps the data queryable but changes the very shape the contract fixes.

The correct response is to accept the ceiling and fail cleanly at it.
Return `413 Payload Too Large` with a message naming the limit, rather than letting a driver `FormatException` escape as a 500 in the middle of an apply, and document the limit alongside the backend configuration so that it is known before a workspace grows into it.

### H2. State values outside the BSON numeric range are corrupted or rejected

**Fixed on 2026-08-12**, covered by `StateFidelityTest`, which drives a table of payloads through a full write and read and compares the two.

The fix is the one scoped below, and it is confined to the .NET layer: `JsonToBsonConverter` reproduces what `BsonDocument.Parse` did for every value in range and maps the two out-of-range cases to `Decimal128` instead of failing or storing an infinity, and `BsonToJsonConverter` renders the stored document as plain JSON on the way out.

Verified against a running instance:

- `{"v":123456789012345678901234567890}` stores and reads back identically, where it used to answer 500.
- `{"v":1e400}` reads back as `{"v":1E+400}`, a JSON number, where it used to read back as `{"v":{"$numberDouble":"Infinity"}}`.
- `{"c":0.1}` reads back as `0.1` rather than `0.10000000000000001`, since the renderer now writes the shortest representation that reads back as the same double.

What is stored is unchanged in kind: both values above are native `Decimal128` in `tf_state`, and a numeric range query still matches them, so the document remains queryable field by field.
`1e3` still reads back as `1000.0` and `-0.0` as `0.0`, which the finding already recorded as acceptable.

The finding as originally proven follows.

**Proven.**
The state is round-tripped through BSON, so JSON numbers are coerced into BSON numeric types, and the response is produced by `BsonDocument.ToJson()`, which emits MongoDB Extended JSON rather than the JSON that was stored.

Integers beyond `Int64` fail the write outright:

```bash
curl -u admin:admin123 -X POST http://localhost:5293/dummy/state/n1 \
  -H "Content-Type: application/json" --data '{"v":123456789012345678901234567890}'
```

The response is `500` with `System.OverflowException: Value was either too large or too small for an Int64`, thrown from `BsonDocument.Parse`.

Values beyond the `double` range are worse, because the write succeeds and the corruption is silent:

```bash
curl -u admin:admin123 -X POST http://localhost:5293/dummy/state/n2 \
  -H "Content-Type: application/json" --data '{"v":1e400}'   # 200 OK
curl -u admin:admin123 http://localhost:5293/dummy/state/n2
```

The read returns `{ "v" : { "$numberDouble" : "Infinity" } }`.
That is not the document that was written: a scalar became an object, and Terraform receives a state whose shape differs from the one it produced.

Numeric normalisation is visible on ordinary values as well: `1e3` reads back as `1000.0`, and `-0.0` reads back as `0.0`.
Those are semantically equivalent in JSON and are unlikely to break Terraform, but they confirm that the stored bytes are not the submitted bytes.

The narrower concern raised as M1 in the previous review is **withdrawn**: keys containing dots and keys prefixed with `$` round-trip correctly on MongoDB 8.2, verified with `{"tags":{"kubernetes.io/cluster":"x","$ref":"y"}}`.

The fix does not require giving up the document model, and it is narrower than it first appears.
Measuring what BSON can and cannot carry gives a precise scope for the change.

- `Decimal128` holds both values that break today, `1e400` and a thirty-digit integer, and only gives out beyond roughly `1e6144`.
  Parsing an out-of-range JSON number into `Decimal128` instead of letting `Int64.Parse` throw removes the 500 on the write path.
- On the read path, relaxed Extended JSON already emits plain JSON for the types that matter: `Int32`, `Int64`, `Double` and `String` all serialise as bare JSON values, which is why ordinary Terraform states round-trip cleanly today.
  Only two BSON types leak a `$`-prefixed wrapper, non-finite `Double` and `Decimal128`, and both leak in every output mode the driver offers.

So the read path needs a thin serialisation pass that renders those two types as plain JSON numbers, and nothing else needs to change.
That keeps `tf_state` fully queryable, keeps every field indexable, and gives Terraform strict JSON.
It also improves what the consuming applications see, since a `Decimal128` is a usable number whereas the current `{"$numberDouble":"Infinity"}` is not.

A note on urgency: these values are rare in real Terraform state, which comes from provider attributes such as counts, ports and identifiers.
The finding is ranked high because the failure is a broken apply and because silent corruption is hard to detect, not because it is likely to be hit this week.
H1 is the more probable of the two to affect a real workspace.

### Calibration against real data

Both findings above were reproduced with constructed inputs, so the development database was scanned on 2026-08-11 to see whether either has occurred in practice.
Neither has.

- No document in `tf_state` contains a non-finite number or any other value that fails to round-trip, across all 14 states.
- The largest stored state is 1.1 MB, which is about seven per cent of the BSON document limit.
- No stale locks were present in `tf_state_lock`.

That argues for treating H1 and H2 as cheap robustness work rather than as an emergency.
The ranking stands, because a broken apply is a bad failure and silent corruption is hard to notice, but nothing is on fire.
It also sets the scale for the ceiling: a workspace would need to grow roughly fifteenfold beyond the current largest state before it reaches 16 MB.

A related correction to a common assumption.
A crash during development cannot leave a half-written document, because a single-document write in MongoDB is atomic.
What a crash does leave is a lock that no run will ever release, and, because of M4 below, a history entry describing a state transition that never completed.
Those are the artefacts worth looking for after an interrupted run, not corrupt state values.

## Security

This section supersedes M5 of the first pass and is ranked ahead of the medium findings, because the application is in daily production use and the authentication endpoint is the only thing standing between the public internet and the infrastructure state of every workspace.

### What Terraform allows, and what it does not

The threat model is shaped by the client, which cannot be changed.
The `http` backend authenticates with `username` and `password`, sent as an HTTP Basic header on every single request, and it offers no bearer token, no OAuth flow and no custom headers.
Two consequences follow and neither is negotiable.

- A long-lived credential travels on every request, so transport security is not optional and the credential cannot be short-lived.
- The server pays the cost of verifying that credential on every request, including on every unauthenticated attempt.

One option is often missed: the `http` backend does support mutual TLS through `client_certificate_pem`, `client_private_key_pem` and `client_ca_certificate_pem`.
That is a Terraform-native way to add a second factor in front of Basic authentication, and it is the single largest available improvement to the authentication posture.

### S1. Unlimited authentication attempts, with no lockout and no record

**Fixed on 2026-08-12**, covered by `AuthenticationLockoutTest`.

`ThrottledCredentialAuthenticator` counts consecutive failures and refuses the pair once the threshold is reached, and `BasicAuthenticationHandler` now logs every failure with the caller's address instead of the supplied username.

Two decisions in that fix are worth stating, because both look like details and are not.

The lockout is scoped to the username **and the caller's address together**, never to the username alone.
A lockout on the username alone hands a denial of service to anybody on the internet: guessing at `admin` from anywhere would lock the real operator out of the backend their applies depend on, which is a worse outcome than the attack.
An attacker spread across many addresses still gets only the configured allowance per address, and the volume case belongs to the ingress rate limit in S7 rather than to the application.

A lockout answers with the same `401` as a wrong password.
Answering with `429` would tell an attacker which usernames exist and which guesses were worth making, which is the oracle S3 exists to close.

The finding as originally proven follows.

**Proven.**
There is no rate limiting, no failed-authentication lockout, and no backoff anywhere in the pipeline.

```bash
# 50 consecutive wrong passwords for a known account
for i in $(seq 1 50); do
  curl -s -o /dev/null -w "%{http_code} " -u "admin:guess$i" http://localhost:5293/dummy/state/probe
done
```

All 50 returned `401` in 6.94 seconds, with no throttling, no `429`, and no growing delay.
The 51st attempt was served exactly like the first.

The absence of a record is the more serious half.
The run above produced 361 authentication failures in the application log, and not one of them carries a source IP address, so a brute-force campaign is neither detectable nor attributable after the fact.
`UserRepository.CheckAuthentication` logs the attacker-supplied username at `Information` level, which is the wrong half of the pair to record: it fills the log with attacker-controlled text, and it will capture a password verbatim on the day a user transposes the two fields.

The fix is a rate limiter keyed on source IP and on username, a lockout after a threshold of consecutive failures, and an authentication-failure log carrying the source IP rather than the supplied username.

### S2. BCrypt on every request is a CPU-exhaustion lever

**Fixed on 2026-08-12.**
A verified credential is now cached for a configurable window, 60 seconds by default, so repeat traffic from the same run no longer pays BCrypt.
The cache is keyed by an HMAC of the username and password under a salt generated per process, so an entry cannot be carried back to a credential and nothing survives a restart.
Only successes are cached: a wrong password must always pay the verify and always reach the lockout counter, or the cache would become a way around S1.

The measured effect on the suite is a drop from roughly 9 seconds to 6.

The finding as originally proven follows.

**Proven, with the severity calibrated below.**
The stored hashes are `$2b$10$`, so every request pays a work-factor-10 verify.
Measured on this instance, a request that reaches BCrypt costs about 139 ms of CPU, and the credential is verified again on every request because Terraform re-sends it.

Calibration matters here, because the finding is easy to overstate.
On the 22-core development machine, 100 concurrent failed authentications completed in 0.96 s and `/health` continued to answer in 3 to 6 ms throughout.
Nothing was saturated.
The exposure is a function of the CPU limit of the deployment, not of the code: at roughly 139 ms per attempt, one core absorbs about seven attempts per second before it is fully consumed, and a container limited to one or two cores is therefore within reach of a single attacker on a domestic connection.

That also sets the brute-force budget.
Without the lockout of S1, an attacker gets on the order of seven guesses per second per core, which is around 600,000 guesses per day against a single core.
That is fatal to a human-chosen password and irrelevant to a high-entropy generated one, which is why the credential guidance in S6 is part of this fix rather than separate from it.

A short-lived cache of successful verifications removes BCrypt from the hot path for legitimate traffic and shrinks the attack surface to first-contact requests.
It also removes the constant latency that every Terraform operation currently pays.

### S3. The username enumeration oracle is blatant

**Fixed on 2026-08-12**, covered by `AuthenticationTimingTest`, and worth reading for how the fix went wrong first.

`UserRepository` now verifies a dummy hash when the lookup misses, so both outcomes cost a BCrypt verify.
The first attempt at that closed nothing: the dummy was generated at the library's default work factor of 11 while every stored hash is written at 10, so an unknown username became roughly twice as *slow* as a known one, 123 to 184 ms against 58 to 74 ms.
An inverted oracle is still an oracle.
The work factor is now pinned in `UserRepository.StoredHashWorkFactor` and the test fixture seeds through the same constant, since a user created at a different cost reopens the gap for that account.
After the fix both paths measure 82 to 90 ms.

The original finding follows, since the reproduction is the regression test.

**Proven.**
`UserRepository.CheckAuthentication` returns immediately when the username does not exist and runs BCrypt when it does, so the two cases are separated by more than an order of magnitude.

```bash
curl -s -o /dev/null -w "%{time_total}\n" -u "nosuchuser:whatever" http://localhost:5293/dummy/state/probe
curl -s -o /dev/null -w "%{time_total}\n" -u "admin:wrongpassword"  http://localhost:5293/dummy/state/probe
```

An unknown username answers in 3 to 11 ms, a known one in 160 to 175 ms.
The gap is roughly fortyfold and needs no statistics to read, so an attacker can enumerate the valid accounts of every tenant before spending a single guess on a password.
Verifying against a fixed dummy hash when the lookup misses equalises the two paths and costs one line.

### S4. Basic credentials depend entirely on transport security

**Observed.**
`Features:IsHttpsRedirectionEnabled` defaults to `true` in `appsettings.json`, which is the right default, but the container serves plain HTTP on 8080 and TLS is expected to terminate at an ingress that is outside this repository.
HSTS is not configured anywhere.
Since the password is replayed on every request, a single plaintext hop exposes a credential that unlocks the state of every workspace in the tenant.
The deployment documentation should state that TLS termination is mandatory rather than recommended, and that `skip_cert_verification` in a backend block defeats the protection it appears to configure.

### S5. Terraform state is a secret store, and `tf_state` holds it in the clear

**Observed.**
This follows from the storage contract rather than from a defect, which is exactly why it needs to be written down.
A Terraform state contains provider credentials, generated passwords and private keys in plaintext, and the design of this project stores that state as a queryable document that other applications, `liveship` among them, read directly.
The blast radius of the database is therefore the blast radius of every secret in every managed workspace.

Nothing here argues for changing the storage model.
It argues for the controls that the model makes necessary: encryption at rest on the MongoDB deployment, a least-privilege read-only database user for the consuming applications rather than a shared one, network isolation of the database, and an explicit statement in the documentation that a `tf_state` backup is a secret-bearing artefact.

### S6. Credential handling in `tfbeadm` leaks, and the quoting hid an injection

**Fixed on 2026-08-12**, covered by `TfbeadmTest`.

The password was taken as a command-line argument, visible in `ps` to every other user on the host for the lifetime of the command and written to the shell history, and it was passed on to `htpasswd -b` as an argument again.
`create-user` now takes `<username> <tenant>` and reads the password from a prompt or from standard input, and `htpasswd -i` reads it from a pipe.
The three-argument form still works, since the compose `dbinit` service and existing scripts use it, and it now warns.

The quoting fragility noted in the operations section turned out to be the smaller half of a worse problem.
Values were interpolated into a JavaScript string that was passed through two shells, and only the hash went through `printf %q`: the username and the tenant went in raw.
That is an injection, and it was confirmed against the previous script rather than assumed.
A username of the form

```text
x'}); db.user.insertOne({username: 'chosen-name', password_hash: '
```

closes the `insertOne` call and opens a second one, leaving the fields that follow to complete the injected document, so the command creates a second account under a username and tenant of the caller's choosing.
The payload shape matters: appending a field is won by the later real value, and terminating with a comment only produces a syntax error.

Exploiting it requires supplying the username, so the exposure is bounded by who drives the script.
It is an operator tool today, and the flaw would become serious the moment account creation is automated from any input the operator does not control.

The fix removes interpolation rather than escaping it: the username, hash and tenant now travel through the environment and are read in JavaScript as `process.env`, so nothing supplied by the caller is ever parsed as JavaScript and nothing is quoted through two shells by hand.
The work factor is pinned to a named constant that must stay in step with `UserRepository.StoredHashWorkFactor`, since an account created at a different cost reopens the S3 oracle for that account.

No strength requirement is enforced, which remains open: the usage documentation now recommends a generated credential, and the brute-force budget in S2 is why that matters.

### S7. Authentication thresholds had no startup validation, and were undocumented

**Fixed on 2026-08-31**, covered by `AuthenticationServiceCollectionExtensionsTest`.
`AddCredentialAuthentication` now throws `InvalidOperationException` before registering anything if `Authentication:MaxFailedAttempts` is below 1 or `Authentication:LockoutSeconds` is at or below zero, either of which would silently defeat the lockout of S1.
`Authentication:CredentialCacheSeconds` needs no such check: `ThrottledCredentialAuthenticator` already treats zero or negative as "do not cache", which is a valid opt-out rather than a misconfiguration.
The six settings behind S1 and S2 are now documented in [setup.md](setup.md#authentication-and-network-settings), including `Network:KnownProxies` and `Network:KnownNetworks`, without which the trusted-proxy prerequisite noted above could not be configured at all.

The finding as originally observed follows.

**Observed.**
`ApplicationConfiguration.MaxFailedAttempts` and `LockoutDuration` read straight from configuration with a numeric default and no bounds check.
`Authentication__MaxFailedAttempts=0` locked out the first attempt of every account from every address, and `Authentication__LockoutSeconds=0` made a lockout expire immediately, neither failing until the bad value surfaced as a confusing runtime symptom rather than at startup.
None of the six settings appeared in `setup.md`.

### Where each control belongs

Most of this section should not be built in C#, and saying which parts should is the useful half of the answer.

The dividing line is what the control needs to know.
A control that needs only the connection, the request rate or the certificate belongs to the platform, which sees those before any application code runs and enforces them across every replica at once.
A control that needs the identity inside the request belongs to the application, because a Basic credential is Base64 inside a header and nothing in front of the application can read the username out of it without decoding credentials in the proxy, which is worse than the problem it solves.

Control | Where | Why
------- | ----- | ---
Rate limit by source IP | Platform | Ingress, WAF or CDN rejects the request before any CPU is spent on it, holds one counter across all replicas, and survives a restart. An in-process limiter is per-pod and resets on deploy.
TLS termination and HSTS | Platform | Standard ingress responsibility, with certificate rotation already solved there.
Mutual TLS | Platform | The ingress validates the client certificate and passes the verified subject as a header. Doing it in Kestrel means owning certificate distribution and revocation.
Encryption at rest, network isolation, database RBAC | Platform | A managed MongoDB (Atlas or the cloud provider's equivalent) gives all three, plus database auditing, with no application code.
Alerting on authentication-failure bursts | Platform | The `compose.yaml` OpenTelemetry collector is already the pipeline. The application emits the event, the platform correlates and alerts.
**Lockout after N consecutive failures for one account** | **Application** | Needs the username, which only the application decodes, and needs state tied to the user record.
**Short-lived credential cache** | **Application** | Purely internal to the authentication handler. Nothing external can shortcut a BCrypt verify.
**Dummy-hash verify on a username miss** | **Application** | The timing oracle is created inside `CheckAuthentication` and can only be closed there.
**Authentication-failure events carrying the source IP** | **Application** | The platform can only alert on what the application emits, and today it emits no IP at all.
**`tfbeadm` credential handling** | **Application** | An operator tool in this repository.

Two things follow from the table.

The irreducible application work is small, which is the argument for doing it: the four application rows are a cache, one dummy-hash line, a lockout counter and a log field.
That is days, not weeks, and none of it depends on which platform the deployment lands on.

The platform rows are worth nothing if the application cannot trust what the platform tells it.
Behind an ingress, `HttpContext.Connection.RemoteIpAddress` is the proxy address, so an authentication-failure log carrying the source IP and any per-IP decision inside the application are both wrong until `UseForwardedHeaders` is configured with the known proxies and networks.
`Program.cs` does not configure it today, which makes it a prerequisite for S1 and for B-41 rather than a detail.

There is also a floor that no platform raises: rate limiting at the edge does not make a weak password safe, it only slows the attempt rate.
The generated-credential guidance in S6 is what actually sets the difficulty of the guess.

### Tenant isolation holds, and is now covered

`TenantAuthorizationFilter` rejects any request whose route `{tenant}` does not exactly match the tenant claim, every repository query filters on tenant, and the controller passes the route value that the filter has already validated.
No cross-tenant path was found.

`TenantIsolationTest` now holds that ground, using a second fully valid account on a second tenant.
Asserting isolation with one account only proves that a route naming an unclaimed tenant is refused, which was already covered; what these assert is that a caller who authenticates perfectly well still cannot read, overwrite, delete or lock another tenant's state, and that the refusal leaves the state untouched rather than merely returning the right status code.
One of the five guards the repository filter instead of the authorization filter, by writing the same state name under two tenants and checking each reads back its own.

The tests were verified against a deliberately disabled `TenantAuthorizationFilter`: four of the five fail, and the fifth survives, which is the expected split given that the repository still filters on tenant.

## Medium severity

### M1. A state POST without a `Content-Type` header returns 500

**Proven.**
This is a correction of finding M4 of the previous review, which was aimed at the wrong case.
`StateController.Create` carries `[Consumes("application/json", "text/json")]`, so a `text/plain` body is now rejected with `415 Unsupported Media Type` rather than the predicted 500.

A request with no `Content-Type` header at all behaves differently, because `ConsumesAttribute` does not reject a missing content type:

```bash
curl -u admin:admin123 -X POST http://localhost:5293/dummy/state/x \
  -H "Content-Type:" --data-binary '{"version":4}'
```

The body is bound by `RawRequestBodyFormatter` as a `string`, `JsonSerializer.Serialize` then double-encodes it into a quoted JSON string, and `BsonDocument.Parse` throws.
The response is `500`.

The finding has a second half that matters more than the bug.
Because `[Consumes]` restricts the state endpoints to JSON, `RawRequestBodyFormatter` is unreachable for every case it was written to serve, so it is effectively dead code that still creates one live failure path.
The cheapest correct fix is to delete the formatter and let a missing content type fall through to the JSON formatter.
Keeping it requires widening `[Consumes]` and handling the string case explicitly, which is more work for no known caller.
The exact content-type comparison in `CanRead` is a real defect, since `text/plain; charset=utf-8` is not matched, but it is moot if the formatter is removed.

### M2. `created_at` in `tf_state` is overwritten on every update

**Proven.**
`StateRepository.CreateAsync` always writes `created_at = DateTime.UtcNow`, so the field records the last update and the original creation time is lost.
After creating a state and updating it a second later, the stored `created_at` is later than the `created_at` of the history entry generated by that same update.
Keep the existing `created_at` on update and add a separate `updated_at` field.
(Renamed from `createdAt` on 2026-09-01; the semantics described here are unchanged by that rename.)

### M3. Every state POST reads and re-parses the entire existing state

**Observed.**
`CreateAsync` fetches the whole existing document, calls `ToJson()` on it, computes a JSON Patch against the new state, and then performs two writes.
For a state anywhere near the ceiling described in H1 that is tens of megabytes of allocation, a full JSON parse, and a full structural diff on every single `terraform apply`.
The cost is paid whether or not anyone ever reads the history.

Making the history capture opt-in per tenant, or computing the diff outside the request path, would remove a large constant cost from the hot path.
This should be settled before B-15 and B-16 build more features on top of the current shape.

### M4. The state write and the history write are not atomic

**Observed.**
`CreateAsync` inserts the history entry and then replaces the state document, as two independent operations with no transaction and no compensating action.
A failure between them leaves a history entry describing a state transition that never happened.

This is worth recording now because the constraint that used to block it is gone: `compose.yaml` already starts MongoDB as a replica set (`rs0`), so multi-document transactions are available.
CI still installs a standalone `mongod`, which cannot run transactions, so aligning the CI database with the compose topology is a prerequisite for fixing this, and for B-15 generally.

### M5. BCrypt verification on every request, with no throttling

**Superseded.**
Promoted to the [security](#security) section and split into S1, S2 and S3, where each half is now proven with measurements rather than estimated.

### M6. Cancellation tokens are not propagated

**Observed.**
Controller actions and repository methods do not accept or forward `CancellationToken`, so aborted Terraform requests keep running their MongoDB queries.
Low effort to add, since `HttpContext.RequestAborted` flows naturally through the driver's async APIs.

### M7. The failed-attempt lockout and credential cache are scoped to one replica

**Fixed on 2026-09-01** for the lockout half, covered by `LockoutRepositoryTest`, `AuthenticationLockoutTest` and `AuthenticationTimingTest`.
The failed-attempt counter now lives in MongoDB, in `auth_lockout`, through `ILockoutRepository`, rather than in the process: `RecordFailureAsync` increments or starts a window through a single aggregation-pipeline update, evaluated atomically against one document, so the counter is correct under concurrent failures and holds across every replica of the deployment.
A document expires through a TTL index on `expires_at`, and the read path filters on the same field independently, so a document the TTL monitor has not yet swept still reads as expired rather than as still counting.
Field names in `auth_lockout` are snake_case (`remote_address`, `expires_at`), an explicit override of the app's own camelCase convention, matching `user.password_hash` and the Terraform state's own field names in `tf_state.value` rather than this app's camelCase envelope fields around it.

The credential cache is deliberately left in `IMemoryCache`, unchanged.
It is a performance optimisation, not a security control: a cache miss on a different pod costs a repeat BCrypt verify, not a weakened lockout, so moving it would add a MongoDB round trip to the hot path S2 exists to remove for no security benefit.

The finding as originally observed follows.

**Observed.**
`ThrottledCredentialAuthenticator` (S1, S2) was backed by a singleton `IMemoryCache`, registered in-process.
Behind a load balancer with more than one replica, each pod held its own failure counter, so the effective lockout budget was `MaxFailedAttempts` per pod rather than per deployment.

Neither was a defect in what shipped.
The "where each control belongs" table above already assigns per-IP rate limiting to the platform for exactly this reason, one counter across every replica that survives a restart, an in-process limiter cannot offer that.
The gap was that S1 did not state the same boundary, and nothing in `setup.md` or the backlog said that scaling this deployment past one replica needed a shared store.

### M8. `BCrypt.Verify` runs synchronously inside an async method

**Observed.**
`UserRepository.CheckAuthentication` calls `BCrypt.Net.BCrypt.Verify` directly, with no offload, inside a method awaited from the async request pipeline.
At roughly 139 ms of CPU per verify (S2), every cold-cache request occupies a thread-pool worker for that duration: the thread that resumes after the MongoDB lookup runs the verify to completion before the method returns.

Wrapping the call in `Task.Run` would not help.
The work is CPU-bound, so `Task.Run` still queues it onto the same shared thread pool; it moves which thread pays the cost without reducing how much thread-pool time the deployment spends on it, which is the well-known reason `Task.Run` is discouraged for CPU-bound work in ASP.NET Core.
A real fix, if this ever needs one, is to bound concurrent verifies with a `SemaphoreSlim` so a burst cannot occupy the whole pool at once, not to relocate the work.

No action is recommended today.
This is the same cost the review already calibrated for S2, "nothing was saturated" on a 22-core development machine with 100 concurrent failed authentications, and that calibration covers this finding too.

## Low severity

### L1. The route name constraint is unanchored

**Proven.**
`{name:regex([[a-zA-Z]]+)}` requires one ASCII letter somewhere in the value rather than constraining the whole value.
The two cases are distinguishable by the response body, since an unmatched route returns an empty 404 and a matched route returns a problem detail:

```bash
curl -u admin:admin123 http://localhost:5293/dummy/state/12345        # 404, empty body: route rejected
curl -u admin:admin123 http://localhost:5293/dummy/state/123-abc-456  # 404, problem+json: route matched
```

Either anchor and widen it, for example `^[a-zA-Z][a-zA-Z0-9._-]*$`, or remove it and validate explicitly.
Anchoring it as it stands would break the scenario tests, which use names of the form `local-files-<guid>`, so widening is required rather than optional.

### L2. The authentication handler path workaround is redundant

**Proven.**
The `/scalar/` and `/openapi/` prefix check in `BasicAuthenticationHandler` duplicates what `AllowAnonymous` on those endpoints already achieves.
An anonymous `GET /scalar` returns 200 even though the check does not match the path, which demonstrates that `AllowAnonymous` is what actually grants access.
The check can be deleted, and `ScalarResourceTest` already covers the behaviour that must be preserved.

### L3. History entries are hard to consume

**Proven.**
`tf_state_history.upgrade` stores the JSON Patch as a string rather than a BSON document, for example `'[{"op":"replace","path":"/serial","value":2}]'`, and the field name does not convey "patch" or "diff".
Only the forward diff is kept, so reconstructing an older version requires replaying patches in the right direction with no API support.
This matters mostly for the planned history and revision features (see the [backlog](backlog.md)).

### L4. Version metadata is duplicated

**Observed.**
`OpenApi.Version` (`v1.2` in `appsettings.json`) must be kept in sync with `VersionPrefix` (`1.2.3` in `Directory.Build.props`) by hand.
Consider injecting the assembly version into the OpenAPI document at startup.

### L5. Unbounded growth and stale data

**Observed.**
`tf_state_history` has no TTL or retention policy, and locks never expire, so a crashed run requires a manual `terraform force-unlock`.
Both deserve at least documentation, and optionally a TTL index or a cleanup command in `tfbeadm`.

### L6. The MongoDB health check has no bounded timeout

**Observed.**
`MongoDbHealthCheck` forwards only the caller's cancellation token, and `AddCheck<MongoDbHealthCheck>("mongodb")` is registered without a timeout.
With default driver settings, server selection waits 30 seconds, so `/health` can hold a request open that long while MongoDB is unreachable.
The probe fails either way, so the impact is confined to resource use during an outage, which is when spare capacity matters most.
Registering the check with an explicit timeout bounds it in one line.

### L7. `StateModel` does not describe what is stored in `tf_state`

**Partially fixed on 2026-09-01.**
`StateRepository` and `StateHistoryRepository` now write `created_at`, matching the mapping `StateModel.CreatedAt` already had, so that half of the drift is closed.
`tfbeadm migrate-created-at` renames the field on documents written before the change; see [setup.md](setup.md#upgrading-from-before-the-created_at-rename).
`StateHistoryTest` asserts the new field name directly on both collections, and `MigrateCreatedAtTest` covers the migration command itself, including that running it twice does not disturb an already-migrated document.

The nested `StateValueModel` still does not correspond to the shape of a real Terraform state, which is the finding as originally observed.

**Observed.**
The nested `StateValueModel` does not correspond to the shape of a real Terraform state.
Nothing breaks today, because the model is only used by the test fakers, but it is a trap for the history and revision APIs in B-15 and B-16, which will be tempted to deserialise `tf_state` into it.
Either align the model with what is stored or delete it and move the faker to a test-local type.

### L8. The `Lock` endpoint can answer 423 where the protocol expects 409

**Observed.**
`StateController.Lock` returns `423` with a `{"message": ...}` body when a lock exists and the request body carries no ID, whereas every other lock conflict returns `409` with the existing lock as the body.
Terraform always sends an ID on lock, so the branch is not reachable through the CLI and the practical impact is nil.
It is noted only because the inconsistency is easy to mistake for intent when reading the controller.

### L9. The MongoDB connection pool uses driver defaults

**Fixed on 2026-08-31.**
[setup.md](setup.md#database-server) now states the driver default of 100, and that raising it needs no code change: `maxPoolSize` is a standard connection string option, for example `mongodb://<host>/<db>?maxPoolSize=200`.

The finding as originally observed follows.

**Observed.**
`InfrastructureServiceCollectionExtensions` constructs `MongoClient(configuration.ConnectionString)` with no `MongoClientSettings`, so the connection pool size is the driver default of 100.
Fine at today's scale, and invisible today: nothing sets it, and nothing documents it.

## Test coverage

The integration suite covers the happy paths well: state create, read and delete, the full lock lifecycle including 423 and 409 responses, wrong-tenant 401, malformed `Authorization` headers, health, an OpenAPI snapshot, a Scalar feature-flag override, and a real `terraform init/plan/apply` scenario.
Three of the gaps listed in the previous review have been closed.

### T1. The test suite fails confusingly against a drifted local database

**Proven.**
`dotnet test` requires a MongoDB seeded by hand with an `admin` user on tenant `dummy`, and those values are hardcoded in `IntegrationTestBase`.
A first run of the suite for this review failed 6 of 13 tests, because the local `tfbackend_dev` database had accumulated an `admin` user belonging to a different tenant from unrelated work.
The reported symptom was an authentication failure, which points nowhere near the actual cause.
Re-running against a freshly seeded database passed 13 of 13.

Any long-lived development database drifts this way, and the failure mode does not distinguish "the code is broken" from "the database is stale".
That makes B-24, provisioning MongoDB through Testcontainers or compose, more valuable than its P3 ranking suggests, and it is raised to P2 in the backlog.

The drift recurred within a day.
On 2026-08-12 the local `tfbackend_dev` database held four users, and `admin` belonged to tenant `platform-eng` rather than to `dummy`, so every test that authenticates as `admin:admin123` on tenant `dummy` fails with a `401` that says nothing about the cause.
A suite failing on authentication is therefore a reason to reseed before it is a reason to read the code, and the recurrence raises B-24 to P1.

### T2. Nothing asserts what comes back out of storage

**Fixed on 2026-08-12**, covered by `StateFidelityTest`, and extended on 2026-08-27 by `ComplexStateScenarioTest`.
The round trip is now asserted, and the payload now varies in both senses described below: `StateFidelityTest` drives a table of constructed payloads that includes the numeric edge cases and an oversized document, and `ComplexStateScenarioTest` drives a real Terraform-produced state whose value classes a hand-built payload cannot reproduce, nested maps, a list of objects, and an integer beyond `Int64`, then queries it back out of MongoDB by resource attribute rather than only reading it back through the API.

The finding as originally observed follows.

**Observed.**
The suite does exercise the storage layer with a real state.
The scenario test drives `terraform apply` over three real resources, reads the state back, applies a second time and destroys, so a genuine Terraform state does make the full round trip.
Two narrower gaps are what let H1 and H2 through.

The round trip is never asserted.
In `StateResource_CreateFindDelete_IsSuccess` the GET is checked for status and content type, but `expectedContent` is left unset, so the returned body is never compared to the body that was posted.
The scenario test asserts Terraform's own output, which proves that Terraform tolerated the state rather than that the state was preserved.
Terraform re-parses the JSON it receives, so a normalisation such as `1e3` becoming `1000.0` is invisible to it.

The payload never varies.
`samples/local-files` produces strings and small integers, because that is what a `local_file`, a `null_resource` and a `random_string` contain, and `StateFaker` is declared with no rules so it contributes a default-constructed object.
The value classes that break cannot appear.

The fix reuses what already exists: assert that the GET body equals the posted state, then drive that same test from a table of payloads that includes the numeric edge cases and an oversized document.

### T3. `IntegrationTestBase` mutates process-wide environment variables

**Fixed on 2026-08-12**, in `IntegrationTestBase.CreateClient`.
The Scalar feature flag is now applied by `TestHostConfiguration` through `UseSetting`, which is scoped to one factory rather than to the whole process.

The finding as originally observed follows.

**Observed.**
`CreateClient` calls `Environment.SetEnvironmentVariable("Features__IsScalarEnabled", "true")` on every invocation.
That value leaks across tests within the process and is never reset, so the suite depends on the fact that no test currently needs the opposite value from the environment.
`ScalarResourceTest` already demonstrates the cleaner mechanism, `UseSetting` on the host builder, which is scoped to one factory.

### T4. The actual coverage number, read directly rather than through Sonar

**Observed.**
CI already collects coverage: `dotnet-test-args` has carried `--coverage --coverage-output-format cobertura` since Sonar was enabled, and the reusable `build-test-sonar` action converts the result with ReportGenerator into `SonarQube.xml`, feeds it to the scanner via `sonar.coverageReportPaths`, and archives the HTML and cobertura reports as a workflow artifact.
The number has therefore been visible on the Sonar dashboard the whole time; a first draft of this finding claimed otherwise and was wrong.

What had not been done before is reading the number directly, without opening Sonar.
Running the same collector locally on 2026-08-31 measured 43.4% line coverage on `src/` (903/2081 lines), with `StateController.cs` (44.5%), `BasicAuthenticationHandler.cs` (45.8%) and `Program.cs` (46.2%, mostly the untested `IsScalarEnabled: false` and `IsHttpsRedirectionEnabled: false` branches) the thinnest, and the H1/H2 numeric converters the best covered at 88.7% and 95.5%.

### Remaining gaps

- No test for a state POST without a `Content-Type` header (would have caught M1).
  `RawRequestBodyFormatterTest`, added 2026-08-27, pins the formatter's own behavior for that case in isolation, and confirms a real `terraform apply` never sends it, but the controller-level 500 itself is still unasserted end to end, since deleting the formatter (B-06) is a decision still open.
- The diff logic in `StateRepository` is still not tested in isolation.
  A `WebApi.UnitTests` project exists now, added 2026-08-27, and covers the H1/H2 numeric conversion at the unit level, but `GenerateJsonDiff` is private and reached today only through the integration and scenario suites.

## CI/CD and operations

- The CI pipeline installs MongoDB and Terraform inline via `custom-commands`, and installs a standalone `mongod`, whereas `compose.yaml` runs a replica set.
  That divergence blocks any fix that needs a transaction from being tested in CI (see M4), and it is the main reason to prefer a `services:` container or the existing compose file.
- The Dockerfile is solid: SUSE BCI base images, a non-root user, checksum-verified OpenTelemetry auto-instrumentation, and no secrets baked in.
- `tfbeadm` is a pragmatic admin tool.
  It depends on `htpasswd` producing a `$2y$` BCrypt prefix, which `BCrypt.Net-Next` accepts.
  User creation interpolates values into a `mongosh` command through `printf %q`, which survives the double shell hop by relying on JavaScript treating `\$` as `$`.
  It works, but it works by coincidence rather than by design, and a password containing a quote would break it.
- The `demo` and `pkg` workflows were not reviewed in depth.

## Strengths worth preserving

- Strict protocol fidelity in `StateController`, documented in `AGENTS.md` so that future changes check the spec first.
- Scenario tests that exercise the real Terraform CLI end to end, which is what gives the NuGet verification above its weight.
- Central package management with transitive pinning, and a zero-CVE budget on image scans.
- Global MongoDB conventions (camelCase, string enums) registered in one place.
- The previous review's high-severity fixes were implemented cleanly, and `StateLockRepository.CreateAsync` in particular is a textbook insert-first duplicate-key mapping.

## Status of the previous review

Every finding of the 2026-07-10 review, re-checked against commit `d380eaa`.

Previous | Verdict | Now
-------- | ------------- | ---
H1 Health check does not verify MongoDB connectivity | Fixed | Closed, see L6 for a residual detail
H2 `Application__*` does not match `Features:*` | Fixed | Closed, verified in `compose.yaml` and CI
H3 Lock acquisition is not atomic | Fixed | Closed, though still untested under a real race
M1 State JSON round-trips through Extended JSON | **Escalated** | H2, proven to corrupt state and to return 500
M2 Malformed `Authorization` header returns 500 | Fixed | Closed, covered by a test
M3 `createdAt` is overwritten on every update | Confirmed | M2, renamed to `created_at` on 2026-09-01
M4 Raw `text/plain` state POST likely fails with 500 | **Corrected** | M1, `text/plain` returns 415; the real hole is a missing `Content-Type`
M5 BCrypt on every request, no throttling | **Escalated** | S1, S2 and S3, all three now proven by measurement
M6 Cancellation tokens are not propagated | Confirmed | M6
L1 Route name constraint is unanchored | Confirmed | L1, proven
L2 Authentication handler path workaround is redundant | Confirmed | L2, proven
L3 History entries are hard to consume | Confirmed | L3
L4 Version metadata is duplicated | Confirmed | L4
L5 Unbounded growth and stale data | Confirmed | L5

One claim from the previous review did not survive contact with the code.
The concern that `$`-prefixed and dotted keys might not round-trip is withdrawn: MongoDB 8.2 accepts both and returns them unchanged.
