﻿namespace PdfAutoSigner.LocalApi.Config
{
    public class TokenOptions
    {
        public const string TokensConfigPath = "TokensConfig";
        public const string Pkcs11DevicesConfigPath = $"{TokensConfigPath}:Pkcs11Devices";
        public const string CertificatesConfigPath = $"{TokensConfigPath}:Certificates";

        public List<Pkcs11DeviceData> Pkcs11Devices { get; set; }
        public List<CertificateData> Certificates { get; set; }
    }
}
