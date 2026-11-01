# Rotationator

OpenPak's Splatoon 1 (Wii U) stage rotation generator.

| | |
| --- | --- |
| Network | Wii U |
| Role | Generates a fresh `VSSetting.byaml` on a schedule and uploads it to the BOSS server |
| Depends on | [`nn-boss`](https://github.com/openpak/nn-boss) (management gRPC) |
| Image | `ghcr.io/openpak/rotationator` |
| Based on | [Pretendo `Rotationator`](https://github.com/PretendoNetwork/Rotationator) by OatmealDome (MIT) |
| Licence | MIT |

## What it does

Each run takes the previous schedule, has Rotationator build a new one (4 hour phases,
30 days ahead, rebased to the current phase), and uploads it to nn-boss for the three Splatoon
BOSS app ids (`bb6tOEckvgZ50ciH`, `rjVlM7hUXPxmYQJh`, `zvGSM4kOrXpkKnpT`, task `schdat2`). That is the same
call as `boss file create <app> schdat2 --name VSSetting.byaml --type AppData --notify-new app`.
The new schedule is kept in `/data/last.byaml` for the next run.

## Run

See [Deploy](docs/deploy.md). Settings are `ROTATIONATOR_*` environment variables, listed there.

## Docs

- [Deploy](docs/deploy.md)
- [Upstream README](docs/upstream.md)

## Licence

MIT. See [LICENSE](LICENSE). Original work copyright OatmealDome.
