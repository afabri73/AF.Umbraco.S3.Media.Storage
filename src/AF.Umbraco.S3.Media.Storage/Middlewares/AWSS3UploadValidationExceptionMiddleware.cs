using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Resources;
using System.Threading.Tasks;

namespace AF.Umbraco.S3.Media.Storage.Middlewares
{

    /// <summary>
    /// Intercepts media upload requests and returns localized, user-friendly error payloads.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public class AWSS3UploadValidationExceptionMiddleware(ILogger<AWSS3UploadValidationExceptionMiddleware> logger) : IMiddleware
    {
        /// <summary>
        /// Gets the log prefix used by this component.
        /// </summary>
        private const string LogPrefix = "[AFUS3MS]";
        /// <summary>
        /// Gets the logger used by this component.
        /// </summary>
        private readonly ILogger<AWSS3UploadValidationExceptionMiddleware> _logger = logger;
        /// <summary>
        /// Gets the resource manager used by this component.
        /// </summary>
        private static readonly ResourceManager ResourceManager =
            new("AF.Umbraco.S3.Media.Storage.Core.AWSS3FileSystem", typeof(AWSS3UploadValidationExceptionMiddleware).Assembly);

        /// <summary>
        /// Invokes the middleware pipeline.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="next">The next request delegate.</param>
        /// <returns>A task that completes when request processing is finished.</returns>
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
            CultureInfo requestCulture = ResolveRequestCulture(context);
            try
            {
                CultureInfo.CurrentCulture = requestCulture;
                CultureInfo.CurrentUICulture = requestCulture;

                if (await TryRejectInvalidImageSelectionAsync(context).ConfigureAwait(false))
                {
                    return;
                }

                await next(context).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex) when (IsInvalidImageUploadError(context, ex))
            {
                if (context.Response.HasStarted)
                {
                    throw;
                }

                LogInvalidUploadDetails(context, ex);
                await WriteValidationProblemAsync(context, ex.Message).ConfigureAwait(false);
            }
            catch (AWSS3UserAlertException ex)
            {
                if (context.Response.HasStarted)
                {
                    throw;
                }

                LogStorageAlertDetails(context, ex);
                await WriteStorageProblemAsync(context, ex.Message).ConfigureAwait(false);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
                CultureInfo.CurrentUICulture = originalUiCulture;
            }
        }

        /// <summary>
        /// Attempts to reject Invalid Image Selection asynchronously.
        /// </summary>
        private async Task<bool> TryRejectInvalidImageSelectionAsync(HttpContext context)
        {
            if (!IsMediaMultipartRequest(context.Request))
            {
                return false;
            }

            IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            IFormFile invalidFile = null;

            foreach (IFormFile file in form.Files.Where(IsImageCandidate))
            {
                await using Stream stream = file.OpenReadStream();
                try
                {
                    if (Image.DetectFormat(stream) == null)
                    {
                        invalidFile = file;
                        break;
                    }
                }
                catch (Exception ex) when (ex is UnknownImageFormatException || ex is InvalidImageContentException)
                {
                    invalidFile = file;
                    break;
                }
            }

            if (invalidFile is null)
            {
                return false;
            }

            string message = GetLocalizedMessage("InvalidImageFileMessage", invalidFile.FileName);
            await WriteValidationProblemAsync(context, message).ConfigureAwait(false);
            return true;
        }

