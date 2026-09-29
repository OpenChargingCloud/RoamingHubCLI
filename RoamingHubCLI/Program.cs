/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of RoamingHub <https://github.com/OpenChargingCloud/RoamingHub>
 *
 * Licensed under the Affero GPL license, Version 3.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.gnu.org/licenses/agpl.html
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

using cloud.charging.open.RoamingHub.CommandLine;
using cloud.charging.open.RoamingHub.Configuration;
using cloud.charging.open.protocols.WWCP.Node;
using cloud.charging.open.protocols.WWCP.Node.Certificates;
using cloud.charging.open.protocols.WWCP.Node.Configuration;
using cloud.charging.open.protocols.WWCP.Node.Logging;

// Inside this namespace "RoamingHub" is the namespace and not the class, so
// the class needs a name of its own here.
using Hub = cloud.charging.open.RoamingHub.RoamingHub;

#endregion

namespace cloud.charging.open.RoamingHub.CLI
{

    /// <summary>
    /// One OCPI roaming hub, with its JSON API and a prompt, until 'quit' or
    /// Ctrl+C.
    /// </summary>
    public class Program
    {

        #region Data

        /// <summary>
        /// Where a password for a protected PKCS#12 is read from when the
        /// command line gives none - named like the vehicle's EV_CERT_PASSWORD
        /// and the gateway's GATEWAY_CERT_PASSWORD, so that one of these
        /// programs set up beside another is set up the same way.
        /// </summary>
        private const String CertificatePasswordVariable = "ROAMINGHUB_CERT_PASSWORD";

        #endregion

        #region (private static) TryTakeValue(Arguments, ref Index, out Value)

        private static Boolean TryTakeValue(String[]     Arguments,
                                            ref Int32    Index,
                                            out String?  Value)
        {

            if (Index + 1 < Arguments.Length && !Arguments[Index + 1].StartsWith("--"))
            {
                Value = Arguments[++Index];
                return true;
            }

            Value = null;
            return false;

        }

        #endregion

        #region (private static) RepositoryRoot()

        /// <summary>
        /// The directory holding RoamingHubCLI.slnx, looked up from the binary
        /// and from the current directory; the current directory when neither
        /// leads to it.
        /// </summary>
        /// <remarks>
        /// The accounts, the configuration and the OCPI files beside it
        /// default to a place below it, so that they do not end up in bin/ -
        /// where the next "dotnet clean" would take this hub's password and
        /// everyone peered with it.
        /// </remarks>
        private static String RepositoryRoot()
        {

            foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            {

                var directory = new DirectoryInfo(start);

                while (directory is not null)
                {

                    if (File.Exists(Path.Combine(directory.FullName, "RoamingHubCLI.slnx")))
                        return directory.FullName;

                    directory = directory.Parent;

                }

            }

            return Environment.CurrentDirectory;

        }

        #endregion

        #region (private static) ListCertificates(Hub)

        /// <summary>
        /// What is in this hub's certificate store, as a table.
        /// </summary>
        /// <remarks>
        /// Printed and not returned: this is what <c>--list-certificates</c>
        /// exists for - what the store holds, whether each one is switched on,
        /// until when, and what a root or a server certificate is kept for,
        /// for somebody at a console rather than on the Certificates page.
        /// Without the vehicle's "chosen" beside a line: a hub has no session
        /// that picks one certificate of a kind, and every usable root is
        /// believed.
        /// </remarks>
        private static void ListCertificates(Hub hub)
        {

            Console.WriteLine();
            Console.WriteLine($"  Certificates in {hub.Certificates.Directory}");
            Console.WriteLine();

            var entries = hub.Certificates.Entries;

            if (entries.Count == 0)
            {
                Console.WriteLine("  (empty - put one there with --import-certificate <kind>=<file>)");
                Console.WriteLine();
                return;
            }

            foreach (var kind in hub.Certificates.Kinds)
            {

                var ofKind = entries.Where(entry => entry.Kind == kind).ToArray();

                if (ofKind.Length == 0)
                    continue;

                Console.WriteLine($"  {kind.Describe()}");

                foreach (var entry in ofKind)
                {

                    var state = !entry.IsActive       ? "off"
                                : entry.IsExpired     ? "EXPIRED"
                                : entry.IsNotYetValid ? "not yet valid"
                                : "on";

                    Console.WriteLine($"    {entry.Id}  {state,-13}  until {entry.NotAfter.UtcDateTime:yyyy-MM-dd}  " +
                                      $"{entry.Label}{(kind.HasUsages() ? $"  ({CertificateUsages.Describe(entry.Usages)})" : "")}");

                }

                Console.WriteLine();

            }

        }

