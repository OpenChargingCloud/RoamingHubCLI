# POI3: issues found while documenting the entities

Found while writing `libs/WWCP_POI/docs/` (2026-10-13) and then checked against the code (2026-10-14).
Each entry is meant to be pasted as a GitHub issue. Paths are relative to the repository named in the
title (`WWCP_POI` unless stated otherwise); line numbers are those of 2026-10-14.

Severity: **high** means wrong results, crashes or data loss; **medium** means wrong or surprising
behaviour with a workaround; **low** covers cleanup, naming and docs.


## High

### 1. Change sets with nested entities without timestamps are refused (after-ETag mismatch)
`bug` · high · **fixed in WWCP_POI e416403, WWCP_DatexII dee93dd: `created` is mandatory when reading JSON/CBOR (POIEntityMetadata), C# constructors still default it; DATEX II adapter gives new pools their creation**

- **Where:** `WWCP_POI3/RoamingNetwork/RoamingNetwork.InfrastructureChanges.cs:349-351`,
  `RoamingNetwork.ChangeSets.cs:621-622`, `WWCP_POI3/DataStructures/AImmutableInternalData.cs:41-43`.
- **Problem:** `Add` stamps `created`/`lastChange` only on the top-level object. Nested children have no
  timestamps when the change JSON is written by hand or by another implementation: EVSEs of an added
  station, energy meters. They then get `Timestamp.Now` when parsed (`created ?? lastChange ?? now`).
  The same applies to energy meters given through `UpdateProperty` of `energyMeters`, `energyMeter` or
  `gridConnectionPoint`.
- **Impact:** `CreateChangeSet` computes the after-ETags at T1, and `Commit` re-applies the change set at
  T2. `MatchETags(AfterETags)` then refuses it ("The JSON ETag … differs!"). This happens on peers, and
  also on the originating server unless both steps fall in the same millisecond. A commit that did pass
  can no longer be recomputed when the history is loaded from disk (`TryApplyKnown`).
  Tests don't catch it because they build changes from typed objects, which always write timestamps.
- **Fix:** before re-parsing, stamp every nested entity object in added or updated values with
  `created ??= lastChange ??= changeSet.CreatedAt`, or refuse entity JSON without timestamps.

### 2. `ChargingStationOperator.ToString()` throws a NullReferenceException
`bug` · high · **fixed in WWCP_POI d272e42: the reference is removed, as versions share unchanged operators**

- **Where:** `WWCP_POI3/RoamingNetwork/ChargingStationOperator/ChargingStationOperator.cs:257`
  (property), `:729` (the only assignment, commented out), `:1606-1614` (`ToString()` ends with
  `RoamingNetwork.Id.ToString()`).
- **Problem:** `RoamingNetwork` is never set.
- **Impact:** every `ToString()` on an operator throws: logging, string interpolation, the debugger.
  `roamingNetworkId` is never serialized either (the guard at `:1193-1196` is always false).
- **Fix:** make `ToString()` null-safe. Then either remove the property and its serializer branch, or set
  it when the network adopts the operator.

### 3. Infrastructure changes compare `@id` as raw text, not as identifications
`bug` · high · **fixed in WWCP_POI 95d8763**: `StoredId` resolves the change's ids through the operator's typed lookups; test `InfrastructureTests.Entities_are_found_in_every_spelling_of_their_identification`

- **Where:** `WWCP_POI3/RoamingNetwork/RoamingNetwork.InfrastructureChanges.cs:417,433,449,458`
  (`Find`: `operatorChild["@id"]?.Value<String>() == EntityId`), `:244-246` (parent check), `:327-354`
  (Add).
- **Problem:** `ChargingStation_Id`, `EVSE_Id`, `ChargingTariff_Id`, … are equal ignoring the case of the
  suffix, the operator prefix form and `*` in the suffix. The change set code compares the JSON text
  exactly instead.
- **Impact:**
  - A change naming `DE*GEF*Sab` for a stored `DE*GEF*SAB` is refused with "does not exist".
    The same happens for `DEGEF*S1` vs `DE*GEF*S1` and for `S1*2` vs `S12`.
  - Adding such a variant is appended first. It fails only on re-parse, with a misleading
    "must not be given twice" error.
