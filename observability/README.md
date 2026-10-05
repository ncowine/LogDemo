# Desktop telemetry add-on

The desktop apps send their logs and traces to the existing Grafana stack (Loki, Tempo, Grafana), the
same one the API uses. That stack is defined and run from the CleanArchitecture repository's
`observability/` folder. **This folder never edits it.** It runs next to it on the same Docker host and
plugs in from outside:

```
  Windows desktops                          Docker host
  ┌──────────────────────┐                  ┌───────────────────────────────────────────────┐
  │ LogDemo-Net          │                  │  this project          the existing stack     │
  │ LogDemo-NetFx        │ ── OTLP/HTTP ──▶ │  otel-collector :4318 ──▶ loki:3100   (logs)  │
  │ (Telemetry:Endpoint) │                  │                       ──▶ tempo:4317  (traces)│
  └──────────────────────┘                  │  grafana-setup ─(once)─▶ grafana:3000         │
                                            └───────────────────────────────────────────────┘
```

| File | What it is |
|---|---|
| `docker-compose.yml` | the collector, plus a one-shot container that sets up Grafana. Joins the stack's Docker network |
| `otel-collector/otel-collector.yaml` | receives OTLP/HTTP on 4318, tags everything `service.namespace=desktop`, forwards to Loki and Tempo |
| `grafana/dashboard-logdemo-desktop.json` | the **LogDemo — Desktop Apps** dashboard |
| `grafana/alert-rules/*.json` | two alert rules, one file each, named after the rule's uid |
| `grafana/provision.sh` | puts the dashboard and rules into Grafana over its HTTP API. Safe to re-run |

## Why a collector, and not the apps straight to Loki

Loki and Tempo have no authentication, and Loki's port 3100 also answers **queries**. Opening it to the
desktop subnets would let any employee read every server log. So user machines get exactly one port,
4318 on the collector, and Loki and Tempo stay reachable only from the Docker network and the IIS
server, as they are today.

The collector is also the one place for the rules that must hold for every client version: the
`desktop` namespace, dropping `service.instance.id` (a Loki index label, see below), memory limits and
batching.

## What arrives

Every log record and span carries:

| | Where in Loki | Example |
|---|---|---|
| app, version | label `service_name`, metadata `service_version` | `LogDemo-Net`, `1.2.0` |
| desktop vs server | label `service_namespace` | `desktop` (set by the collector) |
| environment | label `deployment_environment` | `production` |
| who | metadata `user_name` | `CONTOSO\jsmith` |
| which machine | metadata `host_name` | `PC-042` |
| which app start | metadata `session_id` | `61bf1675` (also in the local log file header) |
| severity | metadata `severity_number`, `severity_text` | 17 = Error, 21 = Critical |
| exception | metadata `exception_type`, `exception_message`, `exception_stacktrace` | |
| command | metadata `CommandName`, `CommandId` | from the command scope |
| trace | metadata `trace_id`, `span_id` | opens the trace in Tempo |

Only the first three are **labels**. Everything per user, machine or session is structured metadata on
purpose: a label per user or per session would create a Loki stream for each. You can still filter on
metadata: `{service_namespace="desktop"} | user_name="CONTOSO\\jsmith"`.

Each command execution is a span with the user and session on it. Outgoing `HttpClient` calls become
child spans and carry `traceparent`, so a call into the API continues the same trace on the server.

## Development

The dev stack from the CleanArchitecture repository must be running (`docker compose up -d` in its
`observability/dev`). Then here:

```bash
cd observability
cp .env.example .env            # defaults fit the dev stack: cleanarch-dev_default, 127.0.0.1, no password
docker compose up -d
docker compose logs grafana-setup   # ends with "LogDemo dashboard and alert rules are in Grafana"
```

