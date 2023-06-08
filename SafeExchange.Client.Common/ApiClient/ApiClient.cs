/// <summary>
/// ApiClient
/// </summary>

namespace SafeExchange.Client.Common
{
    using SafeExchange.Client.Common.Model;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class ApiClient
    {
        public static readonly string DefaultHttpClientName = "BackendApi";

        public static readonly string ApiVersion = "v2";

        private readonly HttpClient client;

        private readonly JsonSerializerOptions jsonOptions;

        public ApiClient(IHttpClientFactory clientFactory)
        {
            if (clientFactory == null)
            {
                throw new ArgumentNullException(nameof(clientFactory));
            }

            this.client = clientFactory.CreateClient(DefaultHttpClientName);

            this.jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        #region compound operations

        public async Task<BaseResponseObject<CompoundModel>> GetCompoundModelAsync(string secretId)
        {
            var accessListData = await this.ListAccessAsync(secretId);
            if (!"ok".Equals(accessListData.Status))
            {
                return new BaseResponseObject<CompoundModel>()
                {
                    Status = accessListData.Status,
                    SubStatus = accessListData.SubStatus,
                    Error = accessListData.Error
                };
            }

            var secretMetadata = await this.GetSecretMetadataAsync(secretId);
            if (!"ok".Equals(secretMetadata.Status))
            {
                return new BaseResponseObject<CompoundModel>()
                {
                    Status = secretMetadata.Status,
                    SubStatus = secretMetadata.SubStatus,
                    Error = secretMetadata.Error
                };
            }

            if (secretMetadata.Result is null)
            {
                return new BaseResponseObject<CompoundModel>()
                {
                    Status = "error",
                    Error = "Received empty metadata reponse."
                };
            }

            StringBuilder? mainDataBuilder = null;
            foreach (var secretContent in secretMetadata.Result.Content)
            {
                if (!secretContent.IsMain)
                {
                    continue; // do not download attachments here
                }

                var mainDataLength = secretContent.Chunks.Sum(x => (int)x.Length);
                mainDataBuilder = new StringBuilder(mainDataLength);
                foreach (var chunk in secretContent.Chunks)
                {
                    var dataStream = await this.GetSecretDataStreamAsync(secretId, secretContent.ContentName, chunk.ChunkName);
                    if (!"ok".Equals(secretMetadata.Status))
                    {
                        return new BaseResponseObject<CompoundModel>()
                        {
                            Status = dataStream.Status,
                            SubStatus = dataStream.SubStatus,
                            Error = dataStream.Error
                        };
                    }

                    if (dataStream.Result is null)
                    {
                        return new BaseResponseObject<CompoundModel>()
                        {
                            Status = "error",
                            Error = $"Received empty data reponse ('{secretContent.ContentName}'-'{chunk.ChunkName}', {(secretContent.IsMain ? "main content" : "attachment")})."
                        };
                    }

                    string data = string.Empty;
                    using (var reader = new StreamReader(dataStream.Result))
                    {
                        mainDataBuilder.Append(await reader.ReadToEndAsync());
                    }
                }
            }

            var result = new CompoundModel()
            {
                Metadata = new ObjectMetadata(secretMetadata.Result),
                Permissions = new List<SubjectPermissions>(
                    accessListData.Result?.Select(p => new SubjectPermissions(p))
                    ?? Array.Empty<SubjectPermissions>()),
                MainData = mainDataBuilder?.ToString() ?? "Could not get data."
            };

            return new BaseResponseObject<CompoundModel>()
            {
                Status = "ok",
                Result = result
            };
        }

        public Uri GetContentDataStreamUri(string secretId, string contentId)
        {
            return new Uri(client.BaseAddress, $"{ApiVersion}/secret/{secretId}/content/{contentId}/all");
        }

        public async Task<BaseResponseObject<Stream>> GetContentDataStreamAsync(string secretId, string contentId)
            => await ProcessStreamResponseAsync(async () =>
        {
            return await client.GetAsync($"{ApiVersion}/secret/{secretId}/content/{contentId}/all", HttpCompletionOption.ResponseHeadersRead);
        });

        public async Task<BaseResponseObject<string>> CreateFromCompoundModelAsync(CompoundModel input, List<AttachmentModel> attachments)
        {
            var secretMetadata = await this.CreateSecretMetadataAsync(input.Metadata.ObjectName, input.Metadata.ToCreationDto());
            if (!"ok".Equals(secretMetadata.Status))
            {
                return new BaseResponseObject<string>()
                {
                    Status = secretMetadata.Status,
                    Error = secretMetadata.Error
                };
            }

            if (secretMetadata.Result == null)
            {
                return new BaseResponseObject<string>()
                {
                    Status = "error",
                    Error = "Could not get metadata from response."
                };
            }

            var content = new ContentMetadata(secretMetadata.Result.Content.First(c => c.IsMain));
            using (var dataStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(dataStream, Encoding.UTF8, 4096, leaveOpen: true))
                {
                    await writer.WriteAsync(input.MainData);
                    await writer.FlushAsync();
                    dataStream.Position = 0;
                }

                var secretMainData = await this.PutSecretDataStreamAsync(input.Metadata.ObjectName, content.ContentName, dataStream);
                if (!"ok".Equals(secretMainData.Status))
                {
                    return new BaseResponseObject<string>()
                    {
                        Status = secretMainData.Status,
                        Error = secretMainData.Error
                    };
                }
            }

            await this.UploadAttachmentsAsync(input.Metadata.ObjectName, attachments);

            var permissions = input.Permissions.Where(p => !string.IsNullOrWhiteSpace(p.SubjectName)).ToList();
            if (permissions.Count > 0)
            {
                var accessReply = await this.GrantAccessAsync(
                    input.Metadata.ObjectName, permissions.Select(p => p.ToDto()).ToList());
                if (!"ok".Equals(accessReply.Status))
                {
                    // TODO ...
                }
            }

            return new BaseResponseObject<string>()
            {
                Status = "ok",
                Result = "ok"
            };
        }

        public async Task UploadAttachmentsAsync(string secretId, List<AttachmentModel> attachments)
        {
            foreach (var attachment in attachments)
            {
                try
                {
                    attachment.Status = UploadStatus.InProgress;
                    attachment.ProgressPercents = 0.0f;

                    var contentInput = new ContentMetadataCreationInput()
                    {
                        ContentType = String.IsNullOrWhiteSpace(attachment.SourceFile.ContentType) ? "application/octet-stream" : attachment.SourceFile.ContentType,
                        FileName = attachment.SourceFile.Name
                    };

                    var createdContent = await this.CreateContentMetadataAsync(secretId, contentInput);
                    if (!"ok".Equals(createdContent.Status) || createdContent.Result == null)
                    {
                        attachment.Status = UploadStatus.Error;
                        attachment.Error = $"'Attachment {attachment.SourceFile.Name}' creation failed.";
                        continue; // no-op, skip to next attachment
                    }

                    var content = new ContentMetadata(createdContent.Result);

                    var dataStream = attachment.SourceFile.OpenReadStream(Constants.MaxAttachmentDataLength);
                    var chunkLengths = GetChunkLengths(attachment.SourceFile.Size, Constants.MaxChunkDataLength);
                    var accessTicket = string.Empty;
                    for (int chunkIndex = 0; chunkIndex < chunkLengths.Count; chunkIndex++)
                    {
                        var isInterim = chunkIndex < (chunkLengths.Count - 1);
                        var secretData = await this.PutSecretDataStreamAsync(
                            secretId, content.ContentName, dataStream, isInterim, chunkLengths[chunkIndex], accessTicket);
                        if (!"ok".Equals(secretData.Status) || secretData.Result == null)
                        {
                            attachment.Status = UploadStatus.Error;
                            attachment.Error = $"'Attachment {attachment.SourceFile.Name}' upload failed.";
                            break;
                        }

                        accessTicket = secretData.Result.AccessTicket;
                        attachment.ProgressPercents += 100.0f * ((float)chunkLengths[chunkIndex] / attachment.SourceFile.Size);
                    }

                    if (attachment.Status == UploadStatus.Error)
                    {
                        continue; // no-op, skip to next attachment
                    }

                    attachment.Status = UploadStatus.Success;
                    attachment.ProgressPercents = 100.0f;
                }
                catch (Exception exception)
                {
                    attachment.Status = UploadStatus.Error;
                    attachment.Error = $"{exception.GetType()}: {exception.Message}";
                }
            }
        }

        #endregion compound operations

        #region access requests

        public async Task<BaseResponseObject<List<AccessRequestOutput>>> GetAccessRequestsAsync()
            => await this.ProcessResponseAsync<List<AccessRequestOutput>>(async () =>
        {
            return await client.GetAsync($"{ApiVersion}/accessrequest-list");
        });

        public async Task<BaseResponseObject<string>> CreateAccessRequestAsync(string secretId, SubjectPermissionsInput input)
            => await this.ProcessResponseAsync<string>(async () =>
        {
            return await client.PostAsJsonAsync($"{ApiVersion}/accessrequest/{secretId}", input);
        });

        public async Task<BaseResponseObject<string>> ProcessAccessRequestAsync(string secretId, AccessRequestUpdateInput input)
            => await this.ProcessResponseAsync<string>(async () =>
        {
            var httpRequestMessage = new HttpRequestMessage(new HttpMethod("PATCH"), $"{ApiVersion}/accessrequest/{secretId}")
            {
                Content = JsonContent.Create(input, mediaType: null)
            };
            return await client.SendAsync(httpRequestMessage);
        });

        public async Task<BaseResponseObject<string>> CancelAccessRequestAsync(string secretId, AccessRequestDeletionInput input)
            => await this.ProcessResponseAsync<string>(async () =>
        {
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{ApiVersion}/accessrequest/{secretId}")
            {
                Content = JsonContent.Create(input, mediaType: null)
            };
            return await client.SendAsync(httpRequestMessage);
        });

        #endregion access requests

        #region permissions

        public async Task<BaseResponseObject<string>> GrantAccessAsync(string secretId, List<SubjectPermissionsInput> input)
            => await this.ProcessResponseAsync<string>(async () =>
        {
            return await client.PostAsJsonAsync($"{ApiVersion}/access/{secretId}", input);
        });

        public async Task<BaseResponseObject<List<SubjectPermissionsOutput>>> ListAccessAsync(string secretId)
            => await this.ProcessResponseAsync<List<SubjectPermissionsOutput>>(async () =>
        {
            return await client.GetAsync($"{ApiVersion}/access/{secretId}");
        });

        public async Task<BaseResponseObject<string>> RevokeAccessAsync(string secretId, List<SubjectPermissionsInput> input)
            => await this.ProcessResponseAsync<string>(async () =>
        {
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{ApiVersion}/access/{secretId}")
            {
                Content = JsonContent.Create(input, mediaType: null)
            };

            return await client.SendAsync(httpRequestMessage);
        });

        #endregion permissions

        #region secret metadata

        public async Task<BaseResponseObject<List<SubjectPermissionsOutput>>> ListSecretMetadataAsync()
            => await this.ProcessResponseAsync<List<SubjectPermissionsOutput>>(async () =>
        {
            return await client.GetAsync($"{ApiVersion}/secret-list");
        });

        public async Task<BaseResponseObject<ObjectMetadataOutput>> CreateSecretMetadataAsync(string secretId, MetadataCreationInput input)
            => await this.ProcessResponseAsync<ObjectMetadataOutput>(async () =>
        {
            return await client.PostAsJsonAsync($"{ApiVersion}/secret/{secretId}", input);
        });

        public async Task<BaseResponseObject<ObjectMetadataOutput>> GetSecretMetadataAsync(string secretId)
            => await this.ProcessResponseAsync<ObjectMetadataOutput>(async () =>
        {
            return await client.GetAsync($"{ApiVersion}/secret/{secretId}");
        });

        public async Task<BaseResponseObject<ObjectMetadataOutput>> UpdateSecretMetadataAsync(string secretId, MetadataUpdateInput data)
            => await this.ProcessResponseAsync<ObjectMetadataOutput>(async () =>
        {
            var httpRequestMessage = new HttpRequestMessage(new HttpMethod("PATCH"), $"{ApiVersion}/secret/{secretId}")
            {
                Content = JsonContent.Create(data, mediaType: null)
            };
            return await client.SendAsync(httpRequestMessage);
        });

        public async Task<BaseResponseObject<string>> DeleteSecretDataAsync(string secretId)
            => await this.ProcessResponseAsync<string>(async () =>
        {
            return await client.DeleteAsync($"{ApiVersion}/secret/{secretId}");
        });

        #endregion secret metadata

        #region secret content metadata

        public async Task<BaseResponseObject<ContentMetadataOutput>> CreateContentMetadataAsync(string secretId, ContentMetadataCreationInput input)
            => await this.ProcessResponseAsync<ContentMetadataOutput>(async () =>
        {
            return await client.PostAsJsonAsync($"{ApiVersion}/secret/{secretId}/content", input);
        });

        public async Task<BaseResponseObject<ContentMetadataOutput>> UpdateContentMetadataAsync(string secretId, string contentId, ContentMetadataUpdateInput input)
            => await this.ProcessResponseAsync<ContentMetadataOutput>(async () =>
        {
            var httpRequestMessage = new HttpRequestMessage(new HttpMethod("PATCH"), $"{ApiVersion}/secret/{secretId}/content/{contentId}")
            {
                Content = JsonContent.Create(input, mediaType: null)
            };
            return await client.SendAsync(httpRequestMessage);
        });

        public async Task<BaseResponseObject<string>> DeleteContentMetadataAsync(string secretId, string contentId)
            => await this.ProcessResponseAsync<string>(async () =>
        {
            return await client.DeleteAsync($"{ApiVersion}/secret/{secretId}/content/{contentId}");
        });

        public async Task<BaseResponseObject<ContentMetadataOutput>> DropContentDataAsync(string secretId, string contentId)
            => await this.ProcessResponseAsync<ContentMetadataOutput>(async () =>
        {
            var httpRequestMessage = new HttpRequestMessage(new HttpMethod("PATCH"), $"{ApiVersion}/secret/{secretId}/content/{contentId}/drop");
            return await client.SendAsync(httpRequestMessage);
        });

        #endregion secret content metadata

        #region secret data stream
         
        public async Task<BaseResponseObject<ChunkCreationOutput>> PutSecretDataStreamAsync(string secretId, string contentId, Stream dataStream, bool isInterimChunk = false, int uploadSize = 0, string? accessTicket = null)
            => await this.ProcessResponseAsync<ChunkCreationOutput>(async () =>
        {
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, $"{ApiVersion}/secret/{secretId}/content/{contentId}/chunk");
            if (isInterimChunk)
            {
                Console.WriteLine($"Uploading interim content '{contentId}', size: {uploadSize}.");

                httpRequestMessage.Content = new PartialStreamContent(dataStream, uploadSize);
                httpRequestMessage.Content.Headers.ContentLength = uploadSize;
                httpRequestMessage.Headers.Add("X-SafeExchange-OpType", "interim");
            }
            else
            {
                Console.WriteLine($"Uploading content '{contentId}'{(uploadSize > 0 ? $", size: {uploadSize}" : string.Empty)}.");

                httpRequestMessage.Content = new StreamContent(dataStream);
            }

            if (!string.IsNullOrEmpty(accessTicket))
            {
                httpRequestMessage.Headers.Add("X-SafeExchange-Ticket", accessTicket);
            }

            return await client.SendAsync(httpRequestMessage);
        });

