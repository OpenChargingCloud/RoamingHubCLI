# WWCP_OCPI side findings 2026-10-24

The open side findings that sessions working on WWCP_OCPI and the hub reported between 2026-10-02 and 2026-10-04 and
did not fix, checked again on 2026-10-24 at WWCP_OCPI `42669383` and RoamingHub `10d8a56` (super `a0c1841`), with
those found while checking them. Every finding was read from the code with the file and line cited; nothing was run.
Paths are relative to `libs/WWCP_OCPI` unless they start with `RoamingHub/` (`libs/RoamingHub`).

**What the hub serves:** OCPI 2.2.1 and 2.3.0 only (`RoamingHub/RoamingHub.OCPI.cs:363-369` refuses any other
version), and of them only the `CommonAPI` (`RoamingHub/OCPI/OCPIv2_2_1.cs:72-96`, `OCPIv2_3_0.cs:72-102`; no
CPO/EMSP/HUB HTTP API, see the comments at `OCPIv2_2_1.cs:98-110`, `OCPIv2_3_0.cs:104-116`): GET `/versions`,
`versions/{v}`, `{v}/credentials` (GET/POST/PUT/DELETE) and 2.3.0 `hubclientinfo`. It sets `LocationsAsOpenData` to
`false` unless configured (`RoamingHub/RoamingHub.OCPI.cs:310`); the library default is `true`
(`WWCP_OCPI_Common/HTTP/CommonHTTPAPI.cs:651`). Each finding says whether the hub is exposed.

