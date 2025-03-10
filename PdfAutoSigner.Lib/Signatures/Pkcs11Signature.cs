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

using iText.Signatures;
using Net.Pkcs11Interop.Common;
using Net.Pkcs11Interop.HighLevelAPI;
using Net.Pkcs11Interop.HighLevelAPI.Factories;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;

namespace PdfAutoSigner.Lib.Signatures
{
    /// <summary>
    /// Original code obtained from https://git.itextsupport.com/projects/I7NS/repos/samples/browse/itext/itext.publications/itext.publications.signing-examples.pkcs11/iText/SigningExamples/Pkcs11/Pkcs11Signature.cs
    /// </summary>
    public class Pkcs11Signature : IExternalSignatureWithChain, IDisposable
    {
        // For now only SHA256 is supported.
        public static readonly string Pkcs11HashAlgorithm = "SHA256";

        ISlot? slot;
        ITokenInfo tokenInfo;
        ISession? session;
        IObjectHandle? privateKeyHandle;

        X509Certificate[]? chain;
        string? encryptionAlgorithm;
        string? hashAlgorithm;

        public Pkcs11Signature(ISlot slot)
        {
            this.slot = slot;
            this.tokenInfo = slot.GetTokenInfo();
            this.hashAlgorithm = DigestAlgorithms.GetDigest(DigestAlgorithms.GetAllowedDigest(Pkcs11HashAlgorithm));
        }

        public IExternalSignatureWithChain Select(string pin)
        {
            return Select(null, null, pin);
        }

        public Pkcs11Signature Select(string? alias, string? certLabel, string pin)
        {
            List<CKA> pkAttributeKeys = new List<CKA>();
            pkAttributeKeys.Add(CKA.CKA_KEY_TYPE);
            pkAttributeKeys.Add(CKA.CKA_LABEL);
            pkAttributeKeys.Add(CKA.CKA_ID);
            List<CKA> certAttributeKeys = new List<CKA>();
            certAttributeKeys.Add(CKA.CKA_VALUE);
            certAttributeKeys.Add(CKA.CKA_LABEL);


            CloseSession();
            if (slot == null)
            {
                throw new ApplicationException("Signature device's slot cannot be found");
            }

            session = slot.OpenSession(SessionType.ReadWrite);
            var sessionInfo = session.GetSessionInfo();
            
            try
            {
                // Try to logout the user if it was already logged in
                if (sessionInfo.State != CKS.CKS_RO_PUBLIC_SESSION && sessionInfo.State != CKS.CKS_RW_PUBLIC_SESSION)
                    session.Logout();
            }
            catch (Exception)
            {
                // Continue
            }

            session.Login(CKU.CKU_USER, pin);
            ObjectAttributeFactory objectAttributeFactory = new ObjectAttributeFactory();

            List<IObjectAttribute> attributes = new List<IObjectAttribute>();
            attributes.Add(objectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_PRIVATE_KEY));
            List<IObjectHandle> keys = session.FindAllObjects(attributes);

            bool found = false;
            foreach (IObjectHandle key in keys)
            {
                List<IObjectAttribute> keyAttributes = session.GetAttributeValue(key, pkAttributeKeys);

                ulong type = keyAttributes[0].GetValueAsUlong();
                string encryptionAlgorithm;
                switch (type)
                {
                    case (ulong)CKK.CKK_RSA:
                        encryptionAlgorithm = "RSA";
                        break;
                    case (ulong)CKK.CKK_DSA:
                        encryptionAlgorithm = "DSA";
                        break;
                    case (ulong)CKK.CKK_ECDSA:
                        encryptionAlgorithm = "ECDSA";
                        break;
                    default:
                        continue;
                }
                string thisAlias = keyAttributes[1].GetValueAsString();
                if (thisAlias == null || thisAlias.Length == 0)
                    thisAlias = keyAttributes[2].GetValueAsString();
                if (alias != null && !alias.Equals(thisAlias))
                    continue;

                attributes.Clear();
                attributes.Add(objectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_CERTIFICATE));
                attributes.Add(objectAttributeFactory.Create(CKA.CKA_CERTIFICATE_TYPE, CKC.CKC_X_509));
                if (certLabel == null && thisAlias != null && thisAlias.Length > 0)
                    certLabel = thisAlias;
                if (certLabel != null)
                    attributes.Add(objectAttributeFactory.Create(CKA.CKA_LABEL, certLabel));
                List<IObjectHandle> certificates = session.FindAllObjects(attributes);
                if (certificates.Count != 1)
                    continue;

                IObjectHandle certificate = certificates[0];
                List<IObjectAttribute> certificateAttributes = session.GetAttributeValue(certificate, certAttributeKeys);
                X509Certificate x509Certificate = new X509Certificate(X509CertificateStructure.GetInstance(certificateAttributes[0].GetValueAsByteArray()));

