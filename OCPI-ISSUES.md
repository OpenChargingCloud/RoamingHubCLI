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

**O3 fixed on 2026-10-29** (WWCP_OCPI `91db094a`, on O4): 3.0's `CommonAPI` constructor files `OurCredentialRoles` as
its parties before the assets are read back, registers its version information with the base API and its remote
parties as a provider, as 2.x do: the shared `/versions` lists 3.0 and lets its peers in with their tokens. GET
credentials answers 3000 saying so where there is no party of its own, as POST/PUT do since O4; a 3.0 API is made
with at least one and never loses it, so O4's test of a registration without parties is gone. The constructor's
`LocationsAsOpenData` stays unused (the base API's switch counts); `VersionsListTests`.

**O5 fixed on 2026-10-29** (WWCP_OCPI `1292e3ea`): a renewal the other side accepted with other roles than it holds
here (2.2.1, 2.3.0), or as another party than it is here (2.1.1), has its new tokens taken and stored all the same,
because the other side uses them already: we call it with its new token, it calls us with ours, now and after a
restart. Its roles, its party, stay as they were, and the new `CredentialsResult.Warning` says what was not taken; a
client without a remote party says there that nothing was stored. `RegistrationFileTests` of each version,
`ARenewalAnsweredWithOtherRolesKeepsItsNewTokens` / `ARenewalAnsweredAsAnotherPartyKeepsItsNewTokens`.

**O6 fixed on 2026-10-29** (WWCP_OCPI `7d901fc9`): a new token of ours keeps the settings of the one
it takes the place of - its TOTP, its validity window, how it is encoded, whether it allows downgrades, its HTTP
modifiers - in `TryRegister`, `TryPutCredentials` and the token they store before sending (2.1.1, 2.2.1, 2.3.0), and
token C keeps those of token A on the server side of the handshake (2.1.1, 2.2.1, 2.3.0, 3.0):
`LocalAccessInfos.ReplacedBy(NewToken, Current)` in `WWCP_OCPI_Common`. A peer set to send its token as it is is no
longer shut out by its new token. `RegistrationFileTests` of each 2.x version, `ARenewalKeepsTheSettingsOfOurToken`,
`ARegistrationKeepsTheSettingsOfOurToken`, `TheOtherSideRegisteringKeepsTheSettingsOfItsToken`; 3.0's
`ARegistrationPutsTokenCInPlaceOfTokenA` sends token C as token A was set to be sent.

**O10 fixed on 2026-10-29** (WWCP_OCPI `0dc3a790`): 2.3.0's receiver of a registration or renewal keeps how the other
side is reached - client certificates, TLS versions, IPv4 first, timeouts, retries, content types, user agent, the
encoding, TOTP and validity window of its token - and the versions it sees, as 2.2.1 and 3.0 do; a peer kept to some
versions saw all of them after renewing its credentials. Whether downgrades are allowed for its token is now kept in
every version (2.1.1, 2.2.1, 2.3.0, 3.0). `RegistrationFileTests` of each 2.x version,
`TheOtherSideRenewingKeepsHowItIsReached`; 3.0's downgrades are not tested.

**O7 fixed on 2026-10-29** (WWCP_OCPI `94af88ea`): the version details announce the CPO and the EMSP modules where
a `CPO_HTTPAPI` or an `EMSP_HTTPAPI` was built on the Common API, at the path it serves them at - it says so in
`CommonAPI.CPOModulesPath` / `EMSPModulesPath`, as the HUB_HTTPAPI does in `HUBModulesPath` - and not because the
platform or the other side has a role (2.1.1, 2.2.1, 2.3.0, 3.0). Charging profiles are announced nowhere: no module
API serves them yet. OCPI 3.0 has no module API for the CPO or the EMSP modules, so it announces the credentials
alone. The URLs announced carry the base API's `AdditionalURLPathPrefix`, the HUB modules' too, which left it out.
`ModulesAnnouncedTests` of each 2.x version; 3.0's `VersionsListTests.TheVersionDetailsAnnounceNoModuleNoModuleAPIServes`.
The tests that pinned charging profiles in the version details no longer expect them: `WWCP_OCPI_Tests`'
`GetVersionDetails_*`, and `CPO_GetVersion_Test` / `EMSP_GetVersion_Test` of 2.2.1 and 2.3.0.

Items marked **needs a design decision** wait on the maintainer. Severity as in POI3-ISSUES.md.


## Low

### O8. OCPI 3.0 registers its routes as templates of the version id
`bug` · low · **open; not verified by running** (found 2026-10-24) · hub not exposed

- **Where:** `WWCP_OCPIv3.0/Common/CommonAPI.cs:2038`, `:2068` (`"versions/{versionId}"`), `:2384`, `:2458`, `:2550`,
  `:2598`, `:2675` (`"{versionId}/credentials"`); only version details checks the id (`:2133`).
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

### O11. The request's local access names the validity window of the party's first token
`bug` · low · **open; not verified by running** (found 2026-10-29, with O6)

- **Where:** `WWCP_OCPIv2.2.1/IO/OCPIRequest.cs:574-575`, 2.3.0 `:575-576`, 3.0 `:646-647`:
  `RemoteParty.LocalAccessInfos.FirstOrDefault()?.NotBefore` / `NotAfter`, where the token matched is
  `localAccessInfo`.
- **Problem:** where a party has more than one local token - one pending beside the one in effect, as every
  registration and renewal leaves one for a while - `Request.LocalAccessInfo` names the window of another token.
- **Fix:** `localAccessInfo.NotBefore` / `NotAfter`.

### O12. 2.1.1's routes take a token as it is, whatever is set for it
`design` · low · **needs a design decision** (found 2026-10-29, with O6)

- **Where:** `WWCP_OCPIv2.1.1/IO/OCPIRequest.cs` reads no Base64; the shared `/versions` of the base API honours
  `AccessTokenIsBase64Encoded`.
- **Problem:** a 2.1.1 local token set to be sent Base64 is let in at `/versions` and refused at `versions/2.1.1` and
  every other 2.1.1 route ("Unknown access token!").
- **Fix:** either 2.1.1 reads tokens as their setting says, as 2.2.1 does, or the setting is ignored for 2.1.1 in
  the base API too (2.1.1 tokens are raw by the standard).


## Checked, fixed

- GET credentials refuses an unknown token in every version, and 2.1.1 `TryGetRemoteParties` compares the token
  before the TOTP (`e677ff43`; 2.1.1 `Common/CommonAPI.cs:2612`, `:7077-7078`, 2.2.1 `:2832`, 2.3.0 `:4714`, 3.0
  `:2447`).
- The HUB HTTP APIs of 2.2.1 and 2.3.0 can be built: their receiver routes are below `receiver/` (`a2f0b038`), their
  modules announced only when built (`HubModulesTests`). The hub does not build them, on purpose.
- `LogRemoteParty` without data writes its command (`0c67f105`); the unused `LogFileWriter` of WWCP_Core's
  `WWCP_HTTPAPI` is gone (WWCP_Core `f871694e`).
