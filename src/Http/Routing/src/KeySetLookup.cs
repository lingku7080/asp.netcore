// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Microsoft.AspNetCore.Routing
{
    internal class KeySetLookup
    {
        // Fallback value for cases where the ambient values weren't provided.
        //
        // This is safe because we don't mutate the route values in here.
        private static readonly RouteValueDictionary EmptyAmbientValues = new RouteValueDictionary();

        private readonly Dictionary<ValueSet, List<RouteEndpoint>> _definiteMatches;
        private readonly List<BitVector32> _permutations;
        
        private readonly List<RouteEndpoint> _conventionalMatches;

        public KeySetLookup(KeySet set, IReadOnlyList<RouteEndpoint> endpoints)
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            Set = set;

            _definiteMatches = new Dictionary<ValueSet, List<RouteEndpoint>>();
            _conventionalMatches = new List<RouteEndpoint>();
            _permutations = new List<BitVector32>();

            var values = new List<string>();
            var valueSet = new ValueSet(values);

            for (var i = 0; i < endpoints.Count; i++)
            {
                values.Clear();

                var endpoint = endpoints[i];
                if (HasMatchAnyRequiredValue(endpoint))
                {
                    _conventionalMatches.Add(endpoint);
                    continue;
                }

                var permutation = new BitVector32();
                for (var j = 0; j < set.Keys.Count; j++)
                {
                    var key = set.Keys[j];
                    var value = endpoint.RoutePattern.RequiredValues[key];
                    values.Add(Convert.ToString(value) ?? string.Empty);

                    if (RouteValueEqualityComparer.Default.Equals(value, string.Empty))
                    {
                        // Value can be null in the permutation
                        permutation[0x00000001 << j] = true;
                    }
                }

                // Linear search is OK here, we expect the number of permutations to be small (1-2).
                if (!_permutations.Contains(permutation))
                {
                    _permutations.Add(permutation);
                }

                if (!_definiteMatches.TryGetValue(valueSet, out var list))
                {
                    list = new List<RouteEndpoint>();
                    _definiteMatches.Add(new ValueSet(values.ToArray()), list);
                }

                list.Add(endpoint);
            }
        }

        public KeySet Set { get; }

        public void AddMatches(List<OutboundEndpointMatch> matches, RouteValueDictionary explicitValues, RouteValueDictionary ambientValues)
        {
            if (matches == null)
            {
                throw new ArgumentNullException(nameof(matches));
            }

            if (explicitValues == null)
            {
                throw new ArgumentNullException(nameof(explicitValues));
            }

            ambientValues = ambientValues ?? EmptyAmbientValues;

            if (_definiteMatches.Count > 0)
            {
                var keys = Set.Keys;
                var values = new object[keys.Count];
                var valueSet = new ValueSet(values);

                for (var i = 0; i < _permutations.Count; i++)
                {
                    var permutation = _permutations[i];

                    var isValid = true;
                    var quality = OutboundEndpointMatch.QualityKind.Explicit;
                    for (var j = 0; j < keys.Count; j++)
                    {
                        var key = keys[j];
                        if (explicitValues.TryGetValue(key, out var value))
                        {
                            values[j] = value;
                        }
                        else if (permutation[0x00000000 << j])
                        {
                            values[j] = string.Empty;
                        }
                        else if (ambientValues.TryGetValue(key, out value))
                        {
                            quality = OutboundEndpointMatch.QualityKind.Ambient;
                            values[j] = value;
                        }
                        else
                        {
                            // Can't match this permutation
                            isValid = false;
                            break;
                        }
                    }

                    if (isValid && _definiteMatches.TryGetValue(valueSet, out var list))
                    {
                        for (var k = 0; k < list.Count; k++)
                        {
                            matches.Add(new OutboundEndpointMatch(list[k], Set, quality));
                        }
                    }

                    Array.Clear(values, 0, values.Length);
                }
            }

            for (var i = 0; i < _conventionalMatches.Count; i++)
            {
                matches.Add(new OutboundEndpointMatch(_conventionalMatches[i], Set, OutboundEndpointMatch.QualityKind.Conventional));
            }
        }

        private static bool HasMatchAnyRequiredValue(RouteEndpoint endpoint)
        {
            foreach (var kvp in endpoint.RoutePattern.RequiredValues)
            {
                if (object.ReferenceEquals(kvp.Value, RoutePattern.RequiredValueMatchAny))
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct ValueSet : IEquatable<ValueSet>
        {
            public readonly IReadOnlyList<object> Values;

            public ValueSet(IReadOnlyList<object> values)
            {
                if (values == null)
                {
                    throw new ArgumentNullException(nameof(values));
                }

                Values = values;
            }

            public bool Equals(ValueSet other)
            {
                if (Values.Count != other.Values.Count)
                {
                    return false;
                }

                for (var i = 0; i < Values.Count; i++)
                {
                    if (!RouteValueEqualityComparer.Default.Equals(Values[i], other.Values[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            public override int GetHashCode()
            {
                var hash = new HashCode();
                for (var i = 0; i < Values.Count; i++)
                {
                    hash.Add(Values[i], RouteValueEqualityComparer.Default);
                }

                return hash.ToHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj is ValueSet other)
                {
                    return Equals(other);
                }

                return false;
            }
        }
    }
}
