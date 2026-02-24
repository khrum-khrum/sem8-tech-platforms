#!/usr/bin/env bash
# test.sh — end-to-end smoke test for CdrBilling using the example files.
# Starts the API, runs the full workflow, checks results against expected values.
#
# Requirements: dotnet, docker (postgres), jq

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
BASE_URL="http://localhost:5000"
API_PROJECT="$REPO_ROOT/src/CdrBilling.Api"

PASS=0
FAIL=0

green() { printf '\033[32m%s\033[0m\n' "$*"; }
red()   { printf '\033[31m%s\033[0m\n' "$*"; }

check() {
    local desc="$1" actual="$2" expected="$3"
    if [[ "$actual" == "$expected" ]]; then
        green "  PASS  $desc"
        ((PASS++))
    else
        red   "  FAIL  $desc"
        red   "        expected: $expected"
        red   "        actual:   $actual"
        ((FAIL++))
    fi
}

# ─── 1. Ensure PostgreSQL is running ──────────────────────────────────────────
echo ""
echo "==> Starting PostgreSQL (docker compose)..."
docker compose -f "$REPO_ROOT/docker-compose.yml" up -d
sleep 2   # give postgres a moment to accept connections

# ─── 2. Start the API in the background ───────────────────────────────────────
echo ""
echo "==> Building & starting API..."
API_LOG=$(mktemp)
dotnet run --project "$API_PROJECT" --no-launch-profile \
    > "$API_LOG" 2>&1 &
API_PID=$!

cleanup() {
    echo ""
    echo "==> Stopping API (PID $API_PID)..."
    kill "$API_PID" 2>/dev/null || true
}
trap cleanup EXIT

# Wait until the API is listening (up to 60 s)
echo -n "    Waiting for API"
for i in $(seq 1 60); do
    if curl -sf "$BASE_URL/openapi/v1.json" -o /dev/null 2>/dev/null; then
        echo " ready."
        break
    fi
    echo -n "."
    sleep 1
    if [[ $i -eq 60 ]]; then
        echo ""
        red "ERROR: API did not start within 60 seconds."
        echo "--- API log ---"
        cat "$API_LOG"
        exit 1
    fi
done

# ─── 3. Create session ─────────────────────────────────────────────────────────
echo ""
echo "==> Creating billing session..."
CREATE_RESP=$(curl -sf -X POST "$BASE_URL/api/sessions")
SESSION_ID=$(echo "$CREATE_RESP" | jq -r '.sessionId')
echo "    Session ID: $SESSION_ID"

# ─── 4. Upload files ───────────────────────────────────────────────────────────
echo ""
echo "==> Uploading CDR file..."
curl -sf -F "file=@$SCRIPT_DIR/cdr.txt" \
    "$BASE_URL/api/sessions/$SESSION_ID/upload/cdr" | jq .

echo "==> Uploading tariff file..."
curl -sf -F "file=@$SCRIPT_DIR/tariffs.csv" \
    "$BASE_URL/api/sessions/$SESSION_ID/upload/tariff" | jq .

echo "==> Uploading subscriber file..."
curl -sf -F "file=@$SCRIPT_DIR/subscribers.csv" \
    "$BASE_URL/api/sessions/$SESSION_ID/upload/subscribers" | jq .

# ─── 5. Run tariffication and poll until done ─────────────────────────────────
echo ""
echo "==> Starting tariffication..."
curl -sf -X POST "$BASE_URL/api/sessions/$SESSION_ID/run" | jq .

echo -n "    Waiting for completion"
for i in $(seq 1 60); do
    STATUS=$(curl -sf "$BASE_URL/api/sessions/$SESSION_ID/status" \
        | jq -r '.status')
    if [[ "$STATUS" == "Completed" ]]; then
        echo " done."
        break
    elif [[ "$STATUS" == "Failed" ]]; then
        echo ""
        red "ERROR: Tariffication failed."
        curl -sf "$BASE_URL/api/sessions/$SESSION_ID/status" | jq .
        exit 1
    fi
    echo -n "."
    sleep 1
    if [[ $i -eq 60 ]]; then
        echo ""
        red "ERROR: Tariffication did not complete within 60 seconds."
        curl -sf "$BASE_URL/api/sessions/$SESSION_ID/status" | jq .
        exit 1
    fi
done

# ─── 6. Fetch and display results ─────────────────────────────────────────────
echo ""
echo "==> Fetching summary results..."
SUMMARY=$(curl -sf "$BASE_URL/api/sessions/$SESSION_ID/results/summary")
echo "$SUMMARY" | jq .

echo ""
echo "==> Fetching call detail results..."
CALLS=$(curl -sf "$BASE_URL/api/sessions/$SESSION_ID/results/calls?page=1&pageSize=50")
echo "$CALLS" | jq .