- **Fix:** parse the id of the entity type and compare with `Equals`, or compare canonical `ToString()`
  forms, in `Find`, the parent check and the Add check.

### 4. Parking changes: ownership is matched by exact text; products can hit the wrong entity
`bug` · high · **fixed in WWCP_POI 95d8763**: `ParkingStoredId`/`ParkingId` in apply and ownership; test `ParkingTests.Parking_is_found_in_every_spelling_of_its_identification`

- **Where:** `WWCP_POI3/RoamingNetwork/RoamingNetwork.Ownership.cs:396,400,427-435` (exact string
  match); `RoamingNetwork.ParkingChanges.cs:162-169,219` (typed ids, `OrdinalIgnoreCase`);
  `WWCP_POI2/RoamingNetwork/ParkingOperator/ParkingProduct/ParkingProduct_Id.cs:347,360` (Ordinal).
- **Problem:** Ownership is checked first, by exact string. Applying the change compares
  case-insensitively. Parking product ids are case-sensitive, but the JSON child is found with
  `OrdinalIgnoreCase`.
- **Impact:**
  - A case variant of a garage, space, sensor or group id fails with "belongs to no parking operator".
  - With products `ABC` and `abc`, a change to `abc` edits or removes `ABC`.
- **Fix:** use the typed `TryGet…` lookups in ownership, and match by typed-id `Equals` in
  ParkingChanges (Ordinal for products).

### 5. The RNO alone can remove an operator together with all its data
`design` · high · **resolved as decided in WWCP_POI 07ff97a: intended and documented, the RNO decides who is a member**

- **Where:** `WWCP_POI3/RoamingNetwork/RoamingNetwork.Ownership.cs:323` (Add/Remove of a party: owner
  is the RNO only), `:457` (children check only for Add); `RoamingNetwork.ChangeSets.cs:639-647`;
  `RoamingNetwork.RegistryChanges.cs:334-335`.
- **Problem:** Removing a charging station operator or parking operator drops all its pools, stations,
  EVSEs, tariffs and groups, or all its parking data. The RNO's signature is enough. Adding requires the
  operator to come without children, but removing has no such check.
- **Impact:** the RNO can delete operator-owned data that it could never add or change itself. This
  contradicts "a charging station operator changes only its own data".
- **Fix:** refuse a party `Remove` while it still has children (mirroring `Add`), or also require the
  party's own signature.

### 6. A network without keys is frozen for good; the network's keys can't be recovered
`design` · high · **resolved as decided in WWCP_POI c0a897d**: no constraint in code; signing keys are now rotated by the signing keys they replace or by an identity key, identity keys only by an identity key. README, SPECIFICATION and docs advise at least two RNO identity keys with different `notAfter`.

- **Where:** `WWCP_POI3/DataStructures/POIPartyKeys.cs:84-98,121-131` (no count check);
  `RoamingNetwork.Ownership.cs:463-468,484-489` (1..n enforced only in change sets), `:312` (network
  keys only by RNO identity key), `:195-199,234-238`.
- **Problem:** Constructors and `TryParse` accept parties and networks without `signingKeys`/`identityKeys`.
  The SPECIFICATION §6 vector is an example. Such a network can't accept any change, not even one that
  adds keys. Party keys can be recovered with an RNO identity key; the network's own keys have no
  recovery path at all.
- **Fix:** enforce at least one signing and one identity key in `POIPartyKeys.Check`/`TryParse` (at least
  for the network), and design a recovery path for network keys or document that there is none.

### 7. Styx: `Address` loses its time zone on every round trip
`bug` · high · **repository: Styx** · **fixed in Styx 80074364**: reads `timeZone`; test `Address_Tests.AnAddressKeepsItsTimeZoneWhenReadAgain`

- **Where:** `Styx/Illias/Helpers/Address.cs:522` writes `"timeZone"`, `:362` reads `"TimeZone"`.
  The lookup is case-sensitive.
- **Impact:** the time zone of every address is dropped silently when parsed. In POI3, an in-memory
  network has a different ETag than its parsed copy, e.g. after a snapshot sync.
