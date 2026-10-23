# Security audit 2026-10-23

The open findings of a security review of RoamingHubCLI and above all WWCP_POI, asked for on 2026-10-22, at
WWCP_POI `5832482` (super `567c2d8`). The findings already fixed (network-wide unique party keys and a co-signing
signature limit in WWCP_POI `6661d2d`, read-only API keys in WWCP_Node `7dc7c84`, masked OCPI tokens in RoamingHub
`07aa584`, super `cb56283`) have been taken out of this list.

The critical and high WWCP_POI findings were verified end to end, several by proof-of-concept tests; the
WWCP_Node, Hermod, RoamingHub and proof/checkpoint findings were read from the code with the file and line cited
(line numbers as of 2026-10-22). Paths name their repository. Items marked **needs a design decision** wait on the
maintainer. Severity: **critical** means the trust model is broken; **high** means wrong results, a freeze or a
denial of service; **medium** and **low** as usual.


## Critical

### S2. The head is the highest-revision tip, so one party can roll back everyone else
`design` · critical · **open; needs a design decision**

- **Where:** `WWCP_POI/WWCP_POI3/History/RoamingNetworkHistory.cs:1219-1222` (tips sorted, head = `tips[0]`),
  `:1548-1560` (`IsCanonicalOrder`, higher revision first), `:1262-1311` (conflicting tips stay);
  the RoamingHub attests every new head (`RoamingHub/RoamingHub/RoamingHub.Attestations.cs`).
- **Problem:** change sets may be committed onto any retained version (`FindVersionFor`,
  `RoamingNetworkHistory.Persistence.cs:685-719`). A party makes a branch off an old version that conflicts with
  the main line on a property it owns, then extends it with its own changes until its revision is the highest. That
  tip becomes the head.
- **Impact:** every other party's commits since the fork fall out of the head, and a party the RNO removed returns.
  `GET`, the DATEX II publication, the runtime status and the RNO-signed attestations then all show the attacker's
  version. Repeatable. Proven by PoC tests.
- **Fix:** do not choose between conflicting tips by a revision one owner can inflate; keep the previous head's line
  until the conflict is resolved, and/or bound how old a version a branch may fork from.

### S3. A change set made for an old version is merged into the head without re-checking keys, so nothing is revoked
`design` · critical · **open; needs a design decision**

- **Where:** `WWCP_POI/WWCP_POI3/RoamingNetwork/RoamingNetwork.Ownership.cs:365-390` (ownership checked against
  the version the change set applies to), `History/RoamingNetworkHistory.cs:772-776`, `:926-927`,
  `RoamingNetwork.Merge.cs` (the three-way merge does no ownership check).
- **Problem:** a change set is checked against the keys of the version it is committed to. A commit made for a
  version before a key rotation, a certificate removal or a party removal is accepted as a branch and then merged
  into the head, without re-checking it against the head's keys. Contradicts SPECIFICATION §8.6 ("a key is revoked
  by removing its certificate"). The window is the whole retained history (in the hub ≥ 1000 revisions).
- **Impact:** a leaked or withdrawn signing key, a removed RNO signing-key certificate, or a replaced compromised
  identity key keeps working. Proven by a PoC test (removed key still changes the head).
- **Fix:** re-verify a branch's change sets against the head's keys when merging and treat a failure as a conflict,
  or accept change sets only for the tips / recent versions.


## High

### S5. `createdAt`/`signedAt` are chosen by the signer, which neutralises `notBefore`/`notAfter`
`design` · high · **open; needs a design decision**

- **Where:** `WWCP_POI/WWCP_POI3/RoamingNetwork/RoamingNetwork.Ownership.cs:532` (`key.IsValidAt(ChangeSet.CreatedAt)`);
  `createdAt` is never compared with the parent's `lastChange` or with server time; the same for price statements'
  `signedAt` (`History/RoamingNetworkPriceStatement.cs`).