# ─── 7. Assertions ────────────────────────────────────────────────────────────
echo ""
echo "=== Checking results ==="

# --- Summary checks ---
# Иванов Иван Иванович (78123260000): 2 billed calls, total = 6.3000
IVANOV=$(echo "$SUMMARY" | jq '.[] | select(.phoneNumber == "78123260000")')
check "Иванов: callCount == 2" \
    "$(echo "$IVANOV" | jq -r '.callCount')" "2"
check "Иванов: totalBillableSec == 210 (120+90)" \
    "$(echo "$IVANOV" | jq -r '.totalBillableSec')" "210"
check "Иванов: totalCharge == 6.3000" \
    "$(echo "$IVANOV" | jq -r '.totalCharge')" "6.3000"

# Петров Пётр Петрович (78123261111): 2 billed calls, total = 3.7500
PETROV=$(echo "$SUMMARY" | jq '.[] | select(.phoneNumber == "78123261111")')
check "Петров: callCount == 2" \
    "$(echo "$PETROV" | jq -r '.callCount')" "2"
check "Петров: totalBillableSec == 105 (60+45)" \
    "$(echo "$PETROV" | jq -r '.totalBillableSec')" "105"
check "Петров: totalCharge == 3.7500" \
    "$(echo "$PETROV" | jq -r '.totalCharge')" "3.7500"

# --- Call detail checks ---
ITEMS=$(echo "$CALLS" | jq '.items')

# call0001: outgoing to 79161234567, tariff prefix 7916, charge 3.6000
CALL_A=$(echo "$ITEMS" | jq '.[] | select(.callId == "call0001")')
check "call0001: computedCharge == 3.6000" \
    "$(echo "$CALL_A" | jq -r '.computedCharge')" "3.6000"
check "call0001: appliedTariff.prefix == 7916" \
    "$(echo "$CALL_A" | jq -r '.appliedTariff.prefix')" "7916"

# call0002: incoming from 74951112233, tariff prefix 7495, charge 1.0000
CALL_B=$(echo "$ITEMS" | jq '.[] | select(.callId == "call0002")')
check "call0002: computedCharge == 1.0000" \
    "$(echo "$CALL_B" | jq -r '.computedCharge')" "1.0000"
check "call0002: appliedTariff.prefix == 7495" \
    "$(echo "$CALL_B" | jq -r '.appliedTariff.prefix')" "7495"

# call0003: outgoing to 79162345678, tariff prefix 7916, charge 2.7000
CALL_C=$(echo "$ITEMS" | jq '.[] | select(.callId == "call0003")')
check "call0003: computedCharge == 2.7000" \
    "$(echo "$CALL_C" | jq -r '.computedCharge')" "2.7000"
check "call0003: appliedTariff.prefix == 7916" \
    "$(echo "$CALL_C" | jq -r '.appliedTariff.prefix')" "7916"

# call0004: busy — present in results but computedCharge must be null (not billed)
CALL_D=$(echo "$ITEMS" | jq '.[] | select(.callId == "call0004")')
check "call0004 (busy): computedCharge is null" \
    "$(echo "$CALL_D" | jq -r '.computedCharge // "null"')" "null"
check "call0004 (busy): appliedTariff is null" \
    "$(echo "$CALL_D" | jq -r '.appliedTariff // "null"')" "null"

# call0005: internal — present in results but computedCharge must be null (always skipped)
CALL_E=$(echo "$ITEMS" | jq '.[] | select(.callId == "call0005")')
check "call0005 (internal): computedCharge is null" \
    "$(echo "$CALL_E" | jq -r '.computedCharge // "null"')" "null"
check "call0005 (internal): appliedTariff is null" \
    "$(echo "$CALL_E" | jq -r '.appliedTariff // "null"')" "null"

# call0006: outgoing to 79189999999, tariff prefix 791, charge 2.7500
CALL_F=$(echo "$ITEMS" | jq '.[] | select(.callId == "call0006")')
check "call0006: computedCharge == 2.7500" \
    "$(echo "$CALL_F" | jq -r '.computedCharge')" "2.7500"
check "call0006: appliedTariff.prefix == 791" \
    "$(echo "$CALL_F" | jq -r '.appliedTariff.prefix')" "791"

# --- Total call count (all 6 records stored, regardless of disposition) ---
TOTAL=$(echo "$CALLS" | jq -r '.totalCount')
check "results/calls totalCount == 6 (all stored records)" \
    "$TOTAL" "6"

# ─── 8. Summary ───────────────────────────────────────────────────────────────
echo ""
echo "================================"
printf "  Results: %d passed, %d failed\n" "$PASS" "$FAIL"
echo "================================"
echo ""

if [[ $FAIL -gt 0 ]]; then
    exit 1
fi
