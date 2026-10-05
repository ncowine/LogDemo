#!/bin/sh
# =============================================================================
#  One-shot: puts the LogDemo dashboard and alert rules into the stack's Grafana
# =============================================================================
# Uses Grafana's HTTP API instead of provisioning files, so nothing in the stack's
# own grafana/provisioning folder has to change. Safe to re-run: the folder is
# created once, the dashboard and the rule group are overwritten with this copy.
#
# Environment (from ../.env via docker-compose.yml):
#   GRAFANA_URL       default http://grafana:3000 (service name on the stack network)
#   GRAFANA_USER      default admin
#   GRAFANA_PASSWORD  empty = no auth (the dev stack's anonymous admin)
# =============================================================================
set -eu

GRAFANA_URL="${GRAFANA_URL:-http://grafana:3000}"
DIR="$(cd "$(dirname "$0")" && pwd)"
FOLDER_UID=logdemo
RULE_GROUP=logdemo-desktop

api() {
  method=$1
  path=$2
  shift 2
  if [ -n "${GRAFANA_PASSWORD:-}" ]; then
    set -- -u "${GRAFANA_USER:-admin}:${GRAFANA_PASSWORD}" "$@"
  fi
  # On failure, show Grafana's answer: callers discard stdout, and a bare "error: 400" helps nobody.
  if ! response="$(curl -sS --fail-with-body -X "$method" "$GRAFANA_URL$path" -H "Content-Type: application/json" "$@")"; then
    echo "  $method $path failed: $response" >&2
    return 1
  fi
  printf '%s' "$response"
}

echo "- waiting for $GRAFANA_URL"
i=0
until curl -sf "$GRAFANA_URL/api/health" >/dev/null; do
  i=$((i + 1))
  if [ "$i" -ge 60 ]; then
    echo "Grafana did not answer within 2 minutes. Is STACK_NETWORK right and the stack running?" >&2
    exit 1
  fi
  sleep 2
done

echo "- folder LogDemo"
if ! api GET "/api/folders/$FOLDER_UID" >/dev/null 2>&1; then
  api POST /api/folders -d "{\"uid\":\"$FOLDER_UID\",\"title\":\"LogDemo\"}" >/dev/null
fi

echo "- dashboard LogDemo — Desktop Apps"
{
  printf '{"folderUid":"%s","overwrite":true,"message":"provision.sh","dashboard":' "$FOLDER_UID"
  cat "$DIR/dashboard-logdemo-desktop.json"
  printf '}'
} | api POST /api/dashboards/db --data-binary @- >/dev/null

# One file per rule in alert-rules/, named after the rule's uid. X-Disable-Provenance keeps the
# rules editable in the UI; edits there are overwritten on the next run, so change the files.
echo "- alert rules ($RULE_GROUP)"

# The group PUT below only updates rules that already exist (Grafana 11.5 answers 500 for an
# unknown uid), so a rule that is new gets created on its own first.
for rule in "$DIR"/alert-rules/*.json; do
  uid="$(basename "$rule" .json)"
  if ! api GET "/api/v1/provisioning/alert-rules/$uid" >/dev/null 2>&1; then
    api POST /api/v1/provisioning/alert-rules -H "X-Disable-Provenance: true" --data-binary "@$rule" >/dev/null
    echo "  created $uid"
  fi
done

# Then the whole group in one go: sets the evaluation interval, updates every rule to its file,
# and removes rules from the group whose file was deleted.
{
  printf '{"title":"%s","folderUid":"%s","interval":60,"rules":[' "$RULE_GROUP" "$FOLDER_UID"
  separator=""
  for rule in "$DIR"/alert-rules/*.json; do
    printf '%s' "$separator"
    cat "$rule"
    separator=","
  done
  printf ']}'
} | api PUT "/api/v1/provisioning/folder/$FOLDER_UID/rule-groups/$RULE_GROUP" \
  -H "X-Disable-Provenance: true" --data-binary @- >/dev/null

echo "LogDemo dashboard and alert rules are in Grafana"