- **Fix:** read `"timeZone"`, optionally also accepting the legacy `"TimeZone"`.


## Medium

### 8. Transparency software certificates write timestamps in the wrong format
`bug` · medium · **fixed in WWCP_POI f1b9ea8**

- **Where:** `WWCP_POI2/RoamingNetwork/TransparencySoftware/TransparencySoftwareCertificate.cs:288,292`
  (`ToString("O")`).
- **Problem:** `notBefore`/`notAfter` come out as `2026-01-01T00:00:00.0000000+00:00`, with sub-millisecond
  precision. SPECIFICATION.md §2 requires `yyyy-MM-ddTHH:mm:ss.fffZ` for content.
- **Impact:** the values are part of the network content and its ETags. .NET writes them
  deterministically, but other implementations can hardly reproduce them.
- **Fix:** `ToISO8601()`, and truncate to milliseconds in the constructor.

### 9. Grid connection point identifications are not unique
`bug` · medium · **fixed in WWCP_POI 57f372a (by the HTTP session): grid connection points belong to their grid operators and are unique within the network**

- **Where:** no check in `RoamingNetwork.cs` (only `UnknownGridOperator`, `:1527-1539`);
  `WWCP_POI3/API/POI_API.Entities.cs:188-194` (`FirstOrDefault`).
- **Impact:** two pools can embed different grid connection points under the same id.
  `GET ~/RNs/{RN_Id}/GOs/{GO_Id}/GCPs/{GCP_Id}` returns one of them and hides the other.
- **Fix:** require ids that are unique within the network (or identical content where an id is shared),
  or return all instances.

### 10. Charging tariff: `RemoveProperty` of `currency`/`elements` is allowed but always fails
`bug` · medium · **fixed in WWCP_POI ea0fb05**

- **Where:** `WWCP_POI3/RoamingNetwork/RoamingNetwork.InfrastructureChanges.cs:163,165`;
  `WWCP_POI3/RoamingNetwork/ChargingTariff/ChargingTariff.cs:138,267-270,282-285`.
- **Impact:** the removal fails with a re-parse error ("needs its currency…") instead of "can not be
  removed".
- **Fix:** keep a separate set of removable properties, or refuse removing mandatory properties up front.

### 11. Empty `dataLicenses` is written inconsistently
`bug` · medium · **fixed in WWCP_POI 3c6fe8d: the operator omits empty `dataLicenses`; SPECIFICATION vectors recalculated**

- **Where:** `RoamingNetwork.cs:844` (omitted), `ChargingStationOperator.cs:1186-1191` with
  `POIDataLicenses.cs:107-109` (`[]`), `ChargingPool.cs:443-444` (omitted).
- **Impact:** the same meaning ("no licenses") has different canonical JSON and different old values in
  change sets.
- **Fix:** have the operator also omit empty `dataLicenses`. Note that this changes ETags.

### 12. `ChargingStationOperator.ToJSON()` writes license ids only, which don't round-trip
`bug` · medium · **fixed in WWCP_POI ce563ab: `dataLicenses` complete by default**

- **Where:** `ChargingStationOperator.cs:1151` (`ExpandDataLicenses = ShowIdOnly`), `:1187-1190`;
  `WWCP_POI3/DataStructures/POIDataLicenses.cs:199-204`.
- **Impact:** custom (not well-known) licenses can't be parsed back from the plain `ToJSON()`.
  `ToEmbeddedJSON()`, the network and CBOR use the complete form.
- **Fix:** default to `Expanded`, or write ids only for well-known licenses.

### 13. Child changes don't update the operator's `lastChange`
`design` · medium · **fixed in WWCP_POI 2e27241: charging station, pool and operator, parking operator and grid operator get the createdAt of a change set changing what they embed**

- **Where:** `RoamingNetwork.InfrastructureChanges.cs:318,351,360-367`; `RoamingNetwork.ParkingChanges.cs:272,303`;
  `RoamingNetwork.ChangeSets.cs:591,622`.
- **Problem:** changing a pool, station, EVSE, tariff or group (or a parking element) sets only that
  element's `lastChange`. The operator, the parent pool and the parent station are never touched;
  `Remove` sets no timestamp at all.
