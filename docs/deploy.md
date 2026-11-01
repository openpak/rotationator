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

`/data` holds `last.byaml` (the previous schedule) and an optional `overrides.json`
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

## Status on production (2026-11-01)

Image pulled, quadlet `openpak-rotationator.container` (IP 10.89.2.225) and
`openpak-rotationator.timer` (weekly, Mondays 03:00 UTC) installed from `deploy/`, timer
**not enabled**. A manual run exits 3 ("no last.byaml and no keys"), which is the expected
state until the BOSS keys exist. Also, nn-boss returns 404 for the Splatoon `schdat2` and
`optdat2` tasksheets, so check those are served before trusting an upload.

To go live:

1. Set `PN_BOSS_CONFIG_BOSS_WIIU_AES_KEY` and `PN_BOSS_CONFIG_BOSS_WIIU_HMAC_KEY` in
   `~/openpak/deploy/.env` (console-dumped, md5-checked by nn-boss) and restart `openpak-nn-boss`.
2. Seed the first schedule: either let the first run decrypt the served file with those keys,
   or copy a plaintext `VSSetting.byaml` to the `openpak-rotationator-data` volume as `last.byaml`.
3. `systemctl --user start openpak-rotationator.service`, check the log shows three data IDs,
   then `systemctl --user enable --now openpak-rotationator.timer`.
