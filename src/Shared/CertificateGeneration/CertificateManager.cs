// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.AspNetCore.Certificates.Generation
{
    internal class CertificateManager
    {
        public const string AspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";
        public const string AspNetHttpsOidFriendlyName = "ASP.NET Core HTTPS development certificate";

        private const string ServerAuthenticationEnhancedKeyUsageOid = "1.3.6.1.5.5.7.3.1";
        private const string ServerAuthenticationEnhancedKeyUsageOidFriendlyName = "Server Authentication";

        private const string LocalhostHttpsDnsName = "localhost";
        private const string LocalhostHttpsDistinguishedName = "CN=" + LocalhostHttpsDnsName;

        public const int RSAMinimumKeySizeInBits = 2048;

        private static readonly TimeSpan MaxRegexTimeout = TimeSpan.FromMinutes(1);
        private const string CertificateSubjectRegex = "CN=(.*[^,]+).*";
        private const string MacOSSystemKeyChain = "/Library/Keychains/System.keychain";
        private static readonly string MacOSUserKeyChain = Environment.GetEnvironmentVariable("HOME") + "/Library/Keychains/login.keychain-db";
        private const string MacOSFindCertificateCommandLine = "security";
        private static readonly string MacOSFindCertificateCommandLineArgumentsFormat = "find-certificate -c {0} -a -Z -p " + MacOSSystemKeyChain;
        private const string MacOSFindCertificateOutputRegex = "SHA-1 hash: ([0-9A-Z]+)";
        private const string MacOSRemoveCertificateTrustCommandLine = "sudo";
        private const string MacOSRemoveCertificateTrustCommandLineArgumentsFormat = "security remove-trusted-cert -d {0}";
        private const string MacOSDeleteCertificateCommandLine = "sudo";
        private const string MacOSDeleteCertificateCommandLineArgumentsFormat = "security delete-certificate -Z {0} {1}";
        private const string MacOSTrustCertificateCommandLine = "sudo";
        private static readonly string MacOSTrustCertificateCommandLineArguments = "security add-trusted-cert -d -r trustRoot -k " + MacOSSystemKeyChain + " ";
        private const int UserCancelledErrorCode = 1223;

        // Setting to 0 means we don't append the version byte,
        // which is what all machines currently have.
        public static int AspNetHttpsCertificateVersion { get; set; } = 1;

        public static bool IsHttpsDevelopmentCertificate(X509Certificate2 certificate) =>
            certificate.Extensions.OfType<X509Extension>()
            .Any(e => string.Equals(AspNetHttpsOid, e.Oid.Value, StringComparison.Ordinal));

        public static IList<X509Certificate2> ListCertificates(
            CertificatePurpose purpose,
            StoreName storeName,
            StoreLocation location,
            bool isValid,
            bool requireExportable = true,
            DiagnosticInformation diagnostics = null)
        {
            diagnostics?.Debug($"Listing '{purpose.ToString()}' certificates on '{location}\\{storeName}'.");
            var certificates = new List<X509Certificate2>();
            try
            {
                using (var store = new X509Store(storeName, location))
                {
                    store.Open(OpenFlags.ReadOnly);
                    certificates.AddRange(store.Certificates.OfType<X509Certificate2>());
                    IEnumerable<X509Certificate2> matchingCertificates = certificates;
                    switch (purpose)
                    {
                        case CertificatePurpose.All:
                            matchingCertificates = matchingCertificates
                                .Where(c => HasOid(c, AspNetHttpsOid));
                            break;
                        case CertificatePurpose.HTTPS:
                            matchingCertificates = matchingCertificates
                                .Where(c => HasOid(c, AspNetHttpsOid));
                            break;
                        default:
                            break;
                    }

                    diagnostics?.Debug(diagnostics.DescribeCertificates(matchingCertificates));
                    if (isValid)
                    {
                        // Ensure the certificate hasn't expired, has a private key and its exportable
                        // (for container/unix scenarios).
                        diagnostics?.Debug("Checking certificates for validity.");
                        var now = DateTimeOffset.Now;
                        var validCertificates = matchingCertificates
                            .Where(c => c.NotBefore <= now &&
                                now <= c.NotAfter &&
                                // requireExportable = false is only passed when we are checking for a certificate in the trusted root certificate store
                                (!requireExportable || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || IsExportable(c))
                                && MatchesVersion(c))
                            .ToArray();

                        var invalidCertificates = matchingCertificates.Except(validCertificates);

                        diagnostics?.Debug("Listing valid certificates");
                        diagnostics?.Debug(diagnostics.DescribeCertificates(validCertificates));
                        diagnostics?.Debug("Listing invalid certificates");
                        diagnostics?.Debug(diagnostics.DescribeCertificates(invalidCertificates));

                        matchingCertificates = validCertificates;
                    }

                    // We need to enumerate the certificates early to prevent disposing issues.
                    matchingCertificates = matchingCertificates.ToList();

                    var certificatesToDispose = certificates.Except(matchingCertificates);
                    DisposeCertificates(certificatesToDispose);

                    store.Close();

                    return (IList<X509Certificate2>)matchingCertificates;
                }
            }
            catch
            {
                DisposeCertificates(certificates);
                certificates.Clear();
                return certificates;
            }

            bool HasOid(X509Certificate2 certificate, string oid) =>
                certificate.Extensions.OfType<X509Extension>()
                    .Any(e => string.Equals(oid, e.Oid.Value, StringComparison.Ordinal));

            bool MatchesVersion(X509Certificate2 c)
            {
                var byteArray = c.Extensions.OfType<X509Extension>()
                    .Where(e => string.Equals(AspNetHttpsOid, e.Oid.Value, StringComparison.Ordinal))
                    .Single()
                    .RawData;

                if ((byteArray.Length == AspNetHttpsOidFriendlyName.Length && byteArray[0] == (byte)'A') || byteArray.Length == 0)
                {
                    // No Version set, default to 0
                    return 0 >= AspNetHttpsCertificateVersion;
                }
                else
                {
                    // Version is in the only byte of the byte array.
                    return byteArray[0] >= AspNetHttpsCertificateVersion;
                }
            }
#if !XPLAT
            bool IsExportable(X509Certificate2 c) =>
                ((c.GetRSAPrivateKey() is RSACryptoServiceProvider rsaPrivateKey &&
                    rsaPrivateKey.CspKeyContainerInfo.Exportable) ||
                (c.GetRSAPrivateKey() is RSACng cngPrivateKey &&
                    cngPrivateKey.Key.ExportPolicy == CngExportPolicies.AllowExport));
#else
            // Only check for RSA CryptoServiceProvider and do not fail in XPlat tooling as
            // System.Security.Cryptography.Cng is not part of the shared framework and we don't
            // want to bring the dependency in on CLI scenarios. This functionality will be used
            // on CLI scenarios as part of the first run experience, so checking the exportability
            // of the certificate is not important.
            bool IsExportable(X509Certificate2 c) =>
                ((c.GetRSAPrivateKey() is RSACryptoServiceProvider rsaPrivateKey &&
                    rsaPrivateKey.CspKeyContainerInfo.Exportable) || !(c.GetRSAPrivateKey() is RSACryptoServiceProvider));
#endif
        }

        internal enum KeyAccessResult
        {
            Success,
            TrustedCanAccess,
            UntrustedCanAccess,
            Failure
        }

        internal KeyAccessResult CanAccessKey(IList<X509Certificate2> certificates, DiagnosticInformation diagnostics = null)
        {
            var certificatesWithInaccessibleKeys = certificates.Where(c => !CheckDeveloperCertificateKey(c)).ToList();
            if (certificatesWithInaccessibleKeys.Count > 0)
            {
                diagnostics?.Debug("The current process can't access the certificate key for the following certificates:");
                diagnostics?.Debug(diagnostics.DescribeCertificates(certificatesWithInaccessibleKeys));
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return certificates.Except(certificatesWithInaccessibleKeys).Any() ? KeyAccessResult.Success : KeyAccessResult.Failure;
            }
            else
            {
                // Due to new restrictions in Mac OS Catalina .NET becomes a "hardened" runtime which means that it is signed and
                // notarized by Apple. This produces additional complexity when running .NET applications in different ways as the
                // way you run your application impacts whether or not it has access to the underlying key chain the certificate store
                // is based on.
                // The scenarios are as follows:
                // dotnet run -> unsigned.
                // dotnet <<path-to-dll> signed.
                // ./<<path-to-published-app>> unsigned.
                // Running dotnet dev-certs the application runs through the signed process host, so it is considered "signed".
                // Running dotnet ./<<path-to-dotnet-dev-certs.dll>> the application runs through the unsigned process host, so it is considered unsigned.
                // For that reason we need to make sure that our certificate is accessible across security partitions (or accessible by
                // unsigned processes).
                // Unfortunately that can't be done without user interaction, so to ensure that 'dotnet run' is able to work with ASP.NET Core
                // apps after the first run experience we need to import the same certificate twice inside the key chain so that it can be
                // accessible by all partitions.
                // Then, the first time the user chooses to trust the certificate (through their IDE or through dotnet dev-certs https --trust)
                // we will take the necessary steps to make sure the key is accessible across boundaries (and potentially remove any unnecessary
                // key/certificate) from the store.

                // Pending:
                // We need to have a certificate with an associated key in the trusted partition. Ideally we want to have that same certificate
                // with a key in the untrusted partition so that it only needs to be trusted once. The code below guarantees that there is a valid
                // certificate in each partition, but not that they are the same. In the general case, they will be though.
                // We can refine this later.
                bool trustedProcessCanAccessKey = certificates.Except(certificatesWithInaccessibleKeys).Any();
                bool untrustedProcessCanAccessKey = CanAccessCertificatesFromUnsignedProcess();

                return (trustedProcessCanAccessKey, untrustedProcessCanAccessKey) switch
                {
                    (true, true) => KeyAccessResult.Success,
                    (true, false) => KeyAccessResult.TrustedCanAccess,
                    (false, true) => KeyAccessResult.UntrustedCanAccess,
                    (false, false) => KeyAccessResult.Failure
                };
            }
        }

        // This code runs on osx only.
        private bool CanAccessCertificatesFromUnsignedProcess()
        {
            if (Environment.GetEnvironmentVariable("ASPNETCORE_EXECUTIONCONTEXT") == "UNTRUSTED")
            {
                // We are running as an unsigned process, simply return.
                return true;
            }

            // We are running in the context of a signed process so we have access to the security partition where the key is stored inside
            // the login keychain. In order to check if an unsigned host has access to the certificate, then we are going to do the following:
            // We are going to find our platform-specific executable (that we compiled and bundled alongside with the SDK) which is not signed.
            // We are going to launch a process from that executable and check the keys there. That process runs this same code. We are going
            // pass a bunch of environment variables to the new process to indicate:
            // * That the process is "untrusted" (We consider the default to be "trusted").
            //   * This affects the logic and answers it gives.
            //   * An optional certificate hash, to see if it has access to that certificate key from an untrusted host.

            return RunDevCertsCommandAsUntrusted("--check -q") == 0;
        }

        private static int RunDevCertsCommandAsUntrusted(string action, IDictionary<string, string> additionalEnvironmentVariables = null)
        {
            // The layout inside the tool/SDK is as follows
            // <<dotnet-dev-certs>>/<<version>>/any/dotnet-dev-certs.dll (this is where we are currently running).
            // <<dotnet-dev-certs>>/<<version>>/osx-x64/dotnet-dev-certs (this is the untrusted executable we are going to run).
            var currentDllFolder = Path.GetDirectoryName(typeof(CertificateManager).Assembly.Location);

            var executablePath = ResolveExecutablePath(currentDllFolder);

            var processStartInfo = new ProcessStartInfo(executablePath, $"https {action}");
            processStartInfo.EnvironmentVariables.Add("ASPNETCORE_EXECUTIONCONTEXT", "UNTRUSTED");
            if (additionalEnvironmentVariables != null)
            {
                foreach (var (key, value) in additionalEnvironmentVariables)
                {
                    processStartInfo.EnvironmentVariables.Add(key, value);
                }
            }

            var checkProcess = Process.Start(processStartInfo);
            checkProcess.WaitForExit(5000);
            if (!checkProcess.HasExited)
            {
                try
                {
                    checkProcess.Kill();
                }
                catch (Exception)
                {
                }
                return -1;
            }
            else
            {
                return checkProcess.ExitCode;
            }
        }

        private static string ResolveExecutablePath(string currentDllFolder)
        {
            var subPath = typeof(CertificateManager).Assembly.GetName().Name != "Microsoft.AspNetCore.DeveloperCertificates.XPlat" ?
                "../osx-x64/dotnet-dev-certs" :
                ResolveToolFrom(currentDllFolder, "DotnetTools/dotnet-dev-certs/");

            return Path.GetFullPath(Path.Combine(currentDllFolder, subPath));
        }

        private static string ResolveToolFrom(string currentDllFolder, string subPath)
        {
            var fullPath = Path.Combine(currentDllFolder, subPath);
            Debug.Assert(Directory.Exists(fullPath));
            var versionFolders = Directory.GetDirectories(fullPath);
            Debug.Assert(versionFolders.Length == 1);
            var pathWithVersion = Path.Combine(versionFolders[0], "tools");
            var targetFrameworkFolders = Directory.GetDirectories(pathWithVersion);
            Debug.Assert(targetFrameworkFolders.Length == 1);
            var pathToExe = Path.Combine(targetFrameworkFolders[0], "osx-x64/dotnet-dev-certs");
            return Path.GetRelativePath(currentDllFolder, pathToExe);
        }

        private static void DisposeCertificates(IEnumerable<X509Certificate2> disposables)
        {
            foreach (var disposable in disposables)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                }
            }
        }

        public X509Certificate2 CreateAspNetCoreHttpsDevelopmentCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter, string subjectOverride, DiagnosticInformation diagnostics = null)
        {
            var subject = new X500DistinguishedName(subjectOverride ?? LocalhostHttpsDistinguishedName);
            var extensions = new List<X509Extension>();
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(LocalhostHttpsDnsName);

            var keyUsage = new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, critical: true);
            var enhancedKeyUsage = new X509EnhancedKeyUsageExtension(
                new OidCollection() {
                    new Oid(
                        ServerAuthenticationEnhancedKeyUsageOid,
                        ServerAuthenticationEnhancedKeyUsageOidFriendlyName)
                },
                critical: true);

            var basicConstraints = new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true);

            byte[] bytePayload;

            if (AspNetHttpsCertificateVersion != 0)
            {
                bytePayload = new byte[1];
                bytePayload[0] = (byte)AspNetHttpsCertificateVersion;
            }
            else
            {
                bytePayload = Encoding.ASCII.GetBytes(AspNetHttpsOidFriendlyName);
            }

            var aspNetHttpsExtension = new X509Extension(
                new AsnEncodedData(
                    new Oid(AspNetHttpsOid, AspNetHttpsOidFriendlyName),
                    bytePayload),
                critical: false);

            extensions.Add(basicConstraints);
            extensions.Add(keyUsage);
            extensions.Add(enhancedKeyUsage);
            extensions.Add(sanBuilder.Build(critical: true));
            extensions.Add(aspNetHttpsExtension);

            var certificate = CreateSelfSignedCertificate(subject, extensions, notBefore, notAfter);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                certificate.FriendlyName = AspNetHttpsOidFriendlyName;
            }

            return certificate;
        }

        internal static bool CheckDeveloperCertificateKey(X509Certificate2 candidate)
        {
            // Tries to use the certificate key to validate it can't access it
            try
            {
                var rsa = candidate.GetRSAPrivateKey();
                if (rsa == null)
                {
                    return false;
                }

                // Encrypting a random value is the ultimate test for a key validity.
                // Windows and Mac OS both return HasPrivateKey = true if there is (or there has been) a private key associated
                // with the certificate at some point.
                var value = new byte[32];
                RandomNumberGenerator.Fill(value);
                rsa.Decrypt(rsa.Encrypt(value, RSAEncryptionPadding.Pkcs1), RSAEncryptionPadding.Pkcs1);

                // Being able to encrypt and decrypt a payload is the strongest guarantee that the key is valid.
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public X509Certificate2 CreateSelfSignedCertificate(
            X500DistinguishedName subject,
            IEnumerable<X509Extension> extensions,
            DateTimeOffset notBefore,
            DateTimeOffset notAfter)
        {
            var key = CreateKeyMaterial(RSAMinimumKeySizeInBits);

            var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            foreach (var extension in extensions)
            {
                request.CertificateExtensions.Add(extension);
            }

            return request.CreateSelfSigned(notBefore, notAfter);

            RSA CreateKeyMaterial(int minimumKeySize)
            {
                var rsa = RSA.Create(minimumKeySize);
                if (rsa.KeySize < minimumKeySize)
                {
                    throw new InvalidOperationException($"Failed to create a key with a size of {minimumKeySize} bits");
                }

                return rsa;
            }
        }

        public static X509Certificate2 SaveCertificateInStore(X509Certificate2 certificate, StoreName name, StoreLocation location, DiagnosticInformation diagnostics = null)
        {
            diagnostics?.Debug("Saving the certificate into the certificate store.");
            var imported = certificate;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // On non OSX systems we need to export the certificate and import it so that the transient
                // key that we generated gets persisted.
                var export = certificate.Export(X509ContentType.Pkcs12, "");
                imported = new X509Certificate2(export, "", X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                Array.Clear(export, 0, export.Length);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                imported.FriendlyName = certificate.FriendlyName;
            }

            using (var store = new X509Store(name, location))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(imported);
                store.Close();
            };

            return imported;
        }

        public void ExportCertificate(X509Certificate2 certificate, string path, bool includePrivateKey, string password, DiagnosticInformation diagnostics = null)
        {
            diagnostics?.Debug(
                $"Exporting certificate to '{path}'",
                includePrivateKey ? "The certificate will contain the private key" : "The certificate will not contain the private key");
            if (includePrivateKey && password == null)
            {
                diagnostics?.Debug("No password was provided for the certificate.");
            }

            var targetDirectoryPath = Path.GetDirectoryName(path);
            if (targetDirectoryPath != "")
            {
                diagnostics?.Debug($"Ensuring that the directory for the target exported certificate path exists '{targetDirectoryPath}'");
                Directory.CreateDirectory(targetDirectoryPath);
            }

            byte[] bytes;
            if (includePrivateKey)
            {
                try
                {
                    diagnostics?.Debug($"Exporting the certificate including the private key.");
                    bytes = certificate.Export(X509ContentType.Pkcs12, password);
                }
                catch (Exception e)
                {
                    diagnostics?.Error($"Failed to export the certificate with the private key", e);
                    throw;
                }
            }
            else
            {
                try
                {
                    diagnostics?.Debug($"Exporting the certificate without the private key.");
                    bytes = certificate.Export(X509ContentType.Cert);
                }
                catch (Exception ex)
                {
                    diagnostics?.Error($"Failed to export the certificate without the private key", ex);
                    throw;
                }
            }
            try
            {
                diagnostics?.Debug($"Writing exported certificate to path '{path}'.");
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception ex)
            {
                diagnostics?.Error("Failed writing the certificate to the target path", ex);
                throw;
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }

        public void TrustCertificate(X509Certificate2 certificate, DiagnosticInformation diagnostics = null)
        {
            // Strip certificate of the private key if any.
            var publicCertificate = new X509Certificate2(certificate.Export(X509ContentType.Cert));

            if (!IsTrusted(publicCertificate))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    diagnostics?.Debug("Trusting the certificate on Windows.");
                    TrustCertificateOnWindows(certificate, publicCertificate, diagnostics);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    diagnostics?.Debug("Trusting the certificate on MAC.");
                    TrustCertificateOnMac(publicCertificate, diagnostics);
                }
            }
        }

        private void TrustCertificateOnMac(X509Certificate2 publicCertificate, DiagnosticInformation diagnostics)
        {
            var tmpFile = Path.GetTempFileName();
            try
            {
                ExportCertificate(publicCertificate, tmpFile, includePrivateKey: false, password: null);
                diagnostics?.Debug("Running the trust command on Mac OS");
                using (var process = Process.Start(MacOSTrustCertificateCommandLine, MacOSTrustCertificateCommandLineArguments + tmpFile))
                {
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        throw new InvalidOperationException("There was an error trusting the certificate.");
                    }
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(tmpFile))
                    {
                        File.Delete(tmpFile);
                    }
                }
                catch
                {
                    // We don't care if we can't delete the temp file.
                }
            }
        }

        private static void TrustCertificateOnWindows(X509Certificate2 certificate, X509Certificate2 publicCertificate, DiagnosticInformation diagnostics = null)
        {
            publicCertificate.FriendlyName = certificate.FriendlyName;

            using (var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                var existing = store.Certificates.Find(X509FindType.FindByThumbprint, publicCertificate.Thumbprint, validOnly: false);
                if (existing.Count > 0)
                {
                    diagnostics?.Debug("Certificate already trusted. Skipping trust step.");
                    DisposeCertificates(existing.OfType<X509Certificate2>());
                    return;
                }

                try
                {
                    diagnostics?.Debug("Adding certificate to the store.");
                    store.Add(publicCertificate);
                }
                catch (CryptographicException exception) when (exception.HResult == UserCancelledErrorCode)
                {
                    diagnostics?.Debug("User cancelled the trust prompt.");
                    throw new UserCancelledTrustException();
                }
                store.Close();
            };
        }

        public bool IsTrusted(X509Certificate2 certificate)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ListCertificates(CertificatePurpose.HTTPS, StoreName.Root, StoreLocation.CurrentUser, isValid: true, requireExportable: false)
                    .Any(c => c.Thumbprint == certificate.Thumbprint);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var subjectMatch = Regex.Match(certificate.Subject, CertificateSubjectRegex, RegexOptions.Singleline, MaxRegexTimeout);
                if (!subjectMatch.Success)
                {
                    throw new InvalidOperationException($"Can't determine the subject for the certificate with subject '{certificate.Subject}'.");
                }
                var subject = subjectMatch.Groups[1].Value;
                using (var checkTrustProcess = Process.Start(new ProcessStartInfo(
                    MacOSFindCertificateCommandLine,
                    string.Format(MacOSFindCertificateCommandLineArgumentsFormat, subject))
                {
                    RedirectStandardOutput = true
                }))
                {
                    var output = checkTrustProcess.StandardOutput.ReadToEnd();
                    checkTrustProcess.WaitForExit();
                    var matches = Regex.Matches(output, MacOSFindCertificateOutputRegex, RegexOptions.Multiline, MaxRegexTimeout);
                    var hashes = matches.OfType<Match>().Select(m => m.Groups[1].Value).ToList();
                    return hashes.Any(h => string.Equals(h, certificate.Thumbprint, StringComparison.Ordinal));
                }
            }
            else
            {
                return false;
            }
        }

        public void CleanupHttpsCertificates(string subject = LocalhostHttpsDistinguishedName)
        {
            CleanupCertificates(CertificatePurpose.HTTPS, subject);
        }

        public void CleanupCertificates(CertificatePurpose purpose, string subject)
        {
            // On OS X we don't have a good way to manage trusted certificates in the system keychain
            // so we do everything by invoking the native toolchain.
            // This has some limitations, like for example not being able to identify our custom OID extension. For that
            // matter, when we are cleaning up certificates on the machine, we start by removing the trusted certificates.
            // To do this, we list the certificates that we can identify on the current user personal store and we invoke
            // the native toolchain to remove them from the sytem keychain. Once we have removed the trusted certificates,
            // we remove the certificates from the local user store to finish up the cleanup.
            var certificates = ListCertificates(purpose, StoreName.My, StoreLocation.CurrentUser, isValid: false);
            foreach (var certificate in certificates)
            {
                RemoveCertificate(certificate, RemoveLocations.All);
            }
        }

        public DiagnosticInformation CleanupHttpsCertificates2(string subject = LocalhostHttpsDistinguishedName)
        {
            return CleanupCertificates2(CertificatePurpose.HTTPS, subject);
        }

        public DiagnosticInformation CleanupCertificates2(CertificatePurpose purpose, string subject)
        {
            var diagnostics = new DiagnosticInformation();
            // On OS X we don't have a good way to manage trusted certificates in the system keychain
            // so we do everything by invoking the native toolchain.
            // This has some limitations, like for example not being able to identify our custom OID extension. For that
            // matter, when we are cleaning up certificates on the machine, we start by removing the trusted certificates.
            // To do this, we list the certificates that we can identify on the current user personal store and we invoke
            // the native toolchain to remove them from the sytem keychain. Once we have removed the trusted certificates,
            // we remove the certificates from the local user store to finish up the cleanup.
            var certificates = ListCertificates(purpose, StoreName.My, StoreLocation.CurrentUser, isValid: false, requireExportable: true, diagnostics);
            foreach (var certificate in certificates)
            {
                RemoveCertificate(certificate, RemoveLocations.All, diagnostics);
            }

            return diagnostics;
        }

        public void RemoveAllCertificates(CertificatePurpose purpose, StoreName storeName, StoreLocation storeLocation, string subject = null)
        {
            var certificates = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ?
                ListCertificates(purpose, StoreName.My, StoreLocation.CurrentUser, isValid: false) :
                ListCertificates(purpose, storeName, storeLocation, isValid: false);
            var certificatesWithName = subject == null ? certificates : certificates.Where(c => c.Subject == subject);

            var removeLocation = storeName == StoreName.My ? RemoveLocations.Local : RemoveLocations.Trusted;

            foreach (var certificate in certificates)
            {
                RemoveCertificate(certificate, removeLocation);
            }

            DisposeCertificates(certificates);
        }

        private void RemoveCertificate(X509Certificate2 certificate, RemoveLocations locations, DiagnosticInformation diagnostics = null)
        {
            switch (locations)
            {
                case RemoveLocations.Undefined:
                    throw new InvalidOperationException($"'{nameof(RemoveLocations.Undefined)}' is not a valid location.");
                case RemoveLocations.Local:
                    RemoveCertificateFromUserStore(certificate, diagnostics);
                    break;
                case RemoveLocations.Trusted when !RuntimeInformation.IsOSPlatform(OSPlatform.Linux):
                    RemoveCertificateFromTrustedRoots(certificate, diagnostics);
                    break;
                case RemoveLocations.All:
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        RemoveCertificateFromTrustedRoots(certificate, diagnostics);
                    }
                    RemoveCertificateFromUserStore(certificate, diagnostics);
                    break;
                default:
                    throw new InvalidOperationException("Invalid location.");
            }
        }

        private static void RemoveCertificateFromUserStore(X509Certificate2 certificate, DiagnosticInformation diagnostics)
        {
            diagnostics?.Debug($"Trying to remove certificate with thumbprint '{certificate.Thumbprint}' from certificate store '{StoreLocation.CurrentUser}\\{StoreName.My}'.");
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                var matching = store.Certificates
                    .OfType<X509Certificate2>()
                    .Single(c => c.SerialNumber == certificate.SerialNumber);

                store.Remove(matching);
                store.Close();
            }
        }

        private void RemoveCertificateFromTrustedRoots(X509Certificate2 certificate, DiagnosticInformation diagnostics)
        {
            diagnostics?.Debug($"Trying to remove certificate with thumbprint '{certificate.Thumbprint}' from certificate store '{StoreLocation.CurrentUser}\\{StoreName.Root}'.");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using (var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadWrite);
                    var matching = store.Certificates
                        .OfType<X509Certificate2>()
                        .SingleOrDefault(c => c.SerialNumber == certificate.SerialNumber);

                    if (matching != null)
                    {
                        store.Remove(matching);
                    }

                    store.Close();
                }
            }
            else
            {
                if (IsTrusted(certificate)) // On OSX this check just ensures its on the system keychain
                {
                    try
                    {
                        diagnostics?.Debug("Trying to remove the certificate trust rule.");
                        RemoveCertificateTrustRule(certificate);
                    }
                    catch
                    {
                        diagnostics?.Debug("Failed to remove the certificate trust rule.");
                        // We don't care if we fail to remove the trust rule if
                        // for some reason the certificate became untrusted.
                        // The delete command will fail if the certificate is
                        // trusted.
                    }
                    RemoveCertificateFromKeyChain(MacOSSystemKeyChain, certificate);
                }
                else
                {
                    diagnostics?.Debug("The certificate was not trusted.");
                }
            }
        }

        private static void RemoveCertificateTrustRule(X509Certificate2 certificate)
        {
            var certificatePath = Path.GetTempFileName();
            try
            {
                var certBytes = certificate.Export(X509ContentType.Cert);
                File.WriteAllBytes(certificatePath, certBytes);
                var processInfo = new ProcessStartInfo(
                    MacOSRemoveCertificateTrustCommandLine,
                    string.Format(
                        MacOSRemoveCertificateTrustCommandLineArgumentsFormat,
                        certificatePath
                    ));
                using (var process = Process.Start(processInfo))
                {
                    process.WaitForExit();
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(certificatePath))
                    {
                        File.Delete(certificatePath);
                    }
                }
                catch
                {
                    // We don't care about failing to do clean-up on a temp file.
                }
            }
        }

        private static void RemoveCertificateFromKeyChain(string keyChain, X509Certificate2 certificate)
        {
            var processInfo = new ProcessStartInfo(
                MacOSDeleteCertificateCommandLine,
                string.Format(
                    MacOSDeleteCertificateCommandLineArgumentsFormat,
                    certificate.Thumbprint.ToUpperInvariant(),
                    keyChain
                ))
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(processInfo))
            {
                var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException($@"There was an error removing the certificate with thumbprint '{certificate.Thumbprint}'.

{output}");
                }
            }
        }

        public DetailedEnsureCertificateResult EnsureAspNetCoreHttpsDevelopmentCertificate(
            DateTimeOffset notBefore,
            DateTimeOffset notAfter,
            string path = null,
            bool trust = false,
            bool includePrivateKey = false,
            string password = null,
            string subject = LocalhostHttpsDistinguishedName)
        {
            return EnsureValidCertificateExists(notBefore, notAfter, CertificatePurpose.HTTPS, path, trust, includePrivateKey, password, subject);
        }

        public DetailedEnsureCertificateResult EnsureValidCertificateExists(
            DateTimeOffset notBefore,
            DateTimeOffset notAfter,
            CertificatePurpose purpose,
            string path,
            bool trust,
            bool includePrivateKey,
            string password,
            string subject)
        {
            if (purpose == CertificatePurpose.All)
            {
                throw new ArgumentException("The certificate must have a specific purpose.");
            }

            var result = new DetailedEnsureCertificateResult();

            var currentUserCertificates = ListCertificates(purpose, StoreName.My, StoreLocation.CurrentUser, isValid: true, requireExportable: true, result.Diagnostics);
            var localMachineCertificates = ListCertificates(purpose, StoreName.My, StoreLocation.LocalMachine, isValid: true, requireExportable: true, result.Diagnostics);
            var certificates = currentUserCertificates.Concat(localMachineCertificates).ToList();

            if (subject != null)
            {
                currentUserCertificates = currentUserCertificates.Where(c => c.Subject == subject).ToList();
                localMachineCertificates = localMachineCertificates.Where(c => c.Subject == subject).ToList();

                var filteredCertificates = currentUserCertificates.Concat(localMachineCertificates).ToList();
                var excludedCertificates = certificates.Except(filteredCertificates);

                result.Diagnostics.Debug($"Filtering found certificates to those with a subject equal to '{subject}'");
                result.Diagnostics.Debug(result.Diagnostics.DescribeCertificates(filteredCertificates));
                result.Diagnostics.Debug($"Listing certificates excluded from consideration.");
                result.Diagnostics.Debug(result.Diagnostics.DescribeCertificates(excludedCertificates));

                certificates = filteredCertificates.ToList();
            }
            else
            {
                result.Diagnostics.Debug("Skipped filtering certificates by subject.");
            }

            result.ResultCode = EnsureCertificateResult.Succeeded;

            X509Certificate2 certificate = null;

            certificates = FilterCertificatesWithInaccesibleKeys(currentUserCertificates.ToList());

            if (certificates.Count() > 0)
            {
                result.Diagnostics.Debug("Found valid certificates present on the machine.");
                result.Diagnostics.Debug(result.Diagnostics.DescribeCertificates(certificates));

                // When in Mac OS we might be invoked as an untrusted process and asked to export a given certificate.
                // The presence of the environment variable below determines that. As such, it might be that there is none, one or many
                // instances of the same certificate in the keychain and we try to determine which one is the best. For that we check the
                // certificates with a matching hash and we try to pick the one that has access to the key.
                // If we don't find any, then we resort to the first one that matches.
                // Otherwise, we simply pick the first valid certificate.
                var certificateHash = Environment.GetEnvironmentVariable("ASPNETCORE_CERTIFICATE_HASH");
                certificate = string.IsNullOrEmpty(certificateHash) ? certificates.First() : certificates.FirstOrDefault(c => c.GetCertHashString() == certificateHash && CheckDeveloperCertificateKey(c));
                certificate ??= certificates.First(c => c.GetCertHashString() == certificateHash);

                result.Diagnostics.Debug("Selected certificate");
                result.Diagnostics.Debug(result.Diagnostics.DescribeCertificates(certificate));
                result.ResultCode = EnsureCertificateResult.ValidCertificatePresent;
            }
            else
            {
                result.Diagnostics.Debug("No valid certificates present on this machine. Trying to create one.");
                try
                {
                    switch (purpose)
                    {
                        case CertificatePurpose.All:
                            throw new InvalidOperationException("The certificate must have a specific purpose.");
                        case CertificatePurpose.HTTPS:
                            certificate = CreateAspNetCoreHttpsDevelopmentCertificate(notBefore, notAfter, subject, result.Diagnostics);
                            break;
                        default:
                            throw new InvalidOperationException("The certificate must have a purpose.");
                    }
                }
                catch (Exception e)
                {
                    result.Diagnostics.Error("Error creating the certificate.", e);
                    result.ResultCode = EnsureCertificateResult.ErrorCreatingTheCertificate;
                    return result;
                }

                try
                {
                    certificate = SaveCertificateInStore(certificate, StoreName.My, StoreLocation.CurrentUser, result.Diagnostics);
                }
                catch (Exception e)
                {
                    result.Diagnostics.Error($"Error saving the certificate in the certificate store '{StoreLocation.CurrentUser}\\{StoreName.My}'.", e);
                    result.ResultCode = EnsureCertificateResult.ErrorSavingTheCertificateIntoTheCurrentUserPersonalStore;
                    return result;
                }
            }

            if (path != null)
            {
                result.Diagnostics.Debug("Trying to export the certificate.");
                result.Diagnostics.Debug(result.Diagnostics.DescribeCertificates(certificate));
                try
                {
                    ExportCertificate(certificate, path, includePrivateKey, password, result.Diagnostics);
                }
                catch (Exception e)
                {
                    result.Diagnostics.Error("An error ocurred exporting the certificate.", e);
                    result.ResultCode = EnsureCertificateResult.ErrorExportingTheCertificate;
                    return result;
                }
            }

            if ((RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) && trust)
            {
                try
                {
                    result.Diagnostics.Debug("Trying to export the certificate.");
                    TrustCertificate(certificate, result.Diagnostics);
                }
                catch (UserCancelledTrustException)
                {
                    result.Diagnostics.Error("The user cancelled trusting the certificate.", null);
                    result.ResultCode = EnsureCertificateResult.UserCancelledTrustStep;
                    return result;
                }
                catch (Exception e)
                {
                    result.Diagnostics.Error("There was an error trusting the certificate.", e);
                    result.ResultCode = EnsureCertificateResult.FailedToTrustTheCertificate;
                    return result;
                }
            }

            return result;
        }

        private List<X509Certificate2> FilterCertificatesWithInaccesibleKeys(List<X509Certificate2> currentUserCertificates)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_UNTRUSTED")))
            {
                // We don't want to check the key accessibility here as we want to prevent systemic issues where we can't access the key after
                // we created it and that could cause the system to be spammed with developer certificates.
                // We are only going to check the key in the OS X case where we know it's an OS level issue.
                return currentUserCertificates;
            }
            else
            {
                // Given the limitations imposed by Mac OS Catalina, we need to be extra thorough here, check for access to the key
                // in both, trusted and untrusted contexts, and try and offer the best fix for a key we can offer.
                // The dotnet executable can itself be trusted or untrusted, depending on the sequence of installations that the user
                // made. While we plan to sign all supported versions, users might have older versions in their machines that can
                // install on top. We might consider discarding this scenario and only support moving forward, which assumes that when
                // the tool runs for the first time, it does so in the context of a trusted process.
                // Scenarios:
                // * We can be upgrading from an existing installation to a new version (signed): In this case, it is likely that the user
                //   has an existing valid certificate that was created in the untrusted partition but that we don't have access to from
                //   the current partition.
                //   To fix this case, we are going to invoke an untrusted executable (that we included in the SDK) and we are going to ask
                //   it to export the certificate into a location and with a key that we provided. If that completes successfully, then we
                //   can import the certificate into the store again and the key will be imported into the trusted partition.
                //   This should work without requiring user-interaction, so it is appropriate for the first run experience.
                // * We can be on a clean 3.1 install, in which there is no certificate available and we will be creating one during the first
                //   run experience. We will have to populate the certificate in the trusted and in the untrusted partition. To that end we will
                //   export the certificate to a temporary file and we will invoke the untrusted executable to import the certificate into the
                //   key chain. That way there should be a certificate instance with a key available on both partitions.
                // * We can be in a situation where a user installed an older version of the SDK that is not signed and overrode the 'dotnet'
                //   executable or is using an unsigned 'dotnet' from a binary package. In this case this process won't even have access to the
                //   key and we won't be able to do anything, other than to ask the user to run dotnet dev-certs 'https --clean and then dotnet-
                //   dev-certs https --trust'.
                //   The result of that action will be that all the certificates get purged from the user machine, which means that a new untrusted
                //   certificate will be created. At that point, 'dotnet run' and './publishedapp' should work, and I believe that 'dotnet './app.dll'
                //   would work or not depending on whether the host is considered signed or unsigned at that point.
                // Fixes:
                // * There are two types of fixes that we plan to do:
                //   * Importing the certificate into the user keychain through an unsigned executable. There will be two certificates on the user
                //     machine with the same key but for which each key is in a different security partition. At runtime we will simply iterate over
                //     the valid certificates and will perform a keycheck to validate the certificate works.
                //     * We will print a message too, indicating that to avoid future problems we will make the key accessible across partitions.
                //   * Making the key accessible across partitions. We can't do this inside the first run experience as it requires interaction, but we
                //     will keep track of it in the following situations:
                //     * When running dotnet dev-certs https --check: We will indicate that the key is not available in some context through a new return code.
                //     * When running dotnet dev-certs https: If no key is present, we will generate a new one, installing it on the trusted partition and
                //       making the key accessible across partitions. This will require user interaction, which will technically be breaking (as some script that
                //       might be invoking the tool will break, but it is highly unlikely).
                //       We will only try to enable access to the key across partitions automatically when we determine that all the 'localhost' certificates present
                //       on the current key chain are associated with ASP.NET Core.
                //     * When running 'dotnet dev-certs https --trust' as we are already asking him for permisions to trust the certificate.

                // At this initial stage we are going to evaluate the state of the certificates in the current user store.
                // If we find one that is accessible in both partitions, we will filter results to that, essentially ignoring others.
                // If we find one that is accessible in the untrusted partition, we will try to import it here.
                // If we find one that is accessible in the trusted partition, we will try to import it there by invoking the tool in a special "secret" way.
                // If we find that the key is innaccessible in any partition it can mean that someone overrode the 'dotnet' executable and is untrusted.
                // We are going to have a maximum threshold of 10 keys after which we stop trying. This is to prevent users from running dotnet dev-certs https
                // multiple times and filling their stores/keychains with certificates if there is a systemic issue that is preventing certificates from being
                // accessed after the cert is imported.
                var keyAccessStatus = new Dictionary<X509Certificate2, (KeyAccessResult result, string path, string password)>();
                foreach (var candidate in currentUserCertificates)
                {
                    // We consider our process trusted by default, if that isn't the case, we'll end up creating a new key/certificate.
                    var canAccessCurrentProcess = CheckDeveloperCertificateKey(candidate);
                    var certificatePassword = Guid.NewGuid().ToString("N");
                    var certificateFilePath = Path.GetTempFileName();

                    var additionalEnvironmentVariables = new Dictionary<string, string>
                    {
                        ["ASPNETCORE_CERTIFICATE_HASH"] = candidate.GetCertHashString(HashAlgorithmName.SHA256)
                    };

                    var untrustedResult = RunDevCertsCommandAsUntrusted(
                        $"-ep ${certificateFilePath} -p ${certificatePassword}",
                        additionalEnvironmentVariables);

                    var canAccessFromUntrustedProcess = untrustedResult == 0;
                    keyAccessStatus[candidate] = (canAccessCurrentProcess, canAccessCurrentProcess) switch
                    {
                        (true, true) => (KeyAccessResult.Success, certificateFilePath, certificatePassword),
                        (true, false) => (KeyAccessResult.TrustedCanAccess, certificateFilePath, certificatePassword),
                        (false, true) => (KeyAccessResult.UntrustedCanAccess, certificateFilePath, certificatePassword),
                        (false, false) => (KeyAccessResult.Failure, certificateFilePath, certificatePassword)
                    };
                }

                // We will do two passes, one to discard easy cases, where there are existing certificates we can use
                // (in which case we won't even bother repairing the old certificates).
                // or when we've found ourselves in a situation where there are two many invalid keys and we give up to
                // make sure other issues can't cause the certificate store/key chain to be populated with a large amount of keys.
                var failedCertificates = new List<X509Certificate2>();
                var successfulCertificates = new List<X509Certificate2>();
                foreach (var (candidate, (result, _, _)) in keyAccessStatus)
                {
                    if (result == KeyAccessResult.Failure)
                    {
                        failedCertificates.Add(candidate);
                    }
                    else if (result == KeyAccessResult.Success)
                    {
                        successfulCertificates.Add(candidate);
                    }
                }

                if (successfulCertificates.Count > 0)
                {
                    return successfulCertificates;
                }

                // Don't try to filter if there is more than 10 innaccessible certs and simply fail.
                if (failedCertificates.Count > 10)
                {
                    return currentUserCertificates;
                }

                // We are collecting a new list because we might need to export the certificate again later, so we need to ensure we have the
                // right handle to it.
                var resultingCertificates = new List<X509Certificate2>();

                // We are now on to repair keys.
                foreach (var (candidate, (result, path, password)) in keyAccessStatus)
                {
                    if (result == KeyAccessResult.UntrustedCanAccess)
                    {
                        // At this point the key has been exported to a known PFX that we have stored in path with password password.
                        var candidateWithKey = new X509Certificate2(path, password, X509KeyStorageFlags.Exportable);
                        resultingCertificates.Add(SaveCertificateInStore(candidateWithKey, StoreName.My, StoreLocation.CurrentUser));
                    }
                    else if (result == KeyAccessResult.TrustedCanAccess)
                    {
                        // At this point we can access the key, but the untrusted process can't. We will export the certificate and
                        // invoke the executable in a special way to signal it needs to import the certificate provided through environment
                        // variables.
                        ExportCertificate(candidate, path, includePrivateKey: true, password);
                        var additionalEnvironmentVariables = new Dictionary<string, string>
                        {
                            ["ASPNETCORE_ACTION"] = "IMPORT_CERTIFICATE",
                            ["ASPNETCORE_CERTIFICATE_PATH"] = path,
                            ["ASPNETCORE_CERTIFICATE_PASSWORD"] = password
                        };

                        _ = RunDevCertsCommandAsUntrusted("", additionalEnvironmentVariables);
                        resultingCertificates.Add(candidate);
                    }
                }

                return resultingCertificates;
            }
        }

        private class UserCancelledTrustException : Exception
        {
        }

        private enum RemoveLocations
        {
            Undefined,
            Local,
            Trusted,
            All
        }

        internal class DetailedEnsureCertificateResult
        {
            public EnsureCertificateResult ResultCode { get; set; }
            public DiagnosticInformation Diagnostics { get; set; } = new DiagnosticInformation();
        }

        internal class DiagnosticInformation
        {
            public IList<string> Messages { get; } = new List<string>();

            public IList<Exception> Exceptions { get; } = new List<Exception>();

            internal void Debug(params string[] messages)
            {
                foreach (var message in messages)
                {
                    Messages.Add(message);
                }
            }

            internal string[] DescribeCertificates(params X509Certificate2[] certificates)
            {
                return DescribeCertificates(certificates.AsEnumerable());
            }

            internal string[] DescribeCertificates(IEnumerable<X509Certificate2> certificates)
            {
                var result = new List<string>();
                result.Add($"'{certificates.Count()}' found matching the criteria.");
                result.Add($"SUBJECT - THUMBPRINT - NOT BEFORE - EXPIRES - HAS PRIVATE KEY");
                foreach (var certificate in certificates)
                {
                    result.Add(DescribeCertificate(certificate));
                }

                return result.ToArray();
            }

            private static string DescribeCertificate(X509Certificate2 certificate) =>
                $"{certificate.Subject} - {certificate.Thumbprint} - {certificate.NotBefore} - {certificate.NotAfter} - {certificate.HasPrivateKey}";

            internal void Error(string preamble, Exception e)
            {
                Messages.Add(preamble);
                if (Exceptions.Count > 0 && Exceptions[Exceptions.Count - 1] == e)
                {
                    return;
                }

                var ex = e;
                while (ex != null)
                {
                    Messages.Add("Exception message: " + ex.Message);
                    ex = ex.InnerException;
                }

            }
        }
    }
}
