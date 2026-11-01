# Deploy: rotationator

Image `ghcr.io/openpak/rotationator` on tag. Runs as a oneshot unit fired by a systemd timer.

| Variable | Default | Purpose |
| --- | --- | --- |
| `ROTATIONATOR_GRPC_APIKEY` | required | nn-boss management key (`PN_BOSS_CONFIG_GRPC_BOSS_SERVER_API_KEY`) |
| `ROTATIONATOR_GRPC_HOST` | `openpak-nn-boss:20081` | nn-boss gRPC |
| `ROTATIONATOR_APP_IDS` | the three Splatoon ids | comma list of BOSS app ids |
| `ROTATIONATOR_NPDI_URL` / `_HOST` | `http://openpak-nn-boss:20080` / `npdi.cdn.openpak.org` | only for the first-run download |
| `ROTATIONATOR_WIIU_AES_KEY` / `_HMAC_KEY` | unset | BOSS keys, only to decrypt the served file on the first run |
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