- **Impact:** consumers that use `lastChange` to find changes miss changes below an operator.
- **Fix:** also set `lastChange` on the operator (and the parents), or document that `lastChange` is per
  element.

### 14. Element change kinds exist but are never applied
`design` · medium · **fixed in WWCP_POI 055fa22: element changes are applied as the change of the property they are part of, with the element as old value; merges, and so the importers' differences, merge and write arrays of identified elements element by element**

- **Where:** `WWCP_POI3/ChangeSet/RoamingNetworkChange.cs:145-182,1336-1455`; every apply path ends in
  `default: … not supported yet` (`ChangeSets.cs:651,801`, `RegistryChanges.cs:339`,
  `ParkingChanges.cs:325`, `InfrastructureChanges.cs:372`); `WWCP_POI3_Tests/ApplyChangeSetTests.cs:376-377`.
- **Problem:** `AddElement`, `RemoveElement`, `ReplaceElement`, `UpdateElementProperty` and
  `RemoveElementProperty` are offered by the public API but always refused.
- **Fix:** implement them, or mark them reserved or obsolete in the API and the README.

### 15. `RoamingNetworkChange.Add(operator)` can't add an operator that has children
`design` · medium · **fixed in WWCP_POI 0a7fb94: `Add` leaves the children of charging station, parking and grid operators out; `AddChildren(…)` gives the changes their owners sign to add them**

- **Where:** `RoamingNetworkChange.cs:231-235` (`ToEmbeddedJSON()`), `:560-564` (`ToJSON()` of a parking
  operator); `RoamingNetwork.Ownership.cs:470-474`.
- **Impact:** both helpers send the complete representation including children, which ownership then
  refuses.
- **Fix:** strip the child arrays in the helpers, or throw early with a clear message.

### 16. `TryParse` neither checks `eTags` nor `@context`
`design` · medium · **fixed in WWCP_POI fdf4e69, WWCP_DatexII ab619e2, WWCP_OCPI d80a7a88: declared `eTags` must be those of the network read, a declared `@context` the own one (also of an operator); the importers read changed JSON without the head's ETags**

- **Where:** `RoamingNetwork.cs:531ff` (JSON), `:1049ff` (CBOR); `ValidateETags` is only called in tests.
- **Impact:** wrong or stale `eTags` and a foreign `@context` are accepted silently, unless the caller
  checks them itself.
- **Fix:** check them in `TryParse` when present, or call `ValidateETags` at the import and sync
  boundaries.

### 17. Status types accept any text, and nothing aggregates them
`design` · medium · repository: WWCP_POI (WWCP_POI2) · **reframed: accepting any text is intended (future values); open is only a status of groups calculated by a delegate, design proposed** · **fixed in WWCP_POI 03f53ad: group status set manually or calculated by a delegate (RoamingNetworkRuntimeStatus.CalculateStatus…, POIGroupStatus defaults)**

- **Where:** e.g. `WWCP_POI2/.../EVSE/AdminStatus/EVSEAdminStatusType.cs:146-160` (any trimmed non-empty
  text; the same in all `*StatusType`s).
- **Impact:** typos become valid status values. Group, pool and station status (`PartialAvailable`) is
  never derived from the members, so it must be set by hand and can contradict them.
- **Fix:** validate against the predefined values (or flag unknown ones), and add an aggregation helper.

### 18. Charging station `maxPower` and `modelId` are not checked
`design` · medium · **fixed in WWCP_POI cf9a40b, WWCP_DatexII 6c6d9ce: maxPower at most that of its EVSEs together (DATEX II importer leaves an exceeding one out), docs corrected; modelId stays free text, certificates name their model as ChargingStationModel_Id**

- **Where:** `ChargingStation.cs:173,242,580`; `WWCP_POI2/.../ChargingStationModel_Id.cs:157-170`;
  `ChargingStationManufacturer.cs`, `EnergyMeterManufacturer.cs` (no model lists).
- **Problem:** `maxPower` is documented as the power "of all its EVSEs", but it is neither computed nor
  checked against the EVSEs. `modelId` of stations and energy meters is free text.
