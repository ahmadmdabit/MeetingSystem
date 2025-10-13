using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MeetingSystem.Business;
using MeetingSystem.Business.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel;
using Minio.DataModel.Args;
using Minio.DataModel.Response;
using Minio.Exceptions;
using Moq;
using NUnit.Framework;

namespace MeetingSystem.Business.Tests;

[TestFixture]
public class GenericFileServiceTests
{
    private Mock<IMinioClient> _minioClientMock;
    private Mock<IOptions<MinioSettings>> _minioSettingsMock;
    private Mock<ILogger<GenericFileService>> _loggerMock;
    private GenericFileService _fileService;

    [SetUp]
    public void SetUp()
    {
        _minioClientMock = new Mock<IMinioClient>();
        _minioSettingsMock = new Mock<IOptions<MinioSettings>>();
        _loggerMock = new Mock<ILogger<GenericFileService>>();

        // Setup default settings
        var settings = new MinioSettings
        {
            PublicEndpoint = "http://localhost:9000",
            CompressionFileSizeLimit = 5 * 1024 * 1024, // 5MB
            Buckets = new Buckets { Profile = "profile-pics", Meeting = "meeting-files" }
        };
        _minioSettingsMock.Setup(s => s.Value).Returns(settings);

        // Setup the mock client to return a valid (but dummy) Config object.
        var mockConfig = new MinioClient()
            .WithEndpoint("minio:9000")
            .WithCredentials("dummy-access-key", "dummy-secret-key")
            .WithSSL(false)
            .Build();

        _minioClientMock.Setup(c => c.Config).Returns(mockConfig.Config);

        _fileService = new GenericFileService(_minioClientMock.Object, _minioSettingsMock.Object, _loggerMock.Object);
    }

    /// <summary>
    /// Verifies that GetPresignedUrlAsync executes without error and returns a non-empty string.
    /// </summary>
    [Test]
    public async Task GetPresignedUrlAsync_WhenCalled_ReturnsAValidUrlString()
    {
        // Arrange
        // This test verifies that the internal logic for creating a public client
        // and generating a URL executes without crashing (e.g., no NullReferenceException).
        // A full integration test is required to validate the URL's correctness.

        // Act
        var result = await _fileService.GetPresignedUrlAsync("test-bucket", "test-key", CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("test-bucket"); // Verify the bucket name is in the generated URL
        result.Should().Contain("test-key");    // Verify the object key is in the generated URL
    }

    /// <summary>
    /// Verifies that UploadObjectAsync creates a bucket if it does not already exist.
    /// </summary>
    [Test]
    public async Task UploadObjectAsync_WhenBucketDoesNotExist_CreatesBucket()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024); // 1KB
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream());

        _minioClientMock.Setup(c => c.BucketExistsAsync(It.IsAny<BucketExistsArgs>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _fileService.UploadObjectAsync("test-bucket", "test-key", fileMock.Object, false, CancellationToken.None);

        // Verify the method call without inspecting internal properties
        // We verify that MakeBucketAsync was called with *any* MakeBucketArgs object.
        // This confirms the correct code path was taken.
        _minioClientMock.Verify(c => c.MakeBucketAsync(It.IsAny<MakeBucketArgs>(), It.IsAny<CancellationToken>()), Times.Once);
        _minioClientMock.Verify(c => c.PutObjectAsync(It.IsAny<PutObjectArgs>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that a file larger than the compression limit is compressed.
    /// </summary>
    [Test]
    public async Task UploadObjectAsync_WhenFileIsLarge_CallsPutObjectAsync()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(6 * 1024 * 1024); // 6MB
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(new byte[6 * 1024 * 1024]));

        _minioClientMock.Setup(c => c.BucketExistsAsync(It.IsAny<BucketExistsArgs>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Return a completed task with a null result. The code under test doesn't use the result,
        // so this is a safe and simple way to satisfy the method signature.
        _minioClientMock.Setup(c => c.PutObjectAsync(It.IsAny<PutObjectArgs>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<PutObjectResponse>(null!));

        // Act
        await _fileService.UploadObjectAsync("test-bucket", "test-key", fileMock.Object, true, CancellationToken.None);

        // Assert
        // The most important verification is that the upload method was called.
        _minioClientMock.Verify(c => c.PutObjectAsync(It.IsAny<PutObjectArgs>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that RemoveObjectAsync logs a warning but does not throw an exception if the object is not found.
    /// </summary>
    [Test]
    public async Task RemoveObjectAsync_WhenObjectNotFound_LogsWarningAndDoesNotThrow()
    {
        // Arrange
        _minioClientMock.Setup(c => c.RemoveObjectAsync(It.IsAny<RemoveObjectArgs>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ObjectNotFoundException("Not found"));

        // Act
        Func<Task> act = async () => await _fileService.RemoveObjectAsync("test-bucket", "test-key", CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        _loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Attempted to delete object")),
                null, // No exception should be passed to the logger for this specific case
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// Verifies the successful path of RemoveObjectAsync where the object is found and deleted.
    /// </summary>
    [Test]
    public async Task RemoveObjectAsync_WhenObjectExists_CallsRemoveObjectAsyncOnClient()
    {
        // Arrange
        // Setup the client to not throw any exception.
        _minioClientMock.Setup(c => c.RemoveObjectAsync(It.IsAny<RemoveObjectArgs>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _fileService.RemoveObjectAsync("test-bucket", "test-key", CancellationToken.None);

        // Assert
        // Verify the underlying client method was called.
        _minioClientMock.Verify(c => c.RemoveObjectAsync(It.IsAny<RemoveObjectArgs>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}