The apps' `appsettings.json` already points at `http://127.0.0.1:4318`. Start one, open **Diagnostics**,
and trigger an error. Within a few seconds it shows in Grafana (<http://localhost:3000>) → Dashboards →
LogDemo → **LogDemo — Desktop Apps**, and the *Desktop app error* rule fires within a minute.

## Production

### 1. Firewall: one port from the desktops

On the Docker host, next to the stack's existing rules:

```bash
sudo ufw allow from 10.20.40.0/22 to any port 4318 proto tcp   # desktop subnets -> collector
sudo ufw allow from 10.20.30.0/24 to any port 13133 proto tcp  # admins -> collector health check
```

Nothing else changes: 3100 and 4317 stay limited to the IIS server.

### 2. Start the add-on

```bash
rsync -av observability/ user@10.20.30.40:/opt/logdemo/observability/
cd /opt/logdemo/observability
cp .env.example .env && chmod 600 .env
nano .env   # STACK_NETWORK=cleanarch-prod_default, BIND_ADDR=<host LAN address>,
            # GRAFANA_PASSWORD=<the stack's GRAFANA_ADMIN_PASSWORD>
docker compose up -d
docker compose logs grafana-setup
```

### 3. Point the apps at it

In each app's `appsettings.json`, or machine-wide without touching the install (GPO, Intune):

```
LOGDEMO_Telemetry__Endpoint    = http://10.20.30.40:4318
LOGDEMO_Telemetry__Environment = production
```

Empty `Endpoint` = file only. What gets sent is set by `Logging:OpenTelemetry:LogLevel` (Information by
default). For one user, support can raise it to Debug in that user's `appsettings.user.json`. It is
hot-reloaded, so no restart is needed.

### 4. Verify, do not assume

| Check | Where |
|---|---|
| Collector up | `curl http://<host>:13133/` returns `{"status":"Server available"...}` |
| Collector forwarding | `docker compose logs otel-collector` has no `Exporting failed` |
| Logs arriving | Grafana → Explore → Loki → `{service_namespace="desktop"}` |
| Traces arriving | Grafana → Explore → Tempo → Search → Service name `LogDemo-Net` |
| Dashboard and rules | Dashboards → LogDemo, and Alerting → Alert rules → LogDemo |

## Alerts

| Rule | Fires when | Severity |
|---|---|---|
| **Desktop app crashed** | any Critical record in the last 5 minutes: startup failure, unhandled exception on a worker thread, error-loop shutdown | `critical` |
| **Desktop app error** | any Error record: a failed command, an exception the global handler caught while the app kept running | `warning` |

Both fire on the first occurrence (`for: 0s`), one alert per app, version, user, machine and exception
type, so the notification already says who hit what. The summary reads like *"LogDemo-Net 1.2.0
crashed for CONTOSO\jsmith on PC-042"*.

They reach nobody until the stack's Grafana has a contact point and notification policy (see the
stack's README, *Getting an alert to actually email you*). Their labels are `severity` and
`service=logdemo-desktop`, so a policy can route desktop alerts separately from the API's.

To change a rule, edit its file in `grafana/alert-rules/` and re-run `docker compose up grafana-setup`.
Edits made in the Grafana UI are overwritten on the next run.

## Limits worth knowing

- **Best effort, by design.** The apps batch every 2 s and give up after 2 s at shutdown. A collector
  outage loses that window, not the data: the local log file is always complete, and the
  `session_id` in the alert names the file to ask for.
- **Shared Loki ingest limit.** `ingestion_rate_mb: 8` in the stack's `loki.yaml` covers the API and
  all desktops together. Watch for `429` in `docker compose logs otel-collector` after rollout.
- **No client authentication.** The collector accepts OTLP from whatever the firewall allows. A header
  check can be added (`Telemetry:Headers` in the apps plus a collector auth extension), but a value
  shipped with a desktop app is readable by its users. It filters out accidental traffic; it is not a
  secret.
- **How this was tested.** The collector config was validated with `otelcol-contrib 0.162.0`, and the
  whole path (app → collector → Loki 3.4.2 / Tempo 2.7.1 → Grafana 11.5.1 dashboard and firing alerts)
  was run end to end with the native Windows binaries of those versions, not inside Docker.

## Troubleshooting

| Symptom | Cause |
|---|---|
| `network cleanarch-..._default declared as external, but could not be found` | the stack is not running, or `STACK_NETWORK` is wrong: `docker network ls` |
| `grafana-setup` waits, then exits 1 | same, or `GRAFANA_URL` is wrong |
| `grafana-setup`: `401` | `GRAFANA_PASSWORD` does not match the stack's admin password |
| Collector logs `Exporting failed ... 400` from Loki | `allow_structured_metadata: true` missing in `loki.yaml` |
| App logs nothing centrally | `Telemetry:Endpoint` empty or wrong. The local log's header says which endpoint it uses, or that export is off |
| Export is slow to give up when the collector is down | the endpoint says `localhost`. Use an IP or a real host name: on Windows `localhost` tries IPv6 first, and each refused connection costs about 2 s |
