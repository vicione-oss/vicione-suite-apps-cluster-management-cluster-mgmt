using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using ClusterManagement.Internal.Contracts;

namespace ClusterManagement.Backend.Connections;

/// <summary>
/// Checks an application certificate against the settings of an OPC UA connection the way the OPC UA stack uses them.
/// </summary>
internal static partial class OpcUaApplicationCertificateCheck
{
    private const string SubjectAlternativeNameOid = "2.5.29.17";
    private static readonly Asn1Tag s_uniformResourceIdentifierTag = new(TagClass.ContextSpecific, 6);

    /// <summary>
    /// Gets the application URI in the certificate's subject alternative name, <see langword="null"/> if it has none.
    /// </summary>
    public static string? GetApplicationUri(X509Certificate2 certificate)
    {
        if (certificate.Extensions[SubjectAlternativeNameOid] is not { } extension)
            return null;

        try
        {
            // GeneralNames ::= SEQUENCE OF GeneralName, the URI is the [6] IA5String choice
            var names = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence();
            while (names.HasData)
            {
                if (names.PeekTag().HasSameClassAndValue(s_uniformResourceIdentifierTag))
                    return names.ReadCharacterString(UniversalTagNumber.IA5String, s_uniformResourceIdentifierTag);

                names.ReadEncodedValue();
            }
        }
        catch (AsnContentException)
        {
            // a malformed extension carries no usable URI
        }

        return null;
    }

    /// <summary>
    /// Returns the settings the certificate does not match.
    /// </summary>
    /// <remarks>
    /// The OPC UA stack replaces <c>DC=localhost</c> in the subject by the name of the host it runs on, so does the check;
    /// the application URI it uses as it is.
    /// </remarks>
    public static IReadOnlyCollection<OpcUaApplicationCertificateWarning> GetWarnings(
        OpcUaCertificateInfo certificate, string applicationCertificateSubject, string applicationUri, string hostName)
    {
        List<OpcUaApplicationCertificateWarning> warnings = [];

        if (!IsSameDistinguishedName(certificate.Subject, LocalhostDomainComponent().Replace(applicationCertificateSubject, "DC=" + hostName)))
            warnings.Add(OpcUaApplicationCertificateWarning.SubjectMismatch);

        if (!string.Equals(certificate.ApplicationUri, applicationUri, StringComparison.OrdinalIgnoreCase))
            warnings.Add(OpcUaApplicationCertificateWarning.ApplicationUriMismatch);

        return warnings;
    }

    // the parts of a distinguished name may come in any order, with any spacing and casing
    private static bool IsSameDistinguishedName(string actual, string expected)
    {
        try
        {
            return GetParts(actual).SetEquals(GetParts(expected));
        }
        catch (CryptographicException)
        {
            // a subject the connection cannot express as a distinguished name does not match any certificate
            return false;
        }

        static HashSet<string> GetParts(string distinguishedName)
            => [.. new X500DistinguishedName(distinguishedName)
                .Decode(X500DistinguishedNameFlags.UseNewLines | X500DistinguishedNameFlags.DoNotUsePlusSign)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.ToUpperInvariant())];
    }

    [GeneratedRegex(@"DC=localhost\b", RegexOptions.IgnoreCase)]
    private static partial Regex LocalhostDomainComponent();
}
