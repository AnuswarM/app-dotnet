﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Neo4j.Driver;
using Neoflix.Exceptions;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Neoflix.Services
{
    public class AuthService
    {
        private readonly IDriver _driver;

        /// <summary>
        /// Initializes a new instance of <see cref="AuthService"/> that handles Auth database calls.
        /// </summary>
        /// <param name="driver">Instance of Neo4j Driver, which will be used to interact with Neo4j</param>
        // tag::constructor[]
        public AuthService(IDriver driver)
        {
            _driver = driver;
        }
        // end::constructor[]

        /// <summary>
        /// Create a new User node in the database with the email and name provided,<br/>
        /// along with an encrypted version of the password and a "userId" property generated by the server.<br/><br/>
        /// The properties also be used to generate a JWT "token" which should be included with the returned user.
        /// </summary>
        /// <param name="email">The email for the new user.</param>
        /// <param name="plainPassword">The password for the new user.</param>
        /// <param name="name">The name of the new user.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a registered user.
        /// </returns>
        /// <exception cref="ValidationException"></exception>
        // tag::register[]
        public async Task<Dictionary<string, object>> RegisterAsync(string email, string plainPassword, string name)
        {
            var rounds = Config.UnpackPasswordConfig();
            var encrypted = BCryptNet.HashPassword(plainPassword, rounds);

            var session = _driver.AsyncSession();

            try
            {
                var user = await session.ExecuteWriteAsync(async tx =>
                {
                    var cursor = await tx.RunAsync(@"
                                        CREATE (u:User {
                                            userId: randomUuid(),
                                            email: $email,
                                            password: $encrypted,
                                            name: $name
                                        })
                                        RETURN u { .userId, .name, .email } as u", new { email, encrypted, name });
                    var user = await cursor.SingleAsync();
                    return user["u"].As<Dictionary<string, object>>();
                });

                var safeProperties = SafeProperties(user);
                safeProperties.Add("token", JwtHelper.CreateToken(GetUserClaims(safeProperties)));

                return safeProperties;
            }
            catch (ClientException ex) when (ex.Code == "Neo.ClientError.Schema.ConstraintValidationFailed")
            {
                throw new ValidationException(ex.Message, email);
            }
            catch (Exception ex)
            {
                throw new BadHttpRequestException($"Failed to register user. Details: {ex.Message}");
            }
        }
        // end::register[]

        /// <summary>
        /// Find a user by the email address provided and attempt to verify the password.<br/><br/>
        /// If a user is not found or the passwords do not match, null should be returned.<br/>
        /// Otherwise, the users properties should be returned along with an encoded JWT token with a set of 'claims'.
        /// <code>
        /// {
        ///    userId: 'some-random-uuid',
        ///    email: 'graphacademy@neo4j.com',
        ///    name: 'GraphAcademy User',
        ///    token: '...'
        /// }
        /// </code>
        /// </summary>
        /// <param name="email">The email for the new user.</param>
        /// <param name="plainPassword">The password for the new user.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains an authorized user or null when the user is not found or password is incorrect.
        /// </returns>
        // tag::authenticate[]
        public async Task<Dictionary<string, object>> AuthenticateAsync(string email, string plainPassword)
        {
            var session = _driver.AsyncSession();

            try
            {
                return await session.ExecuteReadAsync(async tx =>
                {
                    var cursor = await tx.RunAsync(@"MATCH (u: User {email: $email}) RETURN u { .* } AS u", new { email });

                    if (!await cursor.FetchAsync())
                    {
                        return null;
                    }

                    var user = cursor.Current["u"].As<Dictionary<string, object>>();
                    if (!BCryptNet.Verify(plainPassword, user["password"].As<string>()))
                    {
                        return null;
                    }

                    var safeProperties = SafeProperties(user);
                    safeProperties.Add("token", JwtHelper.CreateToken(GetUserClaims(safeProperties)));
                    return safeProperties;
                });
            }
            catch(Exception ex)
            {
                throw new BadHttpRequestException(ex.Message);
            }
        }
        // end::authenticate[]

        /// <summary>
        /// Sanitize properties to ensure password is not included.
        /// </summary>
        /// <param name="user">The User's properties from the database</param>
        /// <returns>The User's properties from the database without password</returns>
        private static Dictionary<string, object> SafeProperties(Dictionary<string, object> user)
        {
            return user
                .Where(x => x.Key != "password")
                .ToDictionary(x => x.Key, x => x.Value);
        }

        /// <summary>
        /// Convert user's properties and convert the "safe" properties into a set of claims that can be encoded into a JWT.
        /// </summary>
        /// <param name="user">User's properties from the database.</param>
        /// <returns>select properties in a new dictionary.</returns>
        private Dictionary<string, object> GetUserClaims(Dictionary<string, object> user)
        {
            return new Dictionary<string, object>
            {
                ["sub"] = user["userId"],
                ["userId"] = user["userId"],
                ["name"] = user["name"]
            };
        }

        /// <summary>
        /// Take the claims encoded into a JWT token and return the information needed to authenticate this user against the database.
        /// </summary>
        /// <param name="claims"></param>
        /// <returns>claims in a dictionary.</returns>
        public Dictionary<string, object> ConvertClaimsToRecord(Dictionary<string, object> claims)
        {
            return claims
                .Append(new KeyValuePair<string, object>("userId", claims["sub"]))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}
