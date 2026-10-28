# Security audit 2026-10-23

The open findings of a security review of RoamingHubCLI and above all WWCP_POI, asked for on 2026-10-22, at
WWCP_POI `5832482` (super `567c2d8`). The findings already fixed (network-wide unique party keys and a co-signing
signature limit in WWCP_POI `6661d2d`, read-only API keys in WWCP_Node `7dc7c84`, masked OCPI tokens in RoamingHub
`07aa584`, super `cb56283`) have been taken out of this list.

The critical and high WWCP_POI findings were verified end to end, several by proof-of-concept tests; the
WWCP_Node, Hermod, RoamingHub and proof/checkpoint findings were read from the code with the file and line cited.
Paths name their repository.

**Checked again on 2026-10-24** at WWCP_POI `801bbce` (RoamingHub `10d8a56`, WWCP_Node `7dc7c84`, Hermod `fbb442f3`,
Styx `91901a9d`, WWCP_OCPI `42669383`, super `a0c1841`): every finding still holds, and the line numbers are those of
these commits. The parties added since the audit (transparency software manufacturers in WWCP_POI `0c3c53c`,
conformity assessment bodies in `bbd4cbd`, charging station and energy meter manufacturers with their models in
`801bbce`) are further instances of several findings, noted in each, and gave the new findings S31 to S36.

**Checked again on 2026-10-27** at WWCP_POI `5a03f14` (RoamingHub `397e478`, Styx `06b592d3`, super `a5ceb23`; WWCP_Node,
Hermod and WWCP_OCPI unchanged). A review written while porting WWCP_POI to TypeScript (WWCPPOIConformanceTests,
`docs/csharp-review.md`, cited as *csharp-review N*) found several of these findings again; WWCP_POI fixed them, and
they are taken out of this list:

| Finding | Fixed in WWCP_POI | How |
|---|---|---|
| S3 a change set for an old version escapes revocation | `f2ae688` (csharp-review 3) | keys checked at `createdAt` and when the server takes it; a revoked key conflicts in the merge |
| S6 the quorum counts holders a key statement names | `3e32f48` (csharp-review 1) | the trust anchor names its keys per holder (v2); a key keeps its holder |
| S7 attestations without expiry, keys checked at `signedAt` | `be07037` (csharp-review 7) | `validUntil` required, at most 24 h; keys valid when attesting and when verified |
| S15 a change set committed by its `@id` alone | `bc3b1bd`, `1717ee2` (csharp-review 4, 12) | the `@id` is the SHA-256 of the content; `rebasedFrom` entries recalculated |
| S16 a subject attestation verifies the whole proof | `6e469a0`, `65e658d` (csharp-review 6, 19) | root attestations in the role `roamingNetworkOperator` only; `VerifySubject`; no statement or certificate of another operator |
| S18 witnesses sign unchecked checkpoints | `c5cfc62` (csharp-review 5) | a checkpoint taken without its commits is not witnessed; a witness keeps a journal |
| S33 references block withdrawals | `52f1172` (csharp-review 18) | decided as designed: SPECIFICATION.md 4.1 says what another party names is not withdrawn while it names it, also not by the RNO; both change it in one change set both sign |
| S34 RNO-stated certificates look like the body's | `65e658d` (csharp-review 15) | `"statedBy": "roamingNetworkOperator"`; the hub sets it since RoamingHub `397e478` |
| S35 a meter's certificate is not tied to the model | `987fa26`, `7e0b0ac` (csharp-review 16, 17) | a certificate is for one model, its revisions and firmware versions, and is checked against each station and meter naming it |

S5, S11 and S26 are fixed in part; what is left is said in each. The others still hold. Lines given for
`5a03f14` were checked again; the rest are those of the commits of 2026-10-24.

**Decided and fixed on 2026-10-27** at WWCP_POI `ff2c56f` (super `ac1d46e`), as the maintainer chose:

| Finding | Fixed in WWCP_POI | How |
|---|---|---|
| S2 the highest-revision tip is the head, so one party rolls back everyone else | `ef1312d` | a server takes no change set whose commit would conflict with the head (409 with the conflicts); one resolving a conflict on a side tip is taken |
| S9 conflicting branches without bound (DoS of consensus) | `ef1312d` | a change set only for a version at most `MaxRevisionsBehindHead` (1000) below the head and not before the latest checkpoint (422); conflicting tip pairs remembered until a tip changes; merge bases by one walk back to the fork |
| S10 the peer trusts a snapshot by a name check | `ff2c56f` | `TrustAnchor` replaces `TrustedRoamingNetworkOperatorKeyIds` and is mandatory; a snapshot carries the attestations of its head, a checkpoint the quorum of the operator; a history held is replaced only where anchored checkpoints prove its head, else kept until `ResetHistoryAsync`; never an earlier epoch |

Left of S2 and S9: commits appended from servers allowed to push (`AuthorizeCommitPush`) are bound by neither rule,
so such a server can still make a conflicting branch the head of those taking its commits, and a branch it forked long
ago still holds compaction back (SPECIFICATION.md section 7, open point 2). The hub allows no pushes. Keeping the head
on its line by the operator's attestations was put off until servers push to each other.

**S13 fixed on 2026-10-27** (WWCP_Node `c2422e1`, Hermod `f1a6bbd8`, RoamingHub `80c1db7`), as the maintainer
chose: every node, the hub among them, serves HTTPS of its own or trusts the proxies named in
`"web": { "trustedProxies" }`, from the `"web"` section of the configuration file or from `--https`,
`--https-cert`/`--https-key`, `--redirect-port` and `--trusted-proxy`. The certificate comes from the store (the TLS
server identity for the listener `web`) or from PEM files read again when they change; with nothing to show a
connection is refused. The session cookie is `Secure` and HSTS is sent where the browser's leg is HTTPS, behind a
trusted proxy by its `Forwarded`/`X-Forwarded-Proto`. Plain HTTP beyond the loopback is still served, and said
loudly: in the log, below the banner, on the Configuration page and in `/api/v1/auth/me` for an administrator.
Left: HSTS is sent on the web interface, not on the JSON API; the other CLIs read the `"web"` section, but take the
switches only once they pass `NodeArguments.Web` to their node.