        public async Task<BaseResponseObject<Stream>> GetSecretDataStreamAsync(string secretId, string contentId, string chunkId)
            => await ProcessStreamResponseAsync(async () =>
        {
            return await client.GetAsync($"{ApiVersion}/secret/{secretId}/content/{contentId}/chunk/{chunkId}", HttpCompletionOption.ResponseHeadersRead);
        });

        #endregion secret data stream

        #region notification subscription

        public async Task<BaseResponseObject<string>> RegisterWebPushSubscriptionAsync(NotificationSubscriptionCreationInput input)
            => await this.ProcessResponseAsync<string>(async () =>
        {
            return await client.PostAsJsonAsync($"{ApiVersion}/notificationsub/web", input);
        });

        public async Task<BaseResponseObject<string>> UnregisterWebPushSubscriptionAsync(NotificationSubscriptionDeletionInput input)
            => await this.ProcessResponseAsync<string>(async () =>
        {
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{ApiVersion}/notificationsub/web")
            {
                Content = JsonContent.Create(input, mediaType: null)
            };

            return await client.SendAsync(httpRequestMessage);
        });

        #endregion notification subscription

        #region applications

        public async Task<BaseResponseObject<List<ApplicationOverviewOutput>>> GetRegisteredApplicationsAsync()
            => await this.ProcessResponseAsync<List<ApplicationOverviewOutput>>(async () =>
        {
            return await client.GetAsync($"{ApiVersion}/applications-list");
        });

