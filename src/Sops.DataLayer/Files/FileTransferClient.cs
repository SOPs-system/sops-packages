using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sops.DataLayer.Abstractions;
using Sops.DataLayer.Endpoints;
using Sops.DataLayer.Models.Files;
using Sops.DataLayer.Models.Responses;

namespace Sops.DataLayer.Files
{
    /// <summary>
    /// Implements safe file uploads and atomic file downloads with progress reporting.
    /// </summary>
    public sealed class FileTransferClient : IFileTransferClient
    {
        private readonly HttpClient _httpClient;
        private readonly IEndpointResolver _endpointResolver;
        private readonly IEndpointRegistry _endpointRegistry;
        private readonly ILogger<FileTransferClient>? _logger;

        public FileTransferClient(
            HttpClient httpClient,
            IEndpointResolver endpointResolver,
            IEndpointRegistry endpointRegistry,
            ILogger<FileTransferClient>? logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
            _endpointRegistry = endpointRegistry ?? throw new ArgumentNullException(nameof(endpointRegistry));
            _logger = logger;
        }

        public async Task<DataResult<string>> UploadAsync(UploadRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            string endpointId = !string.IsNullOrWhiteSpace(request.EndpointId) ? request.EndpointId : "files.upload";
            if (!_endpointRegistry.TryGet(endpointId, out var endpoint))
            {
                return DataResult<string>.NotFound($"Endpoint {endpointId} is not registered");
            }

            var uri = _endpointResolver.ResolveUri(endpoint!);

            try
            {
                using (var content = new MultipartFormDataContent())
                {
                    var fileContent = new StreamContent(request.ContentStream);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(request.ContentType ?? "application/octet-stream");
                    content.Add(fileContent, "file", request.FileName);

                    using (var resp = await _httpClient.PostAsync(uri, content, cancellationToken).ConfigureAwait(false))
                    {
                        if (!resp.IsSuccessStatusCode)
                        {
                            return DataResult<string>.Failure(DataOutcome.Failure, DataError.Create("UPLOAD_FAILED", $"Upload failed with HTTP {resp.StatusCode}"));
                        }

                        string resultId = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return DataResult<string>.Success(resultId.Trim('\"', ' '));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return DataResult<string>.Cancelled();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to upload file {FileName}", request.FileName);
                return DataResult<string>.Failure(DataOutcome.Failure, DataError.Create("UPLOAD_EXCEPTION", ex.Message));
            }
        }

        public async Task<DataResult<FileInfo>> DownloadAsync(DownloadRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            string endpointId = !string.IsNullOrWhiteSpace(request.EndpointId) ? request.EndpointId : "files.download";
            if (!_endpointRegistry.TryGet(endpointId, out var endpoint))
            {
                return DataResult<FileInfo>.NotFound($"Endpoint {endpointId} is not registered");
            }

            var uri = _endpointResolver.ResolveUri(endpoint!);
            var sw = Stopwatch.StartNew();

            try
            {
                using (var resp = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        return DataResult<FileInfo>.Failure(DataOutcome.Failure, DataError.Create("DOWNLOAD_FAILED", $"Download failed with HTTP {resp.StatusCode}"));
                    }

                    long? totalBytes = resp.Content.Headers.ContentLength;

                    using (var responseStream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var memoryOrTemp = new MemoryStream())
                    {
                        byte[] buffer = new byte[81920];
                        long bytesReadTotal = 0;
                        int read;

                        while ((read = await responseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            await memoryOrTemp.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                            bytesReadTotal += read;

                            request.Progress?.Report(new TransferProgress(bytesReadTotal, totalBytes, sw.Elapsed));
                        }

                        memoryOrTemp.Position = 0;
                        await AtomicFileWriter.WriteAtomicAsync(request.DestinationFilePath, memoryOrTemp, cancellationToken).ConfigureAwait(false);
                    }

                    var fileInfo = new FileInfo(request.DestinationFilePath);
                    return DataResult<FileInfo>.Success(fileInfo);
                }
            }
            catch (OperationCanceledException)
            {
                return DataResult<FileInfo>.Cancelled();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to download file to {Destination}", request.DestinationFilePath);
                return DataResult<FileInfo>.Failure(DataOutcome.Failure, DataError.Create("DOWNLOAD_EXCEPTION", ex.Message));
            }
        }
    }
}
