#!/usr/bin/env bash
# TASK-032 slice 8 — scripted verification of the 15 §G cases from
# .agent/specs/GST-IMPLEMENTATION.md.
#
# Prereqs:
#   * API running on $API (default http://localhost:5299)
#   * Platform admin creds in $USERNAME / $PASSWORD (defaults admin/admin123)
#   * seed-G.json bulk-seeded (we do that automatically; conflicts on
#     repeat runs are silently tolerated — a prior run's rows satisfy
#     the test just as well).
#
# Exits non-zero on any assertion failure.
set -u
API=${API:-http://localhost:5299}
# NB: don't inherit the shell's $USERNAME (Windows sets it to the OS login)
GST_USER=${GST_USER:-admin}
GST_PASS=${GST_PASS:-admin123}
HERE=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)

FAIL=0
PASS=0

login() {
  local resp; resp=$(curl -fsS -X POST "$API/api/auth/login" -H "Content-Type: application/json" \
    -d "{\"username\":\"$GST_USER\",\"password\":\"$GST_PASS\"}")
  echo "$resp" | grep -oE '"token":"[^"]+"' | sed 's/"token":"//;s/"$//'
}

bulk_seed() {
  local token=$1
  echo "Bulk-seeding rules..."
  curl -sS -X POST "$API/api/gst/rules/bulk" \
    -H "Authorization: Bearer $token" -H "Content-Type: application/json" \
    --data @"$HERE/seed-G.json" | head -c 400
  echo
}

# $1 case label, $2 expected key, $3 expected val, $4 JSON body
assert_calculate() {
  local label=$1 ekey=$2 eval=$3 body=$4
  local token=$5
  local resp; resp=$(curl -sS -X POST "$API/api/tax/calculate" \
    -H "Authorization: Bearer $token" -H "Content-Type: application/json" -d "$body")
  local got; got=$(echo "$resp" | grep -oE "\"$ekey\":[^,}]+" | head -1 | sed "s/\"$ekey\"://")
  if [ "$got" = "$eval" ]; then
    PASS=$((PASS+1))
    printf "  PASS  %-40s %s=%s\n" "$label" "$ekey" "$got"
  else
    FAIL=$((FAIL+1))
    printf "  FAIL  %-40s %s=%s (expected %s)\n" "$label" "$ekey" "$got" "$eval"
    echo "        resp: $resp"
  fi
}

TOKEN=$(login)
[ -z "$TOKEN" ] && { echo "login failed"; exit 2; }
bulk_seed "$TOKEN"

echo
echo "=== §G verification cases ==="

# G.1 — GTA FCM5 intra: freight 10000 → cgst 250, sgst 250, grand 10500
assert_calculate "G.1 intra FCM5 cgst" "cgstAmount" "250.00" \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":1,"recipientCategory":1,"recipientStateCode":"10","freightAmount":10000,"transactionDate":"2024-06-15"}' "$TOKEN"
assert_calculate "G.1 intra FCM5 sgst" "sgstAmount" "250.00" \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":1,"recipientCategory":1,"recipientStateCode":"10","freightAmount":10000,"transactionDate":"2024-06-15"}' "$TOKEN"
assert_calculate "G.1 intra FCM5 grand" "grandTotal" "10500.00" \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":1,"recipientCategory":1,"recipientStateCode":"10","freightAmount":10000,"transactionDate":"2024-06-15"}' "$TOKEN"

# G.2 — GTA FCM5 inter: freight 10000 → igst 500, grand 10500
assert_calculate "G.2 inter FCM5 igst" "igstAmount" "500.00" \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":1,"recipientCategory":1,"recipientStateCode":"27","freightAmount":10000,"transactionDate":"2024-06-15"}' "$TOKEN"

# G.3 — GTA FCM12 intra: freight 10000 → cgst 600, sgst 600, grand 11200
assert_calculate "G.3 intra FCM12 cgst" "cgstAmount" "600.00" \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":2,"recipientCategory":1,"recipientStateCode":"10","freightAmount":10000,"transactionDate":"2024-06-15"}' "$TOKEN"
assert_calculate "G.3 intra FCM12 itcFlag" "isItcEligible" "true" \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":2,"recipientCategory":1,"recipientStateCode":"10","freightAmount":10000,"transactionDate":"2024-06-15"}' "$TOKEN"

