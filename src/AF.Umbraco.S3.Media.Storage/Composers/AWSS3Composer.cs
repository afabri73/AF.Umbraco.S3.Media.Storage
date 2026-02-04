using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace AF.Umbraco.S3.Media.Storage.Composers
{

    /// <summary>
    /// Composes package services and middleware required for AWS S3 integration in Umbraco.
    /// </summary>
    public class AWSS3Composer : IComposer
    {
        /// <summary>
        /// Registers AWS SDK services and upload-validation middleware into the Umbraco DI container.
        /// </summary>
        /// <param name="builder">The Umbraco builder.</param>
        public void Compose(IUmbracoBuilder builder)
        {
            AWSOptions awsOptions = builder.Config.GetAWSOptions();
            string accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            string secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
            string sessionToken = Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN");

            if (!string.IsNullOrWhiteSpace(accessKey) && !string.IsNullOrWhiteSpace(secretKey))
            {
                awsOptions.Credentials = string.IsNullOrWhiteSpace(sessionToken)
                    ? new BasicAWSCredentials(accessKey, secretKey)
                    : new SessionAWSCredentials(accessKey, secretKey, sessionToken);
            }

            builder.Services.AddDefaultAWSOptions(awsOptions);
            builder.Services.AddAWSService<IAmazonS3>();
            builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            builder.Services.TryAddTransient<AWSS3UploadValidationExceptionMiddleware>();
            builder.Services.AddHostedService<AWSS3StartupConnectivityHostedService>();
            builder.Services.Configure<UmbracoPipelineOptions>(options =>
                options.AddFilter(new UmbracoPipelineFilter(
                    "AWSS3UploadValidation",
                    prePipeline: app => app.UseMiddleware<AWSS3UploadValidationExceptionMiddleware>())));
        }
    }
}
