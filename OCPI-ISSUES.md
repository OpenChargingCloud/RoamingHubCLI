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

**O11 fixed on 2026-10-29** (WWCP_OCPI `1aee8c1a`): `Request.LocalAccessInfo` names when the token the request came with may
be used, not when the party's first token may (2.2.1, 2.3.0, 3.0, and 2.1.1, which the finding left out); nothing read it yet but
its JSON. `CredentialsAccessTests` of each 2.x version and 3.0's `VersionsListTests`,
`TheRequestNamesTheValidityOfTheTokenItCameWith`.

**O13 fixed on 2026-11-07** (WWCP_OCPI `114115ce`): the 2.2.1 and 2.3.0 POI3 publishers date a tariff, a location and
an EVSE status they store with the time they store it, ms-truncated, or 1 ms after the one stored before where that is not
earlier; what did not change keeps the stored one. A change committed long after its `createdAt` is no longer dated before a
pull with `date_from` had passed. The mappers are unchanged. `POI3AdapterTests.WhatIsPublishedIsDatedWhenItIsStored`.

**O8 fixed on 2026-11-07** (WWCP_OCPI `326690ef`): 3.0 registers the routes of its own version, as 2.x do - `versions/3.0`
and `v3.0/credentials`, no longer `versions/{versionId}` and `{versionId}/credentials`. Run with 2.2.1 on one HTTP API
and below one path, as a platform serving both has them, Hermod asks a literal route before a template: 2.2.1 was not
shadowed. But 3.0 answered the credentials of every version served by none - `GET v2.1.1/credentials` with a 3.0 token
was given 3.0's credentials, and a POST there would have registered the peer with 3.0. The details of another version
were refused before as well. `WWCP_OCPI_Tests/SharedHTTPAPI/VersionRoutesTests.cs`. Found with it: O14.

**O12 fixed on 2026-11-07** (WWCP_OCPI `5cc89ad5`), as the maintainer chose: 2.1.1's routes read a token as it was sent
and BASE64-decoded, and take it in the reading that is the token of a party - as the versions list of the base API
reads it, which does not ask the setting either (the finding said it did). A token sent BASE64-encoded, as our clients
send it where the party says nothing (`?? true`), is let in at every route of 2.1.1; one sent as it is, as 2.1.1 has
it, still is. Reading it only as the setting says, as 2.2.1 and 2.3.0 do, was not taken: a 2.1.1 peer sending its token
as the standard has it, added without a setting, would have been shut out. 2.1.1's `CredentialsAccessTests`,
`ATokenSentBASE64EncodedIsLetInAtTheRoutesOfThisVersion`, `ATokenSentAsItIsIsLetInWhateverItsAccessSays`.

**O9 fixed on 2026-11-07** (WWCP_OCPI `a575f8ec`): 3.0's `RemovedAtOnceTests` read the lines files have from before - a
`removeToken` line of a token status, `removeAllTokens` and `removeAllTokenStatus`, `removeAllSessions` and
`removeAllChargeDetailRecords` without a list - and 3.0 has `CredentialsAccessTests` as 2.x have them (the validity of
the token a request came with stays in `VersionsListTests`). `addRemoteSessionIfNotExists` is kept, and written now:
`AddRemoteSessionIfNotExists` of 2.2.1's EMSP and HUB APIs wrote `addRemoteSession`, and `AddRemoteLocationIfNotExists`
wrote `addRemoteLocation`, so the two lines the next start reads for them were written by nobody. They write their own,
as tariffs and CDRs do; 2.2.1's `RemoteCPOFileTests`, `ARemoteLocationAddedIfNotThereIsThereAtTheNextStart`,
`ARemoteSessionAddedIfNotThereIsThereAtTheNextStart`.

Items marked **needs a design decision** wait on the maintainer. Severity as in POI3-ISSUES.md.


## Low

### O14. OCPI 3.0 puts no path of the base API before its own
`design` · low · **needs a design decision** (found 2026-11-07, with O8) · hub not exposed (it serves no 3.0)

- **Where:** `WWCP_OCPIv3.0/Common/CommonAPI.cs:1919` hands its base its `URLPathPrefix` alone, and lists its version at
  `URLPathPrefix + AdditionalURLPathPrefix + "/versions/3.0"` (`:1976`); 2.x hand theirs `BaseAPI.URLPathPrefix +
  URLPathPrefix` and list it there (`WWCP_OCPIv2.2.1/Common/CommonAPI.cs:2255`, `:2309`; 2.1.1 and 2.3.0 alike).
- **Problem:** a `URLPathPrefix` is a path below the base API's in 2.x and a whole path in 3.0. On a base API with
  `RootPath` "/ocpi", 2.2.1 given "/ocpi" serves at "/ocpi/ocpi/versions/2.2.1", 3.0 given "/ocpi" at
  "/ocpi/versions/3.0"; given none, 2.x serve below "/ocpi" and 3.0 at the root. The tests of each version give
  "/ocpi/v<version>", so 2.x serve theirs below "/ocpi/ocpi" and nobody notices, as the URLs are followed.
- **Fix:** 3.0 puts the base API's path before its own, as 2.x do - where a 3.0 API is served moves unless its caller
  gives the path without the base API's - or 2.x take theirs whole, as 3.0 does.


## Checked, fixed

- GET credentials refuses an unknown token in every version, and 2.1.1 `TryGetRemoteParties` compares the token
  before the TOTP (`e677ff43`; 2.1.1 `Common/CommonAPI.cs:2612`, `:7077-7078`, 2.2.1 `:2832`, 2.3.0 `:4714`, 3.0
  `:2447`).
- The HUB HTTP APIs of 2.2.1 and 2.3.0 can be built: their receiver routes are below `receiver/` (`a2f0b038`), their
  modules announced only when built (`HubModulesTests`). The hub does not build them, on purpose.
- `LogRemoteParty` without data writes its command (`0c67f105`); the unused `LogFileWriter` of WWCP_Core's
  `WWCP_HTTPAPI` is gone (WWCP_Core `f871694e`).
