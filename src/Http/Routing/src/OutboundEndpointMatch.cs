// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Routing
{
    internal readonly struct OutboundEndpointMatch
    {
        public readonly RouteEndpoint Endpoint;
        public readonly KeySet Set;
        public readonly QualityKind Quality;

        public OutboundEndpointMatch(RouteEndpoint endpoint, KeySet set, QualityKind quality)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            Endpoint = endpoint;
            Set = set;
            Quality = quality;
        }

        public enum QualityKind
        {
            Explicit,
            Conventional,
            Ambient,
        }
    }

    internal class OutboundEndpointMatchComparer : IComparer<OutboundEndpointMatch>
    {
        public int Compare(OutboundEndpointMatch x, OutboundEndpointMatch y)
        {
            // For this comparison lower is better.
            if (x.Endpoint.Order != y.Endpoint.Order)
            {
                return x.Endpoint.Order.CompareTo(y.Endpoint.Order);
            }

            if (x.Endpoint.RoutePattern.OutboundPrecedence != y.Endpoint.RoutePattern.OutboundPrecedence)
            {
                // Reversed because higher is better
                return y.Endpoint.RoutePattern.OutboundPrecedence.CompareTo(x.Endpoint.RoutePattern.OutboundPrecedence);
            }

            if (x.Quality != y.Quality)
            {
                // A fallback match is worse than a non-fallback
                return x.Quality.CompareTo(y.Quality);
            }

            return string.Compare(
                x.Endpoint.RoutePattern.RawText,
                y.Endpoint.RoutePattern.RawText,
                StringComparison.Ordinal);
        }
    }
}