        #endregion

        #region (private static) PrintUsage()

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: RoamingHubCLI [--port <number>] [--any] [--frontend <dist directory>]");
            Console.WriteLine("                     [--config <file>] [--accounts <directory>]");
            Console.WriteLine("                     [--certificates <directory>] [--import-certificate <kind>=<file>]");
            Console.WriteLine("                     [--certificate-password <password>] [--list-certificates]");
            Console.WriteLine("                     [--verbose | --quiet] [--no-trace]");
            Console.WriteLine();
            Console.WriteLine("Server:");
            Console.WriteLine($"  --port <number>   TCP port to listen on (default: {Hub.DefaultHTTPPort})");
            Console.WriteLine("  --any             listen on all addresses instead of 127.0.0.1");
            Console.WriteLine("  --frontend <dir>  serve the web interface from a directory on disk instead of the");
            Console.WriteLine("                    bundle embedded in the assembly - use it together with");
            Console.WriteLine("                    'npm run watch' in libs/RoamingHub/RoamingHub/Frontend");
            Console.WriteLine();
            Console.WriteLine("Accounts:");
            Console.WriteLine($"  --accounts <dir>    where the accounts live (default: {Hub.DefaultAccountsPath}/ below the");
            Console.WriteLine("                      repository root): the users, their roles, the organizations and");
            Console.WriteLine("                      the API keys. Without them a password is made up at the first");
            Console.WriteLine($"                      start for the user '{Hub.DefaultAdminUser}' and shown once.");
            Console.WriteLine();
            Console.WriteLine("Configuration:");
            Console.WriteLine("  --config <file>   where the name servers, the time servers and the OCPI identity of");
            Console.WriteLine($"                    this hub live (default: {WWCPConfigFile.DefaultFileName} below the repository");
            Console.WriteLine("                    root). Without the file the hub runs on the system defaults and is");
            Console.WriteLine($"                    {OCPIConfiguration.DefaultCountryCode}-{OCPIConfiguration.DefaultPartyId} in OCPI; the name and time servers written there take");
            Console.WriteLine("                    effect at once. Who this hub is in OCPI is read once, at the start,");
            Console.WriteLine("                    and deliberately not changeable while running: it is what every");
            Console.WriteLine("                    peer wrote into its credentials. The peers themselves are not in");
            Console.WriteLine($"                    the file: the OCPI library keeps them below {Hub.OCPIDirectoryName}/ beside it, one");
            Console.WriteLine("                    set per OCPI version, and reads them back at every start.");
            Console.WriteLine();
            Console.WriteLine("The certificate store. Everything this hub believes and presents in TLS is kept");
            Console.WriteLine("here, one file per certificate, and switched on and off one at a time:");
            Console.WriteLine($"  --certificates <dir>      where the store is (default: {CertificatesConfiguration.DefaultDirectory}/ beside the");
            Console.WriteLine("                    configuration file, or what the file's certificates.directory");
            Console.WriteLine("                    says). Certificates already in that directory are read again at");
            Console.WriteLine("                    every start, so copying one in is a way to install it. The");
            Console.WriteLine("                    Certificates page manages the same store");
            Console.WriteLine("  --import-certificate <kind>=<file>");
            Console.WriteLine("                    copy a certificate into the store, as PEM, DER or PKCS#12. A root");
            Console.WriteLine("                    is a certificate on its own; a tlsIdentity has to bring its private");
            Console.WriteLine("                    key, so a PEM for one carries the key beside it. May be given");
            Console.WriteLine("                    several times. <kind> is one of:");
            Console.WriteLine("                      tlsRoot    what a name or a time server over TLS may chain to");
            Console.WriteLine("                      tlsServer  a server's own certificate, to hold it to by fingerprint");
            Console.WriteLine("                      clientRoot, tlsIdentity  kept, and used by nothing here yet");
            Console.WriteLine("                    A tlsRoot or a tlsServer goes in for every use; the Certificates");
            Console.WriteLine("                    page says what it is for - the time servers, the name servers.");
            Console.WriteLine("                    A root is believed as soon as it is in");
            Console.WriteLine("  --certificate-password <pw>");
            Console.WriteLine("                    what opens a protected PKCS#12 being imported. Used once and not");
            Console.WriteLine("                    kept: the store holds what it has without a password. A password");
            Console.WriteLine("                    given here stands in the process list for every other user of the");
            Console.WriteLine($"                    machine, so prefer the environment: {CertificatePasswordVariable}");
            Console.WriteLine("  --list-certificates       print the store, with the handle of each certificate");
            Console.WriteLine();
            Console.WriteLine("Traffic:");
            Console.WriteLine("  Every OCPI call that touched this hub is written down - which way it went, which");
            Console.WriteLine("  peer was at the other end, the two parties, the HTTP status and the OCPI status");
            Console.WriteLine("  inside the envelope, how long it took - and served at /api/v1/traffic, with a");
            Console.WriteLine("  stream beside it. Reading it is its own permission, traffic:read, and not part of");
            Console.WriteLine("  configuration:read: the configuration is what this hub is, and the traffic is what");
            Console.WriteLine("  its peers did through it. The hub role may read it and the viewer may not. It is");
            Console.WriteLine("  in memory and nowhere else, and the bodies are left out unless the configuration");
            Console.WriteLine("  file says ocpi.logging.payloads. A deployment that has to keep more should read");
            Console.WriteLine("  the stream and put it where it keeps such things.");
            Console.WriteLine();
            Console.WriteLine("OCPI versions:");
            Console.WriteLine($"  {String.Join(" and ", OCPIConfiguration.KnownVersions)}, and no 2.1.1: the hub role arrived with OCPI 2.2, and the library");
            Console.WriteLine("  has no hub side for the version before it. A CPO or an EMSP that speaks only");
            Console.WriteLine("  2.1.1 cannot be peered with this hub; it has to talk to its counterpart directly.");
            Console.WriteLine();
            Console.WriteLine("Log:");
            Console.WriteLine("  -v, --verbose     write every entry to the console, down to the debug ones");
            Console.WriteLine("  -q, --quiet       write only warnings and worse");
            Console.WriteLine("      --no-trace    do not pick up what the libraries below write with DebugX");
            Console.WriteLine();
            Console.WriteLine("Whatever the console shows, /api/v1/logs has the whole log and /api/v1/events has");
            Console.WriteLine("it as it happens.");
            Console.WriteLine();
            Console.WriteLine("Once it is up, the console is a prompt: 'help' lists what can be typed there,");
            Console.WriteLine("Tab completes it, and 'quit' or Ctrl+C stops the hub. Started where there is no");
            Console.WriteLine("terminal - from a script, under a service manager, in CI, or with the output going");
            Console.WriteLine("into a file - there is no prompt and it simply runs.");
        }

