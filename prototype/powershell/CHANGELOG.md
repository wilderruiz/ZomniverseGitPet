# Changelog

## 0.1.2 - responsive dashboard

- Split read and write Git backends.
- Prefer Windows Git for fast read-only monitoring; fall back to WSL.
- Preserve WSL for staging/commit operations by default.
- Combine branch and file-status discovery.
- Avoid full dashboard refreshes when the working-tree fingerprint has not changed.
- Add immediate busy feedback for slower actions.
- Add automatic config migration from v0.1.1.
- Keep the protected baseline unchanged.

