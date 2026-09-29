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

using cloud.charging.open.RoamingHub.CommandLine;
using cloud.charging.open.RoamingHub.Configuration;
using cloud.charging.open.protocols.WWCP.Node.CommandLine;
using cloud.charging.open.protocols.WWCP.Node.Configuration;

// Inside this namespace "RoamingHub" is the namespace and not the class, so
// the class needs a name of its own here.
using Hub = cloud.charging.open.RoamingHub.RoamingHub;

#endregion

namespace cloud.charging.open.RoamingHub.CLI
{

    /// <summary>
    /// One OCPI roaming hub, with its JSON API and a prompt, until 'quit',
    /// Ctrl+C or SIGTERM.
    /// </summary>
    /// <remarks>
    /// What every kind of node's program does is the node's: the switches and
    /// the words -h explains them with, why it could not be set up or could not
    /// start, what goes into the certificate store, the banner and the prompt.
    /// What is left here is the hub's: what its configuration holds, what -h
    /// says of its traffic and its OCPI versions, and what its banner says of
    /// both.
    /// </remarks>
    public class Program
    {

        #region (private static) Usage

        /// <summary>
        /// What -h shows: every node's switches, in a roaming hub's words.
        /// </summary>
        private static readonly NodeUsage Usage = new (

            Program:            "RoamingHubCLI",
            Kind:               Hub.RoamingHubKind,
            DefaultPort:        Hub.DefaultHTTPPort,
            FrontendSources:    "libs/RoamingHub/RoamingHub/Frontend",

            ConfigurationSays:  "where the name servers, the time servers and the OCPI identity of this hub live " +
                               $"(default: {WWCPConfigFile.DefaultFileName} below the repository root). Without the file the " +
                               $"hub runs on the system defaults and is {OCPIConfiguration.DefaultCountryCode}-" +
                               $"{OCPIConfiguration.DefaultPartyId} in OCPI; the name and time servers written there take " +
                                "effect at once. Who this hub is in OCPI is read once, at the start, and deliberately not " +
                                "changeable while running: it is what every peer wrote into its credentials. The peers " +
                               $"themselves are not in the file: the OCPI library keeps them below {Hub.OCPIDirectoryName}/ " +
                                "beside it, one set per OCPI version, and reads them back at every start.",

            CertificateKinds:   Hub.CertificateKinds,

            BeforeTheLog:       [

                "Traffic:",

                .. NodeUsage.Wrap("Every OCPI call that touched this hub is written down - which way it went, which peer " +
                                  "was at the other end, the two parties, the HTTP status and the OCPI status inside the " +
                                  "envelope, how long it took - and served at /api/v1/traffic, with a stream beside it. " +
                                  "Reading it is its own permission, traffic:read, and not part of configuration:read: the " +
                                  "configuration is what this hub is, and the traffic is what its peers did through it. " +
                                  "The hub role may read it and the viewer may not. It is in memory and nowhere else, and " +
                                  "the bodies are left out unless the configuration file says ocpi.logging.payloads. A " +
                                  "deployment that has to keep more should read the stream and put it where it keeps such " +
                                  "things.",
                                  "  ", "  "),

                "",

                "OCPI versions:",

                .. NodeUsage.Wrap($"{String.Join(" and ", OCPIConfiguration.KnownVersions)}, and no 2.1.1: the hub role " +
                                   "arrived with OCPI 2.2, and the library has no hub side for the version before it. A " +
                                   "CPO or an EMSP that speaks only 2.1.1 cannot be peered with this hub; it has to talk " +
                                   "to its counterpart directly.",
                                  "  ", "  "),

                ""

            ]

        );

        #endregion


        public static async Task<Int32> Main(String[] Arguments)
        {

            Console.OutputEncoding = System.Text.Encoding.UTF8;

            #region Arguments

            // Every node's switches; a hub has none of its own.
            var arguments = NodeArguments.Parse(Arguments);

            if (arguments.Refused(Usage) is Int32 refused)
                return refused;

            if (arguments.RefuseTheRest(Usage) is Int32 unknown)
                return unknown;

            var root = NodeProgram.RepositoryRoot("RoamingHubCLI.slnx");

            #endregion

            #region The hub

            Hub hub;

            try
            {
                hub = new Hub(
                          HTTPHostname:      arguments.HTTPHostname,
                          HTTPPort:          arguments.Port,
                          AccountsPath:      arguments.AccountsPathBelow(root),
                          ConfigFile:        new WWCPConfigFile(arguments.ConfigFilePathBelow(root)),
                          Frontend:          arguments.Frontend,
                          CertificatesPath:  arguments.CertificatesPath,
                          ConsoleLogLevel:   arguments.ConsoleLogLevel,
                          LogPath:           arguments.LogPathBelow(root),
                          BridgeDebugLog:    !arguments.NoTrace
                      );
            }
            catch (Exception e)
            {
                return NodeProgram.CouldNotBeSetUp(Hub.RoamingHubKind, e, arguments.Verbose);
            }

            await using (hub)
            {

                if (hub.ImportCertificates(arguments, out _) is Int32 notImported)
                    return notImported;

                if (arguments.ListCertificates)
                    hub.ListCertificates();

                if (await hub.Started(arguments.Verbose) is Int32 notStarted)
                    return notStarted;

                #region What somebody who just started this needs to know

                foreach (var line in hub.Banner(
                                         BesideTheInterfaces: [
                                             ("traffic",         $"{hub.APIURL}v1/traffic"),
                                             ("traffic stream",  $"{hub.APIURL}v1/traffic/events")
                                         ],
                                         OfTheKind: [
                                             ("OCPI party",      $"{hub.PartyIdText} ({hub.BusinessDetails.Name})"),
                                             ("OCPI versions",   $"{hub.OCPIVersionsURL} ({String.Join(", ", hub.OCPIVersions.Select(version => version.Label))})"),
                                             ("peers",           $"{hub.RemotePartyCount} peered, {hub.RegisteredPartyCount} of them registered, in {hub.OCPIDirectory}"),
                                             ("calls kept",      $"the last {hub.Traffic.Capacity}, in memory only, " +
                                                                 (hub.OCPI.Logging?.Payloads == true ? "bodies and all" : "without the bodies"))
                                         ]))
                    Console.WriteLine(line);

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