- **Problem:** key validity is checked at a timestamp the signer writes. The holder of an expired but still-listed
  key backdates `createdAt` into its validity; a future `createdAt` pushes `lastChange` and merge timestamps forward.
  Proven by a PoC test.
- **Fix:** require parent `lastChange` ≤ `createdAt` ≤ now + a small skew.

### S6. The offline quorum counts holders taken from the key statement being verified
`design` · high · **open; needs a design decision**

- **Where:** `WWCP_POI/WWCP_POI3/Proofs/RoamingNetworkKeyStatement.cs:621-659`,
  `Proofs/RoamingNetworkTrustAnchor.cs` (pins only key @ids and `minConsensus`, no holder grouping).
- **Problem:** a trust anchor pins only identity-key @ids and a number. When counting the holders that signed a key
  statement, the holder label of each key is read from the statement itself. One company holding several pinned
  identity keys (e.g. during the rotation overlap §4.1 recommends) can label them as different holders and reach the
  quorum alone. Proven by a PoC against the trust-anchor path.
- **Fix:** the trust anchor must pin the holder grouping (a new format), and the verifier must count by the pinned
  groups and refuse a statement that relabels pinned keys.

### S7. Expiry and revocation can not be enforced by offline verifiers
`design` · high · **open; needs a design decision**

- **Where:** `WWCP_POI/WWCP_POI3/History/RoamingNetworkAttestation.cs:417-419` (`IsValidAt` has no lower bound and no
  required `validUntil`), `Proofs/RoamingNetworkProof.cs:381-391` (keys/certificates taken from the proof),
  `Proofs/RoamingNetworkKeyStatement.cs`, `Proofs/RoamingNetworkPriceStatement.cs`.
- **Problem:** an attestation needs no `validUntil`, a key statement's `validUntil` has no upper bound, validity is
  checked only at the signer-chosen `signedAt`, and a dropped certificate still verifies in an old proof. A leaked
  old signing key plus a backdated attestation verifies against a pinned identity key. Proven by a PoC.
- **Fix:** require a short-lived `validUntil` and `signedAt` ≤ verification time; require keys and certifying
  identity keys valid at verification time; add an offline revocation horizon.

### S9. A party can make a conflicting branch the head to roll back and resurrect data (DoS of consensus)
`security` · high · **open; needs a design decision (shares the root of S2/S3)**

- **Where:** `WWCP_POI/WWCP_POI3/History/RoamingNetworkHistory.cs:796,954,1016,1275-1283` (every tip pair retried on
  each commit), `:1462-1486`, `Persistence.cs:414-426` (merge base O(N²)).
- **Problem:** nothing bounds the number of conflicting tips. A party commits many mutually conflicting tips off one
  old version. Every later commit or push then runs O(T²) failing three-way merges, each O(N²), under `historyLock`,
  and a branch forked at the root stops compaction for good.
- **Fix:** cap branches per party and by age, cache pair results until a tip changes, compute merge bases
  incrementally.

### S10. The peer trusts a snapshot by a name check only, so a server can replace or roll back its history
`design` · high · **open (affects library users of `RoamingNetworkPeer`; the hub does not run a peer)**

