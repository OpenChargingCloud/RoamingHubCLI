#!/usr/bin/env bash
#
# A grid operator publishes itself to a roaming network of this hub, end to
# end, over WWCP POI 3.
#
# RoamingHubCLI starts and makes the roaming network QA. GridOperatorCLI starts
# with QA as the roaming network it publishes to, gets a grid connection point
# over its JSON API, as root, and is refused while QA does not know it. The
# public halves of its keys are read off its JSON API, and the hub adds it as a
# party, as the operator of the roaming network does: a change set signed by
# the hub that waits for the signature of the grid operator's identity key, as
# the proof that it holds it. That signature is made from the grid operator's
# own key file by scripts/signChangeSet - the grid operator has no way to make
# it itself yet - and sent to the hub. Then the grid operator publishes: it
# reads the head of QA, signs one change set and COMMITs it. It is green when
# the head of the hub has the grid operator with its point, a second
# publication changes nothing, and a changed point is published as one more
# revision.
#
#   ROAMINGHUB_DLL    RoamingHubCLI.dll    (default: this repository's Release build)
#   GRIDOPERATOR_DLL  GridOperatorCLI.dll  (default: ../GridOperatorCLI's Release build)
#   SIGNER_DLL        signChangeSet.dll    (default: scripts/signChangeSet's Release build)
#
#   dotnet build RoamingHubCLI/RoamingHubCLI.csproj -c Release
#   dotnet build scripts/signChangeSet/signChangeSet.csproj -c Release
#   dotnet build ../GridOperatorCLI/GridOperatorCLI.slnx -c Release
#
# Ports 18357/22357 for the hub, 18133/22133 for the grid operator.
#
# No jq and no python, as in GridOperatorCLI's scripts/endToEnd.sh: the few
# values needed are cut out of the JSON with sed and grep - which the JSON APIs
# write on one line.

set -u

here="$(cd "$(dirname "$0")/.." && pwd)"
hub_dll="${ROAMINGHUB_DLL:-$here/RoamingHubCLI/bin/Release/net11.0/RoamingHubCLI.dll}"
go_dll="${GRIDOPERATOR_DLL:-$here/../GridOperatorCLI/GridOperatorCLI/bin/Release/net11.0/GridOperatorCLI.dll}"
signer_dll="${SIGNER_DLL:-$here/scripts/signChangeSet/bin/Release/net11.0/signChangeSet.dll}"

for dll in "$hub_dll" "$go_dll" "$signer_dll"; do
    if [ ! -f "$dll" ]; then
        echo "There is no $dll: build it first."
        exit 2
    fi
done

hub_port=18357
go_port=18133
hub_url="http://127.0.0.1:$hub_port"
go_url="http://127.0.0.1:$go_port"
network_url="$hub_url/poi/RNs/QA"

for port in $hub_port 22357 $go_port 22133; do
    if curl -sS -m 2 -o /dev/null "http://127.0.0.1:$port/" 2>/dev/null; then
        echo "Something answers on port $port already: stop it first."
        exit 2
    fi
done

tmp="$(mktemp -d)"
mkdir -p "$tmp/hub" "$tmp/go"

hub_pid=""
go_pid=""

stop() {
    for pid in $go_pid $hub_pid; do kill "$pid" 2>/dev/null; done
    for pid in $go_pid $hub_pid; do wait "$pid" 2>/dev/null; done
    return 0
}

# What a console said, without the first-start password and the private SSH
# key printed below it.
said() {
    sed '/BEGIN OPENSSH PRIVATE KEY/,/END OPENSSH PRIVATE KEY/d' "$1" 2>/dev/null | grep -v -i password | grep -v -i "nts" | tail -"$2" || true
}

fail() {
    echo "$1"
    stop
    echo
    echo "What the hub said:"
    said "$tmp/hub/console.log" 60
    echo
    echo "What the grid operator said:"
    said "$tmp/go/console.log" 40
    exit 1
}

# A request to a JSON API as root: the status code, and the body in the given
# answer file.
#   request <base URL> <password> <answer file> <method> <path> [<body>]
request() {
    local base="$1" pw="$2" answer="$3" method="$4" path="$5" body="${6:-}"
    local code
    if [ -n "$body" ]; then
        code="$(curl -sS -m 30 -u "root:$pw" -X "$method" -H 'Content-Type: application/json' -H 'Accept: application/json' -d "$body" \
                     -o "$answer" -w '%{http_code}' "$base$path" 2>/dev/null || true)"
    else
        code="$(curl -sS -m 30 -u "root:$pw" -X "$method" -H 'Accept: application/json' \
                     -o "$answer" -w '%{http_code}' "$base$path" 2>/dev/null || true)"
    fi
    echo "${code:-000}"
}

