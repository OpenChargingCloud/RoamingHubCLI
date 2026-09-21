# RoamingHub - EV Roaming Hub

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

The deepest file in the submodules is well over 140 characters below the clone
root, so under the classic 260-character limit the root has little room to live
in. `D:\src\RoamingHub` is fine; a checkout somewhere below
`C:\Users\<you>\AppData\Local\Temp\...` is not, and the clone fails halfway
through a submodule with `Filename too long` rather than at the start.
Per clone instead of globally: `git clone -c core.longpaths=true ...`.


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
beside it, one set per version, and reads them back at every start.

**No OCPI 2.1.1, and there cannot be.** The hub role arrived with OCPI 2.2 and
the library has no hub side for the version before it. A CPO or an EMSP that
speaks only 2.1.1 cannot be peered with this hub; it has to talk to its
counterpart directly.

`dotnet run --project RoamingHubCLI -- --help` lists the rest: `--port`,
`--any`, `--accounts <dir>`, `--frontend <dir>`, `--config <file>`,
`--verbose`, `--quiet`, `--no-trace`.

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
| `libs/RoamingHub/RoamingHub/` | the hub itself - its configuration, its log, its JSON API, its OCPI bindings, its traffic log |
| `libs/RoamingHub/RoamingHub/Frontend/` | the web interface: TypeScript and SCSS, bundled by webpack |
| `libs/RoamingHub/RoamingHubTests/` | what a hub does when a peer, or a stranger, talks to it |
| `libs/WWCP_OCPI/` | the protocol: OCPI 2.2.1 and 2.3.0 |

The command line is this program's vocabulary and nothing else. What a roaming
hub *is*, and what it does, lives in `libs/RoamingHub`.


### Your participation

This software is Open Source under the **Affero GPL 3.0 license**.
We appreciate your participation in this ongoing project, and your help to
improve it and the e-mobility ICT in general. If you find bugs, want to
request a feature or send us a pull request, feel free to use the normal
GitHub features to do so. For this please read the Contributor License
Agreement carefully and send us a signed copy or use a similar free and
open license.
