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

using org.GraphDefined.Vanaheimr.CLI;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.RoamingHub.CommandLine
{

    /// <summary>
    /// The roaming network operator this hub is: who it is, the public halves of its keys, where its private keys
    /// are kept, and the roaming networks it operates.
    /// </summary>
    /// <remarks>
    /// Public, as every roaming network it operates names all of it; the private keys are never shown.
    /// </remarks>
    /// <param name="CLI">The command line.</param>
    public class RoamingNetworkOperatorCommand(HubCLI CLI) : ACLICommand<HubCLI>(CLI),
                                                             ICLICommand
    {

        #region Data

        /// <summary>
        /// The name this is typed as.
        /// </summary>
        public const String CommandName = "roamingNetworkOperator";

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

            var networkOperator  = cli.Hub.NetworkOperator;
            var name             = networkOperator.Operator.Name.FirstText();
            var operated         = cli.Hub.POI.RoamingNetworks.Where(networkOperator.Operates).
                                                               Select(network => network.Id.ToString()).
                                                               Order(StringComparer.OrdinalIgnoreCase).
                                                               ToArray();

            return Task.FromResult<String[]>([
                $"This hub is the roaming network operator '{networkOperator.Id}'{(name.Length > 0 ? $", {name}" : "")}, since {networkOperator.Operator.Created.ToISO8601()}.",
                $"  signing key   {networkOperator.SigningKey. Id}  {networkOperator.SigningKey. ToJSON()["key"]}",
                $"  identity key  {networkOperator.IdentityKey.Id}  {networkOperator.IdentityKey.ToJSON()["key"]}",
                $"  its private keys are kept in '{networkOperator.Directory}'",
                operated.Length > 0
                    ? $"  it operates {String.Join(", ", operated)}"
                    : $"  it operates no roaming network yet: '{RoamingNetworksCommand.CommandName} add <id> [<name>]' makes one"
            ]);

        }

        #endregion

        #region Help()

        public override String Help()

            => $"{CommandName} - the roaming network operator this hub is, its public keys, and the roaming networks it operates";

        #endregion

    }

}
