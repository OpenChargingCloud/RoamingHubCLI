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
using cloud.charging.open.RoamingHub.Logging;

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

        #region (private static) PrintUsage()

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: RoamingHubCLI [--port <number>] [--any] [--frontend <dist directory>]");
            Console.WriteLine("                     [--config <file>] [--accounts <directory>]");
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
            Console.WriteLine("  --config <file>   where the name servers, the time server and the OCPI identity of");
            Console.WriteLine($"                    this hub live (default: {RoamingHubConfigFile.DefaultFileName} below the repository");
            Console.WriteLine("                    root). Without the file the hub runs on the system defaults and is");
            Console.WriteLine($"                    {OCPIConfiguration.DefaultCountryCode}-{OCPIConfiguration.DefaultPartyId} in OCPI; the name and time servers written there take");
            Console.WriteLine("                    effect at once. Who this hub is in OCPI is read once, at the start,");
            Console.WriteLine("                    and deliberately not changeable while running: it is what every");
            Console.WriteLine("                    peer wrote into its credentials. The peers themselves are not in");
            Console.WriteLine($"                    the file: the OCPI library keeps them below {Hub.OCPIDirectoryName}/ beside it, one");
            Console.WriteLine("                    set per OCPI version, and reads them back at every start.");
            Console.WriteLine();
            Console.WriteLine("Traffic:");
            Console.WriteLine("  Every OCPI call that touched this hub is written down - which way it went, which");
            Console.WriteLine("  peer was at the other end, the two parties, the HTTP status and the OCPI status");
            Console.WriteLine("  inside the envelope, how long it took - and served at /api/v1/traffic, with a");
            Console.WriteLine("  stream beside it. Reading it is its own permission, readTraffic, and not part of");
            Console.WriteLine("  readConfiguration: the configuration is what this hub is, and the traffic is what");
            Console.WriteLine("  its peers did through it. It is in memory and nowhere else, and the bodies are");
            Console.WriteLine("  left out unless the configuration file says ocpi.logging.payloads. A deployment");
            Console.WriteLine("  that has to keep more should read the stream and put it where it keeps such things.");
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

                          ConfigFile:       new RoamingHubConfigFile(
                                                configFilePath ?? Path.Combine(RepositoryRoot(), RoamingHubConfigFile.DefaultFileName)
                                            ),

                          Frontend:         frontend,

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

                await hub.Start();

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
                Console.WriteLine($"  configuration   {hub.ConfigFile.Path}");
                Console.WriteLine($"  accounts        {hub.ExtAPI.Users.Count()} user(s) in {hub.AccountsPath}");
                Console.WriteLine($"  sign in at      {hub.WebInterfaceURL}{extPath}/login");
                Console.WriteLine($"  OCPI party      {hub.PartyIdText} ({hub.BusinessDetails.Name})");
                Console.WriteLine($"  OCPI versions   {hub.OCPIVersionsURL} ({String.Join(", ", hub.OCPIVersions.Select(version => version.Label))})");
                Console.WriteLine($"  peers           {hub.RemotePartyCount} peered, {hub.RegisteredPartyCount} of them registered, in {hub.OCPIDirectory}");
                Console.WriteLine($"  calls kept      the last {hub.Traffic.Capacity}, in memory only, {(hub.OCPI.Logging?.Payloads == true ? "bodies and all" : "without the bodies")}");
                Console.WriteLine($"  name servers    {(hub.DNSEnabled ? String.Join(", ", hub.DNSClient.DNSServers) : "switched off")}");
                Console.WriteLine($"  time server     {hub.NTSClient.Hostname}{(hub.NTSEnabled ? "" : " (switched off)")}");

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

                #region The command line, until 'quit' or Ctrl+C

                // Whether anybody can type here at all. Started from a script,
                // from a service manager or in CI, this process has no terminal
                // on its input and Console.ReadKey throws rather than waiting -
                // and there would be nobody to type anyway. Then the hub simply
                // runs, exactly as it did before there was a command line, and
                // the web interface is how it is spoken to.
                //
                // The output counts too: the prompt is drawn by moving the
                // cursor, and with the output going into "| tee" or a file there
                // is no cursor to move. Measured on Windows with the vehicle,
                // whose prompt then looked at its input only: the prompt threw
                // while drawing itself, before a key was pressed, and the
                // program was gone within 200 ms of its banner - with exit code
                // 0, a program that said all was well.
                var canBeTypedAt = !Console.IsInputRedirected &&
                                   !Console.IsOutputRedirected;

                Console.WriteLine(canBeTypedAt
                                      ? "Type 'help' for what can be typed here, 'quit' or Ctrl+C to stop."
                                      : "Press Ctrl+C to stop. (No terminal here, so nothing to type at.)");
                Console.WriteLine();

                var stopped = new TaskCompletionSource();

                // Ctrl+C still means stop, as it always has here. The command
                // line adds a handler of its own for it, which cancels whatever
                // command is running; both fire, and that is the intended
                // reading of Ctrl+C - abandon what is running and shut the hub
                // down. 'quit' is the same thing said politely.
                Console.CancelKeyPress += (_, e) => {
                    e.Cancel = true;
                    stopped.TrySetResult();
                };

                if (canBeTypedAt)
                {

                    var cli             = new HubCLI(hub);
                    var brokeAtOnce     = false;

                    while (true)
                    {

                        // From here two things write on one screen: this command
                        // line, and the hub's log from whichever thread did the
                        // thing it is reporting. So the log stops writing of its
                        // own accord and asks the command line for the screen
                        // instead - which takes the half-typed command off it,
                        // writes the entry whole, and puts the command back with
                        // the cursor where it was.
                        hub.ShareConsoleWith(cli.WriteBlock);

                        // On a thread of its own, because Console.ReadKey blocks
                        // the one it is called on: awaited directly, the command
                        // line would keep this thread inside ReadKey and Ctrl+C
                        // would have nobody left to wake.
                        var since   = System.Diagnostics.Stopwatch.GetTimestamp();
                        var typing  = Task.Run(cli.Run);

                        await Task.WhenAny(stopped.Task, typing);

                        if (!typing.IsFaulted)
                            break;

                        // A command line that broke is not somebody asking for
                        // the hub to stop. What broke it first, in the vehicle
                        // and the charging station, was a line typed wider than
                        // the window: until Styx learned to show such a line
                        // through a window onto it, it threw out of the line
                        // editor - measured in 80 columns, "Parameter 'left',
                        // actual value was 80" - and a program that took that
                        // for 'quit' shut down with exit code 0. That cause is
                        // gone; this is for the next one.
                        //
                        // The console goes back to the log first, with a lock
                        // of its own, because the command line's way of writing
                        // may be what broke: a prompt that fails while drawing
                        // itself stays registered as the line on the screen,
                        // and every entry after that fails trying to take it
                        // off again.
                        //
                        // Then a new prompt - unless the last one was already
                        // a new one and broke again the moment it started.
                        // That is a console a prompt cannot be drawn on at all,
                        // and asking a third time would only fail a third time.
                        // How fast the first one broke says nothing: a line
                        // pasted in straight after the start is still a line.
                        var padlock = new Lock();

                        hub.ShareConsoleWith(write => { lock (padlock) { write(); } });

                        var atOnce = System.Diagnostics.Stopwatch.GetElapsedTime(since) < TimeSpan.FromSeconds(1);
                        var giveUp = atOnce && brokeAtOnce;

                        brokeAtOnce = atOnce;

                        // On one line, as every entry is: the message of an
                        // exception may carry line breaks of its own, and a
                        // second line of an entry has no time, no level and no
                        // tags.
                        var why = typing.Exception?.GetBaseException().Message.ReplaceLineEndings(" ");

                        hub.Log.Warning(
                            $"The command line stopped working: {why} " +
                            (giveUp
                                 ? "A new one broke again as soon as it started, so there is none; the hub keeps running, and Ctrl+C stops it."
                                 : "A new one is started."),
                            "cli"
                        );

                        if (giveUp)
                        {
                            await stopped.Task;
                            break;
                        }

                    }

                }

                else
                    await stopped.Task;

                #endregion

            }

            #endregion

            return 0;

        }

    }

}
