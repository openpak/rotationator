# Deploy: rotationator

Image `ghcr.io/openpak/rotationator` on tag. Runs as a oneshot unit fired by a systemd timer.

| Variable | Default | Purpose |
| --- | --- | --- |
| `ROTATIONATOR_GRPC_APIKEY` | `PN_BOSS_CONFIG_GRPC_BOSS_SERVER_API_KEY` | nn-boss management key; the shared deploy `.env` already has it |
| `ROTATIONATOR_GRPC_HOST` | `openpak-nn-boss:20081` | nn-boss gRPC |
| `ROTATIONATOR_APP_IDS` | the three Splatoon ids | comma list of BOSS app ids |
| `ROTATIONATOR_NPDI_URL` / `_HOST` | `http://openpak-nn-boss:20080` / `npdi.cdn.openpak.org` | only for the first-run download |
| `ROTATIONATOR_WIIU_AES_KEY` / `_HMAC_KEY` | `PN_BOSS_CONFIG_BOSS_WIIU_*` | BOSS keys, only to decrypt the served file on the first run |
| `ROTATIONATOR_PHASE_HOURS` / `_SCHEDULE_DAYS` | 4 / 30 | Rotationator options |
| `ROTATIONATOR_DRY_RUN` | unset | `1` generates but does not upload |

`/data` holds `schedule.json` (written after each successful upload; the game server reads it, see below), `last.byaml` (the previous schedule) and an optional `overrides.json`
(upstream phase override format).

## Prerequisites on the BOSS side

nn-boss encrypts every upload with the Wii U BOSS AES and HMAC keys
(`PN_BOSS_CONFIG_BOSS_WIIU_AES_KEY`, `..._HMAC_KEY`). They are console-dumped and not yet
set on production, so uploads are refused until they are. The served seed file is encrypted
too, so the first run needs either a plaintext `VSSetting.byaml` placed at `/data/last.byaml`
or the same keys in `ROTATIONATOR_WIIU_*`.

## Cadence

Pretendo's tool builds a 30 day schedule per run; the timer runs weekly, so the schedule
always has more than three weeks left. Rotationator throws if the last file has run out,
so do not let the timer lapse for more than 30 days.

## Going live

The quadlet `openpak-rotationator.container` and the weekly `openpak-rotationator.timer`
(Mondays 03:00 UTC) are installed from `deploy/`; enable the timer only after the steps below.
A manual run exits 3 ("no last.byaml and no keys") until the BOSS keys exist. Check that
nn-boss serves the Splatoon `schdat2` and `optdat2` tasksheets before trusting an upload.

Steps:

1. Set `PN_BOSS_CONFIG_BOSS_WIIU_AES_KEY` and `PN_BOSS_CONFIG_BOSS_WIIU_HMAC_KEY` in
   `~/openpak/deploy/.env` (console-dumped, md5-checked by nn-boss) and restart `openpak-nn-boss`.
2. Seed the first schedule: either let the first run decrypt the served file with those keys,
   or copy a plaintext `VSSetting.byaml` to the `openpak-rotationator-data` volume as `last.byaml`.
3. `systemctl --user start openpak-rotationator.service`, check the log shows three data IDs,
   then `systemctl --user enable --now openpak-rotationator.timer`.

## Game-server side of the rotation

`schedule.json` is the JSON twin of the uploaded `VSSetting.byaml` (`DateTime` base plus
`Phases[]` with `Time` hours, `RegularRule/Stages`, `GachiRule/Stages`). The Splatoon NEX container
(`openpak-splatoon`, nex image) mounts the volume read-only at `/rotation` and reads
`/rotation/schedule.json` (`SPLATOON_ROTATION_FILE` overrides). At every phase boundary it closes
participation on the regular and ranked lobbies that began in an earlier phase, so a lobby never carries
players across a rotation change. With no file the game server does nothing rotation-related, which is the
state today.

### Go-live, in order (waits on the console-dumped BOSS keys)

1. Set `PN_BOSS_CONFIG_BOSS_WIIU_AES_KEY` and `PN_BOSS_CONFIG_BOSS_WIIU_HMAC_KEY` in `~/openpak/deploy/.env`,
   restart `openpak-nn-boss`. Check `schdat2` and `optdat2` tasksheets answer 200 (nn-boss seeding).
2. Seed `/data/last.byaml`: place a plaintext `VSSetting.byaml`, or let the first run decrypt the served one.
3. `systemctl --user start openpak-rotationator.service`; the log shows three data IDs and
   `wrote /data/schedule.json`.
4. Add `Volume=openpak-rotationator-data:/rotation:ro` to the `openpak-splatoon` quadlet and restart it
   (`podman logs openpak-splatoon` then shows `Rotation: phase N began` within 15 seconds).
5. `systemctl --user enable --now openpak-rotationator.timer`.

Step 4 can be done before 1 to 3 (the file is simply absent); it is left for go-live so the splatoon
container is not restarted for nothing. The volume does not exist until the first run creates it.