hub() { request "$hub_url" "$hub_password" "$tmp/answer.json" "$@"; }
go()  { request "$go_url"  "$go_password"  "$tmp/answer.json" "$@"; }

# The head of QA as JSON - without the Accept header the hub answers with its
# web page -, signed in as nobody: the POI API is readable by everybody.
head_of_qa() {
    # The roaming network itself names no revision: its head commit, beside
    # it, does.
    curl -sS -m 15 -H 'Accept: application/json' -o "${1%.json}-commit.json" "$network_url/history/head" 2>/dev/null
    curl -sS -m 15 -H 'Accept: application/json' -o "$1" -w '%{http_code}' "$network_url" 2>/dev/null || echo 000
}

# The string value of the first "Name":"..." in a JSON file.
value() {
    grep -o "\"$1\" *: *\"[^\"]*\"" "$2" 2>/dev/null | head -n 1 | sed "s/.*: *\"\\(.*\\)\"/\\1/"
}

# The number value of the first "Name":123 in a JSON file.
number() {
    grep -o "\"$1\" *: *[0-9][0-9]*" "$2" 2>/dev/null | head -n 1 | sed "s/.*: *//"
}

# How often a text is in a file.
count() {
    grep -o -F "$1" "$2" 2>/dev/null | wc -l | tr -d ' '
}

# The password said once in a banner: "│  password  <it>".
banner_password() {
    local pw=""
    for _ in $(seq 1 60); do
        pw="$(grep -a 'password ' "$1" 2>/dev/null | head -n 1 | awk '{ print $NF }')"
        [ -n "$pw" ] && break
        sleep 0.5
    done
    echo "$pw"
}

# Wait for a web interface to answer: its status code, 000 for none.
wait_up() {
    local url="$1" pid="$2" up=000
    for _ in $(seq 1 90); do
        up="$(curl -sS -m 5 -o /dev/null -w '%{http_code}' "$url/" 2>/dev/null || true)"
        up="${up:-000}"
        [ "$up" != 000 ] && break
        kill -0 "$pid" 2>/dev/null || break
        sleep 1
    done
    echo "$up"
}

#region The hub, and its roaming network QA

cat > "$tmp/hub/configuration.json" <<'EOF'
{
  "roamingNetworkOperator": { "@id": "OCC", "name": "Open Charging Cloud" }
}
EOF

dotnet "$hub_dll" \
    --port          $hub_port \
    --ssh-port      22357 \
    --accounts      "$tmp/hub/accounts" \
    --config        "$tmp/hub/configuration.json" \
    --certificates  "$tmp/hub/certificates" \
    --log-file      "$tmp/hub/logs" \
    < /dev/null > "$tmp/hub/console.log" 2>&1 &
hub_pid=$!

up="$(wait_up "$hub_url" "$hub_pid")"
[ "$up" = 200 ] || [ "$up" = 302 ] || fail "The hub did not start ($up)."

hub_password="$(banner_password "$tmp/hub/console.log")"
[ -n "$hub_password" ] || fail "The hub showed no password for root."

code="$(hub POST /api/v1/poi/RNs '{ "@id": "QA", "name": { "en": "Quality assurance" } }')"
[ "$code" = 201 ] || fail "The hub did not make the roaming network QA ($code): $(head -c 500 "$tmp/answer.json")"

code="$(head_of_qa "$tmp/head-0.json")"
[ "$code" = 200 ] || fail "The head of QA was not served as JSON ($code): $(head -c 500 "$tmp/head-0.json")"
revision_made="$(number revision "$tmp/head-0-commit.json")"

echo "hub               up, root signed in, QA made at revision ${revision_made:-?}: $network_url"

#endregion

#region The grid operator, and its grid connection point

cat > "$tmp/go/configuration.json" <<EOF
{
  "gridOperator":    { "@id": "DE*SWJ", "name": "Netze Beispielstadt" },
  "roamingNetwork":  { "url": "$network_url" }
}
EOF

# Started in its own directory: its keys are made below it, in gridOperator/.
( cd "$tmp/go" && exec dotnet "$go_dll" \
    --port          $go_port \
    --ssh-port      22133 \
    --accounts      "$tmp/go/accounts" \
    --config        "$tmp/go/configuration.json" \
    --certificates  "$tmp/go/certificates" \
    --log-file      "$tmp/go/logs" \
    < /dev/null > "$tmp/go/console.log" 2>&1 ) &
go_pid=$!

up="$(wait_up "$go_url" "$go_pid")"
[ "$up" = 200 ] || [ "$up" = 302 ] || fail "The grid operator did not start ($up)."

go_password="$(banner_password "$tmp/go/console.log")"
[ -n "$go_password" ] || fail "The grid operator showed no password for root."