**S22 and S23 fixed on 2026-10-27** (Hermod `b38805ec`, WWCP_Node `9e6d00d`, WWCP_POI `7862c86`, RoamingHub
`691028c`): SSH passwords count against the web sign-in's ration, per client and per account, are hashed outside
`UsersSemaphore`, and an unknown name is checked against a dummy hash; the legacy `SET /ext/users/{id}/password`
confirms the current password on the ration of `POST auth/password`. POI_API's nine write routes take
`application/json` or `application/cbor` alone (else 415); its WebSocket opens to requests without an Origin, from
its own origin (or the name a trusted proxy gives) or from `allowedOrigins` (the hub's `"poi": { "allowedOrigins" }`),
else 403. Every request below `/ext` that is no read refuses cross-site requests as `/api` did (`Sec-Fetch-Site`,
Origin), in one pipeline of the node. The node has no WebSocket of its own; its log and event streams are read-only
SSE.

**S21 fixed on 2026-10-28** (WWCP_Node `54d25a3`, RoamingHub `24523a8`): every entry of the event log, and every
complaint on stderr, is one line: CR, LF, tab, other control characters, U+2028/U+2029 and the bidirectional marks
are written out (`\r`, `\n`, `\t`, `\uXXXX`) in `EventLog.Write`, so for every sink at once, and a message is cut
at 4096 characters with the number left out. Failed SSH sign-ins are counted per connection and said once, at Info,
when it closes (each attempt at Debug). The OCPI traffic log passes its one-line fields through `EventLog.OneLine`;
bodies stay as they came.

**S28 fixed on 2026-10-28** (RoamingHub `f94ad39`): the answer of `POST /api/v1/ocpi/partners/{version}/{id}/register`
carries the peers' tokens only where the account may edit the peers, as `GET /api/v1/ocpi/partners` does; a role with
`peers:read` and `peers:run` gets them without (`HubAccessTests.WhoMayRunTheHandshakeIsNotHandedTheTokens`).

**S17 fixed on 2026-10-28** (WWCP_POI `4680d99`): the chain of price statements of an entity is keyed by the entity -
`RoamingNetworkHistory.ChainOf`, its identification without `*`, whitespace and case - and a statement has to name it
as the roaming network does (`DE*GEF*E11` for `DE*GEF*E1*1` is refused); a sequence is at most 2^53 − 1 and at most
1,000,000 beyond the current one; the statements for the next checkpoint are bounded per operator
(`MaxPendingPriceStatementsPerOperator`, 100,000), an operator stating more drops its own oldest only; a checkpoint
naming two statements of one entity and sequence with different digests is refused. SPECIFICATION.md 12.1-12.3.

**S12 fixed on 2026-10-28** (WWCP_POI `d184feb`, RoamingHub `106184f`; before, in part, WWCP_POI `33447ac`): no
public reader takes `historyLock` (the read view). A page of commits costs about what it returns: each commit of the view
keeps how many of the first commits it is or follows, and only the commits after those are walked, so a `have` naming
the root alone, `GET commits` without `since` and a server catching up page by page no longer walk the whole history
for every page (`CommitsLookedAt`; `MissingCommitsTests`). `have` is capped (`MaxHaveIds`, 1,000), also over several
`have=` parameters, which count together. The 16 views without parts kept per version are the ones asked for last, so
nobody takes the slots first. Snapshots of the head and of checkpoints, pages of commits and of their metadata,
archive segments and views without parts not kept are heavy reads: at most `MaxHeavyReadsPerMinute` (600) per client
and minute, a client by its address (IPv6 by its /64), behind a trusted proxy the one it names (the hub hands
`ClientAddressOf` on); beyond, 429 with `Retry-After` over HTTP, a wait of up to `MaxHeavyReadWait` (10 s) and then
`GenericError` over the WebSocket. WebSocket batches, list limits and `match` bounds as before. SPECIFICATION.md 5.5.
Left: a limit per client does not bound many clients at once, and a page next to a branch never merged still walks the
commits beside it (only commits of servers allowed to push make such branches; the hub allows none).

**S19 fixed on 2026-10-28** (Hermod `ca91a551`, WWCP_OCPI `19eb3be5`, RoamingHub `e9ca38c`), as the maintainer chose:
`ATCPClient.RemoteNetworkAcl` checks every address a remote resolved to before one is dialled, none resolved again in
between (DNS-rebinding safe); `NetworkAcl` reads `"10.0.0.0/8 => DENY"`, matches `::ffff:a.b.c.d` as IPv4 and denies
every non-public network (`DenyNonPublicNetworks`, IPv6 and NAT64 included). `CommonHTTPAPI.RemoteNetworkAcl` reaches
every OCPI `CommonHTTPClient`; the hub sets it from `"ocpi": { "peerNetworks": [ … ] }` - the file's rules first, then
the non-public networks denied, then the public internet. A refused URL is answered like one nobody answers at.

Items marked **needs a design decision** wait on the maintainer. Severity: **critical** means the trust model is
broken; **high** means wrong results, a freeze or a denial of service; **medium** and **low** as usual.


## Medium

### S20. Session tokens and API keys are stored raw
`design` · low (was medium) · **fixed in part** in WWCP_Node `829896d`, RoamingHub `6824d54` (2026-10-28)

- **Fixed:** `accounts/`, `logs/` (with `logs/metrological/` and its signing key), `certificates/`, `ssh/`,
  `ocpi/`, `traffic/` and `roamingNetworkOperator/` (and its two private keys) are made, and taken back at every
  start, as their owner's alone through `WWCP_Node/OwnerOnly.cs`: 0700/0600 on Unix, and on Windows a protected
  access list for the running account, SYSTEM and the administrators, which what is in a directory inherits; where
  a file system cannot say so the log warns. `/traffic/` is git-ignored in the superproject, and the hub's own
  `.gitignore` names logs/, ssh/, roamingNetworkOperator/, poi/ and traffic/ as well.
- **Left:** session tokens and API keys are stored raw in the accounts' file (Hermod's HTTPExt API); whoever can
  read that file - now only the account itself, SYSTEM and the administrators - can use them.