        /// <summary>
        /// Determines whether media multipart request.
        /// </summary>
        private static bool IsMediaMultipartRequest(HttpRequest request)
        {
            if (!HttpMethods.IsPost(request.Method) && !HttpMethods.IsPut(request.Method) && !HttpMethods.IsPatch(request.Method))
            {
                return false;
            }

            if (!request.Path.StartsWithSegments("/umbraco/management/api/v1/media", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return request.HasFormContentType;
        }

        /// <summary>
        /// Determines whether image candidate.
        /// </summary>
        private static bool IsImageCandidate(IFormFile file)
        {
            if (!string.IsNullOrWhiteSpace(file.ContentType) &&
                file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string extension = Path.GetExtension(file.FileName);
            return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".qoi", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Handles the write validation problem async operation.
        /// </summary>
        private static async Task WriteValidationProblemAsync(HttpContext context, string message)
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";

            var problemDetails = new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["file"] = [message]
            })
            {
                Title = GetLocalizedMessage("FileValidationErrorTitle"),
                Detail = message,
                Status = StatusCodes.Status400BadRequest
            };

            await context.Response.WriteAsJsonAsync(problemDetails).ConfigureAwait(false);
        }

        /// <summary>
        /// Handles the write storage problem async operation.
        /// </summary>
        private static async Task WriteStorageProblemAsync(HttpContext context, string message)
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/problem+json";

            var problemDetails = new ProblemDetails
            {
                Title = GetLocalizedMessage("StorageOperationFailedTitle"),
                Detail = message,
                Status = StatusCodes.Status503ServiceUnavailable
            };

            await context.Response.WriteAsJsonAsync(problemDetails).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets localized Message.
        /// </summary>
        private static string GetLocalizedMessage(string key, params object[] args)
        {
            var localizedText = ResourceManager.GetString(key, CultureInfo.CurrentUICulture)
                ?? ResourceManager.GetString(key, CultureInfo.GetCultureInfo("en-US"))
                ?? "The selected file '{0}' cannot be saved because the image data is not valid. You may still see a preview before save because preview is generated locally by your browser.";

            return string.Format(CultureInfo.CurrentUICulture, localizedText, args);
        }

        /// <summary>
        /// Determines whether invalid image upload error.
        /// </summary>
        private static bool IsInvalidImageUploadError(HttpContext context, InvalidOperationException exception)
        {
            if (!context.Request.Path.StartsWithSegments("/umbraco/management/api", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (exception.InnerException is UnknownImageFormatException || exception.InnerException is InvalidImageContentException)
            {
                return true;
            }

            return exception.StackTrace?.Contains("AWSS3FileSystem.ValidateImageIfNeeded", StringComparison.Ordinal) == true;
        }

        /// <summary>
        /// Resolves request Culture.
        /// </summary>
        private static CultureInfo ResolveRequestCulture(HttpContext context)
        {
            var requestCultureFeature = context.Features.Get<IRequestCultureFeature>();
            if (requestCultureFeature?.RequestCulture?.UICulture is CultureInfo featureCulture)
            {
                return NormalizeSupportedCulture(featureCulture);
            }

            string[] headerCandidates = ["X-UI-Culture", "X-Ui-Culture", "X-UICulture", "X-Umbraco-Culture", "X-UMB-Culture"];
            foreach (string headerName in headerCandidates)
            {
                string headerValue = context.Request.Headers[headerName].ToString();
                if (!string.IsNullOrWhiteSpace(headerValue) && TryGetCulture(headerValue, out CultureInfo headerCulture))
                {
                    return NormalizeSupportedCulture(headerCulture);
                }
            }

            if (TryGetCultureFromClaims(context.User, out CultureInfo claimCulture))
            {
                return NormalizeSupportedCulture(claimCulture);
            }

            string acceptLanguage = context.Request.Headers.AcceptLanguage.ToString();
            if (!string.IsNullOrWhiteSpace(acceptLanguage))
            {
                foreach (string token in acceptLanguage.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    string candidate = token.Split(';', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                    if (TryGetCulture(candidate, out CultureInfo parsedCulture))
                    {
                        return NormalizeSupportedCulture(parsedCulture);
                    }
                }
            }

            return CultureInfo.GetCultureInfo("en-US");
        }

        /// <summary>
        /// Handles the normalize supported culture operation.
        /// </summary>
        private static CultureInfo NormalizeSupportedCulture(CultureInfo culture)
        {
            if (culture.Name.Equals("it", StringComparison.OrdinalIgnoreCase))
            {
                return CultureInfo.GetCultureInfo("it-IT");
            }

            return culture;
        }

        /// <summary>
        /// Attempts to get Culture.
        /// </summary>
        private static bool TryGetCulture(string value, out CultureInfo culture)
        {
            try
            {
                culture = CultureInfo.GetCultureInfo(value);
                return true;
            }
            catch (CultureNotFoundException)
            {
                culture = CultureInfo.GetCultureInfo("en-US");
                return false;
            }
        }

        /// <summary>
        /// Attempts to get Culture From Claims.
        /// </summary>
        private static bool TryGetCultureFromClaims(ClaimsPrincipal user, out CultureInfo culture)
        {
            culture = CultureInfo.GetCultureInfo("en-US");
            if (user?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            foreach (Claim claim in user.Claims)
            {
                string type = claim.Type ?? string.Empty;
                if (!type.Contains("lang", StringComparison.OrdinalIgnoreCase) &&
                    !type.Contains("culture", StringComparison.OrdinalIgnoreCase) &&
                    !type.Contains("locale", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryGetCulture(claim.Value, out CultureInfo parsedCulture))
                {
                    culture = parsedCulture;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Logs invalid Upload Details.
        /// </summary>
        private void LogInvalidUploadDetails(HttpContext context, InvalidOperationException exception)
        {
            HttpRequest request = context.Request;
            string user = context.User?.Identity?.Name ?? "anonymous";
            string userAgent = request.Headers.UserAgent.ToString();
            string innerType = exception.InnerException?.GetType().FullName ?? "none";
            string innerMessage = exception.InnerException?.Message ?? "none";

            _logger.LogWarning(exception,
                "{LogPrefix} Invalid media upload rejected. TraceId={TraceId}; Method={Method}; Path={Path}; Query={Query}; ContentType={ContentType}; User={User}; UserAgent={UserAgent}; ExceptionType={ExceptionType}; InnerExceptionType={InnerExceptionType}; InnerExceptionMessage={InnerExceptionMessage}",
                LogPrefix,
                context.TraceIdentifier,
                request.Method,
                request.Path.Value,
                request.QueryString.HasValue ? request.QueryString.Value : string.Empty,
                request.ContentType ?? "none",
                user,
                string.IsNullOrWhiteSpace(userAgent) ? "none" : userAgent,
                exception.GetType().FullName,
                innerType,
                innerMessage);
        }

        /// <summary>
        /// Logs storage Alert Details.
        /// </summary>
        private void LogStorageAlertDetails(HttpContext context, AWSS3UserAlertException exception)
        {
            HttpRequest request = context.Request;
            string user = context.User?.Identity?.Name ?? "anonymous";
            string userAgent = request.Headers.UserAgent.ToString();
            Exception rootException = exception.GetBaseException();
            string rootType = rootException.GetType().FullName ?? "none";
            string rootMessage = rootException.Message;

            _logger.LogError(exception.InnerException ?? exception,
                "{LogPrefix} Media storage operation failed. TraceId={TraceId}; Method={Method}; Path={Path}; Query={Query}; ContentType={ContentType}; User={User}; UserAgent={UserAgent}; RootExceptionType={RootExceptionType}; RootExceptionMessage={RootExceptionMessage}; UserAlertMessage={UserAlertMessage}",
                LogPrefix,
                context.TraceIdentifier,
                request.Method,
                request.Path.Value,
                request.QueryString.HasValue ? request.QueryString.Value : string.Empty,
                request.ContentType ?? "none",
                user,
                string.IsNullOrWhiteSpace(userAgent) ? "none" : userAgent,
                rootType,
                rootMessage,
                exception.Message);
        }
    }
}
