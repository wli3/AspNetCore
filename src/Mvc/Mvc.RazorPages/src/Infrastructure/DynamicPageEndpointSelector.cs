// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure
{
    internal class DynamicPageEndpointSelector : IDisposable
    {
        private readonly PageActionEndpointDataSource _dataSource;
        private readonly DataSourceDependentCache<ActionSelectionTable<RouteEndpoint>> _cache;

        public DynamicPageEndpointSelector(PageActionEndpointDataSource dataSource)
        {
            if (dataSource == null)
            {
                throw new ArgumentNullException(nameof(dataSource));
            }

            _dataSource = dataSource;
            _cache = new DataSourceDependentCache<ActionSelectionTable<RouteEndpoint>>(dataSource, Initialize);
        }

        private ActionSelectionTable<RouteEndpoint> Table => _cache.EnsureInitialized();

        // This is async because the page version will be async. We don't want to put ourselves in a
        // position where these types become public and then users start using them in sync locations.
        public Task<IReadOnlyList<RouteEndpoint>> SelectEndpointsAsync(RouteValueDictionary values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var table = Table;
            var matches = table.Select(values);
            return Task.FromResult(matches);
        }

        private static ActionSelectionTable<RouteEndpoint> Initialize(IReadOnlyList<Endpoint> endpoints)
        {
            return ActionSelectionTable<RouteEndpoint>.Create(endpoints);
        }

        public void Dispose()
        {
            _cache.Dispose();
        }
    }
}