code="$(go POST /api/v1/gridConnectionPoints '{
    "@id": "GCP-1", "name": { "de": "Netzanschluss GCP-1" },
    "voltageLevel": "LowVoltage", "connectionType": "AC_ThreePhases",
    "nominalVoltage": 400, "nominalFrequency": 50,
    "contractedImportPower": 110000, "contractedImportApparentPower": 120000,
    "marketLocationIds": [ "50123456789" ] }')"
[ "$code" = 201 ] || [ "$code" = 200 ] || fail "The grid connection point was not added ($code): $(head -c 500 "$tmp/answer.json")"

# Not a party of QA yet: nothing to publish, and said so.
code="$(go POST /api/v1/roamingNetwork/publication)"
[ "$code" = 409 ] || fail "Publishing before QA knows the grid operator was answered $code, and not 409: $(head -c 500 "$tmp/answer.json")"
grep -q "has no grid operator 'DE\*SWJ' yet" "$tmp/answer.json" ||
    fail "Publishing before QA knows the grid operator did not say why: $(head -c 500 "$tmp/answer.json")"

echo "grid operator     up, root signed in, GCP-1 added; publishing refused while QA does not know it (409)"

#endregion

#region The hub adds it as a party, and it proves it holds its identity key

code="$(go GET /api/v1/gridOperator)"
[ "$code" = 200 ] || fail "The grid operator did not say who it is ($code): $(head -c 500 "$tmp/answer.json")"
cp "$tmp/answer.json" "$tmp/gridOperator.json"

# What the operator of a roaming network needs to add it: "party", handed over
# as it is.
party="$(sed -n 's/.*"party" *: *\({.*}\) *, *"changeable".*/\1/p' "$tmp/gridOperator.json")"
key_dir="$(value directory "$tmp/gridOperator.json" | sed 's/\\\\/\\/g')"
[ -n "$party" ] || fail "The grid operator said no party: $(head -c 800 "$tmp/gridOperator.json")"
[ -f "$key_dir/identity-key.pem" ] || fail "The grid operator keeps no identity key in '$key_dir'."

code="$(hub POST /api/v1/poi/RNs/QA/GOs "$party")"
[ "$code" = 202 ] || fail "The hub did not add the grid operator, waiting for its identity key ($code): $(head -c 800 "$tmp/answer.json")"
cp "$tmp/answer.json" "$tmp/party-pending.json"

change_set_id="$(value changeSetId "$tmp/party-pending.json")"
[ -n "$change_set_id" ] || fail "Adding the grid operator said no change set: $(head -c 800 "$tmp/party-pending.json")"
grep -q "identity key" "$tmp/party-pending.json" || fail "The change set does not wait for the identity key: $(head -c 800 "$tmp/party-pending.json")"

# The signature of the identity key - made from the grid operator's own key
# file, as the grid operator cannot sign a waiting change set itself - sent by
# anybody, without signing in: whether it counts, the key decides.
signatures="$(dotnet "$signer_dll" "$tmp/party-pending.json" "$key_dir/identity-key.pem" 2> "$tmp/signer.log")" ||
    fail "No signature of the identity key was made: $(cat "$tmp/signer.log")"

code="$(curl -sS -m 30 -X POST -H 'Content-Type: application/json' -H 'Accept: application/json' -d "$signatures" \
             -o "$tmp/answer.json" -w '%{http_code}' "$network_url/history/changeSets/pending/$change_set_id/signatures" 2>/dev/null || echo 000)"
{ [ "$code" = 201 ] || [ "$code" = 200 ]; } && [ "$(value status "$tmp/answer.json")" = committed ] ||
    fail "The signature of the identity key did not commit the grid operator ($code): $(head -c 800 "$tmp/answer.json")"

code="$(head_of_qa "$tmp/head-1.json")"
[ "$code" = 200 ] || fail "The head of QA was not served ($code)."
revision_party="$(number revision "$tmp/head-1-commit.json")"
grep -q '"DE\*SWJ"' "$tmp/head-1.json" || fail "The head of QA has no grid operator DE*SWJ: $(head -c 800 "$tmp/head-1.json")"
grep -q '"GCP-1"' "$tmp/head-1.json" && fail "The head of QA has GCP-1 before the grid operator published it."

echo "party             DE*SWJ added by the hub, change set $change_set_id signed by its identity key: QA at revision $revision_party"

#endregion

#region The grid operator publishes

code="$(go GET /api/v1/roamingNetwork/publication)"
[ "$code" = 200 ] || fail "The grid operator could not say what publishing changes ($code): $(head -c 800 "$tmp/answer.json")"
[ "$(count '"add"' "$tmp/answer.json")" -ge 1 ] || fail "Publishing would add nothing: $(head -c 800 "$tmp/answer.json")"