- **Fix:** store them hashed, with a migration of the existing accounts' files of every CLI; an API key can then be
  shown once, when it is made.

### S31. A transparency software manufacturer can change a release after a conformity assessment body certified it
`security` · medium · **open** (found 2026-10-24 at WWCP_POI `801bbce`, read from the code, no PoC)

- **Where:** `WWCP_POI/WWCP_POI3/RoamingNetwork/RoamingNetwork.TransparencySoftwareChanges.cs:43-54` (`version`,
  `name`, `sourceCodeRepository`, `openSourceLicenses` … changeable), `RoamingNetwork.Ownership.cs:1034-1072` (owned
  by the manufacturer alone), `WWCP_POI2/RoamingNetwork/ConformityAssessmentBody/ConformityCertificate.cs:216` at
  `5a03f14` (`VerifiedTransparencySoftwareIds`: since WWCP_POI `987fa26` a certificate is for a model and names the
  releases among its components, still by their ids only),
  `RoamingNetwork/RoamingNetwork.cs:2368-2404` (`UnknownTransparencyReference` checks only that the id exists).
- **Problem:** before WWCP_POI `0c3c53c` the RNO owned both releases and certificates. Now the manufacturer alone may
  rewrite the `version` (or anything else) of a release that another party's certificate and the CSOs' meters name.
  A release carries no digest of the software, and a certificate pins no content of the release.
- **Impact:** station proofs and the API show a body's certificate "verifying" release R while R now describes a
  version the body never assessed, with no trace in the certificate.
- **Fix:** make `version` immutable (a new version is a new release), or let a certificate pin the release's content
  ETag or version; or refuse content changes of a release a certificate covers.

### S32. A conformity assessment body declares its own notified status and notified-body number
`design` · medium · **open; needs a design decision** (found 2026-10-24 at WWCP_POI `801bbce`, read from the code)

- **Where:** `WWCP_POI/WWCP_POI3/RoamingNetwork/RoamingNetwork.RegistryChanges.cs:230-233` (`notifiedBodyNumber`,
  `notifiedFor` changeable), `RoamingNetwork.Ownership.cs:960-978` (a party property other than its keys needs only
  `SignerOf(party)`, the body itself), `ConformityAssessmentBody.cs:354,371` (the number's format; certificates only
  under legislations of its own `notifiedFor`); no check that a `notifiedBodyNumber` is unique.
- **Problem:** the RNO vets a body when it adds it. Afterwards the body alone can add "MID" or "CRA" to `notifiedFor`,
  or take another body's number (e.g. 0102), and in the same change set issue certificates under that legislation:
  the only check, `NotifiedFor.Contains`, is met by its own edit. Once the body has keys, the RNO cannot correct these
  fields; it can only remove the body, in a change set every party naming its certificates signs as well
  (SPECIFICATION.md 4.1 since WWCP_POI `52f1172`), or recover its keys.
- **Impact:** proofs present a certificate under the MID from a body that "is notified for MID", with a forged
  notified-body number; example A13 prints exactly these fields as what to trust.
- **Fix:** let `notifiedBodyNumber` and `notifiedFor` be owned by the RNO (or need its co-signature); require
  network-unique notified-body numbers.


## Low

### S5. `createdAt` is chosen by the signer and may lie before the version it changes
`design` · low (was high) · **open in part; needs a design decision**

- **Fixed in part:** since WWCP_POI `f2ae688` a key must be valid at `createdAt` and when the server takes the change
  set, and a `createdAt` more than `MaxClockSkew` (5 minutes) after the server's time is refused
  (`History/RoamingNetworkHistory.cs:800-808` at `5a03f14`, the same for co-signing); since `be07037` the key of a
  price statement or an attestation must be valid at the time verified as well. An expired key no longer signs by
  a backdated timestamp, and a future one no longer pushes `lastChange` forward.