                List<X509Certificate> x509Certificates = new List<X509Certificate>();
                x509Certificates.Add(x509Certificate);
                attributes.Clear();
                attributes.Add(objectAttributeFactory.Create(CKA.CKA_CLASS, CKO.CKO_CERTIFICATE));
                attributes.Add(objectAttributeFactory.Create(CKA.CKA_CERTIFICATE_TYPE, CKC.CKC_X_509));
                List<IObjectHandle> otherCertificates = session.FindAllObjects(attributes);
                foreach (IObjectHandle otherCertificate in otherCertificates)
                {
                    if (!certificate.ObjectId.Equals(otherCertificate.ObjectId))
                    {
                        certificateAttributes = session.GetAttributeValue(otherCertificate, certAttributeKeys);
                        X509Certificate otherX509Certificate = new X509Certificate(X509CertificateStructure.GetInstance(certificateAttributes[0].GetValueAsByteArray()));
                        x509Certificates.Add(otherX509Certificate);
                    }
                }

                found = true;
                this.encryptionAlgorithm = encryptionAlgorithm;
                this.privateKeyHandle = key;
                this.chain = x509Certificates.ToArray();
                break;
            }

            if (!found)
            {
                this.encryptionAlgorithm = null;
                this.privateKeyHandle = null;
                this.chain = null;
            }

            return this;
        }

        public void Dispose()
        {
            CloseSession();
            slot = null;
        }

        private void CloseSession()
        {
            if (session != null)
            {
                try
                {
                    session.Logout();
                    session.Dispose();
                }
                finally
                {
                    privateKeyHandle = null;
                    session = null;
                }
            }
        }

        public X509Certificate[]? GetChain()
        {
            return chain;
        }

        public string GetSignatureIdentifyingName()
        {
            return $"{tokenInfo.ManufacturerId} - {tokenInfo.Model} - {tokenInfo.SerialNumber}";
        }

        public string? GetEncryptionAlgorithm()
        {
            return encryptionAlgorithm;
        }

        public string? GetHashAlgorithm()
        {
            return hashAlgorithm;
        }

        public byte[] Sign(byte[] message)
        {
            MechanismFactory mechanismFactory = new MechanismFactory();
            IMechanism mechanism;

            switch (encryptionAlgorithm)
            {
                case "DSA":
                    switch (hashAlgorithm)
                    {
                        case "SHA1":
                            mechanism = mechanismFactory.Create(CKM.CKM_DSA_SHA1);
                            break;
                        case "SHA224":
                            mechanism = mechanismFactory.Create(CKM.CKM_DSA_SHA224);
                            break;
                        case "SHA256":
                            mechanism = mechanismFactory.Create(CKM.CKM_DSA_SHA256);
                            break;
                        case "SHA384":
                            mechanism = mechanismFactory.Create(CKM.CKM_DSA_SHA384);
                            break;
                        case "SHA512":
                            mechanism = mechanismFactory.Create(CKM.CKM_DSA_SHA512);
                            break;
                        default:
                            throw new ArgumentException("Not supported: " + hashAlgorithm + "with" + encryptionAlgorithm);
                    }
                    break;
                case "ECDSA":
                    switch (hashAlgorithm)
                    {
                        case "SHA1":
                            mechanism = mechanismFactory.Create(CKM.CKM_ECDSA_SHA1);
                            break;
                        case "SHA224":
                            mechanism = mechanismFactory.Create(CKM.CKM_ECDSA_SHA224);
                            break;
                        case "SHA256":
                            mechanism = mechanismFactory.Create(CKM.CKM_ECDSA_SHA256);
                            break;
                        case "SHA384":
                            mechanism = mechanismFactory.Create(CKM.CKM_ECDSA_SHA384);
                            break;
                        case "SHA512":
                            mechanism = mechanismFactory.Create(CKM.CKM_ECDSA_SHA512);
                            break;
                        default:
                            throw new ArgumentException("Not supported: " + hashAlgorithm + "with" + encryptionAlgorithm);
                    }
                    break;
                case "RSA":
                    switch (hashAlgorithm)
                    {
                        case "SHA1":
                            mechanism = mechanismFactory.Create(CKM.CKM_SHA1_RSA_PKCS);
                            break;
                        case "SHA224":
                            mechanism = mechanismFactory.Create(CKM.CKM_SHA224_RSA_PKCS);
                            break;
                        case "SHA256":
                            mechanism = mechanismFactory.Create(CKM.CKM_SHA256_RSA_PKCS);
                            break;
                        case "SHA384":
                            mechanism = mechanismFactory.Create(CKM.CKM_SHA384_RSA_PKCS);
                            break;
                        case "SHA512":
                            mechanism = mechanismFactory.Create(CKM.CKM_SHA512_RSA_PKCS);
                            break;
                        default:
                            throw new ArgumentException("Not supported: " + hashAlgorithm + "with" + encryptionAlgorithm);
                    }
                    break;
                default:
                    throw new ArgumentException("Not supported: " + hashAlgorithm + "with" + encryptionAlgorithm);
            }

            if (session == null)
            {
                throw new ApplicationException("Session is not instantiated.");
            }
            return session.Sign(mechanism, privateKeyHandle, message);
        }
    }
}
