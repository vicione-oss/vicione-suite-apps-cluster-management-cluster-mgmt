using System.Globalization;
using System.IO.Abstractions;
using System.Xml.Linq;
using ClusterManagement.Public.Iodds;

namespace ClusterManagement.Backend.Iodds;

internal readonly ref struct IoddParser
{
    private readonly XElement _root;
    private readonly XNamespace _ns;
    private readonly Dictionary<string, string> _texts;

    private IoddParser(XDocument document)
    {
        _ns = document.Root?.GetDefaultNamespace() ?? throw new InvalidDataException("Invalid IODD document: missing namespace.");
        _root = document.Root ?? throw new InvalidDataException("Invalid IODD document: missing root element.");
        _texts = _root
            .Element(_ns + "ExternalTextCollection")?
            .Element(_ns + "PrimaryLanguage")?
            .Elements(_ns + "Text")
            .Where(e => e.Attribute("id") is not null && e.Attribute("value") is not null)
            .ToDictionary(e => e.Attribute("id")!.Value, e => e.Attribute("value")!.Value)
            ?? [];
    }

    internal static IODevice ParseFile(string filePath, IFileSystem fileSystem)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !fileSystem.File.Exists(filePath))
            throw new FileNotFoundException("IODD file not found", filePath);

        using var stream = fileSystem.File.OpenRead(filePath);
        return ParseStream(stream);
    }

    internal static IODevice ParseStream(Stream stream)
        => new IoddParser(XDocument.Load(stream)).Parse();

    internal static IODevice ParseText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("IODD text is empty", nameof(text));
        return new IoddParser(XDocument.Parse(text)).Parse();
    }

    private IODevice Parse()
        => new()
        {
            ProfileHeader = ParseProfileHeader(),
            DocumentInfo = ParseDocumentInfo(),
            DeviceIdentity = ParseDeviceIdentity(),
        };

    private ProfileHeader ParseProfileHeader()
    {
        var profileHeaderElement = _root.Element(_ns + "ProfileHeader");
        return new()
        {
            ProfileRevision = profileHeaderElement?.Element(_ns + "ProfileRevision")?.Value ?? string.Empty,
        };
    }

    private DocumentInfo ParseDocumentInfo()
    {
        var releaseDateElement = _root.Element(_ns + "DocumentInfo")?.Attribute("releaseDate");
        return new()
        {
            ReleaseDate = releaseDateElement is null ? null : DateOnly.Parse(releaseDateElement.Value, CultureInfo.InvariantCulture),
            Copyright = (string?)_root.Element(_ns + "DocumentInfo")?.Attribute("copyright") ?? string.Empty,
            Version = (string?)_root.Element(_ns + "DocumentInfo")?.Attribute("version") ?? string.Empty,
        };
    }

    private DeviceIdentity ParseDeviceIdentity()
    {
        var identity = _root.Element(_ns + "ProfileBody")?
                            .Element(_ns + "DeviceIdentity")
                     ?? throw new InvalidDataException("<DeviceIdentity> element not found");
        var familyId = _root
            .Descendants(_ns + "DeviceFamily")
            .Select(e => (string?)e.Attribute("textId"))
            .FirstOrDefault(id => id is not null);
        var deviceNameId = identity.Element(_ns + "DeviceName")?.Attribute("textId")?.Value;

        List<DeviceVariant> deviceVariants = [];

        foreach (var variant in identity.Element(_ns + "DeviceVariantCollection")?.Elements(_ns + "DeviceVariant") ?? [])
        {
            var nameId = variant.Element(_ns + "ProductName")?.Attribute("textId")?.Value;
            nameId ??= variant.Element(_ns + "Name")?.Attribute("textId")?.Value;
            var textId = variant.Element(_ns + "ProductText")?.Attribute("textId")?.Value;
            textId ??= variant.Element(_ns + "Description")?.Attribute("textId")?.Value;

            deviceVariants.Add(new DeviceVariant
            {
                ProductId = (string?)variant.Attribute("productId") ?? throw new InvalidDataException("Product ID not found in <DeviceVariant> element."),
                HardwareRevision = (string?)variant.Attribute("hardwareRevision") ?? string.Empty,
                FirmwareRevision = (string?)variant.Attribute("firmwareRevision") ?? string.Empty,
                SymbolFile = (string?)variant.Attribute("deviceSymbol") ?? string.Empty,
                IconFile = (string?)variant.Attribute("deviceIcon") ?? string.Empty,
                Name = nameId is not null && _texts.TryGetValue(nameId, out var pn) ? pn : string.Empty,
                Description = textId is not null && _texts.TryGetValue(textId, out var pt) ? pt : string.Empty,
            });
        }

        return new()
        {
            VendorId = (int?)identity.Attribute("vendorId") ?? throw new InvalidDataException("Vendor ID not found in <DeviceIdentity> element."),
            VendorName = (string?)identity.Attribute("vendorName") ?? string.Empty,
            VendorLogo = identity.Element(_ns + "VendorLogo")?.Attribute("name")?.Value ?? string.Empty,
            DeviceId = (int?)identity.Attribute("deviceId") ?? throw new InvalidDataException("Device ID not found in <DeviceIdentity> element."),
            DeviceName = deviceNameId is not null && _texts.TryGetValue(deviceNameId, out var deviceName) ? deviceName : string.Empty,
            DeviceFamily = familyId is not null && _texts.TryGetValue(familyId, out var familyText) ? familyText : string.Empty,
            Variants = deviceVariants,
        };
    }
}
