using NodeGuard.Data.Repositories.Interfaces;
using NodeGuard.Jobs;
using NodeGuard.Services;
using Lnrpc;
using Microsoft.Extensions.Logging;
using Channel = NodeGuard.Data.Models.Channel;
using Node = NodeGuard.Data.Models.Node;


namespace NodeGuard.Tests.Jobs;

public class NodeChannelSubscribeJobTests
{
    private Mock<ILogger<NodeChannelSuscribeJob>> _loggerMock;
    private Mock<ILightningService> _lightningServiceMock;
    private Mock<INodeRepository> _nodeRepositoryMock;
    private Mock<IChannelRepository> _channelRepositoryMock;
    private NodeChannelSuscribeJob _nodeUpdateManager;
    private Mock<ILightningClientsStorageService> _lightningClientsStorageService;

    public NodeChannelSubscribeJobTests()
    {
        _loggerMock = new Mock<ILogger<NodeChannelSuscribeJob>>();
        _nodeRepositoryMock = new Mock<INodeRepository>();
        _channelRepositoryMock = new Mock<IChannelRepository>();
        _lightningServiceMock = new Mock<ILightningService>();
        _lightningClientsStorageService = new Mock<ILightningClientsStorageService>();

        _nodeUpdateManager = new NodeChannelSuscribeJob(
            _loggerMock.Object,
            _lightningServiceMock.Object,
            _nodeRepositoryMock.Object,
            _channelRepositoryMock.Object,
            _lightningClientsStorageService.Object);
    }

    [Fact]
    public async Task NodeUpdateManagement_ThrowsException_WhenCloseAddressIsEmpty()
    {
        // Arrange
        var channelEventUpdate = new ChannelEventUpdate()
        {
            Type = ChannelEventUpdate.Types.UpdateType.OpenChannel,
            OpenChannel = new Lnrpc.Channel()
            {
                CloseAddress = "",
            },
        };

        // Act + Assert
        Assert.ThrowsAsync<Exception>(async () => await _nodeUpdateManager.NodeUpdateManagement(channelEventUpdate, new Node()));
    }

    [Fact]
    public async Task NodeUpdateManagement_UpdatesChannelStatus_WhenClosedChannelEventReceived()
    {
        // Arrange
        var channelEventUpdate = new ChannelEventUpdate()
        {
            Type = ChannelEventUpdate.Types.UpdateType.ClosedChannel,
            ClosedChannel = new ChannelCloseSummary()
            {
                ChanId = 0101010101,
            },
        };
        var channelToClose = new Channel()
        {
            ChanId = channelEventUpdate.ClosedChannel.ChanId,
            Status = Channel.ChannelStatus.Open,
        };
        _channelRepositoryMock.Setup(repo => repo.GetByChanId(channelToClose.ChanId)).ReturnsAsync(channelToClose);
        _channelRepositoryMock.Setup(repo => repo.Update(channelToClose)).Returns((true, ""));

        // Act
        await _nodeUpdateManager.NodeUpdateManagement(channelEventUpdate, new Node());

        // Assert
        Assert.Equal(Channel.ChannelStatus.Closed, channelToClose.Status);
        _channelRepositoryMock.Verify(repo => repo.Update(channelToClose), Times.Once);
    }
}