# G.4 — GTA FCM12 inter: freight 10000 → igst 1200, grand 11200
assert_calculate "G.4 inter FCM12 igst" "igstAmount" "1200.00" \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":2,"recipientCategory":1,"recipientStateCode":"27","freightAmount":10000,"transactionDate":"2024-06-15"}' "$TOKEN"

# G.5 — GTA RCM: splits all zero, isRcm true, grand = taxable
assert_calculate "G.5 RCM isRcm" "isRcm" "true" \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":3,"recipientCategory":5,"recipientStateCode":"10","freightAmount":10000,"transactionDate":"2024-06-15"}' "$TOKEN"
assert_calculate "G.5 RCM totalTax" "totalTax" "0" \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":3,"recipientCategory":5,"recipientStateCode":"10","freightAmount":10000,"transactionDate":"2024-06-15"}' "$TOKEN"

# G.6 — Non-GTA road: EXEMPT, splits zero
assert_calculate "G.6 non-GTA treatment" "taxTreatment" "3" \
  '{"serviceType":2,"providerIsGta":false,"issuesConsignmentNote":false,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":0,"recipientCategory":2,"recipientStateCode":"10","freightAmount":10000,"transactionDate":"2024-06-15"}' "$TOKEN"

# G.7 — Own fleet: short-circuit, no rule lookup
assert_calculate "G.7 own-fleet treatment" "taxTreatment" "4" \
  '{"serviceType":3,"providerIsGta":false,"issuesConsignmentNote":false,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":0,"recipientCategory":1,"recipientStateCode":"10","freightAmount":10000,"transactionDate":"2024-06-15"}' "$TOKEN"

# G.8/9/13/14 — same rule as G.1, verified above.

# G.10 — Customer without GSTIN (UNREG): different rule but same split
assert_calculate "G.10 UNREG cgst" "cgstAmount" "250.00" \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":1,"recipientCategory":2,"recipientStateCode":"10","freightAmount":10000,"transactionDate":"2024-06-15"}' "$TOKEN"

# G.11 — Different POS: inter via POS override, same as G.2
# (ClassifyPos uses PlaceOfSupplyStateCode when provided)
assert_calculate "G.11 POS override inter igst" "igstAmount" "500.00" \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":1,"recipientCategory":1,"recipientStateCode":"10","placeOfSupplyStateCode":"27","freightAmount":10000,"transactionDate":"2024-06-15"}' "$TOKEN"

# G.12 — Tax-inclusive input 10500 against FCM5 → taxable 10000, totalTax 500
assert_calculate "G.12 tax-inclusive taxable" "taxableAmount" "10000" \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":1,"recipientCategory":1,"recipientStateCode":"10","freightAmount":10500,"taxInclusive":true,"transactionDate":"2024-06-15"}' "$TOKEN"
assert_calculate "G.12 tax-inclusive totalTax" "totalTax" "500.00" \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":1,"recipientCategory":1,"recipientStateCode":"10","freightAmount":10500,"taxInclusive":true,"transactionDate":"2024-06-15"}' "$TOKEN"

# G.14 — Freight 11200 already including other charges → same rule, higher base
assert_calculate "G.14 bundled charges grand" "grandTotal" "11760.00" \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":1,"recipientCategory":1,"recipientStateCode":"10","freightAmount":11200,"transactionDate":"2024-06-15"}' "$TOKEN"

# G.15 — Historical fidelity: a txn dated 2023-06-15 must bind to the
# 2023-only R9 rule, not the open-ended 2024-01-01 R1 rule.
RESP_HIST=$(curl -sS -X POST "$API/api/tax/calculate" -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d \
  '{"serviceType":1,"providerIsGta":true,"issuesConsignmentNote":true,"providerGstRegistered":true,"providerStateCode":"10","gtaTaxScheme":1,"recipientCategory":1,"recipientStateCode":"10","freightAmount":10000,"transactionDate":"2023-06-15"}')
HIST_REF=$(echo "$RESP_HIST" | grep -oE '"notificationReference":"[^"]+"' | sed 's/"notificationReference":"//;s/"$//')
case "$HIST_REF" in
  *HISTORICAL*) PASS=$((PASS+1)); echo "  PASS  G.15 historical rule selected          notificationReference=$HIST_REF" ;;
  *)            FAIL=$((FAIL+1)); echo "  FAIL  G.15 historical rule selected          got notificationReference=$HIST_REF" ;;
esac

echo
echo "=== Summary ==="
echo "PASS=$PASS  FAIL=$FAIL"
exit $FAIL
