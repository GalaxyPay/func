// <copyright file="X509.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace FUNC
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    ///     X509 utility methods.
    /// </summary>
    public static class X509
    {
        /// <summary>
        ///     Generates an X509 certificate for the specified <paramref name="subject"/>.
        /// </summary>
        /// <param name="subject">The certificate subject.</param>
        /// <param name="x509KeyStorageFlags">The optional key storage flags for the certificate.</param>
        /// <returns>The generated certificate.</returns>
        public static X509Certificate2 Generate(string subject, X509KeyStorageFlags x509KeyStorageFlags = X509KeyStorageFlags.MachineKeySet)
        {
            var password = Guid.NewGuid().ToString();

            using RSA rsa = RSA.Create(2048);

            var request = new CertificateRequest(
                new X500DistinguishedName($"CN={subject}"),
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            var certificate = request.CreateSelfSigned(
                new DateTimeOffset(DateTime.UtcNow.AddDays(-1)),
                new DateTimeOffset(DateTime.UtcNow.AddDays(365)));

            return new X509Certificate2(certificate.Export(X509ContentType.Pkcs12, password), password, x509KeyStorageFlags);
        }
    }
}