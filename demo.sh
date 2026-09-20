#!/usr/bin/env bash
#
# End-to-end demonstration of the Keyloop Unified Service Scheduler.
#
#   ./demo.sh                     Run the flow against an already-running stack
#   ./demo.sh --reset             Recreate the stack (docker compose down -v, up --build -d), then run
#   ./demo.sh --no-concurrency    Skip the Tier 3 race suite at the end
#   ./demo.sh --help              Show this help
#
# Environment overrides:
#   BASE_URL         API base URL           (default http://localhost:5080)
#   DEMO_DATE        Booking date (future)  (default today + 30 days)
#   RUN_CONCURRENCY  1 to run the race suite (default 1)
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

BASE_URL="${BASE_URL:-http://localhost:5080}"
BASE_URL="${BASE_URL%/}"
DEMO_DATE="${DEMO_DATE:-$(date -u -d '+30 days' +%Y-%m-%d 2>/dev/null || date -u -v+30d +%Y-%m-%d)}"
START_UTC="${DEMO_DATE}T09:00:00Z"

DEALERSHIP="a0000000-0000-0000-0000-000000000001"
BAY1="b0000000-0000-0000-0000-000000000001"
TECH_A="c0000000-0000-0000-0000-000000000001"
OIL="d0000000-0000-0000-0000-000000000001"
EV="d0000000-0000-0000-0000-000000000003"
CUSTOMER_A="e0000000-0000-0000-0000-000000000001"
CUSTOMER_B="e0000000-0000-0000-0000-000000000002"

RUN_CONCURRENCY="${RUN_CONCURRENCY:-1}"

TMP_BODY="$(mktemp)"
TMP_TEST="$(mktemp)"
trap 'rm -f "$TMP_BODY" "$TMP_TEST"' EXIT

if [[ -t 1 && -z "${NO_COLOR:-}" ]]; then
  BOLD=$'\033[1m'; DIM=$'\033[2m'; RED=$'\033[31m'; GREEN=$'\033[32m'; CYAN=$'\033[36m'; RESET=$'\033[0m'
else
  BOLD=""; DIM=""; RED=""; GREEN=""; CYAN=""; RESET=""
fi

FAILURES=0

usage() {
  awk 'NR>1 && /^#/ { sub(/^# ?/, ""); print; next } NR>1 { exit }' "${BASH_SOURCE[0]}"
}

step() {
  printf '\n%s%s%s\n' "${BOLD}${CYAN}" "$1" "$RESET"
  printf '%s────────────────────────────────────────────────────────%s\n' "$DIM" "$RESET"
}

note() { printf '   %s%s%s\n' "$DIM" "$1" "$RESET"; }
pass() { printf '   %s✓ %s%s\n' "$GREEN" "$1" "$RESET"; }
fail() {
  printf '   %s✗ %s%s\n' "$RED" "$1" "$RESET"
  FAILURES=$((FAILURES + 1))
}

pretty() {
  if command -v jq >/dev/null 2>&1; then
    jq . "$TMP_BODY" 2>/dev/null || cat "$TMP_BODY"
  elif command -v python3 >/dev/null 2>&1; then
    python3 -m json.tool "$TMP_BODY" 2>/dev/null || cat "$TMP_BODY"
  else
    cat "$TMP_BODY"
  fi
}

json_field() {
  grep -o "\"$1\":\"[^\"]*\"" "$TMP_BODY" | head -1 | cut -d'"' -f4 || true
}

json_count() {
  if command -v jq >/dev/null 2>&1; then
    jq 'length' "$TMP_BODY" 2>/dev/null || printf '?'
  elif command -v python3 >/dev/null 2>&1; then
    python3 -c 'import json,sys; print(len(json.load(open(sys.argv[1]))))' "$TMP_BODY" 2>/dev/null || printf '?'
  else
    printf '?'
  fi
}

json_id() {
  if [[ -z "$1" || "$1" == "null" ]]; then
    printf 'null'
  else
    printf '"%s"' "$1"
  fi
}

request() {
  local method="$1" path="$2" data="${3:-}"
  if [[ -n "$data" ]]; then
    curl -sS -o "$TMP_BODY" -w '%{http_code}' -X "$method" "${BASE_URL}${path}" \
      -H 'Content-Type: application/json' -d "$data"
  else
    curl -sS -o "$TMP_BODY" -w '%{http_code}' -X "$method" "${BASE_URL}${path}"
  fi
}

expect() {
  local desc="$1" got="$2" want="$3"
  if [[ "$got" == "$want" ]]; then
    pass "$desc (HTTP $got)"
  else
    fail "$desc — expected HTTP $want, got $got"
    pretty
  fi
}

book_body() {
  local customer="$1" vin="$2" start="${3:-$START_UTC}" service="${4:-$OIL}" bay="${5:-$BAY1}" tech="${6:-$TECH_A}"
  printf '{"dealershipId":"%s","customerId":"%s","serviceTypeId":"%s","serviceBayId":%s,"technicianId":%s,"vin":"%s","startTimeUtc":"%s"}' \
    "$DEALERSHIP" "$customer" "$service" "$(json_id "$bay")" "$(json_id "$tech")" "$vin" "$start"
}

reset_stack() {
  step "Resetting stack (docker compose down -v && up --build -d)"
  local log
  log="$(mktemp)"

  if ! docker compose down -v >"$log" 2>&1; then
    printf '\n'
    fail "docker compose down failed"
    cat "$log"
    rm -f "$log"
    return 1
  fi

  printf '   building and starting '
  if ! docker compose up --build -d >>"$log" 2>&1; then
    printf '\n'
    fail "docker compose up failed"
    cat "$log"
    rm -f "$log"
    return 1
  fi
  printf 'done\n'

  printf '   waiting for %s/health ' "$BASE_URL"
  for _ in $(seq 1 90); do
    if curl -fsS "${BASE_URL}/health" >/dev/null 2>&1; then
      printf 'ready\n'
      rm -f "$log"
      return 0
    fi
    printf '.'
    sleep 1
  done

  printf '\n'
  fail "stack did not become healthy within 90s"
  cat "$log"
  rm -f "$log"
  return 1
}

