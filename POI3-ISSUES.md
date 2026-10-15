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
`design` · medium

- **Where:** `WWCP_POI3/ChangeSet/RoamingNetworkChange.cs:145-182,1336-1455`; every apply path ends in
  `default: … not supported yet` (`ChangeSets.cs:651,801`, `RegistryChanges.cs:339`,
  `ParkingChanges.cs:325`, `InfrastructureChanges.cs:372`); `WWCP_POI3_Tests/ApplyChangeSetTests.cs:376-377`.
- **Problem:** `AddElement`, `RemoveElement`, `ReplaceElement`, `UpdateElementProperty` and
  `RemoveElementProperty` are offered by the public API but always refused.
- **Fix:** implement them, or mark them reserved or obsolete in the API and the README.

### 15. `RoamingNetworkChange.Add(operator)` can't add an operator that has children
`design` · medium

- **Where:** `RoamingNetworkChange.cs:231-235` (`ToEmbeddedJSON()`), `:560-564` (`ToJSON()` of a parking
  operator); `RoamingNetwork.Ownership.cs:470-474`.
- **Impact:** both helpers send the complete representation including children, which ownership then
  refuses.
- **Fix:** strip the child arrays in the helpers, or throw early with a clear message.

### 16. `TryParse` neither checks `eTags` nor `@context`
`design` · medium

- **Where:** `RoamingNetwork.cs:531ff` (JSON), `:1049ff` (CBOR); `ValidateETags` is only called in tests.
- **Impact:** wrong or stale `eTags` and a foreign `@context` are accepted silently, unless the caller
  checks them itself.
- **Fix:** check them in `TryParse` when present, or call `ValidateETags` at the import and sync
  boundaries.

### 17. Status types accept any text, and nothing aggregates them
`design` · medium · repository: WWCP_POI (WWCP_POI2) · **reframed: accepting any text is intended (future values); open is only a status of groups calculated by a delegate, design proposed**

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
`design` · low · **deferred 2026-10-14**: eMI3/ISO 15118 identifications stay as the standard defines them; open is only one rule for the free-text identifications (proposal: exact and ordinal, refusing surrounding whitespace and control characters)

- Case-insensitive and trimmed: `Brand_Id`, `EnergyMeterManufacturer_Id`, `RoamingNetwork_Id`,
  `DataLicense_Id`.
- Case-sensitive, refusing surrounding whitespace: `TransparencySoftware_Id`, `TransparencySoftwareCertificate_Id`.
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


## Checked, no issue

- **Pool, station and tariff ids with a lowercase operator prefix** are fine: `ChargingStationOperator_Id`
  uppercases its input before matching (`ChargingStationOperator_Id.cs:232`).
- **Overlapping id patterns** (`DE*GEF*TG1` also matches `ChargingTariff_Id`) are harmless: every caller
  picks the parser by the explicit entity type.
- **`ParkingOperator.Ordered`** finds duplicates case-insensitively and orders by ordinal. Case variants
  can't coexist, so the order is still deterministic.
- **Change order within a change set** matters, because the operator is re-parsed after each change: a
  group must stop naming an EVSE before the EVSE is removed. This is documented in the docs and needs no
  issue.
