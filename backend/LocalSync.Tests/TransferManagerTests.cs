using System;
using System.Threading.Tasks;
using LocalSync.Core.Interfaces;
using LocalSync.Core.Models;
using LocalSync.Core.Services;
using Moq;
using Xunit;

namespace LocalSync.Tests;

public class TransferManagerTests
{
    private readonly Mock<ITransferNotificationService> _mockNotification;
    private readonly TransferManager _sut;

    public TransferManagerTests()
    {
        _mockNotification = new Mock<ITransferNotificationService>();
        _sut = new TransferManager(_mockNotification.Object);
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldCreateSessionAndNotify()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        var fileName = "test.txt";
        long totalSize = 1000;

        // Act
        var session = await _sut.CreateSessionAsync(targetId, fileName, totalSize);

        // Assert
        Assert.NotNull(session);
        Assert.Equal(targetId, session.TargetDeviceId);
        Assert.Equal(fileName, session.FileName);
        Assert.Equal(TransferState.Pending, session.State);
        _mockNotification.Verify(n => n.NotifySessionCreatedAsync(session), Times.Once);
    }

    [Fact]
    public async Task UpdateProgressAsync_ShouldCompleteSession_WhenSizeReached()
    {
        // Arrange
        var session = await _sut.CreateSessionAsync(Guid.NewGuid(), "test.txt", 100);

        // Act
        await _sut.UpdateProgressAsync(session.Id, 50);
        await _sut.UpdateProgressAsync(session.Id, 50);

        // Assert
        var updatedSession = _sut.GetSession(session.Id);
        Assert.NotNull(updatedSession);
        Assert.Equal(100, updatedSession.TransferredSize);
        Assert.Equal(TransferState.Completed, updatedSession.State);
        _mockNotification.Verify(n => n.NotifyStateChangedAsync(session.Id, TransferState.Completed), Times.Once);
    }
}
