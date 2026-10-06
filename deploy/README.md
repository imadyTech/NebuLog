# Deploying NebuLog

Target: **AceMagic-W1** (`192.168.50.188`), compose project `imady-nebulog`, deployment directory
`/home/frank/imady.nebulog/`. The authoritative parameters are in
[`../docs/ops/ENV-REQ-001-reply.md`](../docs/ops/ENV-REQ-001-reply.md).

Nothing here needs `sudo`, and nothing here touches another compose project.

## One-time setup

1. Create the directory and place the two files:

   ```bash
   mkdir -p /home/frank/imady.nebulog
   # copy deploy/compose.yaml from the repository
   cp deploy/.env.example /home/frank/imady.nebulog/.env
   chmod 600 /home/frank/imady.nebulog/.env
   ```

2. Fill in `.env`. It is the only place real secrets exist; it is never committed and never printed.

   | Key | What it is |
   |---|---|
   | `NEBULOG_TAG` | The immutable image tag to run, e.g. `sha-1a2b3c4`. **Never `latest`.** |
   | `NebuLog__Admin__Email` | The administrator account, seeded once if absent |
   | `NebuLog__Admin__Password` | At least 12 characters, or the account is not created |
   | `NebuLog__DemoProducer__ApiKey` | A key in `nbl_<prefix8>_<secret32>` form; only its hash is stored |

3. The images live in GHCR. If the packages are private, authenticate once:

   ```bash
   docker login ghcr.io -u <github-user>
   ```

   Making the two packages public avoids this entirely, which is what ENV-REQ-001 §一.7 suggests.

## Deploy or upgrade

```bash
cd /home/frank/imady.nebulog
# set NEBULOG_TAG in .env to the new tag first
docker compose -p imady-nebulog pull
docker compose -p imady-nebulog up -d
```

Then check:

```bash
docker compose -p imady-nebulog ps
curl -fsS http://127.0.0.1:2000/health/ready
```

`ps` should show both services `running` and not restarting; `/health/ready` should report
`"status":"Healthy"` with the `ingest-queue` and `identity-database` checks present.

## Roll back

Set `NEBULOG_TAG` back to the previous tag and repeat the pull-and-up. Because every tag is
immutable, the previous tag is still exactly the image that was running before.

```bash
cd /home/frank/imady.nebulog
sed -i 's/^NEBULOG_TAG=.*/NEBULOG_TAG=<previous-tag>/' .env
docker compose -p imady-nebulog pull && docker compose -p imady-nebulog up -d
```

State survives a roll-back: the identity database and the Data Protection key ring live in the
`nebulog_data` volume, which no step here removes.

## What persists, and what does not

`nebulog_data` holds `nebulog.db` (accounts and API keys) and `keys/` (the Data Protection key
ring). Per ENV-REQ-001 §一.10 it is **not backed up**: the administrator is re-seeded from `.env`
on start-up and API keys can be reissued; losing the volume costs every signed-in session and any
keys whose clear text was not kept.

The key ring is the reason the volume matters beyond the database: without it, every restart would
invalidate all session cookies.

## Never do this here

From `EXECUTION.md` §5, and worth repeating because this host runs eight other compose projects:

- no `docker system prune`
- no `docker volume rm`
- nothing touching a compose project other than `imady-nebulog`
- no `sudo`
