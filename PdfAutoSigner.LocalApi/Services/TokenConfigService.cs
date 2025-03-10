﻿// PdfAutoSigner signs PDF files automatically using a hardware security module. 
// Copyright (C) Plant An App
//  
// This program is free software: you can redistribute it and/or modify 
// it under the terms of the GNU Affero General Public License as 
// published by the Free Software Foundation, either version 3 of the 
// License, or (at your option) any later version. 
//  
// This program is distributed in the hope that it will be useful, 
// but WITHOUT ANY WARRANTY

using Microsoft.Extensions.Options;
using PdfAutoSigner.LocalApi.Config;
using PdfAutoSigner.LocalApi.Helpers;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PdfAutoSigner.LocalApi.Services
{
    public class TokenConfigService : ITokenConfigService
    {
        private readonly IOptionsSnapshot<TokenOptions> tokenOptionsSnapshot;
        private readonly IOSHelper osHelper;
        private readonly ILogger<TokenConfigService> logger;

        public TokenConfigService(IOptionsSnapshot<TokenOptions> tokenOptionsSnapshot, IOSHelper osHelper, ILogger<TokenConfigService> logger)
        {
            this.tokenOptionsSnapshot = tokenOptionsSnapshot;
            this.osHelper = osHelper;
            this.logger = logger;
        }

        public List<string> GetPkcs11LibPathsByOS()
        {
            var deviceDataList = tokenOptionsSnapshot.Value.Pkcs11Devices;
            logger.LogInformation($"Found {deviceDataList.Count} pkcs11 devices in config file.");

            var os = osHelper.GetOS();
            if (os == null)
            {
                throw new SystemException("OS is not supported.");
            }

            var architecture = osHelper.GetArchitecture();

            var libPaths =
                from device in deviceDataList
                from osLibPath in device.Pkcs11LibPaths
                where osLibPath.OS == os && osLibPath.Architecture == architecture
                select osLibPath.LibPath;

            logger.LogInformation($"Found following library paths for current os and architecture in the config file: {string.Join("", libPaths)}");

            return libPaths.ToList();
        }

        public List<string> GetIssuerNames()
        {
            var certificateDataList = tokenOptionsSnapshot.Value.Certificates;
            logger.LogInformation($"Found {certificateDataList.Count} certificates in config file.");

            var certificateIssuerNames =
                from certificateData in certificateDataList
                select certificateData.CertificateIssuerName;

            logger.LogInformation($"Found following certificates in the config file: {string.Join("", certificateIssuerNames)}");

            return certificateIssuerNames.ToList();
        }
    }
}
