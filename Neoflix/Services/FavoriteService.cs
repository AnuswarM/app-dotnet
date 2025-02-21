﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neo4j.Driver;
using Neoflix.Example;
using Neoflix.Exceptions;

namespace Neoflix.Services
{
    public class FavoriteService
    {
        private readonly IDriver _driver;

        /// <summary>
        /// Initializes a new instance of <see cref="FavoriteService"/> that handles favorite database calls.
        /// </summary>
        /// <param name="driver">Instance of Neo4j Driver, which will be used to interact with Neo4j</param>
        public FavoriteService(IDriver driver)
        {
            _driver = driver;
        }

        /// <summary>
        /// Get a list of movies that have an incoming "HAS_FAVORITE" relationship from a User node with the supplied "userId".<br/><br/>
        /// Records should be ordered by <see cref="sort"/>, and in the direction specified by <see cref="order"/>. <br/>
        /// The maximum number of records returned should be limited by <see cref="limit"/> and <see cref="skip"/> should be used to skip a certain number of records.<br/><br/>
        /// </summary>
        /// <param name="userId">The User's Id.</param>
        /// <param name="sort">The field to order the records by.</param>
        /// <param name="order">The direction of the order.</param>
        /// <param name="limit">The maximum number of records to return.</param>
        /// <param name="skip">The number of records to skip.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a list of records.
        /// </returns>
        // tag::all[]
        public async Task<Dictionary<string, object>[]> AllAsync(string userId, string sort = "title",
            Ordering order = Ordering.Asc, int limit = 6, int skip = 0)
        {
            var session = _driver.AsyncSession();
            return await session.ExecuteReadAsync(async tx =>
            {
                var cursor = await tx.RunAsync(@$"
                                    MATCH (u:User {{userId: $userId}})-[r:HAS_FAVORITE]->(m)
                                    WHERE m.{sort} IS NOT NULL
                                    RETURN m {{ .*, favorite: true }} AS movie
                                    ORDER BY m.{sort} {order.ToString("G").ToUpper()}
                                    SKIP $skip
                                    LIMIT $limit", new { userId, skip, limit });

                var response = await cursor.ToListAsync();
                var movies = response.Select(res => res["movie"].As<Dictionary<string, object>>()).ToArray();
                return movies;
            });
        }
        // end::all[]

        /// <summary>
        /// Create a "HAS_FAVORITE" relationship between the User and Movie ID nodes provided. <br/><br/>
        /// If either the user or movie cannot be found, a `NotFoundError` should be thrown.
        /// </summary>
        /// <param name="userId">The unique ID for the User node.</param>
        /// <param name="movieId">The unique tmdbId for the Movie node.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains The updated movie record with `favorite` set to true.
        /// </returns>
        // tag::add[]
        public async Task<Dictionary<string, object>> AddAsync(string userId, string movieId)
        {
            var session = _driver.AsyncSession();
            var updatedMovie = await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(@"
                                    MATCH (u:User {userId: $userId})
                                    MATCH (m:Movie {tmdbId: $tmdbId})
        
                                    MERGE (u)-[r:HAS_FAVORITE]->(m)
                                    ON CREATE SET r.createdAt = datetime()
        
                                    RETURN m {
                                        .*,
                                        favorite: true
                                    } AS movie",
                                    new
                                    {
                                        userId,
                                        tmdbId = movieId
                                    });

                if (!await cursor.FetchAsync())
                    return null;

                return cursor.Current["movie"].As<Dictionary<string, object>>();
            });

            if(updatedMovie is null)
            {
                throw new NotFoundException($"Couldn't create a favorite relationship for User {userId} and Movie {movieId}");
            }
            return updatedMovie;
        }
        // end::add[]

        /// <summary>
        /// Remove the "HAS_FAVORITE" relationship between the User and Movie ID nodes provided. <br/><br/>
        /// If either the user or movie cannot be found, a `NotFoundError` should be thrown.
        /// </summary>
        /// <param name="userId">The unique ID for the User node.</param>
        /// <param name="movieId">The unique tmdbId for the Movie node.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains The updated movie record with `favorite` set to false.
        /// </returns>
        // tag::remove[]
        public async Task<Dictionary<string, object>> RemoveAsync(string userId, string movieId)
        {
            var session = _driver.AsyncSession();
            var updatedMovie = await session.ExecuteWriteAsync(async tx =>
            {
                var cursor = await tx.RunAsync(@"
                                    MATCH (u:User {userId: $userId})-[r:HAS_FAVORITE]->(m:Movie {tmdbId: $tmdbId})
                                    DELETE r

                                    RETURN m {
                                        .*,
                                        favorite: false
                                    } AS movie",
                                    new
                                    {
                                        userId,
                                        tmdbId = movieId
                                    });

                if (!await cursor.FetchAsync())
                    return null;

                return cursor.Current["movie"].As<Dictionary<string, object>>();
            });

            if (updatedMovie is null)
            {
                throw new NotFoundException($"Couldn't delete a favorite relationship for User {userId} and Movie {movieId}");
            }
            return updatedMovie;
        }
        // end::remove[]
    }
}