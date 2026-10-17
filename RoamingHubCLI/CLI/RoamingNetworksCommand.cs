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

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.CLI;
using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.protocols.WWCP.Node.Web;

#endregion

namespace cloud.charging.open.RoamingHub.CommandLine
{

    /// <summary>
    /// The roaming networks of the points of interest: listed, and a new one made.
    /// </summary>
    /// <remarks>
    /// <para>
    /// roamingNetworks lists them, each with its revision and what it holds; roamingNetworks add &lt;id&gt;
    /// signingKey &lt;key&gt; identityKey &lt;key&gt; [signingKey &lt;key&gt; | identityKey &lt;key&gt; ...]
    /// [&lt;name&gt;] makes one, with the public keys of its roaming network operator: each the Base64 of its
    /// COSE key, as the "key" of a public key in POI 3 says it. The words after the keys are its name, in English.
    /// </para>
    /// <para>
    /// Making one is poi:edit, as on the web interface: the administrators, and the console, which may do
    /// everything. Everything in it afterwards comes as change sets its owners sign, submitted to the POI API.
    /// </para>
    /// </remarks>
    /// <param name="CLI">The command line.</param>
    public class RoamingNetworksCommand(HubCLI CLI) : ACLICommand<HubCLI>(CLI),
                                                      ICLICommand
    {

        #region Data

        /// <summary>
        /// The name this is typed as.
        /// </summary>
        public const String CommandName = "roamingNetworks";

        #endregion

        #region Suggest(Arguments)

        public override IEnumerable<SuggestionResponse> Suggest(String[] Arguments)
        {

            if (Arguments.Length == 1)
                return CommandName.StartsWith(Arguments[0], StringComparison.OrdinalIgnoreCase)
                           ? [ SuggestionResponse.CommandCompleted(CommandName) ]
                           : [];

            return [];

        }

        #endregion

        #region Execute(Arguments, CancellationToken)

        public override Task<String[]> Execute(String[]           Arguments,
                                               CancellationToken  CancellationToken)
        {

            var rest = Arguments.Skip(1).ToArray();

            if (rest.Length == 0)
                return Task.FromResult(Listed());

            if (rest[0].Equals("add", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Add(rest[1..]));

            return Task.FromResult<String[]>([ $"Usage: {Help()}" ]);

        }

        #endregion

        #region Help()

        public override String Help()

            => $"{CommandName} [add <id> signingKey <key> identityKey <key> [signingKey <key> | identityKey <key> ...] [<name>]] - the roaming networks of the points of interest, and a new one";

        #endregion


        #region (private) Listed()

        private String[] Listed()
        {

            var lines = new List<String>();

            foreach (var network in cli.Hub.POI.RoamingNetworks.OrderBy(network => network.Id.ToString(), StringComparer.OrdinalIgnoreCase))
            {

                var revision = cli.Hub.POI.TryGetRoamingNetworkHistory(network.Id, out var history)
                                   ? $"revision {history.Head.Commit.Revision}"
                                   : "no history";

                var operators = network.ChargingStationOperators.Count();
                var pools     = network.ChargingStationOperators.Sum(chargingStationOperator => chargingStationOperator.ChargingPools.Count());

                lines.Add($"{network.Id}  {revision}  {network.Name.FirstText()}".TrimEnd() +
                          $"  ({operators} charging station operator(s), {pools} charging pool(s))");

            }

            return lines.Count > 0
                       ? [ .. lines ]
                       : [ $"This hub serves no roaming network yet: '{CommandName} add ...' makes one." ];

        }

        #endregion

        #region (private) Add(Words)

        private String[] Add(String[] Words)
        {

            if (!cli.MayDo(Permission.Edit(HubAccess.POI), $"{CommandName} add", out var refused))
                return [ refused ];

            if (Words.Length == 0)
                return [ $"Usage: {Help()}" ];

            var signingKeys   = new JArray();
            var identityKeys  = new JArray();
            var index         = 1;

            // The keys, each after the word that says which kind it is; the words after them are the name.
            while (index + 1 < Words.Length)
            {

                if      (Words[index].Equals("signingKey",  StringComparison.OrdinalIgnoreCase))
                    signingKeys. Add(Words[index + 1]);

                else if (Words[index].Equals("identityKey", StringComparison.OrdinalIgnoreCase))
                    identityKeys.Add(Words[index + 1]);

                else
                    break;

                index += 2;

            }

            var name = String.Join(" ", Words[index..]).Trim();

            var json = new JObject(
                           new JProperty("@id",           Words[0]),
                           new JProperty("signingKeys",   signingKeys),
                           new JProperty("identityKeys",  identityKeys)
                       );

            if (name.Length > 0)
                json["name"] = new JObject(new JProperty("en", name));

            var result = cli.Hub.CreateRoamingNetwork(json);

            if (!result.Success || result.History is null)
                return [ $"No roaming network was made: {result.Message}" ];

            var network = result.History.Head.RoamingNetwork;

            cli.Hub.Log.Notice(
                $"{cli.Caller.Asker} made the roaming network '{network.Id}', with {network.SigningKeys.Length} signing and " +
                $"{network.IdentityKeys.Length} identity key(s) of its roaming network operator.",
                cli.Caller.Tags("poi", "security")
            );

            return [
                result.Message,
                $"{cli.Hub.WebInterfaceURL.ToString().TrimEnd('/')}/{RoamingHub.POIPathSegment}/RNs/{network.Id}"
            ];

        }

        #endregion

    }

}