code="$(go POST /api/v1/roamingNetwork/publication)"
[ "$code" = 200 ] || fail "The grid operator did not publish ($code): $(head -c 800 "$tmp/answer.json")"
cp "$tmp/answer.json" "$tmp/published-1.json"

code="$(head_of_qa "$tmp/head-2.json")"
[ "$code" = 200 ] || fail "The head of QA was not served ($code)."
revision_published="$(number revision "$tmp/head-2-commit.json")"

[ "${revision_published:-0}" -eq $(( ${revision_party:-0} + 1 )) ] ||
    fail "Publishing made QA revision $revision_published, and not $(( ${revision_party:-0} + 1 ))."

# The point in the head, below its grid operator, with what it was given.
point="$(sed 's/"@id"/\n"@id"/g' "$tmp/head-2.json" | grep '^"@id" *: *"GCP-1"' | head -n 1)"
[ -n "$point" ] || fail "The head of QA has no GCP-1 after publishing: $(head -c 1500 "$tmp/head-2.json")"
echo "$point" > "$tmp/point-2.json"
[ "$(number contractedImportPower "$tmp/point-2.json")" = 110000 ] ||
    fail "GCP-1 in the head of QA has the contracted import power $(number contractedImportPower "$tmp/point-2.json"), and not 110000."

echo "published         DE*SWJ with GCP-1 (110 kW): QA at revision $revision_published"

#endregion

#region Once more without a change, and once with one

code="$(go GET /api/v1/roamingNetwork/publication)"
[ "$code" = 200 ] || fail "The grid operator could not say what publishing changes now ($code): $(head -c 800 "$tmp/answer.json")"
grep -q "nothing to publish" "$tmp/answer.json" || fail "After publishing, there is still something to publish: $(head -c 800 "$tmp/answer.json")"

code="$(go POST /api/v1/roamingNetwork/publication)"
[ "$code" = 200 ] || fail "Publishing again without a change was answered $code: $(head -c 800 "$tmp/answer.json")"
grep -q "nothing was published" "$tmp/answer.json" || fail "Publishing again without a change did not say nothing was published: $(head -c 800 "$tmp/answer.json")"

code="$(head_of_qa "$tmp/head-3.json")"
[ "$(number revision "$tmp/head-3-commit.json")" = "$revision_published" ] ||
    fail "Publishing again without a change moved QA from revision $revision_published to $(number revision "$tmp/head-3-commit.json")."

echo "again             nothing to publish, nothing published: QA stays at revision $revision_published"

code="$(go PUT /api/v1/gridConnectionPoints/GCP-1 '{
    "@id": "GCP-1", "name": { "de": "Netzanschluss GCP-1" },
    "voltageLevel": "LowVoltage", "connectionType": "AC_ThreePhases",
    "nominalVoltage": 400, "nominalFrequency": 50,
    "contractedImportPower": 150000, "contractedImportApparentPower": 160000,
    "marketLocationIds": [ "50123456789" ] }')"
[ "$code" = 200 ] || fail "GCP-1 was not changed ($code): $(head -c 500 "$tmp/answer.json")"

code="$(go POST /api/v1/roamingNetwork/publication)"
[ "$code" = 200 ] || fail "The grid operator did not publish the changed GCP-1 ($code): $(head -c 800 "$tmp/answer.json")"

code="$(head_of_qa "$tmp/head-4.json")"
revision_changed="$(number revision "$tmp/head-4-commit.json")"
[ "${revision_changed:-0}" -eq $(( revision_published + 1 )) ] ||
    fail "Publishing the changed GCP-1 made QA revision $revision_changed, and not $(( revision_published + 1 ))."

sed 's/"@id"/\n"@id"/g' "$tmp/head-4.json" | grep '^"@id" *: *"GCP-1"' | head -n 1 > "$tmp/point-4.json"
[ "$(number contractedImportPower "$tmp/point-4.json")" = 150000 ] ||
    fail "GCP-1 in the head of QA has the contracted import power $(number contractedImportPower "$tmp/point-4.json"), and not 150000."
[ "$(count '"GCP-1"' "$tmp/head-4.json")" = 1 ] || fail "The head of QA has GCP-1 more than once."

echo "changed           GCP-1 to 150 kW, published: QA at revision $revision_changed"

grep -a -r -q "published the grid operator 'DE\*SWJ' to the roaming network 'QA'" "$tmp/go/console.log" "$tmp/go/logs" 2>/dev/null ||
    fail "The grid operator did not log its publication."

#endregion

stop
echo "End to end: the hub added the grid operator DE*SWJ to QA, it proved its identity key, published itself and GCP-1, a second publication changed nothing and a changed point was one revision more."
exit 0
