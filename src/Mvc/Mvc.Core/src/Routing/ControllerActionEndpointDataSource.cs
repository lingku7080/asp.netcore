// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Microsoft.AspNetCore.Mvc.Routing
{
    internal class ControllerActionEndpointDataSource : ActionEndpointDataSourceBase
    {
        private readonly ActionEndpointFactory _endpointFactory;
        private readonly List<ConventionalRouteEntry> _routes;

        public ControllerActionEndpointDataSource(IActionDescriptorCollectionProvider actions, ActionEndpointFactory endpointFactory)
            : base(actions)
        {
            _endpointFactory = endpointFactory;
            
            _routes = new List<ConventionalRouteEntry>();

            // IMPORTANT: this needs to be the last thing we do in the constructor. 
            // Change notifications can happen immediately!
            Subscribe();
        }

        public void AddRoute(in ConventionalRouteEntry route)
        {
            lock (Lock)
            {
                _routes.Add(route);
            }
        }

        protected override List<Endpoint> CreateEndpoints(IReadOnlyList<ActionDescriptor> actions, IReadOnlyList<Action<EndpointBuilder>> conventions)
        {
            var endpoints = new List<Endpoint>();

            // For each controller action - add the relevant endpoints.
            //
            // 1. If the action is attribute routed, we use that information verbatim
            // 2. If the action is conventional routed
            //      a. Create a *matching only* endpoint for each action X route (if possible)
            //      b. Ignore link generation for now
            for (var i = 0; i < actions.Count; i++)
            {
                if (actions[i] is ControllerActionDescriptor action)
                {
                    _endpointFactory.AddEndpoints(endpoints, action, _routes, conventions);
                }
            }

            // Now create a *link generation only* endpoint for each route. This gives us a very
            // compatible experience to previous versions.
            for (var i = 0; i < _routes.Count; i++)
            {
                var route = _routes[i];
                endpoints.Add(new RouteEndpoint(
                    context => Task.CompletedTask,
                    route.Pattern,
                    1000 + (i * 1000), 
                    new EndpointMetadataCollection(new SuppressMatchingMetadata()),
                    "Route: " + route.Pattern.RawText));
            }

            return endpoints;
        }
    }
}