- **Where:** `WWCP_POI/WWCP_POI3/Sync/RoamingNetworkPeer.cs:943-952` (trust = "a trusted key id appears in the
  snapshot", no signature), `:878-882`, `History/RoamingNetworkHistory.cs:497-531`,
  `Sync/RoamingNetworkPeer.Checkpoints.cs:150-160`, `Sync/RoamingNetworkPeer.Epochs.cs:46-73`.
- **Problem:** `TrustedRoamingNetworkOperatorKeyIds` only checks that a trusted key id is present in the snapshot
  (public keys are public); no signature is verified. A malicious server or a MITM on `ws://` can feed a fabricated
  network, a rolled-back snapshot (a `commits` NotFound forces a re-snapshot that overwrites the store and loses
  unpushed local commits), or a fake next epoch.
- **Fix:** anchor snapshots in a signature by the trusted keys (an attestation or checkpoint of that commit), require
  descent from the current head before replacing a history, never downgrade silently, and fail closed when a verifier
  is configured.

### S11. ECDSA signatures are not required to be low-S (malleable)
`security` · high · **open**

- **Where:** `Styx/Styx/Illias/COSE/COSEAlgorithm.cs:1376-1422` (`VerifyECDSA` accepts both S and n−S).
- **Problem:** a third party can flip the S of a valid ECDSA signature to a second valid one. For a price statement
  this yields a second, differently-serialised statement of the same entity and sequence, which the chain rule treats
  as a split view and refuses on other servers.
- **Fix:** require canonical low-S ECDSA on verification (and reject non-canonical signatures).

### S12. Public, unauthenticated reads can exhaust CPU, memory and the history lock
`security` · high · **open**

- **Where:** `WWCP_POI/WWCP_POI3/WebSocket/POIWebSocketServer.cs:684-750` (batches of up to 1000 heavy reads, no
  cancellation), `API/POI_API.Entities.cs:2095-2187` (up to ~16,383 cached `?without=` views per operator),
  `API/POI_API.Peers.cs:273-311` with `History/RoamingNetworkHistory.cs:1715-1736` (`have` lists unbounded, O(K·N)
  under the lock), `History/RoamingNetworkHistory.PendingChangeSets.cs:195-209` (a read re-verifies every pending
  signature under the lock), `API/POI_API.Query.cs:71-101` (unbounded `match`/`limit`).
- **Problem:** several public endpoints do attacker-scaled work, some under `historyLock`, so commits and sync stall.
- **Fix:** restrict batches to write/status actions and add a byte budget; cache snapshots and `?without=` views per
  version with a bound; cap and deduplicate `have`; cache pending-signature verification; cap `match`/`limit`.

### S13. The hub serves its web interface, API and sign-in over plain HTTP only
`design` · high · **open**

- **Where:** `RoamingHub/RoamingHub/RoamingHub.cs:273-296` (no server certificate),
  `WWCP_Node/WWCP_Node/WWCPNode.cs:943,1019` (`UseSecureCookies` only with HTTPS),
  `CommandLine/NodeArguments.cs:183-187` (`--any`).
- **Problem:** with `--any` (needed for OCPI peers) the login password, Basic-auth and API-key headers, and the
  30-day session cookie (no `Secure` flag) all cross the network in clear text.
- **Fix:** serve the stored TLS identity with HSTS, or refuse/warn loudly when binding beyond loopback without TLS.


## Medium

### S15. A change set's @id and `rebasedFrom` are trusted, so one party can make another's change set vanish
`security` · medium · **open**

- **Where:** `WWCP_POI/WWCP_POI3/History/RoamingNetworkHistory.cs:1786-1793` (`Remember` marks every id and every
  `metadata.rebasedFrom[].@id` committed, without comparing content), `:1805-1836`,
  `History/RoamingNetworkHistory.PendingChangeSets.cs:486-492`.
- **Problem:** pending change sets are public. A party commits a change set of its own whose `@id` equals a victim's,
  or whose `rebasedFrom` lists victims' ids. The victims' change sets are then dropped and reported `committed` with
  the attacker's commit. Proven by a PoC (the RNO's removal of a party is reported committed but never applied).
- **Fix:** compare content before treating an id as committed or forgetting a pending change set; honour
  `rebasedFrom` only for rebases this server verified.

### S16. A charging-station-operator "subject" attestation leaves the root unattested and its role is not checked
`bug` · medium · **open**

- **Where:** `WWCP_POI/WWCP_POI3/Proofs/RoamingNetworkProof.cs:220-242,269-274,385-391`.
- **Problem:** a proof verified with a CSO subject attestation (or, on the trust-anchor path, any RNO-certified key
  with role `chargingStationOperator`) passes while the network name, the RNO, the energy-meter manufacturers and the
  transparency software are fabricated. Root and subject attestations of different roots are counted together.
  Proven by a PoC.