        #endregion


        public static async Task<Int32> Main(String[] Arguments)
        {

            Console.OutputEncoding = System.Text.Encoding.UTF8;

            #region Arguments

            IPPort?  port            = null;
            var      anyAddress      = false;
            String?  frontendDir     = null;
            String?  configFilePath  = null;
            String?  accountsPath    = null;
            var      verbose         = false;
            var      quiet           = false;
            var      noTrace         = false;

            String?  certificatesDir   = null;
            String?  certPassword      = null;
            var      listCertificates  = false;

            // Repeatable, and imported in the order they were typed.
            var      imports           = new List<(CertificateKind Kind, String File)>();

            for (var i = 0; i < Arguments.Length; i++)
            {
                switch (Arguments[i])
                {

                    case "--port":
                        if (i + 1 < Arguments.Length && UInt16.TryParse(Arguments[i + 1], out var parsedPort))
                        {
                            port = IPPort.Parse(parsedPort);
                            i++;
                        }
                        else
                        {
                            Console.Error.WriteLine("Missing or invalid port number after --port!");
                            return 2;
                        }
                        break;

                    case "--any":
                        anyAddress = true;
                        break;

                    case "--frontend":
                        if (!TryTakeValue(Arguments, ref i, out frontendDir))
                        {
                            Console.Error.WriteLine("Missing directory after --frontend!");
                            return 2;
                        }
                        break;

                    case "--accounts":
                        if (!TryTakeValue(Arguments, ref i, out accountsPath))
                        {
                            Console.Error.WriteLine("Missing directory after --accounts!");
                            return 2;
                        }
                        break;

                    case "--config":
                        if (!TryTakeValue(Arguments, ref i, out configFilePath))
                        {
                            Console.Error.WriteLine("Missing file after --config!");
                            return 2;
                        }
                        break;

                    case "-v":
                    case "--verbose":
                        verbose = true;
                        break;

                    case "-q":
                    case "--quiet":
                        quiet = true;
                        break;

                    case "--no-trace":
                        noTrace = true;
                        break;

                    case "--certificates":
                        if (!TryTakeValue(Arguments, ref i, out certificatesDir))
                        {
                            Console.Error.WriteLine("Missing directory after --certificates!");
                            return 2;
                        }
                        break;

                    case "--certificate-password":
                        if (!TryTakeValue(Arguments, ref i, out certPassword))
                        {
                            Console.Error.WriteLine("Missing password after --certificate-password!");
                            return 2;
                        }
                        break;

                    case "--list-certificates":
                        listCertificates = true;
                        break;

                    case "--import-certificate":
                    {

                        if (!TryTakeValue(Arguments, ref i, out var import) || import is null)
                        {
                            Console.Error.WriteLine("Missing <kind>=<file> after --import-certificate!");
                            return 2;
                        }

                        // Split at the FIRST '=' only: everything after it is
                        // the path, and a Windows path is full of things that
                        // are not separators.
                        var split = import.IndexOf('=');

                        if (split < 1 || split == import.Length - 1)
                        {
                            Console.Error.WriteLine($"--import-certificate wants <kind>=<file>, and '{import}' is not that.");
                            return 2;
                        }

                        // The kinds a hub keeps, and not every kind there is: a
                        // vehicle's root named here would only be refused by the
                        // store, once the hub had been made.
                        if (!CertificateKindExtensions.TryParseKind(import[..split], out var importKind) ||
                            !Hub.CertificateKinds.Contains(importKind))
                        {
                            Console.Error.WriteLine($"'{import[..split]}' is not a kind of certificate a roaming hub keeps. " +
                                                    $"Use one of {String.Join(", ", Hub.CertificateKinds.Select(one => one.AsText()))}.");
                            return 2;
                        }

                        imports.Add((importKind, import[(split + 1)..]));

                        break;

                    }

                    case "-h":
                    case "--help":
                        PrintUsage();
                        return 0;

                    default:
                        Console.Error.WriteLine($"Unknown argument '{Arguments[i]}'!");
                        PrintUsage();
                        return 2;

                }
            }

            if (verbose && quiet)
            {
                Console.Error.WriteLine("--verbose and --quiet ask for opposite things!");
                return 2;
            }

            #endregion

            #region Where the web interface comes from

            // A directory given on the command line wins, so that
            // "npm run watch" beside a running hub shows up in the browser on
            // a reload, without rebuilding the C# side.
            IStaticContentSource? frontend = null;

            if (frontendDir is not null)
            {

                if (!Directory.Exists(frontendDir))
                {
                    Console.Error.WriteLine($"The frontend directory '{frontendDir}' does not exist!");
                    return 2;
                }

                frontend = new FileSystemContentSource(frontendDir);

            }

            #endregion

            #region The hub

            Hub hub;

            try
            {
                hub = new Hub(

                          HTTPHostname:     anyAddress
                                                ? IPvXAddress.Any
                                                : IPv4Address.Localhost,

                          HTTPPort:         port,

                          AccountsPath:     accountsPath ?? Path.Combine(RepositoryRoot(), Hub.DefaultAccountsPath),

                          ConfigFile:       new WWCPConfigFile(
                                                configFilePath ?? Path.Combine(RepositoryRoot(), WWCPConfigFile.DefaultFileName)
                                            ),

                          Frontend:         frontend,

                          // Measured from where the hub is started, as every
                          // other path on this command line is. Handed on
                          // relative, it would be measured from the
                          // configuration file.
                          CertificatesPath: certificatesDir is not null
                                                ? Path.GetFullPath(certificatesDir)
                                                : null,

                          ConsoleLogLevel:  verbose ? LogLevel.Debug
                                                : quiet ? LogLevel.Warning
                                                : LogLevel.Info,

                          BridgeDebugLog:   !noTrace

                      );
            }
            catch (Exception e)
            {

                Console.Error.WriteLine($"The hub could not be set up: {e.Message}");

                // A hub that does not come up at all is the one moment the
                // stack trace is worth more than a tidy console.
                if (verbose)
                    Console.Error.WriteLine(e);

                return 1;

            }

            await using (hub)
            {

                #region What the switches said about certificates

                // Before the start, so that a root imported here is believed by
                // the first key exchange with a time server, and not only by the
                // one after it.
                foreach (var (kind, file) in imports)
                {

                    if (!File.Exists(file))
                    {
                        Console.Error.WriteLine($"--import-certificate: there is no file '{file}'.");
                        return 2;
                    }

                    Byte[] content;

                    try
                    {
                        content = await File.ReadAllBytesAsync(file);
                    }
                    catch (Exception problem)
                    {
                        Console.Error.WriteLine($"--import-certificate: '{file}' could not be read: {problem.Message}");
                        return 2;
                    }

                    if (!hub.Certificates.Import(content,
                                                 kind,
                                                 certPassword ?? Environment.GetEnvironmentVariable(CertificatePasswordVariable),
                                                 Label: null,
                                                 out var imported,
                                                 out var problem2))
                    {
                        Console.Error.WriteLine($"--import-certificate: {file} could not be imported as " +
                                                $"{kind.AsText()}: {problem2}");
                        return 2;
                    }

                    Console.WriteLine($"  imported        {imported.Label} as {kind.AsText()}, handle {imported.Id}");

                }

                if (listCertificates)
                    ListCertificates(hub);

                #endregion

                try
                {
                    await hub.Start();
                }
                catch (PortUnavailableException problem)
                {

                    // What somebody starting a second copy of this hub used to
                    // get was a stack trace under the operating system's own
                    // words for a port in use - in German on a German Windows,
                    // with the port named nowhere. The node below says which
                    // port, and what it was for.
                    Console.Error.WriteLine($"The hub could not start: {problem.Message}.");
                    Console.Error.WriteLine("Another copy of this hub already running is the usual answer. Stop it, " +
                                            "or give this one another port with --port <number>.");

                    if (verbose)
                        Console.Error.WriteLine(problem);

                    return 1;

                }

                #region What somebody who just started this needs to know

                // Without a leading slash, because it is put behind a URL that
                // already ends in one.
                var extPath = hub.ExtAPI.RootPath.ToString().Trim('/');

                Console.WriteLine();
                Console.WriteLine($"  web interface   {hub.WebInterfaceURL}");
                Console.WriteLine($"  JSON API        {hub.APIURL}v1/status");
                Console.WriteLine($"  event stream    {hub.APIURL}v1/events");
                Console.WriteLine($"  traffic         {hub.APIURL}v1/traffic");
                Console.WriteLine($"  traffic stream  {hub.APIURL}v1/traffic/events");
                Console.WriteLine($"  HTTPExt API     {hub.WebInterfaceURL}{extPath}/");
                Console.WriteLine($"  frontend from   {(hub.WebInterface is not null ? hub.Frontend.Description : "nothing - a browser asking for '/' gets nothing to render")}");

                // At 18, where this banner's values begin: one further in than
                // the node's.
                foreach (var line in hub.BuiltFrom.BannerLines(18))
                    Console.WriteLine(line);

                Console.WriteLine($"  configuration   {hub.ConfigFile.Path}");
                Console.WriteLine($"  certificates    {hub.Certificates.Entries.Count} in {hub.Certificates.Directory}");
                Console.WriteLine($"  accounts        {hub.ExtAPI.Users.Count()} user(s) in {hub.AccountsPath}");
                Console.WriteLine($"  sign in at      {hub.WebInterfaceURL}{extPath}/login");
                Console.WriteLine($"  OCPI party      {hub.PartyIdText} ({hub.BusinessDetails.Name})");
                Console.WriteLine($"  OCPI versions   {hub.OCPIVersionsURL} ({String.Join(", ", hub.OCPIVersions.Select(version => version.Label))})");
                Console.WriteLine($"  peers           {hub.RemotePartyCount} peered, {hub.RegisteredPartyCount} of them registered, in {hub.OCPIDirectory}");
                Console.WriteLine($"  calls kept      the last {hub.Traffic.Capacity}, in memory only, {(hub.OCPI.Logging?.Payloads == true ? "bodies and all" : "without the bodies")}");
                Console.WriteLine($"  name servers    {(hub.DNSEnabled ? String.Join(", ", hub.DNSClient.DNSServers) : "switched off")}");
                #region The time servers

                var bands = hub.TimeSources.Bands();
                var asked = bands.SelectMany(band => band).ToArray();

                // The group's one server where it has one, and without the
                // root's dot as the servers of a longer list are below:
                // "ptbtime1.ptb.de." is the name exactly, and in the middle of a
                // line somebody reads it reads like a typing mistake. The file
                // keeps it.
                if (asked.Length <= 1)
                    Console.WriteLine($"  time server     {(asked.Length == 1 ? asked[0].Hostname : hub.NTSClient.Hostname).Trimmed}{(hub.NTSEnabled ? "" : " (switched off)")}");

                else
                {

                    // One line per band, because a band is the unit that is
                    // asked at once - putting two bands on one line would read
                    // as six equal servers when it is two and then four. The
                    // names as they are read, as the log names them.
                    for (var i = 0; i < bands.Count; i++)
                        Console.WriteLine((i == 0 ? "  time servers    " : "                  ") +
                                          String.Join(", ", bands[i].Select(source => source.Hostname.Trimmed)) +
                                          (bands.Count > 1 ? $"   (priority {bands[i][0].Priority})" : ""));

                    Console.WriteLine($"                  at least {hub.TimeSources.MinServers} of them must answer" +
                                      (hub.NTSEnabled ? "" : " - and NTS is switched off"));

                }

                #endregion

                if (hub.GeneratedPassword is not null)
                {
                    Console.WriteLine();
                    Console.WriteLine("  ┌─ First start: there were no accounts, so one was made up for you ─────────");
                    Console.WriteLine($"  │  user      {Hub.DefaultAdminUser}");
                    Console.WriteLine($"  │  password  {hub.GeneratedPassword}");
                    Console.WriteLine("  │  It is shown here once and kept only as a hash. Write it down.");
                    Console.WriteLine("  └───────────────────────────────────────────────────────────────────────────");
                }

                Console.WriteLine();

                #endregion

                #region The command line, until 'quit', Ctrl+C or SIGTERM

                // The node's: a prompt where somebody can type, and waiting
                // where nobody can, with the log sharing the screen.
                await new HubCLI(hub).RunUntilStopped();

                #endregion

            }

            #endregion

            return 0;

        }

    }

}