- **Fix:** check `maxPower ≤ Σ EVSE.maxPower` (or derive it) or reword the docs. Add model lists to the
  manufacturers if models are meant to be referenced.

### 19. Tariff restriction accepts an empty date range
`bug` · medium · **fixed in WWCP_POI 57b064e: `startDate` < `endDate`, `startTime` ≠ `endTime`**

- **Where:** `WWCP_POI3/RoamingNetwork/ChargingTariff/ChargingTariffRestrictions.cs:141`
  (`StartDate > EndDate`), `:59` (`endDate` is exclusive).
- **Impact:** with `startDate == endDate` the element can never apply.
- **Fix:** `StartDate >= EndDate`.


## Low

### 20. README: `stepSize` is not optional
`docs` · low · **fixed in WWCP_POI 067a0ff**

- **Where:** `README.md:108-109` (`stepSize?`) vs `WWCP_POI3/RoamingNetwork/ChargingTariff/ChargingPriceComponent.cs:97-104`.
- **Problem:** `stepSize` is required (> 0) for every type except `FLAT`, and refused for `FLAT`.
- **Fix:** correct the README.

### 21. SPECIFICATION §7 is stale; two route schemes
`docs` · low · **fixed: §7 by the HTTP session, routes in WWCP_POI 860410b: history under ~/RNs/{RN_Id}/history**

- **Where:** `SPECIFICATION.md:298` ("not served yet") vs `WWCP_POI3/API/POI_API.Entities.cs:585-590`
  (`304 Not Modified`); `~/RNs/{id}` (entity API) vs `~/roamingNetworks/{id}/history` (history API).
- **Fix:** update §7 (served for the entity API, not yet for the history API), and unify the route names
  or add aliases.

### 22. The network writes `"name": {}` even after the name was removed
`bug` · low · **fixed in WWCP_POI 9b0cc8c**

- **Where:** `RoamingNetwork.cs:834` (always written), `RoamingNetwork.ChangeSets.cs:770-771,1036`.
- **Impact:** change sets treat the name as absent, but the content still has `{}`, so an old value `{}`
  never matches.
- **Fix:** write `name` only when it is not empty, as `description` already does. Note that this changes
  ETags.

### 23. `TryParseFlags` accepts numbers
`bug` · low · **fixed in WWCP_POI fc33789**

- **Where:** `WWCP_POI3/DataStructures/POIJSON.cs:363-365` (`Enum.TryParse`).
- **Impact:** `currentTypes: ["4"]` is accepted and written back as `"DC"`, and so are names padded with
  whitespace. An old value written in that form never matches.
- **Fix:** accept names only (`Enum.GetNames<TEnum>().Contains(text, StringComparer.Ordinal)`).

### 24. Grid connection points and parking products drop `created`/`lastChange`
`design` · low · **fixed in WWCP_POI 1ec819d: brands, grid connection points and parking products refuse created and lastChange**

- **Where:** `GridConnectionPoint.cs:365-368`, `ParkingProduct.cs:181-184` (`out _, out _`).
- **Impact:** the timestamps are read, validated and dropped silently, so the given document does not
  round-trip.
- **Fix:** refuse them on these types, or keep them.

### 25. Dead code in `ChargingStationOperator` and the delegates
`cleanup` · low · **fixed in WWCP_POI addd1e2**

- `DefaultMaxChargingStationOperator(Admin)StatusScheduleSize = 15` (`ChargingStationOperator.cs:246,251`):
  the constructor defaults to the base value of 50, so 15 only applies when `null` is passed explicitly
  (`:700-701,717-718`).
- `OnDataChanged` (`:971`) and `UpdateData` (`:1061`) have no callers.
- The `ExpandChargingTariffIds` parameter (`:1157`) is used only in commented-out code.
- `OnRoamingNetworkDataChangedDelegate` (`RoamingNetworkDelegates.cs:36`) is never used.

### 26. Status types: naming and values
`cleanup` · low · WWCP_POI2 · **fixed in WWCP_POI 117c78d, 6707ade, WWCP_OCPI fc88efad; WWCP_DatexII 6f995b5**: file renamed, value sets per kind of entity, energy meters without EVSE values, LegalStatus camelCase and normalized when read