- **Fix:** require `eTag` == root for a subject attestation or restrict `Given()` to the attested subject; the
  trust-anchor path must require role `roamingNetworkOperator` and no subject.

### S17. Price-statement chains are keyed by raw entity text, and storage is unbounded per operator
`bug` · medium · **open**

- **Where:** `WWCP_POI/WWCP_POI3/History/RoamingNetworkHistory.PriceStatements.cs:586,603-613,664-667,917`,
  `API/POI_API.PriceStatements.cs:219-237` (no authorization beyond the operator's signature).
- **Problem:** the current statement is keyed by the raw `entityId` text, but EVSE ids compare equal ignoring `*`,
  suffix case and whitespace. "DE*GEF*E1*1" and "DE*GEF*E11" are taken as two chains of one EVSE, so a split view
  goes unnoticed; spellings are unlimited, so are chains and dictionary entries. One operator posting >100,000
  statements between checkpoints evicts others' statements from the checkpoint (lost §12.4 evidence); a sequence of
  2^64−1 ends a chain for good.
- **Fix:** key chains by the canonical typed id and refuse a non-canonical `entityId`; per-operator quotas, no
  cross-operator eviction, bound sequence jumps with checked arithmetic.

### S18. Witnesses sign checkpoints they could not check
`bug` · medium · **open**

- **Where:** `WWCP_POI/WWCP_POI3/History/RoamingNetworkHistory.Checkpoints.cs:424-461,870-891,212`.
- **Problem:** a witness that joined from a checkpoint takes its first checkpoint without recomputing `commits` or
  checking `sequence`/`previousId`, and `TryWitness` signs it. A malicious RNO can bootstrap witnesses with a
  checkpoint whose commits tree holds a fabricated commit, which then counts as witnessed (§10.4). A first checkpoint
  of `sequence` 2^64−1 overflows `latest.Sequence + 1`.
- **Fix:** mark checkpoints taken unchecked and refuse to witness them; require `sequence` 0 for the first checkpoint;
  checked arithmetic.

### S19. SSRF through URLs a peer supplies during the OCPI credentials handshake
`design` · medium · **open**

- **Where:** `WWCP_OCPI/WWCP_OCPIv2.2.1/Common/CommonAPI.cs:3238-3311` (used by
  `RoamingHub/RoamingHub/OCPI/OCPIv2_2_1.cs`), no filtering hook in `RoamingHub/RoamingHub/RoamingHub.OCPI.cs`.
- **Problem:** a holder of token A (or C via a PUT) makes the hub GET `credentials.url` and the version-details URL
  through its DNS client with the peer's `Authorization` header; loopback, private and link-local addresses are not
  filtered, and different error texts make a port/service oracle.
- **Fix:** refuse non-public addresses for peer-supplied URLs unless the configuration allows them, and use uniform
  error text.

### S20. Secrets and private keys at rest are world-readable by default; `traffic/` is not git-ignored
`design` · medium · **open**

- **Where:** `RoamingHub/RoamingHub/RoamingHub.RoamingNetworkOperator.cs:749-771` (RNO keys unencrypted PEM, no ACL on
  Windows, chmod after write on Unix), `WWCP_Node` accounts/certificates/logs directories beside the repository root
  (`Protect()` returns early on Windows), session tokens and API keys stored raw, `.gitignore:309-311` (does not
  ignore `/traffic/`).
- **Problem:** on Windows under a drive-root folder, Authenticated Users inherit read/modify, so any local user can
  read the RNO identity key, session tokens or TLS keys; a `git add -A` would commit `traffic/` with payload tokens
  and CDRs.
- **Fix:** create `accounts/`, `certificates/`, `logs/`, `ocpi/`, `traffic/` owner-only on every platform; store
  tokens and API keys hashed; add `/traffic/` to `.gitignore`.

### S21. Unauthenticated input is written raw into every log sink (log injection, memory growth)
`security` · medium · **open**

- **Where:** `WWCP_Node/WWCP_Node/WWCPNode.SSH.cs:752-763` with `Hermod/Hermod/SSH/Auth/UserAuthentication.cs:174`
  (raw username/method of every failed SSH attempt), `WWCP_Node` `Logging/ConsoleLog.cs:150`, `Logging/FileLog.cs:246`
  (no escaping); also change-set descriptions (`RoamingHub.POI.cs:283-287`) and OCPI status messages.
- **Problem:** CR/LF forge log lines and ESC/OSC sequences reach the operator's terminal; SSH is on by default and
  each attempt carries up to ~256 KiB, and the event log/stream buffers grow.
- **Fix:** escape CR, LF and C0/C1 control characters and cap peer-supplied strings centrally in the log writers;
  aggregate failed sign-in lines.

### S22. Two password doors skip the sign-in rate limit
`security` · medium · **open**

- **Where:** `WWCP_Node/WWCP_Node/SecureShell/NodeSSHAuthenticator.cs:130-146` (SSH `VerifyPassword` holds the global
  `UsersSemaphore` through 600,000 PBKDF2 rounds, no per-IP/account limit), `Hermod/.../HTTPExtAPI.cs:7686-7840`
  (legacy `SET /ext/users/{id}/password` verifies the current password with no rate limit).
- **Problem:** when `ssh.passwords` is on, unauthenticated guesses are unthrottled and block legitimate verification
  via the shared semaphore; the legacy route lets a cookie/API-key holder guess the current password at hash speed
  (account takeover).
- **Fix:** ration both doors (per IP and account), verify outside the semaphore with a dummy hash for unknown names.

### S23. Cross-site POST and WebSocket requests reach the mutating routes
`design` · medium · **open**

- **Where:** `WWCP_POI/WWCP_POI3/API/POI_API.History.cs:461-466`, `API/POI_API.CoSigning.cs:438-442`,
  `WebSocket` upgrade (no `AllowedOrigins`); the node's `/ext` routes parse JSON regardless of content type.
- **Problem:** POST handlers accept any non-CBOR content type as JSON, so a "simple" cross-site POST reaches them
  without a preflight, and the WebSocket has no Origin check. Harmless where writes need owner signatures, but real
  CSRF/CSWSH where a deployment authenticates by cookie, Basic auth, client certificate or IP.
- **Fix:** require `application/json`/`application/cbor` on POSTs; set `AllowedOrigins` on the WebSocket endpoints.


## Low

### S24. Pending change sets, commit signatures and co-signing resources are only loosely bounded
`design` · low · **open**

- **Where:** `WWCP_POI/WWCP_POI3/History/RoamingNetworkHistory.PendingChangeSets.cs:154` (one global cap of 1000, up
  to 16 MiB each, no expiry), `History/RoamingNetworkHistory.cs:1065,1145-1177` (commit signatures: role/signer
  self-asserted, no cap).
- **Problem:** any party can fill all 1000 pending slots (gigabytes, no expiry) and so block the RNO's own party
  additions; a trusted key can add unbounded commit signatures by varying `signedAt`; `RequiredSignatureRoles`
  compares only the claimed role text.
- **Fix:** a quota and expiry per party for pending change sets; tie commit-signature roles to keys and cap them.

### S25. A pushed merge commit keeps arbitrary change-set signatures
`bug` · low · **open**

- **Where:** `WWCP_POI/WWCP_POI3/History/RoamingNetworkHistory.cs:918` (merge compared with `IncludeSignatures:
  false`), `:946` (stored as sent).
- **Problem:** SPECIFICATION §5.3 says a merge's change set has no signatures, but a pusher may attach up to the
  limit; they are stored, served and covered by later commit signatures.
- **Fix:** refuse a merge whose change set carries signatures.

### S26. A change-set withdrawal is not bound to the content and can be replayed
`design` · low · **open**

- **Where:** `WWCP_POI/WWCP_POI3/ChangeSet/RoamingNetworkChangeSetWithdrawal.cs:353-366`,
  `History/RoamingNetworkHistory.PendingChangeSets.cs:412-415`.
- **Problem:** a withdrawal names no digest and its timestamp is signer-chosen; after a withdrawal anyone may
  resubmit the public change set and a completed quorum commits it, and a withdrawal can be replayed against a later
  change set of the same @id.
- **Fix:** sign the change set's digest, keep tombstones, require fresh signatures after a withdrawal.

### S27. "Suspend" does not revoke a peer's access, and SSH sessions survive account changes
`bug` · low · **open**

- **Where:** `RoamingHub/RoamingHub/RoamingHub.HubClientInfo.cs:200-212` (suspend only changes presence; the token
  stays ALLOWED); `WWCP_Node/WWCP_Node/CommandLine/NodeCLI.cs:171-192`, `WWCPNode.SSH.cs:595-694` (the account is
  checked once per session).
- **Problem:** a suspended peer still lists everyone and renews its credentials; an open SSH session keeps its rights
  and log stream after its account is disabled or its key removed (web sessions are cut in the same case).
- **Fix:** block a suspended peer's access token; re-check `CanAuthenticate` and the key before each SSH command/line.

### S28. `peers:run` returns every peer's tokens on registration
`bug` · low · **open**

- **Where:** `RoamingHub/RoamingHub/HTTPAPI/RoamingHubHTTPAPI.OCPI.cs:184,202` (authorizes `Run(Peers)` but answers
  `RemotePartiesJSON(IncludeSecrets: true)`).
- **Problem:** the README gives tokens only to `peers:edit`, but a role with `peers:read` + `peers:run` gets every
  token by pressing Register.
- **Fix:** `IncludeSecrets: IsAllowed(user, Peers, Edit)`.

### S29. Robustness and information leaks
`bug` · low · **open**

- **Where:** `WWCP_POI/WWCP_POI3/DataStructures/POIEffectiveTree.cs:353,457-464`,
  `Proofs/RoamingNetworkProof.cs:300,393` (an `@size`/`@index` above Int64 or an object `@id` throws out of
  `Verify`); `History/RoamingNetworkHistory.cs:793` and siblings (store exception text with file paths in 422/WS
  errors); `WWCP_Node` signed log `Logging/SignedLog.cs:311-325` (a backward clock step across UTC midnight makes the
  log read as tampered); `NodeHTTPAPI.Log.cs:67` and `RoamingHub` (`ToReadTheLog` null: any signed-in account reads
  the full debug log); proof child-array completeness is not exposed (`RoamingNetworkProof.cs:563-597`), so an app may
  be shown fewer EVSEs or miss a tariff; one trusted attester can evict others' attestations
  (`History/RoamingNetworkHistory.Attestations.cs:44,262`).
- **Fix:** range/type-check before `Value<…>()` and return false; return generic error text and log details; order the
  signed log by id, not file name; add a `log:read` permission; expose per-array completeness in proofs; cap
  attestations per holder.

### S30. Informative (no change needed, or defence in depth)
`docs` · info

- Production builds publish source maps (`RoamingHub/.../Frontend/webpack.config.js:41`); the inherited
  `~/poi/serviceCheck` returns the machine name; the OCPI/Configuration pages show server file paths and OS to any
  signed-in account; outbound OCPI/Mobilithek TLS does no revocation check; the Mobilithek `url` accepts `http`
  though its error text says https. The frontend was clean of XSS (lit-html escapes; the one `unsafeHTML` renders a
  regex-validated QR SVG), paths are pattern-checked, and the CLI reaches no shell.