Related findings in [POI3-ISSUES.md](POI3-ISSUES.md), all fixed: S19 (SSRF through the URLs a peer gives in the
credentials handshake), S27 (a suspended peer kept its access) and S28 (`peers:run` returned every peer's tokens).

**O1 fixed on 2026-10-28** (WWCP_OCPI `6da6aa86`, with `2cbf367f` restoring source maps it took along by mistake),
as the maintainer chose: `TryRegister` and `TryPutCredentials` (2.1.1, 2.2.1, 2.3.0) take back the token they stored
before sending where the attempt definitely failed - nothing was sent, or the other side answered without
credentials. Where it was sent and no answer came (no response, Hermod's ClientError, 408, 502, 504) it stays, marked
`LocalAccessInfo.Pending` (`"pending": true` in the file), and the next `TryRegister` without a token of its own
sends it again, after a restart as well. A renewal answered with other roles is left to O5.

**O2 fixed on 2026-10-28** (WWCP_OCPI `0b7483b6`): `AddOCPIMethod` of 2.1.1, 2.2.1, 2.3.0 and 3.0 refuses a request
whose token is of nobody here, or whose TOTP is not its party's, with 401 and why before the route is asked
(`OCPIRequest.IsRefusedBeforeTheRoute`, `TokenRefused`); 2.1.1's `TryGetRemoteParties` now says why it found none.
`LocationsAsOpenData` no longer drops "Unknown access token!" for every route: a route of open data says so
(`OpenData`) - GET locations and tariffs of the CPO, EMSP and HUB APIs, with `LocationsAsOpenData`/`TariffsAsOpenData` -
and takes an unknown token as no token, never a wrong TOTP. The shared `/versions` refuses an unknown token with 401
whatever the open data, and a token its table blocks with 401 and "Invalid or blocked access token!" (was 200). The
hub's `versions/2.2.1` answers a stale token 401.

**O4 fixed on 2026-10-29** (WWCP_OCPI `72507f7f`): 3.0's POST/PUT credentials takes over 2.2.1's order. It makes its
answer first and refuses, OCPI 3000 with HTTP 500, where this platform has no parties to name (O3) before anything is
changed; it then stores token C in place of token A in one line and takes token A from any other party only once
that line is in its file. Where the file refuses it, the change is taken back and the peer is answered 3000 with 500,
its token A still valid. How the peer is reached - TLS, timeouts, retries, user agent, the validity of its token - and
the versions it sees are kept. Every line of 3.0's file of the remote parties is now written first-hand, not through
the queue of the assets, with `OnRemotePartyNotSaved` as in 2.x; a change 3.0 keeps where its file refused it (a
registration the other side has answered) is not yet written down later as 2.x does. Two things in 3.0's
`CommonClient` came up with it: it never set its `RemoteVersionsURL`, so no 3.0 client could fetch versions and no
handshake could ever go through, and it did not take the `RemoteNetworkAcl` of S19 (POI3-ISSUES.md). Both are fixed;
`WWCP_OCPIv3.0_Tests/CommonTests/CredentialsHandshakeTests.cs`.

Items marked **needs a design decision** wait on the maintainer. Severity as in POI3-ISSUES.md.


## Medium

### O3. OCPI 3.0 has no parties of its own and is unknown to the shared versions API
`bug` · medium · **open** · hub not exposed (no 3.0)

- **Where:** `WWCP_OCPIv3.0/Common/CommonAPI.cs:1924-1927` (`OurCredentialRoles` checked, its assignment commented out),
  `:1967-1968` (`//foreach (var partyData in OurPartyData) parties.TryAdd(...)`), `:2491-2494` (GET credentials builds
  `Credentials` of `parties.Values`; `DataStructures/Complex/Credentials.cs:77-78` throws on no roles), `:1951` (its
  own `AddVersionInformation`, `:7375-7390`, not `BaseAPI.AddVersionInformation` as 2.1.1 `:2199`, 2.2.1 `:2287`,
  2.3.0 `:4212`), no `BaseAPI.AddRemotePartyProvider` (2.2.1 `:2302`), `:1876` (`LocationsAsOpenData` unused).
- **Problem:** GET credentials answers 3000 unless parties are added by hand; the shared `/versions` lists no 3.0 and,
  looking tokens up in `KnownRemoteParties` (`WWCP_OCPI_Common/HTTP/CommonHTTPAPI.cs:3035`), knows no 3.0 peer's
  token.
- **Impact:** a 3.0 platform on the shared API cannot be found or used.
- **Fix:** store `OurCredentialRoles` as parties in the constructor as 2.x does; register the version information and
  the remote parties with the `BaseAPI`; answer an empty set of parties with an error, not an exception.


## Low

### O5. A renewal answer with other roles switches our client to the new token but stores nothing
`bug` · low · **open** (found 2026-10-24) · hub not exposed (it renews nothing)

- **Where:** `WWCP_OCPIv2.2.1/Common/CommonHTTPClient.cs:1745` (`TokenAuth = TokenAuthFor(response.Data.Token)`) before
  the roles check (`:1751`, `:1776`); 2.3.0 `:1738`.
- **Problem:** where the roles differ nothing is stored, but the peer has already switched to the new tokens: our
  file still holds its old token, so after a restart, or with a new client, we call it with a dead one.
- **Fix:** switch `TokenAuth` only after the new token was stored, or store the peer's new token also on a roles
  mismatch and report the mismatch.

### O6. Registering and renewing drop the settings of our local token
`bug` · low · **open** (found 2026-10-24) · hub not exposed today (it configures no TOTP)

- **Where:** `WWCP_OCPIv2.2.1/Common/CommonHTTPClient.cs:2415-2421` (`TryRegister`: `null` for
  `LocalAccessTokenBase64Encoding`, `LocalTOTPConfig`, `LocalAccessNotBefore`/`NotAfter`), `:1795-1828`
  (`TryPutCredentials`), `Common/CommonAPI.cs:3415` (the server side of the handshake).
- **Problem:** a TOTP or a validity window an operator gave a peer's local token is gone with the new token.
- **Fix:** carry the local settings over to the new token, as the remote ones are since `e637321c`.

### O7. Modules are announced without routes
`bug` · low · **open** · hub not exposed (its parties are HUB only)

- **Where:** charging profiles announced in 2.2.1 `Common/CommonAPI.cs:2514-2522` (EMSP) and `:2601-2609` (CPO, when
  `request.RemoteParty is not null`, `:2589`), 2.3.0 `:4412-4420`, `:4493-4500` (`:4481`), 3.0 `:2178-2181`, `:2283`;
  the routes are comments only (2.2.1 `CPO/CPO_HTTPAPI.cs:3283-3309`, `EMSP/EMSP_HTTPAPI.cs:15993-16024`, 2.3.0
  `CPO/CPO_HTTPAPI.cs:3650-3676`, `EMSP/EMSP_HTTPAPI.cs:8655-8686`). 3.0 has no CPO/EMSP routes at all (active
  `AddOCPIMethod` calls only in `Common/CommonAPI.cs` and `SCSP/SCSPAPI.cs`), yet announces those modules for parties
  of those roles.
- **Problem:** what is announced follows the roles of the party, not the module APIs that were built, so a peer that
  trusts the version details gets 404 for smart charging (and in 3.0 for every CPO/EMSP module).
- **Fix:** announce the modules of what was built, as `HUBModulesPath` does for the HUB modules since `a2f0b038`;
  until then leave out charging profiles.

### O8. OCPI 3.0 registers its routes as templates of the version id
`bug` · low · **open; not verified by running** (found 2026-10-24) · hub not exposed

- **Where:** `WWCP_OCPIv3.0/Common/CommonAPI.cs:2032` (`"versions/{versionId}"`), `:2439`, `:2526`, `:2574`, `:2651`
  (`"{versionId}/credentials"`); only version details checks the id (`:2096`).
- **Problem:** on an HTTP API shared with 2.x, `{versionId}/credentials` matches every `X/credentials`; depending on
  how Hermod orders a literal against a parameter it shadows the 2.x routes or is refused as ambiguous.
- **Fix:** literals like 2.x: `$"versions/{Version.Id}"`, `$"{Version.Id}/credentials"`.

### O9. Untested replay of asset lines
`test-coverage` · low · **open**

- **Where:** 3.0 `removeToken` lines of a token status (`WWCP_OCPIv3.0/Common/CommonAPI.cs:4247-4270`; the test
  `WWCP_OCPIv3.0_Tests/CommonTests/RemovedAtOnceTests.cs:121` covers only the new format), 3.0 `removeAll*` lines
  without a list (`:4078-4082`, `:4296-4301`, `:4490-4494`), `addRemoteSessionIfNotExists`
  (`WWCP_OCPI_Common/HTTP/CommonHTTPAPI.cs:224`, `WWCP_OCPIv2.2.1/Common/RemoteCPOAssets.cs:597-625`; nobody writes
  it). 3.0 has no `CredentialsAccessTests` either (2.1.1, 2.2.1 and 2.3.0 have them).
- **Fix:** a test per case, written as the lines are found in old files; drop `addRemoteSessionIfNotExists` if
  nothing is to write it.


## Checked, fixed

- GET credentials refuses an unknown token in every version, and 2.1.1 `TryGetRemoteParties` compares the token
  before the TOTP (`e677ff43`; 2.1.1 `Common/CommonAPI.cs:2612`, `:7077-7078`, 2.2.1 `:2832`, 2.3.0 `:4714`, 3.0
  `:2447`).
- The HUB HTTP APIs of 2.2.1 and 2.3.0 can be built: their receiver routes are below `receiver/` (`a2f0b038`), their
  modules announced only when built (`HubModulesTests`). The hub does not build them, on purpose.
- `LogRemoteParty` without data writes its command (`0c67f105`); the unused `LogFileWriter` of WWCP_Core's
  `WWCP_HTTPAPI` is gone (WWCP_Core `f871694e`).
