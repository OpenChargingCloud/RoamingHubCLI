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

using System.Reflection;

using cloud.charging.open.protocols.WWCP.Node.CommandLine;

// Inside this namespace "RoamingHub" is the namespace and not the class, so
// the class needs a name of its own here.
using Hub = cloud.charging.open.RoamingHub.RoamingHub;

#endregion

namespace cloud.charging.open.RoamingHub.CommandLine
{

    /// <summary>
    /// The command line of a running roaming hub.
    /// </summary>
    /// <remarks>
    /// The node's command line, with the commands every node has - syncNTS
    /// among them - and the console until 'quit', Ctrl+C or SIGTERM. What only
    /// a roaming hub can be told is a command built from a HubCLI in this
    /// assembly, found as the node's are: a new command is a new file and
    /// nothing else.
    ///
    /// Not in a namespace called CLI, as the program is: Styx's command line
    /// class is called that, and a namespace of the same name one level up
    /// would be found first.
    /// </remarks>
    public class HubCLI : NodeCLI
    {

        #region Data

        /// <summary>
        /// What stands in front of the command being typed.
        /// </summary>
        public const String Prompt = "RoamingHub> ";

        #endregion

        #region Properties

        /// <summary>
        /// The roaming hub these commands are about.
        /// </summary>
        public Hub  Hub  { get; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the command line of the given roaming hub.
        /// </summary>
        /// <param name="Hub">The running roaming hub.</param>
        /// <param name="AssembliesWithCLICommands">Further assemblies to search for commands. This one and the node's are searched either way.</param>
        public HubCLI(Hub                Hub,
                      params Assembly[]  AssembliesWithCLICommands)

            : base(Hub, AssembliesWithCLICommands)

        {

            this.Hub = Hub;

            RegisterCLIType(typeof(HubCLI));

        }

        #endregion


        #region (protected override) GetPrompt()

        /// <summary>
        /// What this program is, because a roaming hub is usually started on a
        /// bench beside the CPOs and EMSPs it is peered with, and their consoles
        /// should not have to be told apart by what scrolls past on them.
        /// </summary>
        /// <remarks>
        /// Not its OCPI party, which is a configured value its peers know it
        /// by: two hubs on one bench usually share the default one, and would
        /// read the same.
        /// </remarks>
        protected override String GetPrompt()

            => Prompt;

        #endregion

    }

}
