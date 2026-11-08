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

S5, S11 and S26 are fixed in part; what is left is said in each. The others still hold.

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

**S27 fixed on 2026-10-28** (WWCP_Node `f08fdee`, RoamingHub `57c9351`): suspending a peer sets every token of the hub
it holds to `BLOCKED` in the OCPI library, on every version it is peered on, all or none (`OCPIVersion.SetLocalAccess`);
resuming sets them `ALLOWED`. The library refuses a blocked token on every route - versions, credentials and their
renewal, hubclientinfo - and writes it to the peers' file, so a suspension the file cannot take is a 500 and changes
nothing, and a start reads a party with blocked tokens as `SUSPENDED`; a party added while suspended gets a blocked
token. An SSH session is asked again what let it in (`NodeSSHAuthenticator.WhyNoLongerIn`: the account there, not
switched off, holding a role; the key still the account's, switched on and valid) before every command that needs a
role and every `SSHSessionRecheckEvery` (5 s); where that fails, the session ends with its command and its log,
"Signed out: …", exit status 1 and a line in the log.

**S20 fixed on 2026-10-28** (Hermod `0fff6c9c`, WWCP_Node `9031bcf`, RoamingHub `1869910`; before, in part, WWCP_Node
`829896d`, RoamingHub `6824d54`): `accounts/`, `logs/`, `certificates/`, `ssh/`, `ocpi/`, `traffic/` and
`roamingNetworkOperator/` are their owner's alone (`WWCP_Node/OwnerOnly.cs`). The HTTPExt API keeps an API key, in
memory and in its database file, under the SHA-256 of the key (`APIKey_Id.Of`, Base64url), with its first six
characters as its hint, and finds a key presented by its hash; `AddAPIKey` takes only a key as `APIKey.AsKept()` makes
it, and `ADD .../APIKeys` the key itself as `"@id"`, answering with the kept form. A session is kept, in memory and in
its file, by the SHA-256 of its token (`Session.TokenHash`); the token itself is on the session `Create` returns alone,
for the cookie. Whoever reads those files can not sign in with what is there. A key is shown once, when it is made -
by `apiKeys add` and on the account page - and named by its hint and the beginning of its hash in lists and the log
(`alices... #41WyMKor`). There is no migration, as the maintainer chose: nothing runs in production yet, and an old
raw entry matches no key and no token.

**S31 and S32 decided and fixed on 2026-10-29** (WWCP_POI `d6b0127`), as the maintainer chose: the `version` of a
transparency software release is the one it was added with - a new version is a new release - so that a certificate
naming a release names the version that was assessed; a change set changing it, also by removing the release and
adding it again, is refused (`ChangedTransparencySoftwareVersion`). The `notifiedBodyNumber` and `notifiedFor` of a
conformity assessment body are the RNO's again, also once the body has keys: a body issuing a certificate under a
legislation the same change set adds to its `notifiedFor` signs it together with the RNO; a number names one body
(`DuplicateNotifiedBodyNumber`, also when a version is read). SPECIFICATION.md 4.1. Left: the other properties of a
release (`name`, `sourceCodeRepository`, `openSourceLicenses` ...) stay its manufacturer's to change.

**Checked again on 2026-10-29** at WWCP_POI `8108522` (RoamingHub `e271007`, WWCP_Node `3e2df0a`, Hermod `f3c0517c`,
Styx `f494c8cd`, WWCP_OCPI `1aee8c1a`, super `60cbf79`): every finding below still holds, and none of those fixed
has come back; the line numbers are those of these commits. What was left of the read-only API keys and of the masked
OCPI tokens fixed on 2026-10-23, beyond WWCP_Node and RoamingHub, is written down again as S37 and S38. Of the
"Left" above, S2/S9 (pushes bound by neither rule, `AuthorizeCommitPush` unset in the hub) and S13 (HSTS on the web
interface only, `WWCPNode.cs:1221-1224`) still hold as said.

**S25, S37 and S38 fixed on 2026-11-05** (WWCP_POI `7e2c198`, WWCP_Node `42ee899`, WWCP_OCPI `05534974`): a merge commit
whose change set is signed is refused where it is appended (`RoamingNetworkHistory.TryAppend`; its identification
leaves those signatures out, so a server holding the merge takes such a copy as known, and one taking it with its
parents refuses it). Below `/ext` a node refuses with 403 every request that is no read (GET, HEAD, OPTIONS) and
brings a read-only API key (`ReadOnlyAPIKeyPipeline`, next to `CrossSitePipeline`), as `NodeHTTPAPI.TryAuthorize`
does below `/api`; before, such a key added itself a key that may write (`ExtReadOnlyAPIKeyTests`). Hermod is left
as it is: a key made there is read-only unless said otherwise, and other programs write with such keys. The
credentials routes of WWCP_OCPI no longer name the token in their status messages, 27 of them in 2.1.1, 2.2.1, 2.3.0
and 3.0: already, not yet, not fully registered, blocked, not deleted, deleted, and no party having token A.

**S36 fixed on 2026-11-05** (WWCP_POI `045e180`, RoamingHub `68a94a5`), as the maintainer chose: the `@id` of a
transparency software release is the `@id` of its manufacturer, a colon and a name of its own without one
(`GraphDefined:chargy-1.5.0`), of a certificate the `@id` of the body issuing it alike (`PTB:2026-0042`, its number as a
document being its `documentNumber`), compared without case; a version or change set holding one identified otherwise
is refused, also when a version is read (`RoamingNetwork.ForeignTransparencySoftware`, `ForeignConformityCertificate`;
SPECIFICATION.md 4.1). So no manufacturer takes another one's release ids, no body another one's certificate numbers,
and two bodies numbering theirs alike both have them; references still name the id alone. A colon, not a slash: the
ids stand in URL paths (`.../TSMs/{TSM_Id}/TSs/{TS_Id}`), where Hermod routes `%2F` nowhere. The examples (A13) and
the documentation name their ids so; WWCP_POI.TS and the vectors of WWCPPOIConformanceTests follow.

**S11 and S29 fixed on 2026-11-05** (WWCP_POI `d35c780`, WWCP_Node `8cc1d2f`, RoamingHub `a918615`):
- S11: two signatures that sign the same by the same key are one, whatever their value: an attestation by its
  signature input (`RoamingNetworkAttestation.AttestsTheSameAs`), a commit or checkpoint signature by its role, signer,
  time, algorithm and key (`RoamingNetworkCommitSignature.SignsTheSameAs`, `NotYetIn`), where a history takes them, a
  commit or checkpoint adds them and where either is read. So the twin (r, n − s) of an ECDSA signature anybody can make
  is no further attestation and no further signature (`SignatureTwinTests`). Styx still verifies both forms, as neither
  BouncyCastle nor .NET signs low-S, and a co-signed change set naming one key twice is refused as before.
- S29: a size or index beyond 64 bits (a `BigInteger` to Newtonsoft) and an object as the identification of the roaming
  network make no proof and `Verify` says so instead of throwing (`POIEffectiveTree.IntegerOf`, `GivenRoamingNetworkId`);
  `RoamingNetworkProof.GivesCompletely` and `SizeOf` tell what a proof gives of a child array partially or not at all
  (`ProofRobustnessTests`); a client is told only that the store refused, its reason with its paths goes to
  `OnStoreError`; beyond `MaxAttestations` the oldest attestation of the key holding the most goes, so one attester
  evicts only its own; the signed log writes on into the file of the later day when the clock steps back across
  midnight, also over a restart (WWCP_Node); the log and its stream are `log:read` on a hub (`NodeResources.Log`),
  which `viewer`, `hub` and the administrators carry and an account without a role does not.

**S5, S24 and S26 decided and fixed on 2026-11-05** (WWCP_POI `12e993a`), as the maintainer chose:
- S5: no `lastChange` goes back. A change set is stamped with its `createdAt`, or with the `lastChange` of the roaming
  network of the version it is applied to, where that is later: as the `lastChange` of the network, of what it changes,
  adds and embeds, and as the `created` of what it adds without one (`RoamingNetwork.TryApplyChanges`; SPECIFICATION.md
  4, the docs of the entities). Nobody is refused for it, and its keys are checked at its `createdAt` still. It changes
  what a version holds, so WWCP_POI.TS and the vectors of WWCPPOIConformanceTests follow (three rebased change sets of
  `poi-histories.json`). Not solved by it: a change committed long after its `createdAt` shows a `lastChange` before a
  consumer's last pull, e.g. OCPI `date_from`; the OCPI export should take the time the hub saw a change.
- S24: a party has at most `MaxPendingChangeSetsPerParty` (50) change sets waiting, counted for the party of the first
  signature of each one, and a change set waits `PendingChangeSetExpiry` (30 days) at most, from when it came, kept in
  the store as `since` (this replaces the "no expiry" of 2026-10-22); the bound of 1000 stays. A server adding
  signatures to a commit or checkpoint it holds takes one of each key in each role, the first
  (`IsOfTheSameKeyAndRoleAs`); a commit or checkpoint read keeps all it comes with, and each is verified (WWCP_POI
  `f7b8643`, as WWCP_POI.TS; reading took one of each as well before and dropped a forged one unseen). A signature
  counts for a required role only by a key the version lets sign in it: a signing key certified for the RNO, or one of the
  charging station operator its `signer` names (`RoamingNetworkCommitSignatureRoles.KeyMaySignIn`); leaders and
  witnesses stay the verifier's to judge. SPECIFICATION.md 4.2 and 5.4.
- S26: a withdrawn change set, or one rebased from it, is refused again, waiting or committed at once, until the
  version it was made for is too old for a change set anyway, so the withdrawals kept bound themselves; they are kept
  in the store (`pendingChangeSetWithdrawn`) and survive a restart. Submitted again, a change set needs new
  signatures. The key of a withdrawal is checked when the server takes it as well. SPECIFICATION.md 4.2.
Tests: `PendingChangeSetBoundsTests`, `LastChangeTests`, `SignatureTwinTests.A_commit_keeps_one_signature_per_key_and_role`,
`SignatureTwinTests.A_commit_read_keeps_its_signatures_and_each_is_checked`.

**S39 found and fixed on 2026-11-06** (WWCP_POI `399b7c8`), as the maintainer chose: a history took a checkpoint
of another server only when every signature of it verified there, so one signature it did not verify refused the
checkpoint: anyone passing it on with a signature added blocked it, and so did a witness one server trusts and another
one does not, whose checkpoints that one never took; WWCP_POI.TS and the proofs pass such signatures over. Now a
checkpoint is taken with the signatures that verify there, one of each key and role, the others left out and refusing
nothing (`TryAddCheckpoint`, `RoamingNetworkCheckpoint.WithSignaturesOnly`); a forged one before a genuine one of the
same key and role keeps it from nothing. Refused still: a checkpoint not signed in a required role by one that
verifies, a signature in an algorithm the epoch does not accept (11.3), a signed one without a verifier.
`TryAddCheckpointSignatures` adds those that verify and is refused only when none does; a peer joins from a checkpoint
signed in the required roles by signatures that verify (`CarriesRequiredSignatures`). SPECIFICATION.md 10.2 and 10.5.
Test: `CheckpointTests.A_checkpoint_is_taken_with_the_signatures_that_verify_here`.

**S40 found and fixed on 2026-11-06** (WWCP_POI `b43ff1a`), found comparing with WWCP_POI.TS: C# read a commit or
checkpoint signature in forms TS refuses, so the two readers did not take the same histories. A signature naming
another property was read, that property signed by nobody and passed on by anyone; a `signer` that is a number or
null was read; a signature embedded in a commit or a checkpoint naming its `@context` was read; and the commit of a
snapshot, anchored or not, and a peer's join from a checkpoint, took signatures of it and of its change set in an
algorithm the epoch does not accept, which every commit taken later refuses. Now all four are refused
(`RoamingNetworkCommitSignature.TryParse` with `Embedded`, `CommitSignatureAlgorithmError` in `TryFromSnapshot`);
a signature on its own may still name its context. SPECIFICATION.md 5.4. Tests:
`SignatureFormTests.A_signature_is_read_in_its_one_form`,
`EpochTests.The_commit_of_a_snapshot_is_signed_in_algorithms_of_its_epoch`. Since WWCP_POI `7c6f91c` a
signature is also refused in the words of WWCP_POI.TS, in its order, and a snapshot names why its commit is refused
("Invalid commit of the snapshot: …"); all 1879 ZProbeRequiredRoles cases give the same verdict in the same words.
Test: `SignatureFormTests.A_signature_is_refused_in_the_words_of_WWCP_POI_TS`.

**S41 found and fixed on 2026-11-08** (WWCP_POI `e76e5d0`), found by the probes of WWCP_POI.TS: an operator took a
charging station given in two of its charging pools, or an EVSE given in two of its charging stations, and its index
kept the first. Its refusals caught the exception of `ImmutableDictionary.CreateRange`, which throws only for a key
given twice with values that are not equal, and charging stations, EVSEs and energy meters are equal by their
identifications alone, so they were never reached. Proofs, price statements and evidence name them by their
identifications alone, so they could not say which of the two was meant; WWCP_POI.TS refuses both. Now the indexes
refuse an identification given twice and name it ("The charging station '...' of the operator '...' must not be
given twice!", the same for an EVSE and an energy meter), and a network holding one is refused when it is read.
With Hermod `f7b00ef2`, data license identifications that differ in case only are found twice too: they compared
without case but hashed with it. Test: `InfrastructureTests.A_station_an_EVSE_or_a_meter_is_given_once_per_operator`.

Items marked **needs a design decision** wait on the maintainer. Severity: **critical** means the trust model is
broken; **high** means wrong results, a freeze or a denial of service; **medium** and **low** as usual.


## Low

### S30. Informative (no change needed, or defence in depth)
`docs` · info

- Production builds publish source maps (`RoamingHub/.../Frontend/webpack.config.js:41`), of code that is AGPL and
  public anyway; the inherited `~/poi/serviceCheck` returns the machine name (`Hermod/Hermod/HTTP/HTTPAPI/HTTPAPI.cs:1222`),
  left as it is, as Hermod's other programs serve it too; the Configuration page shows server file paths, machine and
  OS (`RoamingHub/RoamingHub/RoamingHub.cs:579-584`), since `configuration:read` (`NodeHTTPAPI.cs:383`) to the roles
  `viewer` and `hub` and administrators only, no longer to any signed-in account; outbound OCPI/Mobilithek TLS does no
  revocation check (`CertificateRevocationCheckMode` of Hermod's `ATLSClient` is set nowhere): a check that is strict
  would end the connections whenever an OCSP or CRL server is out of reach, so it waits for a switch per peer. The
  Mobilithek `url` takes `https` alone since RoamingHub `68a94a5`. The frontend was clean of XSS (lit-html escapes; the
  one `unsafeHTML` renders a regex-validated QR SVG), paths are pattern-checked, and the CLI reaches no shell.
