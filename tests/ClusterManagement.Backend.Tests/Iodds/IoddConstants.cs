namespace ClusterManagement.Backend.Tests.Iodds;

internal static class IoddConstants
{
    internal const string IoddDeviceTwo = """
        <?xml version="1.0" encoding="utf-8"?>
        <IODevice xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns="http://www.io-link.com/IODD/2009/11" xsi:schemaLocation="http://www.io-link.com/IODD/2009/11 IODD1.0.1.xsd">
          <DocumentInfo version="V2.4.1" releaseDate="2012-12-10" copyright="Copyright 2012, ifm electronic gmbh" />
          <ProfileHeader>
            <ProfileRevision>1.00</ProfileRevision>
          </ProfileHeader>
          <ProfileBody>
            <DeviceIdentity vendorId="310" vendorName="ifm electronic gmbh" deviceId="2">
              <VendorText textId="TI_VendorText" />
              <VendorUrl textId="TI_VendorUrl" />
              <VendorLogo name="ifm-logo.png" />
              <DeviceFamily textId="TI_DeviceFamily" />
              <DeviceVariantCollection>
                <DeviceVariant productId="PP7550" hardwareRevision="" firmwareRevision="" deviceSymbol="ifm-PPAA-pic.png" deviceIcon="ifm-PPAA-icon.png">
                  <ProductName textId="TI_Product1_Name" />
                  <ProductText textId="TI_Product1_Descr" />
                </DeviceVariant>
                <DeviceVariant productId="PP000E" hardwareRevision="" firmwareRevision="" deviceSymbol="ifm-PPAA-pic.png" deviceIcon="ifm-PPAA-icon.png">
                  <ProductName textId="TI_Product2_Name" />
                  <ProductText textId="TI_Product2_Descr" />
                </DeviceVariant>
              </DeviceVariantCollection>
            </DeviceIdentity>
          </ProfileBody>
          <ExternalTextCollection>
            <PrimaryLanguage xml:lang="en">
              <Text id="TI_VendorText" value="www.ifm.com" />
              <Text id="TI_VendorUrl" value="www.ifm.com/ifmgb/web/io-link_down.htm" />
              <Text id="TI_DeviceFamily" value="PP" />
              <Text id="TI_Product1_Name" value="PP7550" />
              <Text id="TI_Product1_Descr" value="PP7550 electronic pressure sensor, 0 to 400 bar" />
              <Text id="TI_Product2_Name" value="PP000E" />
              <Text id="TI_Product2_Descr" value="PP000E electronic pressure sensor, 0 to 400 bar, E1 Certificate" />
            </PrimaryLanguage>
          </ExternalTextCollection>
        </IODevice>
        """;

    internal const string IoddDeviceThree = """
        <?xml version="1.0" encoding="utf-8"?>
        <IODevice xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns="http://www.io-link.com/IODD/2009/11" xsi:schemaLocation="http://www.io-link.com/IODD/2009/11 IODD1.0.1.xsd">
          <DocumentInfo version="V2.4.1" releaseDate="2012-12-10" copyright="Copyright 2012, ifm electronic gmbh" />
          <ProfileHeader>
            <ProfileRevision>1.00</ProfileRevision>
          </ProfileHeader>
          <ProfileBody>
            <DeviceIdentity vendorId="311" vendorName="ifm electronic gmbh" deviceId="3">
              <VendorText textId="TI_VendorText" />
              <VendorUrl textId="TI_VendorUrl" />
              <VendorLogo name="ifm-logo.png" />
              <DeviceFamily textId="TI_DeviceFamily" />
              <DeviceVariantCollection>
                <DeviceVariant productId="PP7550" hardwareRevision="" firmwareRevision="" deviceSymbol="ifm-PPAA-pic.png" deviceIcon="ifm-PPAA-icon.png">
                  <ProductName textId="TI_Product1_Name" />
                  <ProductText textId="TI_Product1_Descr" />
                </DeviceVariant>
                <DeviceVariant productId="PP000E" hardwareRevision="" firmwareRevision="" deviceSymbol="ifm-PPAA-pic.png" deviceIcon="ifm-PPAA-icon.png">
                  <ProductName textId="TI_Product2_Name" />
                  <ProductText textId="TI_Product2_Descr" />
                </DeviceVariant>
              </DeviceVariantCollection>
            </DeviceIdentity>
          </ProfileBody>
          <ExternalTextCollection>
            <PrimaryLanguage xml:lang="en">
              <Text id="TI_VendorText" value="www.ifm.com" />
              <Text id="TI_VendorUrl" value="www.ifm.com/ifmgb/web/io-link_down.htm" />
              <Text id="TI_DeviceFamily" value="PP" />
              <Text id="TI_Product1_Name" value="PP7550" />
              <Text id="TI_Product1_Descr" value="PP7550 electronic pressure sensor, 0 to 400 bar" />
              <Text id="TI_Product2_Name" value="PP000E" />
              <Text id="TI_Product2_Descr" value="PP000E electronic pressure sensor, 0 to 400 bar, E1 Certificate" />
            </PrimaryLanguage>
          </ExternalTextCollection>
        </IODevice>
        """;