preflight() {
  if ! curl -fsS "${BASE_URL}/health" >/dev/null 2>&1; then
    printf '%sAPI not reachable at %s%s\n' "$RED" "$BASE_URL" "$RESET" >&2
    printf 'Start it with:  ./demo.sh --reset   (or: docker compose up --build -d)\n' >&2
    exit 1
  fi
}

DO_RESET=0
for arg in "$@"; do
  case "$arg" in
    --reset) DO_RESET=1 ;;
    --no-concurrency) RUN_CONCURRENCY=0 ;;
    --help|-h) usage; exit 0 ;;
    *) printf 'Unknown option: %s\n' "$arg" >&2; usage >&2; exit 2 ;;
  esac
done

printf '%sKeyloop Unified Service Scheduler — live demonstration%s\n' "$BOLD" "$RESET"
note "base=$BASE_URL  date=$DEMO_DATE"

if [[ "$DO_RESET" -eq 1 ]]; then reset_stack; else preflight; fi

step "1. Health probe"
status="$(request GET /health)"
expect "service is live" "$status" 200

step "2. Availability — a bay AND a certified technician must both be free"
status="$(request GET "/api/availability?dealershipId=${DEALERSHIP}&serviceTypeId=${OIL}&date=${DEMO_DATE}")"
expect "availability search" "$status" 200
note "$(json_count) candidate (bay, technician) slots on ${DEMO_DATE}"

step "3. Book the 09:00 slot for Alice — expect 201 Created"
status="$(request POST /api/appointments "$(book_body "$CUSTOMER_A" "1HGBH41JXMN109186")")"
expect "appointment created" "$status" 201
appt_id="$(json_field appointmentId)"
note "appointmentId=${appt_id}  bay=Bay 1  technician=Tech A  customer=Alice Nguyen"

step "4. Book the SAME slot again — expect 409 Conflict"
status="$(request POST /api/appointments "$(book_body "$CUSTOMER_B" "2T1BURHE0JC123456")")"
expect "double-booking rejected" "$status" 409
note "title=$(json_field title)"
note "traceId=$(json_field traceId)"

step "5. Cancel the appointment — expect 204 No Content"
if [[ -n "$appt_id" ]]; then
  status="$(request DELETE "/api/appointments/${appt_id}")"
  expect "cancellation releases both resources" "$status" 204
else
  fail "no appointment id captured from step 3"
fi

step "6. Rebook the freed slot — expect 201 Created"
status="$(request POST /api/appointments "$(book_body "$CUSTOMER_B" "2T1BURHE0JC123456")")"
expect "slot available again after cancellation" "$status" 201
rebook_id="$(json_field appointmentId)"
note "appointmentId=${rebook_id}"

step "7. Certification gate — EV service, non-EV technician — expect 400"
status="$(request POST /api/appointments "$(book_body "$CUSTOMER_A" "1HGBH41JXMN109186" "${DEMO_DATE}T14:00:00Z" "$EV" "$BAY1" "$TECH_A")")"
expect "uncertified technician rejected" "$status" 400
note "detail=$(json_field detail)"

step "8. Auto-assignment — omit bay and technician — expect 201 Created"
status="$(request POST /api/appointments "$(book_body "$CUSTOMER_A" "1HGBH41JXMN109186" "${DEMO_DATE}T10:00:00Z" "$OIL" "null" "null")")"
expect "engine picked a compatible bay and certified technician" "$status" 201
auto_id="$(json_field appointmentId)"
note "appointmentId=${auto_id}  bay=$(json_field serviceBayId)  technician=$(json_field technicianId)"

step "9. Validation — non-UTC start — expect 400 Bad Request"
status="$(request POST /api/appointments "$(book_body "$CUSTOMER_A" "1HGBH41JXMN109186" "${DEMO_DATE}T09:00:00")")"
expect "non-UTC timestamp rejected" "$status" 400
note "detail=$(json_field detail)"

step "10. Concurrency — N simultaneous requests for one slot — exactly one winner"
if [[ "$RUN_CONCURRENCY" -ne 1 ]]; then
  note "skipped (--no-concurrency)"
elif ! command -v dotnet >/dev/null 2>&1; then
  fail ".NET SDK not found; cannot run the Tier 3 race suite"
else
  printf '   running the Tier 3 race suite (builds and starts a throwaway PostgreSQL) '
  if dotnet test --filter "Category=Concurrency" --nologo >"$TMP_TEST" 2>&1; then
    printf 'done\n'
    summary="$(grep -E '^Passed!' "$TMP_TEST" | tail -1 || true)"
    pass "${summary:-race suite passed}"
  else
    printf 'failed\n'
    fail "the concurrency suite did not pass"
    tail -n 20 "$TMP_TEST"
  fi
fi

# Release the appointments this run created so the script can be re-run without --reset.
for id in "${rebook_id:-}" "${auto_id:-}"; do
  if [[ -n "$id" ]]; then
    request DELETE "/api/appointments/${id}" >/dev/null 2>&1 || true
  fi
done

printf '\n'
if [[ "$FAILURES" -eq 0 ]]; then
  printf '%s%sAll steps passed.%s\n' "$BOLD" "$GREEN" "$RESET"
else
  printf '%s%s%d step(s) failed.%s\n' "$BOLD" "$RED" "$FAILURES" "$RESET"
  exit 1
fi
