using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Notsy
{

    internal class AzureSQLAuthProvider : SqlAuthenticationProvider
    {
        private static readonly string[] _azureSqlScopes = new[]
        {
            "https://database.windows.net//.default"
        };

        private readonly TokenCredential _credential;

        public AzureSQLAuthProvider()
        {
            var clientId = Environment.GetEnvironmentVariable("ClientId");
            _credential = new ManagedIdentityCredential(clientId);
        }

        public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
        {
            try
            {
                var tokenRequestContext = new TokenRequestContext(_azureSqlScopes);
                var tokenResult = await _credential.GetTokenAsync(tokenRequestContext, default);
                return new SqlAuthenticationToken(tokenResult.Token, tokenResult.ExpiresOn);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error acquiring token: {ex.Message}");
                throw;
            }
        }

        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
            => authenticationMethod.Equals(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity);
    }
}