﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo4j.Driver;
using Neoflix.Example;
using Neoflix.Exceptions;

namespace Neoflix.Services
{
    public class MovieService
    {
        private readonly IDriver _driver;

        /// <summary>
        /// Initializes a new instance of <see cref="MovieService"/> that handles movie database calls.
        /// </summary>
        /// <param name="driver">Instance of Neo4j Driver, which will be used to interact with Neo4j</param>
        public MovieService(IDriver driver)
        {
            _driver = driver;
        }

        /// <summary>
        /// Get a paginated list of Movies. <br/><br/>
        /// Records should be ordered by <see cref="sort"/>, and in the direction specified by <see cref="order"/>. <br/>
        /// The maximum number of records returned should be limited by <see cref="limit"/> and <see cref="skip"/> should be used to skip a certain number of records.<br/><br/>
        /// If a userId value is supplied, a "favorite" boolean property should be returned to signify whether the user has added the movie to their "My Favorites" list.
        /// </summary>
        /// <param name="sort">The field to order the records by.</param>
        /// <param name="order">The direction of the order.</param>
        /// <param name="limit">The maximum number of records to return.</param>
        /// <param name="skip">The number of records to skip.</param>
        /// <param name="userId">Optional user's Id.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a list of records.
        /// </returns>
        // tag::all[]
        public async Task<Dictionary<string, object>[]> AllAsync(string sort = "title",
            Ordering order = Ordering.Asc, int limit = 6, int skip = 0, string userId = null)
        {
            var session = _driver.AsyncSession();

            return await session.ExecuteReadAsync(async tx =>
            {
                var favorites = await GetUserFavoritesAsync(tx, userId);
                var cursor = await tx.RunAsync(@$"
                                    MATCH (m:Movie)
                                    WHERE m.{sort} IS NOT NULL
                                    RETURN m {{ .*, favorite: m.tmdbId IN $favorites }} AS movie
                                    ORDER BY m.{sort} {order.ToString("G").ToUpper()}
                                    SKIP $skip
                                    LIMIT $limit", new { skip, limit, favorites });

                var response = await cursor.ToListAsync();
                var movies = response.Select(res => res["movie"].As<Dictionary<string, object>>()).ToArray();
                return movies;
            });
        }
        // end::all[]

        /// <summary>
        /// Get a paginated list of Movies by Genre. <br/><br/>
        /// Records should be filtered by <see cref="name"/>, ordered by <see cref="sort"/>, and in the direction specified by <see cref="order"/>. <br/>
        /// The maximum number of records returned should be limited by <see cref="limit"/> and <see cref="skip"/> should be used to skip a certain number of records.
        /// If a userId value is supplied, a "favorite" boolean property should be returned to signify whether the user has added the movie to their "My Favorites" list.<br/><br/>
        /// </summary>
        /// <param name="name">The genre name to filter records by.</param>
        /// <param name="sort">The field to order the records by.</param>
        /// <param name="order">The direction of the order.</param>
        /// <param name="limit">The maximum number of records to return.</param>
        /// <param name="skip">The number of records to skip.</param>
        /// <param name="userId">Optional user's Id.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a list of records.
        /// </returns>
        // tag::getByGenre[]
        public async Task<Dictionary<string, object>[]> GetByGenreAsync(string name, string sort = "title",
            Ordering order = Ordering.Asc, int limit = 6, int skip = 0, string userId = null)
        {
            var session = _driver.AsyncSession();

            return await session.ExecuteReadAsync(async tx =>
            {
                var favorites = await GetUserFavoritesAsync(tx, userId);
                var cursor = await tx.RunAsync(@$"
                                    MATCH (m:Movie)-[:IN_GENRE]->(:Genre {{name: $name}})
                                    WHERE m.{sort} IS NOT NULL
                                    RETURN m {{ .*, favorite: m.tmdbId IN $favorites }} AS movie
                                    ORDER BY m.{sort} {order.ToString("G").ToUpper()}
                                    SKIP $skip
                                    LIMIT $limit", new { skip, limit, favorites, name });

                var response = await cursor.ToListAsync();
                var movies = response.Select(res => res["movie"].As<Dictionary<string, object>>()).ToArray();
                return movies;
            });
        }
        // end::getByGenre[]

        /// <summary>
        /// Get a paginated list of Movies that have ACTED_IN relationship to a Person with <see cref="id"/>.<br/><br/>
        /// Records should be ordered by <see cref="sort"/>, and in the direction specified by <see cref="order"/>. <br/>
        /// The maximum number of records returned should be limited by <see cref="limit"/> and <see cref="skip"/> should be used to skip a certain number of records.<br/><br/>
        /// If a userId value is supplied, a "favorite" boolean property should be returned to signify whether the user has added the movie to their "My Favorites" list.
        /// </summary>
        /// <param name="id">the Person's id.</param>
        /// <param name="sort">The field to order the records by.</param>
        /// <param name="order">The direction of the order.</param>
        /// <param name="limit">The maximum number of records to return.</param>
        /// <param name="skip">The number of records to skip.</param>
        /// <param name="userId">Optional user's Id.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a list of records.
        /// </returns>
        // tag::getForActor[]
        public async Task<Dictionary<string, object>[]> GetForActorAsync(string id, string sort = "title",
            Ordering order = Ordering.Asc, int limit = 6, int skip = 0, string userId = null)
        {
            var session = _driver.AsyncSession();

            return await session.ExecuteReadAsync(async tx =>
            {
                var favorites = await GetUserFavoritesAsync(tx, userId);
                var cursor = await tx.RunAsync(@$"
                                    MATCH (:Person {{tmdbId: $id}})-[:ACTED_IN]->(m:Movie)
                                    WHERE m.{sort} IS NOT NULL
                                    RETURN m {{ .*, favorite: m.tmdbId IN $favorites }} AS movie
                                    ORDER BY m.{sort} {order.ToString("G").ToUpper()}
                                    SKIP $skip
                                    LIMIT $limit", new { skip, limit, favorites, id });

                var response = await cursor.ToListAsync();
                var movies = response.Select(res => res["movie"].As<Dictionary<string, object>>()).ToArray();
                return movies;
            });
        }
        // end::getForActor[]

        /// <summary>
        /// Get a paginated list of Movies that have DIRECTED relationship to a Person with <see cref="id"/>.<br/><br/>
        /// Records should be ordered by <see cref="sort"/>, and in the direction specified by <see cref="order"/>. <br/>
        /// The maximum number of records returned should be limited by <see cref="limit"/> and <see cref="skip"/> should be used to skip a certain number of records.<br/><br/>
        /// If a userId value is supplied, a "favorite" boolean property should be returned to signify whether the user has added the movie to their "My Favorites" list.
        /// </summary>
        /// <param name="id">the Person's id.</param>
        /// <param name="sort">The field to order the records by.</param>
        /// <param name="order">The direction of the order.</param>
        /// <param name="limit">The maximum number of records to return.</param>
        /// <param name="skip">The number of records to skip.</param>
        /// <param name="userId">Optional user's Id.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a list of records.
        /// </returns>
        // tag::getForDirector[]
        public async Task<Dictionary<string, object>[]> GetForDirectorAsync(string id, string sort = "title",
            Ordering order = Ordering.Asc, int limit = 6, int skip = 0, string userId = null)
        {
            var session = _driver.AsyncSession();

            return await session.ExecuteReadAsync(async tx =>
            {
                var favorites = await GetUserFavoritesAsync(tx, userId);
                var cursor = await tx.RunAsync(@$"
                                    MATCH (:Person {{tmdbId: $id}})-[:DIRECTED]->(m:Movie)
                                    WHERE m.{sort} IS NOT NULL
                                    RETURN m {{ .*, favorite: m.tmdbId IN $favorites }} AS movie
                                    ORDER BY m.{sort} {order.ToString("G").ToUpper()}
                                    SKIP $skip
                                    LIMIT $limit", new { skip, limit, favorites, id });

                var response = await cursor.ToListAsync();
                var movies = response.Select(res => res["movie"].As<Dictionary<string, object>>()).ToArray();
                return movies;
            });
        }
        // end::getForDirector[]

        /// <summary>
        /// Find a Movie node with the ID passed as <see cref="id"/>.<br/><br/>
        /// Along with the returned payload, a list of actors, directors, and genres should be included.<br/>
        /// The number of incoming RATED relationships should be returned with key "ratingCount".<br/><br/>
        /// If a userId value is supplied, a "favorite" boolean property should be returned to signify whether the user has added the movie to their "My Favorites" list.
        /// </summary>
        /// <param name="id">The tmdbId for a Movie.</param>
        /// <param name="userId">Optional user's Id.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a record.
        /// </returns>
        // tag::findById[]
        public async Task<Dictionary<string, object>> FindByIdAsync(string id, string userId = null)
        {
            var session = _driver.AsyncSession();

            return await session.ExecuteReadAsync(async tx =>
            {
                var favorites = await GetUserFavoritesAsync(tx, userId);
                var cursor = await tx.RunAsync(@$"
                                    MATCH (m:Movie {{tmdbId: $id}})
                                    RETURN m {{
                                      .*,
                                      actors: [ (a)-[r:ACTED_IN]->(m) | a {{ .*, role: r.role }} ],
                                      directors: [ (d)-[:DIRECTED]->(m) | d {{ .* }} ],
                                      genres: [ (m)-[:IN_GENRE]->(g) | g {{ .name }}],
                                      ratingCount: count{{ (m)<-[:RATED]-() }},
                                      favorite: m.tmdbId IN $favorites
                                    }} AS movie
                                    LIMIT 1", new { favorites, id });

                if(!await cursor.FetchAsync())
                {
                    throw new NotFoundException($"Could not find a movie with the id '{id}'.");
                }
                return cursor.Current["movie"].As<Dictionary<string, object>>();
            });
        }
        // end::findById[]

        /// <summary>
        /// Get a paginated list of similar movies to the Movie with the <see cref="id"/> supplied.<br/>
        /// This similarity is calculated by finding movies that have many first degree connections in common: Actors, Directors and Genres.<br/><br/>
        /// The maximum number of records returned should be limited by <see cref="limit"/> and <see cref="skip"/> should be used to skip a certain number of records.<br/><br/>
        /// If a userId value is supplied, a "favorite" boolean property should be returned to signify whether the user has added the movie to their "My Favorites" list.
        /// </summary>
        /// <param name="id">The tmdbId for a Movie.</param>
        /// <param name="limit">The maximum number of records to return.</param>
        /// <param name="skip">The number of records to skip.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a list of records.
        /// </returns>
        // tag::getSimilarMovies[]
        public async Task<Dictionary<string, object>[]> GetSimilarMoviesAsync(string id, int limit, int skip, string userId = null)
        {
            var session = _driver.AsyncSession();

            return await session.ExecuteReadAsync(async tx =>
            {
                var favorites = await GetUserFavoritesAsync(tx, userId);
                var cursor = await tx.RunAsync(@$"
                                    MATCH (:Movie {{tmdbId: $id}})-[:IN_GENRE|ACTED_IN|DIRECTED]->()<-[:IN_GENRE|ACTED_IN|DIRECTED]-(m)
                                    WHERE m.imdbRating IS NOT NULL

                                    WITH m, count(*) AS inCommon
                                    WITH m, inCommon, m.imdbRating * inCommon AS score
                                    ORDER BY score DESC

                                    SKIP $skip
                                    LIMIT $limit

                                    RETURN m {{
                                        .*,
                                        score: score,
                                        favorite: m.tmdbId IN $favorites
                                    }} AS movie", new { skip, limit, favorites, id });

                var response = await cursor.ToListAsync();
                var movies = response.Select(res => res["movie"].As<Dictionary<string, object>>()).ToArray();
                return movies;
            });
            // end::getSimilarMovies[]
        }

        /// <summary>
        /// Get a list of tmdbId properties for the movies that the user has added to their "My Favorites" list.
        /// </summary>
        /// <param name="transaction">The open transaction.</param>
        /// <param name="userId">The ID of the current user.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.<br/>
        /// The task result contains a list of tmdbIds.
        /// </returns>
        // tag::getUserFavorites[]
        private static async Task<string[]> GetUserFavoritesAsync(IAsyncQueryRunner transaction, string userId)
        {
            if (userId == null)
                return new string[] { };

            var cursor = await transaction.RunAsync("MATCH (u:User {userId: $userId})-[:HAS_FAVORITE]->(m) RETURN m.tmdbId AS id", new { userId });
            var records = await cursor.ToListAsync();
            return records.Select(x => x["id"].As<string>()).ToArray();
        }
        // end::getUserFavorites[]
    }
}
