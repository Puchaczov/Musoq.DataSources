using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Musoq.DataSources.Roslyn.Components;

/// <summary>
/// An interface for an HTTP client that provides methods for making HTTP requests.
/// </summary>
public interface IHttpClient
{
    /// <summary>
    /// Creates a new instance of the HTTP client.
    /// </summary>
    /// <returns>An instance of <see cref="IHttpClient"/>.</returns>
    IHttpClient NewInstance();
    
    /// <summary>
    /// Creates a new instance of the HTTP client with the specified configuration.
    /// </summary>
    /// <param name="configure">Configuration action to customize the HTTP client.</param>
    /// <returns>An instance of <see cref="IHttpClient"/>.</returns>
    IHttpClient NewInstance(Action<HttpClient> configure);
    
    /// <summary>
    /// Sends a GET request to the specified URL and returns the response.
    /// </summary>
    /// <param name="requestUrl">The URL to send the request to.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the request.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the HTTP response message.</returns>
    Task<HttpResponseMessage?> GetAsync(string requestUrl, CancellationToken cancellationToken);
    
    /// <summary>
    /// Sends a GET request to the specified URL and returns the response.
    /// </summary>
    /// <param name="requestUrl">The URL to send the request to.</param>
    /// <param name="configure">A delegate to configure the HTTP client.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the request.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the HTTP response message.</returns>
    Task<HttpResponseMessage?> GetAsync(string requestUrl, Action<HttpClient> configure, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a POST request to the specified URL with the specified object and returns the response.
    /// </summary>
    /// <param name="requestUrl">The URL to send the request to.</param>
    /// <param name="obj">Object to send in the request body.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the request.</param>
    /// <typeparam name="T">Type of the object to send.</typeparam>
    /// <typeparam name="TOut">Type of the object to receive in the response.</typeparam>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response object.</returns>
    Task<TOut?> PostAsync<T, TOut>(string requestUrl, T obj, CancellationToken cancellationToken) 
        where T : class 
        where TOut : class;
    
    
    /// <summary>
    /// Sends a POST request to the specified URL with the specified multipart form data and returns the response.
    /// </summary>
    /// <param name="requestUrl">The URL to send the request to.</param>
    /// <param name="multipartFormDataContent">Multipart form data to send in the request body.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the request.</param>
    /// <typeparam name="TOut">Type of the object to receive in the response.</typeparam>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response object.</returns>
    Task<TOut?> PostAsync<TOut>(string requestUrl, MultipartFormDataContent multipartFormDataContent, CancellationToken cancellationToken) 
        where TOut : class;

    /// <summary>
    /// Sends a POST request with the specified HTTP request message and returns the response.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the request.</param>
    /// <typeparam name="TOut">Type of the object to receive in the response.</typeparam>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response object.</returns>
    Task<TOut?> PostAsync<TOut>(HttpRequestMessage request, CancellationToken cancellationToken);
}