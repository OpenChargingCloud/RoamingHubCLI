# RoamingHub - EV Roaming Hub

[![CI](https://github.com/OpenChargingCloud/RoamingHubCLI/actions/workflows/ci.yml/badge.svg)](https://github.com/OpenChargingCloud/RoamingHubCLI/actions/workflows/ci.yml)
[![Nightly](https://github.com/OpenChargingCloud/RoamingHubCLI/actions/workflows/nightly.yml/badge.svg)](https://github.com/OpenChargingCloud/RoamingHubCLI/actions/workflows/nightly.yml)

This software implements an OCPI roaming hub: the thing that sits between the
charge point operators and the e-mobility service providers so that they do not
each have to be peered with all the others. Every one of them is peered with the
hub instead, once, and the hub is what turns that into a mesh.

```
  CPO  ──OCPI 2.2.1──▶  RoamingHub  ◀──OCPI 2.2.1──  EMSP
                        every call through it written down,
                        with the two parties it was between
```

What it is and what it can be told lives in
[libs/RoamingHub](libs/RoamingHub); this repository is the command line that
starts it and the submodules it is built from.


### Getting it

The libraries it is built from are submodules, so they have to come along:

```
git clone --recurse-submodules <this repository>
```

If you already cloned it without them:

```
git submodule update --init --recursive
```

They are fetched from GitHub over https, so nothing but git is needed - no
account, no key.

**On Windows**, turn long paths on first:

```
git config --global core.longpaths true
```

The deepest file in the submodules is 128 characters below the clone root, and
under the classic 260-character limit git creates no file whose whole path is
longer than 259, so the root itself may be at most 130 characters long.
`D:\src\RoamingHub` is fine; a checkout nested deep below
`C:\Users\<you>\AppData\Local\Temp\...` can run out of room, and then the clone
fails halfway through a submodule with `Filename too long` rather than at the
start. Per clone instead of globally: `git clone -c core.longpaths=true ...`.


### Building and running it

```
dotnet build RoamingHubCLI.slnx
dotnet run --project RoamingHubCLI
```

The build needs the .NET 10 SDK and Node.js: the web interface is built by npm
and embedded into the assembly, so the hub is one thing to deploy. `dotnet
build -p:SkipFrontendBuild=true` leaves the npm step out and reuses whatever is
in `libs/RoamingHub/RoamingHub/Frontend/dist`.

At the first start there are no accounts, so the hub makes one up for the user
`root`, keeps its hash with the other accounts below `accounts/` beside the
solution and prints the password once. Then open http://127.0.0.1:2356/ and
sign in. Signing in happens at Hermod's HTTPExt API, mounted under `/ext` - the
same door the other components use, which is what lets one sign-in cover
several of them when they share a server.

What it opens on is the traffic, and that is the whole difference from the
other components: they open on their configuration, because that is what
somebody sets up once and then leaves alone. A hub is set up once and
*watched*.

A peer is given one URL, http://127.0.0.1:2356/ext/versions, and finds
everything else of OCPI from it. Who this hub is - `DE-GDH` unless
`configuration.json` beside the solution says otherwise - and which OCPI
versions it offers are read from that file once, at the start, and deliberately
not changeable while running: it is what every peer wrote into its credentials,
and changing it under a live registration would not rename the hub, it would
make it a second one nobody is peered with. The peers themselves are not in the
file: the OCPI library keeps them in append-only files of its own below `ocpi/`
beside it, one set per version, and reads them back at every start. Beside it
as well, `certificates/`: the certificate store of
[WWCP_Node](https://github.com/OpenChargingCloud/WWCP_Node), the node the hub
is built on, which makes it at every start and which nothing of the hub
chooses from yet.

**No OCPI 2.1.1, and there cannot be.** The hub role arrived with OCPI 2.2 and
the library has no hub side for the version before it. A CPO or an EMSP that
speaks only 2.1.1 cannot be peered with this hub; it has to talk to its
counterpart directly.

`dotnet run --project RoamingHubCLI -- --help` lists the rest: `--port`,
`--any`, `--accounts <dir>`, `--frontend <dir>`, `--config <file>`,
`--verbose`, `--quiet`, `--no-trace`.


### Typing at it

Once it is up, the console is a prompt rather than a place that only scrolls:

```
RoamingHub> syncNTS
succeeded after 712 ms: 4 of 4 server(s) answered (2 required), offset +1043.3 ms, spread 2.9 ms
  ptbtime1.ptb.de  +1043.3 ms, round trip 52.6 ms, key exchange new
  ptbtime2.ptb.de  +1043.3 ms, round trip 52.7 ms, key exchange new
  ptbtime3.ptb.de  +1043.4 ms, round trip 52.6 ms, key exchange new
  ptbtime4.ptb.de  +1046.2 ms, round trip 52.5 ms, key exchange new
```

`help` lists what can be typed, `quit` leaves, **Tab** completes and **↑**
walks back through what was typed before. `syncNTS` is **Sync now** on the
**NTS** page, typed: the same group of time servers is asked, the same entries
go into the log, and the same result is left behind for the page and the
overview to show. The one entry that differs says who asked - the page names
the account that pressed the button, the prompt says it was somebody at the
command line. The console gets a line for each server as well, because the log
only records what the group concluded. Neither of them steps the clock.

With one of the hub's time servers after it, it is that server's **Test**
button instead: one server, on the ports it is configured with, and every step
of the key exchange and the time request with when it happened - the TLS
certificate of the server and every certificate of the chain this machine
built, with both ends of their validity, the root's SHA-256 fingerprint, and
whether all of it held up. Only a server of this hub is tested; anything else
is answered with the ones there are, and nothing is asked. Tab offers them as
soon as the command is typed.

```
RoamingHub> syncNTS ptbtime2.ptb.de
ptbtime2.ptb.de answered, 295 ms altogether:
    +0 ms  Asking ptbtime2.ptb.de: key exchange on port 4460, time on port 123, 10 second(s) allowed.
   +15 ms  'ptbtime2.ptb.de' resolves to 192.53.103.104, 2001:0638:0610:be01:0000:0000:0000:0104.
   +15 ms  Key exchange over TLS ...
  +257 ms  Connected to 2001:0638:0610:be01:0000:0000:0000:0104, of 2 address(es) that were offered.
  +258 ms  Where the time went: name 0 ms, TCP 22 ms, TLS 135 ms, key exchange 84 ms.
  +260 ms  TLS 1.3, TLS_AES_128_GCM_SHA256, ALPN ntske/1.
  +263 ms  Server certificate: CN=ptbtime2.ptb.de, for ptbtime2.ptb.de; RSA 3072-bit, sha256RSA; valid 2026-08-09 03:05:52 to 2026-11-07 03:05:51 UTC, 43 day(s) left.
  +263 ms  Intermediate CA: CN=YR1, O=Let's Encrypt, C=US; RSA 2048-bit, sha256RSA; valid 2025-09-03 00:00:00 to 2028-09-02 23:59:59 UTC, 709 day(s) left.
  +263 ms  Intermediate CA: CN=Root YR, O=ISRG, C=US; RSA 4096-bit, sha256RSA; valid 2026-05-13 00:00:00 to 2032-09-02 23:59:59 UTC, 2170 day(s) left.
  +263 ms  Root CA: CN=ISRG Root X1, O=Internet Security Research Group, C=US; RSA 4096-bit, sha256RSA; valid 2015-06-04 11:04:38 to 2035-06-04 11:04:38 UTC, 3174 day(s) left.
  +263 ms  The root's SHA-256 fingerprint: 96bcec06264976f37460779acf28c5a7cfe8a3c0aae11a8ffcee05c0bddf08c6.
  +263 ms  Validated: the chain ends at a root this machine trusts, nothing in it is revoked (asked online), and 'ptbtime2.ptb.de' is one of the server certificate's names.
  +264 ms  The key exchange succeeded: AES_SIV_CMAC_256, 8 cookie(s).
  +264 ms  It named no NTP server of its own, so the time is asked of this host.
  +264 ms  Authenticated NTP request ...
  +294 ms  Answered by [2001:638:610:be01::104]:123; 8 cookie(s) left, and a fresh one came back.
  +294 ms  Round trip 29.4 ms.
  +294 ms  This RoamingHub's clock is +1045.5 ms off what ptbtime2.ptb.de says.
  +294 ms  The clock was not stepped: that is a different thing, with the sessions and charge detail records of every peer stamped against it, and not something a test does by surprise.
```

The log keeps writing while you type, from whichever thread did the thing it is
reporting, and your half-typed line survives it: the line is taken off the
screen, the entry is written whole, and the line comes back with the cursor
where it was.

Where there is no terminal - from a script, under a service manager, in CI, or
with the output going into a file or through `| tee` - there is no prompt, and
the hub runs until it is stopped, exactly as it did before.

While working on the web interface, run `npm run watch` in
`libs/RoamingHub/RoamingHub/Frontend` and start the hub with `--frontend
libs/RoamingHub/RoamingHub/Frontend/dist`: a reload in the browser then shows
the change, without rebuilding the C# side.


### The traffic, which is the point of a hub

Two peers that talk directly can each read their own log and compare them. The
moment a hub is between them, neither can say what the other actually sent, and
"it works for us" is an answer nobody can check. So every call is written down:

```
GET /api/v1/traffic?limit=200&after=1234&peer=DE*GEF
GET /api/v1/traffic/events                        ← the same, as it happens
GET /api/v1/traffic/peers                         ← everyone seen, for a filter
```

A line says which way the call went, which peer was at the other end, the two
parties out of the OCPI `from` and `to` headers, the version and module, the
HTTP status **and** the OCPI status inside the envelope - because OCPI answers a
refusal with `200` and a status code as readily as with a `4xx` - how long it
took, and how big it was.

The bodies are not kept unless `ocpi.logging.payloads` in the configuration file
says so, and none of it is written to disk: how long a hub may keep its peers'
business is a question with a different answer in every jurisdiction. A
deployment that has to keep more should read the stream and put it where it has
decided to keep it. Reading it is its own permission, `readTraffic`, and not
part of `readConfiguration`: the configuration is what this hub is, and the
traffic is what its peers did through it.


### Where things are

| | |
|---|---|
| `RoamingHubCLI/` | the command line: switches, and what the console says at a start |
| `RoamingHubCLI/CLI/` | the prompt, and one file per command that can be typed at it |
| `libs/RoamingHub/RoamingHub/` | the hub itself - its section of the configuration, its JSON API, its OCPI bindings, its traffic log |
| `libs/RoamingHub/RoamingHub/Frontend/` | the web interface: TypeScript and SCSS, bundled by webpack |
| `libs/RoamingHub/RoamingHubTests/` | what a hub does when a peer, or a stranger, talks to it |
| `libs/WWCP_Node/` | what the hub is before it is a hub: the log, the configuration file, name resolution and the time, the certificate store, the accounts and the web server |
| `libs/WWCP_OCPI/` | the protocol: OCPI 2.2.1 and 2.3.0 |

The command line is this program's vocabulary and nothing else - the switches
at a start and the commands at the prompt. What a roaming hub *is*, and what
it does, lives in `libs/RoamingHub`.


### Your participation

This software is Open Source under the **Affero GPL 3.0 license**.
We appreciate your participation in this ongoing project, and your help to
improve it and the e-mobility ICT in general. If you find bugs, want to
request a feature or send us a pull request, feel free to use the normal
GitHub features to do so. For this please read the Contributor License
Agreement carefully and send us a signed copy or use a similar free and
open license.
