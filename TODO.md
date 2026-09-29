# Running IstarCI on Terraform Backend MongoDB

IstarCI runs this repository's existing CI pipeline locally, in Docker, on every commit, and blocks `git push` when it fails.
The workflow files are read as written: nothing here has to be rewritten or annotated for it.

This file says what runs today, what does not yet, and what to do about each.
It was written from parsing `.github/workflows/ci.yaml` on 2026-09-13 and reading what it became, so the job names and step counts below are what IstarCI would actually run.

The tool itself lives in `devpro/istarci`, and its own list of what is missing is in `docs/backlog.md` there.

## Quickstart

### Requirements

- Docker, running, and reachable by the current user without `sudo`.
- `bash`, `git`, `curl` and `jq`.
- A clone of IstarCI, since it is not published to the public registry: `git clone https://github.com/devpro/istarci`.
- Node.js 22 or later and pnpm 9 or later, for the daemon only.
  The one-off check below needs none of that: it builds and runs the daemon in a container.
- Network access on the first run of a pipeline.
  Reusable workflows and composite actions are fetched once and kept in `~/.istarci/workflow-cache`, and every run after that reads them from disk.

### Check this repository once, without installing anything

From the IstarCI clone:

```bash
scripts/check_repo.sh /mnt/c/Users/BertrandThomas/Projects/terraform-backend-mongodb --workflow .github/workflows/ci.yaml --image mcr.microsoft.com/dotnet/sdk:10.0
```

The repository is mounted read only.
It is never checked out, never committed to and never written to: the commit under test is extracted into a throwaway workspace instead, so this is safe to run against a working tree with uncommitted changes.

Useful options:

```bash
--workflow .github/workflows/ci.yaml   # one workflow instead of every file discovered
--sha <commit>                         # a commit other than HEAD
--job-image '<pattern>=<image>'        # an image for the jobs a pattern names, repeatable
--timeout 1800                         # longer than the 900 second default, for a pipeline that builds
--keep                                 # leave the daemon container up afterwards, to read its log
```

### Run the daemon, and gate `git push` on the result

From the IstarCI clone:

```bash
pnpm install
pnpm build
node packages/cli/dist/cli.js daemon install
node packages/cli/dist/cli.js daemon start
node packages/cli/dist/cli.js daemon ping
```

Then, for this repository:

```bash
node packages/cli/dist/cli.js add /mnt/c/Users/BertrandThomas/Projects/terraform-backend-mongodb
node packages/cli/dist/cli.js install-hook /mnt/c/Users/BertrandThomas/Projects/terraform-backend-mongodb
```

From then on every commit runs the pipeline in the background, and a push is blocked when the run for the commit being pushed failed.

```bash
node packages/cli/dist/cli.js status      # what ran, and how it ended
node packages/cli/dist/cli.js logs        # the output of the last run
node packages/cli/dist/cli.js check       # the decision the pre-push hook makes
node packages/cli/dist/cli.js list        # the repositories being watched
```

The dashboard is at `http://127.0.0.1:7842`.

## What runs today

Four jobs are read out of `ci.yaml`, three of them from reusable workflows in `devpro/github-workflow-parts` that IstarCI fetches and inlines, along with the composite actions inside them that hold the actual commands.

Job                            | Steps | Needs                                      | State
-------------------------------|-------|--------------------------------------------|--------------------------
`git-check__git-check`         | 5     | `git`, the version out of `Directory.Build.props` | Runs, and its change detection was verified: on a docs only commit the two expensive jobs are correctly skipped
`markup-lint__markup-lint`     | 2     | `npx markdownlint-cli2`, `pipx run yamllint` | Runs, but see the byte order mark below
`code-quality__dotnet-quality` | 19    | .NET SDK 10, Terraform, MongoDB, Java for Sonar | Runs on the .NET SDK image, as far as the parts named below
`image-scan__container-scan`   | 12    | `docker build`, Trivy                       | Blocked, see below

## What is blocked, and what to do

### The quality job installs Terraform and MongoDB with `sudo apt`

The `custom-commands` input of the reusable workflow adds an apt repository, installs Terraform, then installs and starts MongoDB 8.2 through `systemctl`.
That is written for a virtual machine: a container has no `systemd`, usually no `sudo`, and the installation runs again on every single run.

Two ways round it, and the second is the better one:

- Use a runner image carrying Terraform, `ghcr.io/devpro/ubuntu-dotnet`, and it is installed once rather than every run.
- Give MongoDB to the job as a service container rather than installing it, which is what a container pipeline does with a database.
  IstarCI already starts `services:` alongside a job on a network of their own, reachable by name, and waits on their health check.
  A `mongo:8` service reachable as `mongo` replaces the whole install and start block.

### The quality job also wants Java, for Sonar

`actions/setup-java` is skipped, a setup action assuming a runner image with toolchains already in it.
`ghcr.io/devpro/ubuntu-dotnet` carries a JRE next to the .NET SDK, and `SONAR_TOKEN` resolves to the placeholder `istarci-secret-SONAR_TOKEN`, so the Sonar steps fail where they use it unless Sonar is disabled locally through the `sonar-enabled` input the reusable workflow takes.

### The image scan job wants a Docker socket and `${{ env.IMAGE_REF }}`

Both are IstarCI gaps, items 1 and 3 of its backlog, and neither needs a change here.

## Suggested `.istarci.yml`

```yaml
runner:
  image: ghcr.io/devpro/ubuntu-dotnet:latest
  images:
    "markup-lint*": ghcr.io/devpro/debian-node:latest
    "git-check*": ghcr.io/devpro/debian-node:latest

workflow:
  exclude:
    - .github/workflows/pkg.yaml     # publishes on a push to main
    - .github/workflows/pages.yaml   # publishes the documentation site
    - .github/workflows/demo.yaml    # deploys, and holds credentials a local run does not
```

### This repository sits on a Windows drive

`/mnt/c` is a Windows filesystem mounted into WSL, which delivers no inotify event, so the watcher polls it rather than waiting for one.
That is selected automatically for a `/mnt/<letter>` path and needs no configuration.
Cloning a workspace across that boundary cannot use hard links, which IstarCI also detects, so a run is a little slower there than on a repository under the Linux home.

Running the pipeline from the WSL side is what was verified, including bind mounting the Windows path into a container and extracting a commit from it.

## Where to report

A fault in the pipeline as IstarCI reads it belongs in `devpro/istarci`, with the workflow file and the job that was read wrongly.
`docs/backlog.md` there already records the gaps named above, so an issue is worth opening for anything not on that list.