- `ChargingStationManufacturerStatusType` lives in `ChargingStationManufacturer/Status/ChargingStationStatusType.cs:53`,
  and its doc comments talk about charging.
- `EnergyMeterStatusType.cs:225-242` has EVSE values (`WaitingForPlugin`, `PluggedIn`, `Charging`,
  `DoorNotClosed`).
- `EVSEAdminStatusType.cs:225,236` has `Deleted` and `UnknownEVSE`.
- `ChargingPoolStatusType` (`removed`, `error`) and `ChargingPoolGroupStatusType` (`planned`,
  `outOfService`, `faulted`, `other`) use different value sets.
- `LegalStatus.cs:188,194,200` mixes `ForInformationOnly`/`GermanCalibrationLaw` with `verified`.

### 27. Comments forget parking space groups and products
`docs` · low · **fixed in WWCP_POI b563fc6**

- `RoamingNetwork.cs:1636-1637` (`DuplicateParking`), `ParkingOperator.cs:380-381,445-446` and
  `RoamingNetwork.ParkingChanges.cs:33,38,134` mention only garages, spaces and sensors, while the code
  also handles space groups and products (`RoamingNetwork.cs:1645-1646`, `ParkingChanges.cs:121-125`).

### 28. No common policy for identifications
`design` · low · **resolved 2026-10-16, WWCP_POI e012acd, 49310b2: the separators are intended (grid operators have no market format and write `*`, e-mobility providers keep the market's spelling, both compare without it); merges find entities by their typed identifications** · **deferred 2026-10-14**: eMI3/ISO 15118 identifications stay as the standard defines them; open is only one rule for the free-text identifications (proposal: exact and ordinal, refusing surrounding whitespace and control characters) · **2026-10-16: transparency software and certificate identifications compare as brands do, WWCP_POI e012acd; open are only the separators**

- Case-insensitive and trimmed: `Brand_Id`, `EnergyMeterManufacturer_Id`, `RoamingNetwork_Id`,
  `DataLicense_Id`, and since WWCP_POI e012acd `TransparencySoftware_Id`, `TransparencySoftwareCertificate_Id`.
- Separators: `GridOperator_Id` is always written with `*` (`:459-463`), while `EMobilityProvider_Id`
  writes the separator it read (`:241-243,526-534`).
- **Fix:** settle on one policy (case, whitespace, canonical separator) and document it in the README.

### 29. Hermod: wrong name of `dl-de/zero-2-0`; context of open source licenses
`bug` · low · **repository: Hermod** · **fixed in Hermod 79c1d6fb, WWCP_OCPI d373a2a2**

- **Where:** `Hermod/HTTP/HTTPAPI/Entities/Licenses/DataLicense.cs:385-396` and `OpenSourceLicense.cs:83`.
- **Problem:** `dl-de/zero-2-0` is named "Datenlizenz Deutschland – Namensnennung – Version 2.0", copied
  from `by-2-0`; it should say "Zero". `OpenSourceLicense.JSONLDContext` is
  `https://opendata.social/contexts/wwcp+json/dataLicenses`.
- **Fix:** correct the name, and give open source licenses a context of their own (e.g. `…/openSourceLicenses`).


### 30. Changes of parties and of the roaming network compare identifications as text
`bug` · high · **fixed in WWCP_POI 71c4623**: owners and the network resolved through typed lookups; test `OwnershipTests.Parties_are_named_in_every_spelling_of_their_identification`

- **Where:** `RoamingNetwork.Ownership.cs` (`new POIPartyId(Change.EntityType, Change.EntityId)`, `TryGetParty` for the network),
  `RoamingNetwork.ChangeSets.cs:516,663`, `RoamingNetwork.RegistryChanges.cs:216`.
- **Problem:** a change of the operator `degef` needed a signature by a key of the party `degef`, which no party has;
  a change of the roaming network `RN1` or naming it as parent was not found.
- **Fix:** the party is named as its keys name it; `IsThisRoamingNetwork` compares roaming network identifications typed.


### 31. A precondition holds only for the version a change set was made for, not for the head it is merged into
`bug` · high · found in the review of 2026-10-16

- **Where:** `WWCP_POI3/History/RoamingNetworkHistory.cs:635-652` (`Commit`), `RoamingNetworkHistory.Persistence.cs:622-656`
  (`FindVersionFor`), `RoamingNetworkHistory.cs:1093-1144` (`MergeTips`), `RoamingNetwork.Merge.cs:349-355` (the merge
  change set has no preconditions); the same for `TryAppend` of a peer's commit whose parent is an older version.
- **Problem:** a client reads the charging pool P at version V1 and makes a change set for V1 with
  `Precondition(ChargingPool, P)`. Meanwhile the head moves to V2, where P's `hotline` changed. `Commit` finds V1,
  checks the precondition there, where it holds by construction, and `MergeTips` merges the branch with V2 property by
  property, without a conflict. The change set lands in the head although P is no longer as the client read it, which
  is what preconditions are for. Over POI_API (`POI_API.History.cs:525`, `POI_API.WebSocket.cs:629`) a precondition
  therefore never refuses anything; `TryRebase` checks them, but the server never rebases.
- **Fix:** when the parent is not the head, or other tips exist, check the preconditions also against every tip the
  commit will be merged with, and refuse with "prepare it again for the head"; or refuse to merge a branch whose
  preconditions do not hold in the other tip.


### 32. Preconditions are not limited, and each one hashes its whole entity again
`bug` · medium · found in the review of 2026-10-16

- **Where:** `WWCP_POI3/ChangeSet/RoamingNetworkChangeSet.cs:605-632` (reading `preconditions`),
  `RoamingNetworkChangeSetLimits.cs:33-48`, `RoamingNetwork.Preconditions.cs:109-126`, `RoamingNetwork.ChangeSets.cs:262-294`.
- **Problem:** the limits cover changes (10 000), signatures (16), size (16 MB) and depth, not preconditions. A change
  set within 16 MB can name some 100 000 preconditions, e.g. all of the roaming network or of a large operator, and
  `TryCheckPreconditions` calculates `EntityETag(entity.Full)`, the canonical JSON of the whole entity, once for each,
  without a cache, under the lock of the history: any party that may sign a change can stall the server.
- **Fix:** a `MaxPreconditions` limit, checked when reading a change set; within one check, an ETag calculated once per
  entity, format and hash algorithm.


### 33. Docs: the ETag a precondition names is that of `?view=stored`, not the HTTP ETag served by default
`docs` · low · found in the review of 2026-10-16

- **Where:** `SPECIFICATION.md` section 4 ("the HTTP `ETag` an entity is served with") and section 7, the remarks of
  `WWCP_POI3/RoamingNetwork/RoamingNetwork.Preconditions.cs:36-38`; `POI_API.Entities.cs` serves charging pools,
  stations and EVSEs as they are at runtime, with the properties their groups give them.
- **Problem:** for a member that takes properties from its groups, the HTTP ETag of the default view differs from
  `TryGetEntityETag`; a client following the specification makes a precondition that always fails as changed.
- **Fix:** name `?view=stored` in section 4, section 7 and the remarks (docs/README.md already does).


### 34. Base64 ETags are read, but written again as hex: signatures over them do not verify
`bug` · low · found in the review of 2026-10-16

- **Where:** `WWCP_POI3/ChangeSet/ETag/ETag.cs:224-231, 410-418` (base64 read) and `:434-440`, `ToJSON` writing hex by
  default; `RoamingNetworkPrecondition.cs:63, 89`; `RoamingNetworkChangeSet.cs:760-761`;
  `RoamingNetworkChangeSet.Signing.cs:241-247`; `SPECIFICATION.md:70-71`.
- **Problem:** section 3.1 allows `base64` in a JSON ETag. A peer that writes the before, after or precondition ETags
  of a change set in base64 and signs its canonical JSON as written has signatures that fail here, and another commit
  identification: the ETag does not keep its encoding, and the signature input is written again with hex.
- **Fix:** keep the encoding an ETag was read with, or state in section 4 that change sets name ETags in hex and refuse
  others there.


### 35. `ReplaceElement` of a single value may change its identification
`bug` · low · found in the review of 2026-10-16

- **Where:** `WWCP_POI3/ChangeSet/POIElements.cs:388-401`: only the branch of an array checks
  `SameId(…, IdOf(replacement), last.ElementId)`; a single value is replaced as it is.
- **Problem:** `ReplaceElement` of the EVSE's `energyMeter` (`{"@id": "M1", …}`) with `{"@id": "M2", …}` is accepted,
  although "ReplaceElement keeps the identification" (SPECIFICATION section 4) and an `UpdateElementProperty` of `@id`
  is refused (`:422`).
- **Fix:** where the single value has an identification, require the replacement to have the same one.


### 36. `AddElement` with an `elementId` makes an absent single value an array of one
`bug` · low · found in the review of 2026-10-16

- **Where:** `WWCP_POI3/ChangeSet/POIElements.cs:323-333`.
- **Problem:** `AddElement` of `[energyMeter@"M1"]` to an EVSE without an energy meter writes `energyMeter: [ {…} ]`;
  only reading the EVSE again refuses it, with a type error about `energyMeter` instead of a clear message.
- **Fix:** make an array only for a property holding an array (e.g. one of `ElementIdTypes`), else set the value after
  checking its `@id`, or refuse with a clear message.


### 37. Search: `limit=0` links to itself as the next page
`bug` · low · found in the review of 2026-10-16

- **Where:** `WWCP_POI3/API/POI_API.Query.cs:394-406` (`QueryResponse`).
- **Problem:** with `limit=0`, `offset + limit < filtered.Count` holds, so `rel="next"` names the same offset: a client
  following `Link` never ends. `(Int32) Query.Limit.Value` wraps for a limit above `Int32.MaxValue`, so `rel="prev"`
  may point forward.
- **Fix:** refuse `limit=0` (or write no next link then), and clamp the limit before casting.


### 38. Spec question: a conflict marker in the virtual merge base and identified arrays
`question` · low · found in the review of 2026-10-16

- **Where:** `WWCP_POI3/RoamingNetwork/RoamingNetwork.Merge.cs:662` (`TryMergeElements`); SPECIFICATION section 5.3.
- **Problem:** where the virtual merge base holds only a conflict marker of an entity, each of its arrays counts as
  empty: two tips keeping the entity with different identified arrays are united without a conflict, and a property
  only one tip sets is taken. That is what "absent counting as empty" says, but not "the marker makes the tips
  conflict unless both tips have the same".
- **Fix:** decide which sentence holds, and make the other one say so.


## Checked, no issue

- **Pool, station and tariff ids with a lowercase operator prefix** are fine: `ChargingStationOperator_Id`
  uppercases its input before matching (`ChargingStationOperator_Id.cs:232`).
- **Overlapping id patterns** (`DE*GEF*TG1` also matches `ChargingTariff_Id`) are harmless: every caller
  picks the parser by the explicit entity type.
- **`ParkingOperator.Ordered`** finds duplicates case-insensitively and orders by ordinal. Case variants
  can't coexist, so the order is still deterministic.
- **Review of 2026-10-16** (element changes, the merge by typed identifications, preconditions and ETags, groups giving
  their members properties): element changes are owned and checked as the change of the property they are part of;
  identifications compare alike in `KeyOf`, `SameId`, `TryMerge`, `Diff` and `MergeKey`; `Diff` checks itself by
  replaying its changes; preconditions are signed, part of the commit identification and kept and checked by
  `TryRebase`, `TryApplyKnown` and `TryAppend`; the entity ETag is the one of `POIRepresentation.Of`, in JSON and CBOR;
  every property groups give round-trips and is taken by their members, effective members share the runtime status of
  their members across versions. Two findings in the then new effective members were fixed with them: a member that
  left its group, or lost a value of it, kept an older last change; identifications in another spelling given by two
  groups of the same priority were refused as different.
- **Change order within a change set** matters, because the operator is re-parsed after each change: a
  group must stop naming an EVSE before the EVSE is removed. This is documented in the docs and needs no
  issue.