- **Left:** `createdAt` is still not compared with the parent's `lastChange`. A party may backdate a change set to
  any time its key was valid; that time becomes the `lastChange` of what it changes, earlier than the version
  before, and of manufacturers, transparency software manufacturers and conformity assessment bodies alike.
- **Fix:** require parent `lastChange` ≤ `createdAt`, or say in the specification that `lastChange` is the signer's
  claim only.

### S11. ECDSA signatures are not required to be low-S (malleable)
`security` · low (was high) · **open in part**

- **Where:** `Styx/Styx/Illias/COSE/COSEAlgorithm.cs:1376-1422` (`VerifyECDSA` accepts both S and n−S; unchanged at
  Styx `06b592d3`).
- **Fixed in part:** the price-statement case, the reason for "high", is fixed in WWCP_POI `dbf39b0`
  (csharp-review 2): the digest of a price statement leaves its signature value out, so a flipped twin is the same
  statement and no longer a split view.
- **Left:** a third party can still flip the S of a valid ECDSA signature to a second valid one. A history keeps the
  flipped copy of a trusted attestation as another one, as it compares the signature value
  (`WWCP_POI/WWCP_POI3/History/RoamingNetworkHistory.Attestations.cs:277` at `5a03f14`), which feeds the eviction of
  S29; anything else keyed by a signature value is open to the same.
- **Fix:** require canonical low-S ECDSA on verification (and reject non-canonical signatures), or compare
  attestations by their signature input.

### S24. Pending change sets, commit signatures and co-signing resources are only loosely bounded
`design` · low · **open**

- **Where:** `WWCP_POI/WWCP_POI3/History/RoamingNetworkHistory.PendingChangeSets.cs:154,320` (one global cap of 1000,
  up to 16 MiB each, no expiry), `History/RoamingNetworkHistory.cs:1053,1134-1166` (commit signatures: role/signer
  self-asserted, no cap).
- **Problem:** any party (of eight party types since WWCP_POI `801bbce`) can fill all 1000 pending slots (gigabytes,
  no expiry) and so block the RNO's own party additions; a trusted key can add unbounded commit signatures by varying
  `signedAt`; `RequiredSignatureRoles` compares only the claimed role text.
- **Fix:** a quota and expiry per party for pending change sets; tie commit-signature roles to keys and cap them.

### S25. A pushed merge commit keeps arbitrary change-set signatures
`bug` · low · **open**

- **Where:** `WWCP_POI/WWCP_POI3/History/RoamingNetworkHistory.cs:906` (merge compared with `IncludeSignatures:
  false`), `:934` (stored as sent).
- **Problem:** SPECIFICATION §5.3 says a merge's change set has no signatures, but a pusher may attach up to the
  limit; they are stored, served and covered by later commit signatures.
- **Fix:** refuse a merge whose change set carries signatures.

### S26. A withdrawn change set can be submitted again, and a withdrawal's time is the signer's
`design` · low · **open in part** (the binding to the content fixed by WWCP_POI `bc3b1bd`)

