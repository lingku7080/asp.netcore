// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Internal;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.AspNetCore.Routing.Tree;

namespace Microsoft.AspNetCore.Routing
{
    internal sealed class RouteValuesAddressScheme : IEndpointAddressScheme<RouteValuesAddress>, IDisposable
    {
        private readonly DataSourceDependentCache<StateEntry> _cache;

        public RouteValuesAddressScheme(EndpointDataSource dataSource)
        {
            _cache = new DataSourceDependentCache<StateEntry>(dataSource, Initialize);
        }

        // Internal for tests
        internal StateEntry State => _cache.EnsureInitialized();

        public IEnumerable<Endpoint> FindEndpoints(RouteValuesAddress address)
        {
            if (address == null)
            {
                throw new ArgumentNullException(nameof(address));
            }

            var state = State;

            List<RouteEndpoint> matches = null;
            if (string.IsNullOrEmpty(address.RouteName) && state.Lookups.Count > 0)
            {
                matches = new List<RouteEndpoint>();

                var outboundEndpointMatches1 = new List<OutboundEndpointMatch>();

                var lookup = state.Lookups[0];
                lookup.AddMatches(outboundEndpointMatches1, address.ExplicitValues, address.AmbientValues);

                OutboundEndpointMatch.QualityKind? quality1 = null;
                for (var i = 0; i < outboundEndpointMatches1.Count; i++)
                {
                    var match = outboundEndpointMatches1[i];
                    if (quality1 == null)
                    {
                        quality1 = match.Quality;
                    }
                    else if (match.Quality.CompareTo(quality1) < 0)
                    {
                        // better quality found
                        quality1 = match.Quality;
                    }
                }

                for (var i = 1; i < state.Lookups.Count; i++)
                {
                    var outboundEndpointMatches2 = new List<OutboundEndpointMatch>();

                    lookup = state.Lookups[i];
                    lookup.AddMatches(outboundEndpointMatches2, address.ExplicitValues, address.AmbientValues);

                    OutboundEndpointMatch.QualityKind? quality2 = null;
                    for (var j = 0; j < outboundEndpointMatches2.Count; j++)
                    {
                        var match = outboundEndpointMatches2[j];
                        if (quality2 == null)
                        {
                            quality2 = match.Quality;
                        }
                        else if (match.Quality.CompareTo(quality2) < 0)
                        {
                            // better quality found
                            quality2 = match.Quality;
                        }
                    }

                    if (quality1.HasValue && (!quality2.HasValue || quality1.Value <= quality2))
                    {
                        // Ignore the second lookup, it's got worse results.
                        outboundEndpointMatches2.Clear();
                    }
                    else if (quality2.HasValue && (!quality1.HasValue || quality2.Value <= quality1))
                    {
                        // Second lookup is better.
                        outboundEndpointMatches1 = outboundEndpointMatches2;
                        quality1 = quality2;
                    }
                    else
                    {
                        // This is ambiguous. Neither of these really wants to match.
                        throw null;
                    }
                }

                outboundEndpointMatches1.Sort(new OutboundEndpointMatchComparer());

                matches = new List<RouteEndpoint>(outboundEndpointMatches1.Count);
                for (var i = 0; i < outboundEndpointMatches1.Count; i++)
                {
                    matches.Add(outboundEndpointMatches1[i].Endpoint);
                }
            }
            else
            {
                state.RouteNameMatches.TryGetValue(address.RouteName, out matches);
            }

            return (IEnumerable<Endpoint>)matches ?? Array.Empty<RouteEndpoint>();
        }

        private StateEntry Initialize(IReadOnlyList<Endpoint> endpoints)
        {
            // We need to filter out anything that can't be used to generate a URL.
            var endpointsThatCanGenerate = new List<RouteEndpoint>();
            var endpointsByRouteName = new Dictionary<string, List<RouteEndpoint>>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < endpoints.Count; i++)
            {
                var endpoint = endpoints[i];
                if (!(endpoint is RouteEndpoint routeEndpoint))
                {
                    continue;
                }

                var routeName = endpoint.Metadata.GetMetadata<IRouteNameMetadata>()?.RouteName;
                if (routeName == null && routeEndpoint.RoutePattern.RequiredValues.Count == 0)
                {
                    continue;
                }

                if (endpoint.Metadata.GetMetadata<ISuppressLinkGenerationMetadata>()?.SuppressLinkGeneration == true)
                {
                    continue;
                }

                endpointsThatCanGenerate.Add(routeEndpoint);

                if (!string.IsNullOrEmpty(routeName))
                {
                    if (!endpointsByRouteName.TryGetValue(routeName, out var list))
                    {
                        list = new List<RouteEndpoint>();
                        endpointsByRouteName.Add(routeName, list);
                    }

                    list.Add(routeEndpoint);
                }
            }

            var sets = KeySetClassifier.Partition(endpointsThatCanGenerate);
            var lookups = sets.Select(s => new KeySetLookup(s.set, s.endpoints)).ToList();

            return new StateEntry(
                endpointsThatCanGenerate,
                lookups,
                endpointsByRouteName);
        }

        public void Dispose()
        {
            _cache.Dispose();
        }

        internal class StateEntry
        {
            // For testing
            public readonly IReadOnlyList<RouteEndpoint> Endpoints;
            public readonly List<KeySetLookup> Lookups;
            public readonly Dictionary<string, List<RouteEndpoint>> RouteNameMatches;

            public StateEntry(
                IReadOnlyList<RouteEndpoint> endpoints,
                List<KeySetLookup> lookups,
                Dictionary<string, List<RouteEndpoint>> routeNameMatches)
            {
                Endpoints = endpoints;
                Lookups = lookups;
                RouteNameMatches = routeNameMatches;
            }
        }
    }
}
