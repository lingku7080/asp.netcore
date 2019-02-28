// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AspNetCore.Routing
{
    internal static class KeySetClassifier
    {
        public static IReadOnlyList<(KeySet set, IReadOnlyList<RouteEndpoint> endpoints)> Partition(IReadOnlyList<RouteEndpoint> endpoints)
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            var sets = new Dictionary<KeySet, List<RouteEndpoint>>();

            var keys = new List<string>();
            var keySet = new KeySet(keys);

            for (var i = 0; i < endpoints.Count; i++)
            {
                keys.Clear();

                var endpoint = endpoints[i];
                keys.AddRange(endpoint.RoutePattern.RequiredValues.Keys);

                if (!sets.TryGetValue(keySet, out var list))
                {
                    list = new List<RouteEndpoint>();
                    sets.Add(new KeySet(keys.ToArray()), list);
                }

                list.Add(endpoint);
            }

            return sets.Select(kvp => (kvp.Key, (IReadOnlyList<RouteEndpoint>)kvp.Value)).ToArray();
        }
    }
}
