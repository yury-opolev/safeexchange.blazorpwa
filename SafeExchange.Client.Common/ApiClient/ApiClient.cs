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
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    public partial class ApiClient
    {
        public static readonly string DefaultHttpClientName = "BackendApi";

        public static readonly string ApiVersion = "v2";

        public const string ChunkHashHeaderName = "X-SafeExchange-Chunk-Hash";

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

            // images-as-attachments spike: replace any base64 <img> in the note with
            // lightweight attachment references before the note is stored.
            input.MainData = await this.ExtractInlineImagesAsync(input.Metadata.ObjectName, input.MainData);

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

            var permissions = input.Permissions.Where(p => !string.IsNullOrWhiteSpace(p.SubjectId)).ToList();
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

                    var resolvedContentType = String.IsNullOrWhiteSpace(attachment.SourceFile.ContentType) ? "application/octet-stream" : attachment.SourceFile.ContentType;
                    var contentInput = new ContentMetadataCreationInput()
                    {
                        ContentType = resolvedContentType,
                        FileName = attachment.SourceFile.Name,
                        // Images-as-attachments: flag image uploads so the UI renders a
                        // thumbnail preview instead of a plain file row.
                        IsImage = resolvedContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                    };

                    var createdContent = await this.CreateContentMetadataAsync(secretId, contentInput);
                    if (!"ok".Equals(createdContent.Status) || createdContent.Result == null)
                    {
                        attachment.Status = UploadStatus.Error;
                        attachment.Error = $"'Attachment {attachment.SourceFile.Name}' creation failed.";
                        continue;
                    }

                    var content = new ContentMetadata(createdContent.Result);

                    var dataStream = attachment.SourceFile.OpenReadStream(Constants.MaxAttachmentDataLength);
                    var chunkLengths = GetChunkLengths(attachment.SourceFile.Size, Constants.MaxChunkDataLength);
                    var accessTicket = string.Empty;
                    using var fileHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                    for (int chunkIndex = 0; chunkIndex < chunkLengths.Count; chunkIndex++)
                    {
                        var isInterim = chunkIndex < (chunkLengths.Count - 1);
                        var size = chunkLengths[chunkIndex];
                        var buffer = new byte[size];
                        var readTotal = 0;
                        while (readTotal < size)
                        {
                            var got = await dataStream.ReadAsync(buffer.AsMemory(readTotal, size - readTotal));
                            if (got == 0)
                            {
                                break;
                            }
                            readTotal += got;
                        }

                        var chunkHashBytes = SHA256.HashData(buffer.AsSpan(0, readTotal));
                        var chunkHash = Convert.ToHexString(chunkHashBytes).ToLowerInvariant();
                        fileHasher.AppendData(buffer, 0, readTotal);

                        using var chunkMemory = new MemoryStream(buffer, 0, readTotal, writable: false);
                        var chunkResponse = await this.PutSecretDataStreamAsync(
                            secretId, content.ContentName, chunkMemory, isInterim, readTotal, accessTicket, chunkHash);

                        if (!"ok".Equals(chunkResponse.Status) || chunkResponse.Result == null)
                        {
                            attachment.Status = UploadStatus.Error;
                            attachment.Error = chunkResponse.Status == "chunk_hash_mismatch"
                                ? $"Chunk {chunkIndex + 1}/{chunkLengths.Count} hash mismatch — upload aborted."
                                : $"'Attachment {attachment.SourceFile.Name}' upload failed.";
                            break;
                        }

                        accessTicket = chunkResponse.Result.AccessTicket;
                        attachment.ProgressPercents += 100.0f * ((float)readTotal / attachment.SourceFile.Size);
                    }

                    if (attachment.Status == UploadStatus.Error)
                    {
                        continue;
                    }

                    var contentHash = Convert.ToHexString(fileHasher.GetHashAndReset()).ToLowerInvariant();
                    var commitResponse = await this.CommitContentAsync(
                        secretId, content.ContentName, contentHash, accessTicket);

                    if (!"ok".Equals(commitResponse.Status))
                    {
                        attachment.Status = UploadStatus.Error;
                        attachment.Error = commitResponse.Status == "hash_mismatch"
                            ? "Whole-content hash mismatch on commit — please retry the upload."
                            : $"Commit failed: {commitResponse.Error}";
                        continue;
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

        private static readonly Regex InlineImgTagRegex =
            new(@"<img\b[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex InlineImgDataSrcRegex =
            new("src\\s*=\\s*\"data:(?<ct>[^;\"]+);base64,(?<b64>[^\"]*)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex InlineImgHasRefRegex =
            new("data-saex-attachment\\s*=", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // images-as-attachments spike: turn base64 <img> in a note into lightweight
        // attachment references so the stored note stays small. Newly pasted images
        // are uploaded as IsImage attachments and their data: src is swapped for
        // data-saex-attachment="<contentName>". An <img> already carrying that marker
        // (a reference re-loaded into the editor) just has the heavy data: src dropped,
        // so re-saving never duplicates it. Geometry/alignment attributes are left intact.
        public async Task<string> ExtractInlineImagesAsync(string secretId, string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return html ?? string.Empty;
            }

            var matches = InlineImgTagRegex.Matches(html);
            if (matches.Count == 0)
            {
                return html;
            }

            var builder = new StringBuilder();
            var lastIndex = 0;
            var counter = 0;
            foreach (Match match in matches)
            {
                builder.Append(html, lastIndex, match.Index - lastIndex);
                lastIndex = match.Index + match.Length;

                var tag = match.Value;
                var dataSrc = InlineImgDataSrcRegex.Match(tag);
                if (!dataSrc.Success)
                {
                    // No base64 src (external URL, or a reference whose src was already
                    // stripped) — leave the tag untouched.
                    builder.Append(tag);
                    continue;
                }

                if (InlineImgHasRefRegex.IsMatch(tag))
                {
                    // Existing reference re-loaded for editing: drop the heavy data: src,
                    // keep the marker. No re-upload.
                    builder.Append(InlineImgDataSrcRegex.Replace(tag, string.Empty));
                    continue;
                }

                counter++;
                var contentType = dataSrc.Groups["ct"].Value;
                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(dataSrc.Groups["b64"].Value);
                }
                catch (FormatException)
                {
                    builder.Append(tag);
                    continue;
                }

                var fileName = $"inline-image-{DateTime.UtcNow:yyyyMMddHHmmss}-{counter}{ExtensionForContentType(contentType)}";
                var contentName = await this.UploadInlineImageAsync(secretId, bytes, contentType, fileName);
                if (string.IsNullOrEmpty(contentName))
                {
                    // Upload failed — keep the inline data so the image isn't lost.
                    builder.Append(tag);
                    continue;
                }

                builder.Append(InlineImgDataSrcRegex.Replace(tag, $"data-saex-attachment=\"{contentName}\""));
            }

            builder.Append(html, lastIndex, html.Length - lastIndex);
            return builder.ToString();
        }

        // Uploads in-memory image bytes as an IsImage attachment (create metadata ->
        // chunked upload -> commit) and returns the created ContentName, or null on
        // failure. Mirrors UploadAttachmentsAsync but for a byte[] source.
        public async Task<string> UploadInlineImageAsync(string secretId, byte[] data, string contentType, string fileName)
        {
            var resolvedContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
            var contentInput = new ContentMetadataCreationInput()
            {
                ContentType = resolvedContentType,
                FileName = fileName,
                IsImage = resolvedContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            };

            var createdContent = await this.CreateContentMetadataAsync(secretId, contentInput);
            if (!"ok".Equals(createdContent.Status) || createdContent.Result == null)
            {
                return null;
            }

            var content = new ContentMetadata(createdContent.Result);
            var chunkLengths = GetChunkLengths(data.Length, Constants.MaxChunkDataLength);
            var accessTicket = string.Empty;
            using var fileHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            var offset = 0;
            for (int chunkIndex = 0; chunkIndex < chunkLengths.Count; chunkIndex++)
            {
                var isInterim = chunkIndex < (chunkLengths.Count - 1);
                var size = chunkLengths[chunkIndex];

                var chunkHashBytes = SHA256.HashData(data.AsSpan(offset, size));
                var chunkHash = Convert.ToHexString(chunkHashBytes).ToLowerInvariant();
                fileHasher.AppendData(data, offset, size);

                using var chunkMemory = new MemoryStream(data, offset, size, writable: false);
                var chunkResponse = await this.PutSecretDataStreamAsync(
                    secretId, content.ContentName, chunkMemory, isInterim, size, accessTicket, chunkHash);
                if (!"ok".Equals(chunkResponse.Status) || chunkResponse.Result == null)
                {
                    return null;
                }

                accessTicket = chunkResponse.Result.AccessTicket;
                offset += size;
            }

            var contentHash = Convert.ToHexString(fileHasher.GetHashAndReset()).ToLowerInvariant();
            var commitResponse = await this.CommitContentAsync(secretId, content.ContentName, contentHash, accessTicket);
            if (!"ok".Equals(commitResponse.Status))
            {
                return null;
            }

            return content.ContentName;
        }

        private static string ExtensionForContentType(string contentType)
            => contentType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/jpg" => ".jpg",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/bmp" => ".bmp",
                "image/svg+xml" => ".svg",
                _ => ".img"
            };

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

        public async Task<BaseResponseObject<string>> UpdateAccessAsync(string secretId, AccessUpdateInput input)
            => await this.ProcessResponseAsync<string>(async () =>
        {
            var httpRequestMessage = new HttpRequestMessage(new HttpMethod("PATCH"), $"{ApiVersion}/access/{secretId}")
            {
                Content = JsonContent.Create(input, mediaType: null)
            };

            return await client.SendAsync(httpRequestMessage);
        });

        public async Task<BaseResponseObject<GiveUpPreviewOutput>> GetGiveUpPreviewAsync(string secretId)
            => await this.ProcessResponseAsync<GiveUpPreviewOutput>(async () =>
        {
            return await client.GetAsync($"{ApiVersion}/access-giveup/{secretId}");
        });

        public async Task<BaseResponseObject<GiveUpResultOutput>> GiveUpAccessAsync(string secretId)
            => await this.ProcessResponseAsync<GiveUpResultOutput>(async () =>
        {
            return await client.DeleteAsync($"{ApiVersion}/access-giveup/{secretId}");
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

        public async Task<BaseResponseObject<SecretAuditPageOutput>> GetSecretAuditAsync(
            string secretId,
            string? direction = null,
            DateTime? from = null,
            DateTime? to = null,
            int? pageSize = null,
            string? continuation = null,
            bool raw = false)
            => await this.ProcessResponseAsync<SecretAuditPageOutput>(async () =>
        {
            var query = new List<string>();
            if (!string.IsNullOrEmpty(direction))
            {
                query.Add($"direction={Uri.EscapeDataString(direction)}");
            }
            if (from.HasValue)
            {
                query.Add($"from={Uri.EscapeDataString(from.Value.ToUniversalTime().ToString("O"))}");
            }
            if (to.HasValue)
            {
                query.Add($"to={Uri.EscapeDataString(to.Value.ToUniversalTime().ToString("O"))}");
            }
            if (pageSize.HasValue)
            {
                query.Add($"pageSize={pageSize.Value}");
            }
            if (!string.IsNullOrEmpty(continuation))
            {
                query.Add($"continuation={Uri.EscapeDataString(continuation)}");
            }
            if (raw)
            {
                query.Add("raw=true");
            }

            var qs = query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
            return await client.GetAsync($"{ApiVersion}/secret/{secretId}/audit{qs}");
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
         
        public async Task<BaseResponseObject<ChunkCreationOutput>> PutSecretDataStreamAsync(string secretId, string contentId, Stream dataStream, bool isInterimChunk = false, int uploadSize = 0, string? accessTicket = null, string? chunkHash = null)
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

            if (!string.IsNullOrEmpty(chunkHash))
            {
                httpRequestMessage.Headers.Add(ChunkHashHeaderName, chunkHash);
            }

            return await client.SendAsync(httpRequestMessage);
        });

        public async Task<BaseResponseObject<ContentCommitOutput>> CommitContentAsync(string secretId, string contentId, string contentHash, string? accessTicket)
            => await this.ProcessResponseAsync<ContentCommitOutput>(async () =>
        {
            var httpRequestMessage = new HttpRequestMessage(new HttpMethod("PATCH"), $"{ApiVersion}/secret/{secretId}/content/{contentId}/commit")
            {
                Content = JsonContent.Create(new { hash = contentHash }, mediaType: null),
            };
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

        #region groups

        public async Task<BaseResponseObject<List<GroupOverviewOutput>>> GetRegisteredGroupsAsync()
            => await this.ProcessResponseAsync<List<GroupOverviewOutput>>(async () =>
            {
                return await client.GetAsync($"{ApiVersion}/groups-list");
            });

        #endregion groups

        #region pinned groups

        public async Task<BaseResponseObject<List<PinnedGroupOutput>>> ListPinnedGroupsAsync()
            => await this.ProcessResponseAsync<List<PinnedGroupOutput>>(async () =>
            {
                return await client.GetAsync($"{ApiVersion}/pinnedgroups-list");
            });

        public async Task<BaseResponseObject<PinnedGroupOutput>> GetPinnedGroupAsync(string pinnedGroupId)
            => await this.ProcessResponseAsync<PinnedGroupOutput>(async () =>
            {
                return await client.GetAsync($"{ApiVersion}/pinnedgroups/{pinnedGroupId}");
            });

        public async Task<BaseResponseObject<PinnedGroupOutput>> PutPinnedGroupAsync(string pinnedGroupId, PinnedGroupInput input)
            => await this.ProcessResponseAsync<PinnedGroupOutput>(async () =>
            {
                return await client.PutAsJsonAsync($"{ApiVersion}/pinnedgroups/{pinnedGroupId}", input);
            });

        public async Task<BaseResponseObject<string>> DeletePinnedGroupAsync(string pinnedGroupId)
            => await this.ProcessResponseAsync<string>(async () =>
            {
                return await client.DeleteAsync($"{ApiVersion}/pinnedgroups/{pinnedGroupId}");
            });

        #endregion pinned groups

        #region pinned secrets

        public async Task<BaseResponseObject<List<PinnedSecretOutput>>> ListPinnedSecretsAsync()
            => await this.ProcessResponseAsync<List<PinnedSecretOutput>>(async () =>
            {
                return await client.GetAsync($"{ApiVersion}/pinnedsecrets-list");
            });

        public async Task<BaseResponseObject<PinnedSecretOutput>> GetPinnedSecretAsync(string secretId)
            => await this.ProcessResponseAsync<PinnedSecretOutput>(async () =>
            {
                return await client.GetAsync($"{ApiVersion}/pinnedsecrets/{secretId}");
            });

        public async Task<BaseResponseObject<PinnedSecretOutput>> PutPinnedSecretAsync(string secretId)
            => await this.ProcessResponseAsync<PinnedSecretOutput>(async () =>
            {
                // PUT carries no body — the secret name is fully in the URL path.
                var httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, $"{ApiVersion}/pinnedsecrets/{secretId}");
                return await client.SendAsync(httpRequestMessage);
            });

        public async Task<BaseResponseObject<string>> DeletePinnedSecretAsync(string secretId)
            => await this.ProcessResponseAsync<string>(async () =>
            {
                return await client.DeleteAsync($"{ApiVersion}/pinnedsecrets/{secretId}");
            });

        #endregion pinned secrets

        #region search

        public async Task<BaseResponseObject<List<GraphUserOutput>>> SearchUsersAsync(SearchInput input)
            => await this.ProcessResponseAsync<List<GraphUserOutput>>(async () =>
            {
                return await client.PostAsJsonAsync($"{ApiVersion}/user-search", input);
            });

        public async Task<BaseResponseObject<List<GraphGroupOutput>>> SearchGroupsAsync(SearchInput input)
            => await this.ProcessResponseAsync<List<GraphGroupOutput>>(async () =>
            {
                return await client.PostAsJsonAsync($"{ApiVersion}/group-search", input);
            });

        public async Task<BaseResponseObject<List<Application>>> SearchApplicationsAsync(SearchInput input)
            => await this.ProcessResponseAsync<List<Application>>(async () =>
            {
                return await client.PostAsJsonAsync($"{ApiVersion}/application-search", input);
            });

        #endregion

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
