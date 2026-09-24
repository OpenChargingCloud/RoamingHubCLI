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

using System.Globalization;

using Newtonsoft.Json.Linq;

using org.GraphDefined.Vanaheimr.CLI;
using org.GraphDefined.Vanaheimr.Illias;

#endregion

namespace cloud.charging.open.RoamingHub.CommandLine
{

    /// <summary>
    /// Ask this roaming hub's time server what the time is.
    /// </summary>
    /// <remarks>
    /// The same thing the NTS page does with 'Sync now'. Both end in
    /// <see cref="RoamingHub.SyncTimeAsync"/>, which asks the same server,
    /// writes every step into the log and keeps the result as the last
    /// synchronisation the page shows - so the log book reads the same
    /// whichever of the two was used, apart from the one line that says who
    /// asked. A command that did any of that itself would be the beginning of
    /// two hubs disagreeing about what they did.
    ///
    /// Like the button, it does not step the clock: it says whether the time
    /// server can be reached and what it thinks of the local clock.
    ///
    /// Without an argument, and only without: the vehicle's and the charging
    /// station's take a server's name to test that one in detail, and this hub
    /// has one server and no such test to offer.
    /// </remarks>
    /// <param name="CLI">The command line of the roaming hub to ask.</param>
    public class SyncNTSCommand(HubCLI CLI) : ACLICommand<HubCLI>(CLI),
                                              ICLICommand
    {

        #region Data

        /// <summary>
        /// The name this is typed as. Taken from the class name, as everywhere
        /// else: "SyncNTSCommand" without its last seven characters. Found in
        /// any case - "syncnts" is the same command.
        /// </summary>
        public static readonly String CommandName = nameof(SyncNTSCommand)[..^7].ToLowerFirstChar();

        #endregion

        #region Suggest(Arguments)

        /// <summary>
        /// Complete the command. It takes nothing after it: which server is
        /// asked is the hub's configuration, as it is for the button.
        /// </summary>
        public override IEnumerable<SuggestionResponse> Suggest(String[] Arguments)
        {

            if (Arguments.Length == 1 &&
                CommandName.StartsWith(Arguments[0], StringComparison.CurrentCultureIgnoreCase))
            {
                return [ SuggestionResponse.CommandCompleted(CommandName) ];
            }

            return [];

        }

        #endregion

        #region Execute(Arguments, CancellationToken)

        public override async Task<String[]> Execute(String[]           Arguments,
                                                     CancellationToken  CancellationToken)
        {

            if (Arguments.Length > 1)
                return [ $"Usage: {Help()}" ];

            // The line the web interface writes when 'Sync now' is pressed, in
            // the same words, at the same level and with the tags it carries
            // there - "cli" where it says "web". There it names the account
            // that pressed the button; here it is whoever is at the console,
            // which the hub cannot tell apart. Said before anything is asked,
            // because the entries that follow record what was asked and what
            // came of it, and nothing in them says who wanted it.
            cli.Hub.Log.Info(
                "Somebody at the command line asked this RoamingHub to synchronise its time.",
                "nts", "test", "cli"
            );

            var result = await cli.Hub.SyncTimeAsync(CancellationToken);

            return Outcome(result);

        }

        #endregion

        #region Help()

        public override String Help()

            => $"{CommandName} - ask the time server what the time is, as 'Sync now' on the NTS page does; the clock is not stepped";

        #endregion


        #region (private static) Outcome(Result)

        /// <summary>
        /// What the NTS page shows after 'Sync now', as lines for the console:
        /// how it went, and what the server said.
        /// </summary>
        /// <remarks>
        /// The key exchange and the NTP answer get a line of their own
        /// because the log has them only as the steps they were taken in, and
        /// somebody at the console wants the verdict first.
        /// </remarks>
        private static String[] Outcome(JObject Result)
        {

            var lines   = new List<String>();

            // Without the root's dot, as the banner writes it:
            // "ptbtime1.ptb.de." is correct and nobody reads it that way.
            var server  = (Result.Value<String>("server") ?? "the time server").TrimEnd('.');
            var took    = Result.Value<Int64?>("runtime_ms") is Int64 runtime
                              ? $" after {runtime} ms"
                              : "";

            if (!Result.Value<Boolean>("ok"))
            {
                lines.Add($"failed{took}: {Result.Value<String>("error") ?? "no reason was given"}");
                return [.. lines];
            }

            lines.Add($"succeeded{took}: {server} says this hub's clock is " +
                      $"{Milliseconds(Result.Value<Double?>("offset_ms"), Signed: true)} off");

            if (Result["ntske"] is JObject keyExchange)
                lines.Add($"  key exchange  {keyExchange.Value<String>("aeadAlgorithm") ?? "unknown"}, " +
                          $"{keyExchange.Value<Int32?>("cookies")} cookie(s), " +
                          $"{keyExchange.Value<Int64?>("runtime_ms")} ms");

            if (Result["ntp"] is JObject ntp)
                lines.Add($"  time          round trip {Milliseconds(ntp.Value<Double?>("roundTrip_ms"))}, " +
                          $"{ntp.Value<Int32?>("cookiesLeft")} cookie(s) left" +
                          (ntp.Value<String>("kissOfDeath") is String kissOfDeath && kissOfDeath.Length > 0
                               ? $", kiss of death {kissOfDeath}"
                               : ""));

            return [.. lines];

        }

        #endregion

        #region (private static) Milliseconds(Value, Signed = false)

        /// <summary>
        /// The one place a number with a fraction is written, and so the one
        /// place the culture has to be kept out: these sentences are English,
        /// and a hub in a German locale would otherwise write "+1,2 ms" in the
        /// middle of one.
        /// </summary>
        private static String Milliseconds(Double? Value, Boolean Signed = false)

            => Value is Double value
                   ? value.ToString(Signed ? "+0.0;-0.0;0.0" : "0.0", CultureInfo.InvariantCulture) + " ms"
                   : "unknown";

        #endregion

    }

}
