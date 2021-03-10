﻿using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Kiota.Abstractions;

namespace KiotaCore
{
    public class HttpCore : IHttpCore
    {
        private const string authorizationHeaderKey = "Authorization";
        private readonly HttpClient client;
        private readonly IAuthenticationProvider authProvider;
        public HttpCore(IAuthenticationProvider authenticationProvider, HttpClient httpClient = null)
        {
            authProvider = authenticationProvider ?? throw new ArgumentNullException(nameof(authenticationProvider));
            client = httpClient ?? new HttpClient();
        }
        public async Task<ModelType> SendAsync<ModelType>(RequestInfo requestInfo, IResponseHandler responseHandler = null)
        {
            if(requestInfo == null)
                throw new ArgumentNullException(nameof(requestInfo));

            if(!requestInfo.Headers.ContainsKey(authorizationHeaderKey)) {
                var token = await authProvider.getAuthorizationToken(requestInfo.URI);
                if(string.IsNullOrEmpty(token))
                    throw new InvalidOperationException("Could not get an authorization token");
                requestInfo.Headers.Add(authorizationHeaderKey, $"Bearer {token}");
            }
            
            using var message = GetRequestMessageFromRequestInfo(requestInfo);
            var response = await this.client.SendAsync(message);
            if(responseHandler == null) {
                response?.Dispose();
                return default; //TODO call default response handler which will handle deserialization.
            }
            else
                return await responseHandler.HandleResponseAsync<HttpResponseMessage, ModelType>(response);
        }
        private HttpRequestMessage GetRequestMessageFromRequestInfo(RequestInfo requestInfo) {
            var message = new HttpRequestMessage {
                Method = new System.Net.Http.HttpMethod(requestInfo.HttpMethod.ToString().ToUpperInvariant()),
                RequestUri = new Uri(requestInfo.URI + 
                                        ((requestInfo.QueryParameters?.Any() ?? false) ? 
                                            "?" + requestInfo.QueryParameters
                                                        .Select(x => $"{x.Key}={x.Value}")
                                                        .Aggregate((x, y) => $"{x}&{y}") :
                                            string.Empty)),
                
            };
            if(requestInfo.Headers?.Any() ?? false)
                requestInfo.Headers.ToList().ForEach(x => message.Headers.Add(x.Key, x.Value));
            if(requestInfo.Content != null)
                message.Content = new StreamContent(requestInfo.Content); //TODO we're making a big assumption here and we probably need to default the content type in case it's not provided
            return message;
        }
    }
}
