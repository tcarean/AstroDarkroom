# 0005. The suite's zip is shared from Google Drive, linked with its hash in the README

Date: 2026-10-25
Status: superseded by 0015; the row's commit of README.md alone superseded by 0008

## Context

The suite's plan (the kit's `docs/plans/2026-10-24-build-the-astro-darkroom-suite.md`) and the
kit's ADR 0036 had the zip and its `.sha256` uploaded by hand to the repository's Bitbucket
Downloads. Atlassian removed Downloads from Bitbucket Cloud's Free plan, enforced from
2025-05-29: "Free workspaces will no longer be able to upload and download additional files"
(the announcement "Important changes coming to Bitbucket Cloud's Free plan",
<https://www.atlassian.com/blog/bitbucket/important-changes-coming-to-bitbucket-clouds-free-plan>).
The same change caps a Free workspace at 1 GB of repository storage in all. The `photo`
repository is `tcarean/photo` on Bitbucket. Downloads takes uploaded files only, never a link.
The zip of the measured 0.1.0 publish is 65.4 MB (ADR 0002). On 2026-10-25 the owner chose to
put the zip in their own Google Drive, shared, with the link and the hash in a README.

## Decision

Each release's zip, `AstroDarkroom-X.Y.Z-win-x64.zip`, is uploaded by the owner, by hand, to
their Google Drive and shared as "Anyone with the link". `README.md` of this project carries a
"Download" section with one table row per version, newest first: the version, the release
date, the share link and the SHA-256 from the zip's `.sha256`, beside the `certutil` command
that checks it. The row is added after the upload, in a commit of `README.md` alone that the
owner makes on `main` like the release commit; `/release` proposes the row (`docs/skills.md`).
Nothing in the repository uploads the zip or holds Drive credentials.

## Alternatives rejected

- **Bitbucket Downloads.** Gone from the Free plan since 2025-05-29; on a paid plan it would
  work, but the owner chose not to depend on the plan for a download.
- **GitHub Releases.** Files up to 2 GB per asset, versioned with a tag, but a second hosting
  account and a copy of the repository or of the tag there for one zip.
- **The zip committed into the repository.** About 65 MB of binary history per release,
  against the Free workspace's 1 GB in all and the 2 GB a repository should stay under.
- **A README at the repository root.** It would show on the Bitbucket front page, but it is a
  file of the whole monorepo, outside every kit project and its gate; the owner may add one
  that points to this section.

## Consequences

The link works as long as the file stays in the owner's Drive with link sharing on; a deleted
or unshared file breaks its row, and the hash still tells a reader which zip the row meant.
The hash is copied from the `.sha256` the release writes, never retyped. A release is two
commits on `main`: the release commit with `Directory.Build.props` and `CHANGELOG.md`, then the
README row once the link exists. The kit's ADR 0036 keeps the zip and its hash; only where the
zip goes changed.
