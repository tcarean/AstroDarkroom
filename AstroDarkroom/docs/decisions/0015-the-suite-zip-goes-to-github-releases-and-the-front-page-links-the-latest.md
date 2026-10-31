# 0015. The suite's zip goes to GitHub Releases, and the front page links the latest

Date: 2026-10-31
Status: accepted 2026-10-31
Supersedes: 0005; 0008 in part (the latest zip's row and its commit)

## Context

On 2026-10-31 the `photo` repository moved from Bitbucket to GitHub: `origin` is
<https://github.com/tcarean/AstroDarkroom.git>, a public repository. ADR 0005 shared each
release's zip from the owner's Google Drive, its link and SHA-256 in a table of this project's
`README.md`, and rejected GitHub Releases as a second hosting account holding a copy of the
repository; ADR 0008 put the latest release's row at the top of the repository's root
`README.md`, the front page, and had `/release` propose the row for both READMEs, in a commit of
its own after the release commit. GitHub is now the repository's own host. A GitHub Release
attaches files to a tag, each "under 2 GiB" (GitHub's "About releases"), against a zip of 66 MB
(measured 2026-10-27, the root `README.md`); GitHub computes the SHA-256 of every uploaded file
and shows it beside the file (GitHub's changelog of 2025-06-03); `releases/latest` after a
repository's address leads to its latest release (GitHub's "Linking to releases"), and a newly
published release becomes the latest by default (`make_latest`, GitHub's REST documentation).
Checked on 2026-10-31: GitHub holds no tag and no release of the repository, both "Download"
tables say "(none yet)", and the GitHub CLI, `gh`, is not installed on the owner's computer. On
2026-10-31 the owner chose GitHub Releases, one link to `releases/latest` on the front page, and
the upload by hand on the web.

## Decision

- A release's zip, `AstroDarkroom-X.Y.Z-win-x64.zip`, and its `.sha256` are attached to the
  GitHub Release of the tag `astrodarkroom-vX.Y.Z`; the repository is public, so the download
  needs no account. The owner pushes the release commit and the tag, then creates the release
  from that tag on the web and uploads the two files by hand, with the title and the notes
  `/release` proposes: the version's section of `CHANGELOG.md` and the zip's `.sha256` line.
  `/release` then checks, through GitHub's public API, that the digest GitHub computed for the
  zip is the hash in the `.sha256` (`docs/skills.md`, step 7). Nothing in the repository uploads
  the zip or holds a GitHub token.
- The root `README.md`'s "Download" section keeps one link,
  <https://github.com/tcarean/AstroDarkroom/releases/latest>, how to start the suite, and the
  `certutil` check against the hash on the release page. Neither README lists the versions: the
  repository's Releases page lists every one.

## Alternatives rejected

- **Google Drive, as in ADR 0005.** Each release would add its link and hash to two READMEs in a
  commit on `main`, and a file deleted or unshared in the Drive breaks its row; the repository's
  host now keeps the zip beside its tag.
- **`/release` runs `gh release create`.** It would spare the owner the web form, but `gh` is
  not installed here and would need `gh auth login` and a token that writes to the repository,
  kept on this computer; it would also vary the kit's `/release` step 7, which leaves every
  upload to the owner, by hand.
- **The version tables kept, linking to GitHub.** Each release would still need its commit of
  the two READMEs, for a list the Releases page shows with every file and its digest.
- **A link straight to the zip, `releases/latest/download/<file>`.** The file's name carries the
  version, so no one address names the latest zip.

## Consequences

A release is one commit on `main`, the release commit: the READMEs name no version, so they do
not change at a release, and the front page is right as soon as the release is published.
"Latest" is a label GitHub moves: a GitHub Release of anything else in this repository, such as
a component's own zip, would take it by default and with it the front page's link (left
unselected, the form's "Set as the latest release" still lets GitHub assign the label by
semantic versioning), so a release other than the suite's needs a decision of its own first. The
tag is pushed before the release is made and picked in the form as it is: a new tag name typed
there is made by GitHub from a branch, not the owner's annotated tag. The hash a reader compares
is on the release page twice, GitHub's digest of the uploaded file and the `.sha256` line in the
notes, copied from the file the release wrote; `/release` compares GitHub's digest with the
`.sha256` right after the upload, so a wrong file is caught at once. The kit's ADR 0036 keeps
the zip and its hash; only where the zip goes changed, as with ADR 0005.
