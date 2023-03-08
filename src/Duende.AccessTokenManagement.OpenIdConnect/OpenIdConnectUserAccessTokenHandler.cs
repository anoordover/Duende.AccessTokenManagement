﻿// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Duende.AccessTokenManagement.OpenIdConnect;

/// <summary>
/// Delegating handler that injects the current access token into an outgoing request
/// </summary>
public class OpenIdConnectUserAccessTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserTokenRequestParameters _parameters;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="httpContextAccessor"></param>
    /// <param name="parameters"></param>
    public OpenIdConnectUserAccessTokenHandler(IHttpContextAccessor httpContextAccessor, UserTokenRequestParameters? parameters = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _parameters = parameters ?? new UserTokenRequestParameters();
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await SetTokenAsync(request, forceRenewal: false).ConfigureAwait(false);
        var response = await base.SendAsync(request, cancellationToken);

        // retry if 401
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            response.Dispose();

            await SetTokenAsync(request, forceRenewal: true).ConfigureAwait(false);
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>
    /// Set an access token on the HTTP request
    /// </summary>
    /// <param name="request"></param>
    /// <param name="forceRenewal"></param>
    /// <returns></returns>
    protected virtual async Task SetTokenAsync(HttpRequestMessage request, bool forceRenewal)
    {
        var parameters = new UserTokenRequestParameters
        {
            SignInScheme = _parameters.SignInScheme,
            ChallengeScheme = _parameters.ChallengeScheme,
            Resource = _parameters.Resource,
            ForceRenewal = forceRenewal,
            Context =  _parameters.Context
        };
              
        var token = await _httpContextAccessor.HttpContext!.GetUserAccessTokenAsync(parameters).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(token.AccessToken))
        {
            // checking for null AccessTokenType and falling back to "Bearer" since this might be coming
            // from an old cache/store prior to adding the AccessTokenType property.
            request.Headers.Authorization = new AuthenticationHeaderValue(token.AccessTokenType ?? "Bearer", token.AccessToken);
        }
    }
}