        #endregion applications

        private async Task<BaseResponseObject<T>> ProcessResponseAsync<T>(Func<Task<HttpResponseMessage>> asyncHttpCall) where T : class
        {
            HttpResponseMessage? response = null;
            string content = String.Empty;
            try
            {
                response = await asyncHttpCall();
                content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<BaseResponseObject<T>>(content, this.jsonOptions);
                if (!string.IsNullOrEmpty(result?.Status))
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                if (response != null && response.StatusCode != HttpStatusCode.OK)
                {
                    return new BaseResponseObject<T>()
                    {
                        Status = response.StatusCode.ToString(),
                        Error = string.IsNullOrEmpty(content) ? $"{ex.GetType()}: {ex.Message}" : content
                    };
                }

                return new BaseResponseObject<T>()
                {
                    Status = "exception",
                    Error = $"{ex.GetType()}: {ex.Message}"
                };
            }

            return new BaseResponseObject<T>()
            {
                Status = response.StatusCode.ToString(),
                Error = content ?? string.Empty
            };
        }

        private static async Task<BaseResponseObject<Stream>> ProcessStreamResponseAsync(Func<Task<HttpResponseMessage>> asyncHttpCall)
        {
            HttpResponseMessage response;
            try
            {
                response = await asyncHttpCall();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var content = await response.Content.ReadAsStreamAsync();
                    return new BaseResponseObject<Stream>()
                    {
                        Status = "ok",
                        Result = content
                    };
                }
            }
            catch (Exception ex)
            {
                return new BaseResponseObject<Stream>()
                {
                    Status = "exception",
                    Error = $"{ex.GetType()}: {ex.Message}"
                };
            }

            return new BaseResponseObject<Stream>()
            {
                Status = response.StatusCode.ToString(),
                Error = "Could not get data."
            };
        }

        private static List<int> GetChunkLengths(long totalSize, int chunkMaxSize)
        {
            var result = new List<int>();
            var sizeLeft = totalSize;
            while (sizeLeft > 0)
            {
                result.Add((int)Math.Min(sizeLeft, chunkMaxSize));
                sizeLeft -= chunkMaxSize;
            }

            return result;
        }
    }
}