- **Where:** `WWCP_POI/WWCP_POI3/ChangeSet/RoamingNetworkChangeSetWithdrawal.cs:353-366`,
  `RoamingNetwork/RoamingNetwork.Ownership.cs:518` at `5a03f14` (key valid at the signer's `withdrawnAt` only),
  `History/RoamingNetworkHistory.PendingChangeSets.cs:407-` (no tombstone).
- **Fixed in part:** since WWCP_POI `bc3b1bd` the `@id` a withdrawal names is the SHA-256 of the change set's content,
  so a withdrawal is bound to the content and can no longer be replayed against another change set.
- **Left:** after a withdrawal anyone may submit the public change set again, and a completed quorum commits it; the
  key of a withdrawal is checked at its signer-chosen `withdrawnAt` only, not also when the server takes it, as
  change sets are since `f2ae688`.
- **Fix:** keep tombstones, require fresh signatures after a withdrawal, check the key when the withdrawal is taken.

### S27. "Suspend" does not revoke a peer's access, and SSH sessions survive account changes
`bug` · low · **open**

- **Where:** `RoamingHub/RoamingHub/RoamingHub.HubClientInfo.cs:200-212` (suspend only changes presence; the token
  stays ALLOWED); `WWCP_Node/WWCP_Node/CommandLine/NodeCLI.cs:171-192`, `WWCPNode.SSH.cs:595-694` (the account is
  checked once per session).
- **Problem:** a suspended peer still lists everyone and renews its credentials; an open SSH session keeps its rights
  and log stream after its account is disabled or its key removed (web sessions are cut in the same case).
- **Fix:** block a suspended peer's access token; re-check `CanAuthenticate` and the key before each SSH command/line.

### S29. Robustness and information leaks
`bug` · low · **open**

- **Where:** `WWCP_POI/WWCP_POI3/DataStructures/POIEffectiveTree.cs:365,469-476`,
  `Proofs/RoamingNetworkProof.cs:300,393` (an `@size`/`@index` above Int64 or an object `@id` throws out of
  `Verify`); `History/RoamingNetworkHistory.cs:781` and siblings (`:938,1079`, `.Attestations.cs:135`,
  `.PendingChangeSets.cs:339,423,531`, `.PriceStatements.cs:626`: store exception text with file paths in 422/WS
  errors); `WWCP_Node` signed log `Logging/SignedLog.cs:311-325` (a backward clock step across UTC midnight makes the
  log read as tampered); `NodeHTTPAPI.Log.cs:67` and `RoamingHub` (`ToReadTheLog` null: any signed-in account reads
  the full debug log); proof child-array completeness is not exposed (`RoamingNetworkProof.cs:563-597`), so an app may
  be shown fewer EVSEs, miss a tariff, or miss the model, release or certificate a station or meter names; one trusted
  attester can evict others' attestations (`History/RoamingNetworkHistory.Attestations.cs:44,254-255,417-418`).
- **Fix:** range/type-check before `Value<…>()` and return false; return generic error text and log details; order the
  signed log by id, not file name; add a `log:read` permission; expose per-array completeness in proofs; cap
  attestations per holder.

### S36. Release and certificate ids are unique in the network, not within their owner
`design` · low · **open** (found 2026-10-24 at WWCP_POI `801bbce`, read from the code)

- **Where:** `TransparencySoftware_Id` and `TransparencySoftwareCertificate_Id` are free text; unique in the network
  (`WWCP_POI/WWCP_POI3/RoamingNetwork/RoamingNetwork.cs:2252,2321`); the owner of an Add is whichever parent it names
  (`RoamingNetwork.Ownership.cs:1052-1061`).
- **Problem:** any transparency software manufacturer can take another's natural release ids (e.g. "chargy-1.4.2"),
  any body another's certificate number. The rightful owner can then not add them under that id, and meters and
  certificates, which reference by id alone, may be pointed at the squatter's entry.
- **Fix:** make the ids unique within their parent, as model ids are, or let references name the owner.


### S30. Informative (no change needed, or defence in depth)
`docs` · info

- Production builds publish source maps (`RoamingHub/.../Frontend/webpack.config.js:41`); the inherited
  `~/poi/serviceCheck` returns the machine name (`Hermod/Hermod/HTTP/HTTPAPI/HTTPAPI.cs:1222`); the OCPI/Configuration
  pages show server file paths, machine and OS (`RoamingHub/RoamingHub/RoamingHub.cs:563-565`) to any signed-in
  account; outbound OCPI/Mobilithek TLS does no revocation check; the Mobilithek `url` accepts `http`
  though its error text says https. The frontend was clean of XSS (lit-html escapes; the one `unsafeHTML` renders a
  regex-validated QR SVG), paths are pattern-checked, and the CLI reaches no shell.
