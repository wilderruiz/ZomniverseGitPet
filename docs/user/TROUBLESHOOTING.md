# Troubleshooting

This page covers failure modes GitPet is built to handle deliberately — situations it recognizes and explains, rather than crashing or guessing.

## "Dubious ownership" / GitPet asks to trust this folder

Git itself refuses to operate on a repository it considers to have "dubious ownership" — common on external drives, network drives, or cloud-synced folders (Dropbox, OneDrive) where file ownership metadata doesn't match the usual pattern. GitPet shows a plain-language dialog explaining this and, only with your explicit approval, adds **that exact folder path** to Git's trusted list. It never trusts every folder on your system at once. See [Filesystem and Sandbox](../safety/FILESYSTEM_AND_SANDBOX.md).

## Get turns into "Reconcile"

This isn't an error — it means your local history and the remote's history have both moved forward independently. See [Reconciliation](RECONCILIATION.md).

## Send is blocked for a logical project

If the project has a saved [Advanced Project Allow List](ALLOW_LISTS.md), GitPet compares it against what would actually be sent before every Send, and blocks the send if they don't match exactly. The dialog GitPet shows lists precisely which files are in the allow list but not in what's about to be sent, and vice versa. See [Project Boundaries](../safety/PROJECT_BOUNDARIES.md).

## GitPet asks about ignored files before Save

If some of your changed files are covered by a `.gitignore` rule, GitPet stops and asks which of them (if any) you actually want tracked this time — it never force-adds an ignored file without you explicitly ticking it.

## GitHub CLI isn't installed or isn't signed in

GitHub-connected actions (repository creation/discovery, some connection flows) depend on the [GitHub CLI](https://cli.github.com/). GitPet can install it for you and will tell you plainly when you're not signed in; local Git actions (Save, History, Tests, Health) keep working regardless.

## GitPet won't publish its own release

This only affects the maintainer-only "Publish application release" feature, and it's the safety mechanism working as intended: GitPet blocks publishing whenever the prepared release package doesn't match the exact branch and commit currently checked out, or when the working tree isn't clean, or local and origin aren't aligned. Rebuilding the release package after a final Save/Send resolves it. See [Build and Release](../developer/BUILD_AND_RELEASE.md).