    internal const string IoddDeviceRevOneOne = """
        <?xml version="1.0" encoding="utf-8"?>
        <IODevice xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns="http://www.io-link.com/IODD/2009/11" xsi:schemaLocation="http://www.io-link.com/IODD/2009/11 IODD1.0.1.xsd">
          <DocumentInfo version="V2.4.1" releaseDate="2012-12-10" copyright="Copyright 2012, ifm electronic gmbh" />
          <ProfileHeader>
            <ProfileRevision>1.1</ProfileRevision>
          </ProfileHeader>
          <ProfileBody>
            <DeviceIdentity vendorId="310" vendorName="ifm electronic gmbh" deviceId="1500">
              <VendorText textId="TI_VendorText" />
              <VendorUrl textId="TI_VendorUrl" />
              <VendorLogo name="ifm-logo.png" />
              <DeviceName textId="TI_DeviceName" />
              <DeviceFamily textId="TI_DeviceFamily" />
              <DeviceVariantCollection>
                <DeviceVariant productId="#171939" deviceSymbol="ifm-PV7-pic.png" deviceIcon="ifm-PV7-icon.png">
                  <Name textId="TI_ProductName0" />
                  <Description textId="TI_ProductDescr0" />
                </DeviceVariant>
              </DeviceVariantCollection>
            </DeviceIdentity>
          </ProfileBody>
          <ExternalTextCollection>
            <PrimaryLanguage xml:lang="en">
              <Text id="TI_ProductName0" value="PV2304" />
              <Text id="TI_ProductDescr0" value="Electronic pressure switch, -1.000...10.000 bar, IO-Link, cULus, G 1/4 A / M5 I nach ISO 1179-2" />
              <Text id="TI_VendorText" value="www.ifm.com" />
              <Text id="TI_VendorUrl" value="www.ifm.com/ifmgb/web/io-link-download.htm" />
              <Text id="TI_DeviceFamily" value="PV2" />
              <Text id="TI_DeviceName" value="#171939" />
            </PrimaryLanguage>
          </ExternalTextCollection>
        </IODevice>
        """;

    internal const string IoddDeviceWithoutSomeValues = """
        <?xml version="1.0" encoding="utf-8"?>
        <IODevice xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns="http://www.io-link.com/IODD/2009/11" xsi:schemaLocation="http://www.io-link.com/IODD/2009/11 IODD1.0.1.xsd">
          <DocumentInfo version="V2.4.1" />
          <ProfileHeader>
            <ProfileRevision>1.00</ProfileRevision>
          </ProfileHeader>
          <ProfileBody>
            <DeviceIdentity vendorId="311" vendorName="ifm electronic gmbh" deviceId="3">
              <VendorText textId="TI_VendorText" />
              <VendorUrl textId="TI_VendorUrl" />
              <VendorLogo name="ifm-logo.png" />
              <DeviceVariantCollection>
                <DeviceVariant productId="PP7550" hardwareRevision="" firmwareRevision="">
                  <ProductName textId="TI_Product1_Name" />
                  <ProductText textId="TI_Product1_Descr" />
                </DeviceVariant>
                <DeviceVariant productId="PP000E" hardwareRevision="" firmwareRevision="">
                  <ProductName textId="TI_Product2_Name" />
                  <ProductText textId="TI_Product2_Descr" />
                </DeviceVariant>
              </DeviceVariantCollection>
            </DeviceIdentity>
          </ProfileBody>
          <ExternalTextCollection>
            <PrimaryLanguage xml:lang="en">
              <Text id="TI_VendorText" value="www.ifm.com" />
              <Text id="TI_VendorUrl" value="www.ifm.com/ifmgb/web/io-link_down.htm" />
              <Text id="TI_DeviceFamily" value="PP" />
              <Text id="TI_Product1_Name" value="PP7550" />
              <Text id="TI_Product1_Descr" value="PP7550 electronic pressure sensor, 0 to 400 bar" />
              <Text id="TI_Product2_Name" value="PP000E" />
              <Text id="TI_Product2_Descr" value="PP000E electronic pressure sensor, 0 to 400 bar, E1 Certificate" />
            </PrimaryLanguage>
          </ExternalTextCollection>
        </IODevice>
        """;
}
