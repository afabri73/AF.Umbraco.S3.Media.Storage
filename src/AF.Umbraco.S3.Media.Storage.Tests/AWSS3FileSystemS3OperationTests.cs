using AF.Umbraco.S3.Media.Storage.Core;
using AF.Umbraco.S3.Media.Storage.Exceptions;
using Amazon.S3;
using Amazon.S3.Model;
using System.Net;
using Xunit;

namespace AF.Umbraco.S3.Media.Storage.Tests;

/// <summary>
/// Verifies synchronous filesystem operations observe and translate asynchronous AWS failures.
/// </summary>
public sealed class AWSS3FileSystemS3OperationTests
{
    /// <summary>
    /// Verifies missing metadata is exposed through the filesystem's documented exception type.
    /// </summary>
    [Fact]
    public void GetLastModified_MissingObject_ThrowsFileNotFoundException()
    {
        AmazonS3Exception s3Exception = CreateS3Exception(HttpStatusCode.NotFound);
        using var s3Client = CreateClientWithMetadataFailure(s3Exception);
        AWSS3FileSystem fileSystem = S3TestInfrastructure.CreateFileSystem(s3Client);

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(
            () => fileSystem.GetLastModified("/media/missing.jpg"));

        Assert.Same(s3Exception, exception.InnerException);
        Assert.Equal(1, s3Client.MetadataRequestCount);
    }

    /// <summary>
    /// Verifies missing size metadata is not wrapped in an aggregate exception.
    /// </summary>
    [Fact]
    public void GetSize_MissingObject_ThrowsFileNotFoundException()
    {
        AmazonS3Exception s3Exception = CreateS3Exception(HttpStatusCode.NotFound);
        using var s3Client = CreateClientWithMetadataFailure(s3Exception);
        AWSS3FileSystem fileSystem = S3TestInfrastructure.CreateFileSystem(s3Client);

        Exception exception = Assert.Throws<FileNotFoundException>(
            () => fileSystem.GetSize("/media/missing.jpg"));

        Assert.IsNotType<AggregateException>(exception);
        Assert.Same(s3Exception, exception.InnerException);
    }

    /// <summary>
    /// Verifies object-read failures use the same NotFound translation as metadata reads.
    /// </summary>
    [Fact]
    public void OpenFile_MissingObject_ThrowsFileNotFoundException()
    {
        AmazonS3Exception s3Exception = CreateS3Exception(HttpStatusCode.NotFound);
        using var s3Client = new TestAmazonS3Client
        {
            ObjectHandler = (_, _) => Task.FromException<GetObjectResponse>(s3Exception)
        };
        AWSS3FileSystem fileSystem = S3TestInfrastructure.CreateFileSystem(s3Client);

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(
            () => fileSystem.OpenFile("/media/missing.jpg"));

        Assert.Same(s3Exception, exception.InnerException);
        Assert.Equal(1, s3Client.ObjectRequestCount);
    }

    /// <summary>
    /// Verifies paged list failures are translated within the protected execution boundary.
    /// </summary>
    [Fact]
    public void DirectoryExists_MissingPrefix_ThrowsFileNotFoundException()
    {
        AmazonS3Exception s3Exception = CreateS3Exception(HttpStatusCode.NotFound);
        using var s3Client = new TestAmazonS3Client
        {
            ListHandler = (_, _) => Task.FromException<ListObjectsResponse>(s3Exception)
        };
        AWSS3FileSystem fileSystem = S3TestInfrastructure.CreateFileSystem(s3Client);

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(
            () => fileSystem.DirectoryExists("/media/missing"));

        Assert.Same(s3Exception, exception.InnerException);
        Assert.Equal(1, s3Client.ListRequestCount);
    }

    /// <summary>
    /// Verifies the existence check converts a missing object to false.
    /// </summary>
    [Fact]
    public void FileExists_MissingObject_ReturnsFalse()
    {
        using var s3Client = CreateClientWithMetadataFailure(CreateS3Exception(HttpStatusCode.NotFound));
        AWSS3FileSystem fileSystem = S3TestInfrastructure.CreateFileSystem(s3Client);

        bool exists = fileSystem.FileExists("/media/missing.jpg");

        Assert.False(exists);
    }

    /// <summary>
    /// Verifies authorization failures are not hidden as missing objects.
    /// </summary>
    [Fact]
    public void GetSize_Forbidden_PropagatesAmazonS3Exception()
    {
        AmazonS3Exception s3Exception = CreateS3Exception(HttpStatusCode.Forbidden);
        using var s3Client = CreateClientWithMetadataFailure(s3Exception);
        AWSS3FileSystem fileSystem = S3TestInfrastructure.CreateFileSystem(s3Client);

        AmazonS3Exception exception = Assert.Throws<AmazonS3Exception>(
            () => fileSystem.GetSize("/media/restricted.jpg"));

        Assert.Same(s3Exception, exception);
    }

    /// <summary>
    /// Verifies delete failures are observed before cache cleanup can continue.
    /// </summary>
    [Fact]
    public void DeleteFile_FaultedDeleteTask_ThrowsUserAlertException()
    {
        AmazonS3Exception s3Exception = CreateS3Exception(HttpStatusCode.InternalServerError);
        using var s3Client = new TestAmazonS3Client
        {
            DeleteHandler = (_, _) => Task.FromException<DeleteObjectResponse>(s3Exception)
        };
        AWSS3FileSystem fileSystem = S3TestInfrastructure.CreateFileSystem(s3Client);

        AWSS3UserAlertException exception = Assert.Throws<AWSS3UserAlertException>(
            () => fileSystem.DeleteFile("/media/example.jpg"));

        Assert.Same(s3Exception, exception.InnerException);
        Assert.Equal(1, s3Client.DeleteRequestCount);
        Assert.Equal(0, s3Client.ListRequestCount);
    }

    private static TestAmazonS3Client CreateClientWithMetadataFailure(AmazonS3Exception exception) =>
        new()
        {
            MetadataHandler = (_, _) => Task.FromException<GetObjectMetadataResponse>(exception)
        };

    private static AmazonS3Exception CreateS3Exception(HttpStatusCode statusCode) =>
        new($"S3 returned {statusCode}")
        {
            StatusCode = statusCode
        };
}
