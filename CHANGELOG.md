# Changelog: rotationator

`git log` stays the source of truth; this file is the readable summary. The early history is
the upstream project (Pretendo, MIT); OpenPak work starts at the fork commit.

## v0.2.2 - 2026-11-03

- Docs only: production state, OpenSplat key variables.

## v0.2.1 - 2026-11-03

- Fix the first run from a file that ran out long ago (the last file Nintendo served ends in a 10 year phase):
  the base time is moved forward to the phase boundary containing now instead of the start of that phase, so
  the new schedule no longer starts in 2023. Docs: how the seed was obtained and the production state.

## v0.2.0 - 2026-11-01

- After a successful upload the run also writes `/data/schedule.json`, the JSON twin of the schedule, which the Splatoon
  game server reads to close lobbies at each rotation-phase boundary. Go-live steps in `docs/deploy.md`.

## v0.1.0 - 2026-11-01

- OpenPak fork: container image, Node uploader (nn-boss management gRPC, the `boss file create` call) for the three Splatoon BOSS apps, tag-driven ghcr release
