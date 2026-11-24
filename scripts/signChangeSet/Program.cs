/*
 * A stand-in for a party of a roaming network that cannot yet sign a change
 * set waiting at the hub - here the grid operator, whose node keeps its keys
 * as PEM files but has no way to sign what the hub waits for.
 *
 *   signChangeSet <pending.json> <private-key.pem> [<private-key.pem> ...]
 *
 * <pending.json> is what GET ~/poi/RNs/{id}/history/changeSets/pending/{changeSetId}
 * answers - the change set, or an object holding it as "changeSet". Written to
 * standard output: { "signatures": [ ... ] }, one signature by each key, for
 * POST ~/poi/RNs/{id}/history/changeSets/pending/{changeSetId}/signatures.
 */

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

using org.GraphDefined.Vanaheimr.Illias;

using cloud.charging.open.protocols.WWCP.POI;

if (args.Length < 2)
{
    Console.Error.WriteLine("signChangeSet <pending.json> <private-key.pem> [<private-key.pem> ...]");
    return 2;
}

var json = JObject.Parse(File.ReadAllText(args[0]));

if (json["changeSet"] is JObject inner)
    json = inner;

if (!RoamingNetworkChangeSet.TryParse(json, out var changeSet, out var error))
{
    Console.Error.WriteLine($"'{args[0]}' holds no change set: {error}");
    return 1;
}

var signatures = new JArray();

foreach (var path in args[1..])
{

    var text = File.ReadAllText(path);

    if (!System.Security.Cryptography.PemEncoding.TryFind(text, out var fields))
    {
        Console.Error.WriteLine($"'{path}' holds no PEM.");
        return 1;
    }

    if (PrivateKeyFactory.CreateKey(Convert.FromBase64String(text[fields.Base64Data])) is not ECPrivateKeyParameters key)
    {
        Console.Error.WriteLine($"'{path}' holds no elliptic curve private key.");
        return 1;
    }

    var algorithm = key.Parameters.Curve.FieldSize switch {
                        256 => COSEAlgorithm.ES256,
                        384 => COSEAlgorithm.ES384,
                        521 => COSEAlgorithm.ES512,
                        var size => throw new InvalidOperationException($"'{path}': no algorithm for a curve of {size} bits.")
                    };

    var signed = changeSet.Sign(key, POIPublicKey.From(key).Id, algorithm);

    signatures.Add(signed.Signatures[^1].ToJSON(Embedded: true));

}

Console.WriteLine(new JObject(new JProperty("signatures", signatures)).ToString(Formatting.None));

return 0;
