/*
 * NodeGuard
 * Copyright (C) 2023  Elenpay
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see http://www.gnu.org/licenses/.
 */

using System.Collections.Specialized;
using AutoFixture;
using AutoMapper;
using FluentAssertions;
using FundsManager.Automapper;
using FundsManager.Data;
using FundsManager.Data.Models;
using FundsManager.Data.Repositories;
using FundsManager.Data.Repositories.Interfaces;
using FundsManager.Services;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBXplorer.DerivationStrategy;
using NBXplorer.Models;
using Nodeguard;
using Quartz;
using Quartz.Impl;
using Key = FundsManager.Data.Models.Key;
using LiquidityRule = FundsManager.Data.Models.LiquidityRule;
using Wallet = FundsManager.Data.Models.Wallet;

namespace FundsManager.Rpc
{
    public class NodeGuardServiceTests
    {
        private readonly Fixture _fixture;
        private readonly Mock<ILogger<NodeGuardService>> _logger;
        private readonly IMapper _mockMapper;

        public NodeGuardServiceTests()
        {
            _logger = new Mock<ILogger<NodeGuardService>>();
            _fixture = new Fixture();
            _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            _mockMapper = new MapperConfiguration(config => { config.AddProfile<MapperProfile>(); }).CreateMapper();
        }


        [Fact]
        public async Task GetLiquidityRules_NoPubkey()
        {
            //Arrange
            var liquidityRuleRepository = new Mock<ILiquidityRuleRepository>();
            liquidityRuleRepository.Setup(x =>
                x.GetByNodePubKey(It.IsAny<string>())).ReturnsAsync(new List<LiquidityRule>());

            var wallet = _fixture.Create<Wallet>();
            var walletRepository = new Mock<IWalletRepository>();

            walletRepository.Setup(x => x.GetById(It.IsAny<int>())).ReturnsAsync(wallet);


            var mockNodeGuardService =
                new NodeGuardService(_logger.Object, liquidityRuleRepository.Object, walletRepository.Object,
                    _mockMapper, new Mock<IWalletWithdrawalRequestRepository>().Object,
                    new Mock<IBitcoinService>().Object, new Mock<INBXplorerService>().Object,
                    new Mock<ISchedulerFactory>().Object);
            var getLiquidityRulesRequest = new GetLiquidityRulesRequest
            {
                NodePubkey = string.Empty
            };
            var context = TestServerCallContext.Create();
            //Act

            await Assert.ThrowsAsync<RpcException>(() =>
                mockNodeGuardService.GetLiquidityRules(getLiquidityRulesRequest, context));
        }

        [Fact]
        public async Task GetLiquidityRules_Success()
        {
            //Arrange
            var liquidityRuleRepository = new Mock<ILiquidityRuleRepository>();
            var liquidityRules = new List<LiquidityRule>
            {
                _fixture.Create<LiquidityRule>()
            };

            liquidityRuleRepository.Setup(x =>
                x.GetByNodePubKey(It.IsAny<string>())).ReturnsAsync(liquidityRules);

            var wallet = _fixture.Create<Wallet>();
            var walletRepository = new Mock<IWalletRepository>();

            walletRepository.Setup(x => x.GetById(It.IsAny<int>())).ReturnsAsync(wallet);


            var mockNodeGuardService =
                new NodeGuardService(_logger.Object, liquidityRuleRepository.Object, walletRepository.Object,
                    _mockMapper, new Mock<IWalletWithdrawalRequestRepository>().Object,
                    new Mock<IBitcoinService>().Object, new Mock<INBXplorerService>().Object,
                    new Mock<ISchedulerFactory>().Object);
            var getLiquidityRulesRequest = _fixture.Create<GetLiquidityRulesRequest>();
            var context = TestServerCallContext.Create();
            //Act

            var result = await mockNodeGuardService.GetLiquidityRules(getLiquidityRulesRequest, context);

            //Assert
            result.Should().NotBeNull();
            result.LiquidityRules.Should().NotBeNull();
            result.LiquidityRules.Should().NotBeEmpty();

            result.LiquidityRules.First().MinimumLocalBalance.Should()
                .Be((float) (liquidityRules.First().MinimumLocalBalance));
        }

        [Fact]
        public async Task GetLiquidityRules_Exception()
        {
            //Arrange
            var liquidityRuleRepository = new Mock<ILiquidityRuleRepository>();

            liquidityRuleRepository.Setup(x =>
                x.GetByNodePubKey(It.IsAny<string>())).ThrowsAsync(new Exception("test"));

            var wallet = _fixture.Create<Wallet>();
            var walletRepository = new Mock<IWalletRepository>();

            walletRepository.Setup(x => x.GetById(It.IsAny<int>())).ReturnsAsync(wallet);


            var mockNodeGuardService =
                new NodeGuardService(_logger.Object, liquidityRuleRepository.Object, walletRepository.Object,
                    _mockMapper, new Mock<IWalletWithdrawalRequestRepository>().Object,
                    new Mock<IBitcoinService>().Object, new Mock<INBXplorerService>().Object,
                    new Mock<ISchedulerFactory>().Object);
            var getLiquidityRulesRequest = _fixture.Create<GetLiquidityRulesRequest>();
            var context = TestServerCallContext.Create();
            //Act

            await Assert.ThrowsAnyAsync<RpcException>(() =>
                mockNodeGuardService.GetLiquidityRules(getLiquidityRulesRequest, context));
        }

        [Fact]
        public async Task GetNewWalletAddress_Success()
        {
            //Arrange
            var wallet = _fixture.Create<Wallet>();
            wallet.Keys = new List<Key>
            {
                new Key
                {
                    XPUB =
                        "tpubDCfM7v7fKZ31gTGGggNMycfCr5cDGinyijveRZ44RYSgAgEARwhaBd6PPpWst8kKbhEVoqNasgjHFWZKrEQoJ9pzPVEmNZDNe92hShzEMDy"
                }
            };

            var walletRepository = new Mock<IWalletRepository>();

            walletRepository.Setup(x => x.GetById(It.IsAny<int>())).ReturnsAsync(wallet);
            var mock = new Mock<INBXplorerService>();

            var keypath = new KeyPathInformation()
            {
                Address = BitcoinAddress.Create("bcrt1q590shaxaf5u08ml8jwlzghz99dup3z9592vxal", Network.RegTest),
            };

            mock.Setup(x => x.GetUnusedAsync(It.IsAny<DerivationStrategyBase>(),
                    It.IsAny<DerivationFeature>(), It.IsAny<int>(), It.IsAny<bool>(), default))
                .ReturnsAsync(keypath);

            var mockNodeGuardService =
                new NodeGuardService(_logger.Object, null, walletRepository.Object,
                    _mockMapper, new Mock<IWalletWithdrawalRequestRepository>().Object,
                    new Mock<IBitcoinService>().Object,
                    mock.Object, new Mock<ISchedulerFactory>().Object);

            var newWalletAddressRequest = new GetNewWalletAddressRequest
            {
                WalletId = wallet.Id
            };

            //Act

            var resp = await mockNodeGuardService.GetNewWalletAddress(newWalletAddressRequest,
                TestServerCallContext.Create());

            //Assert
            resp.Should().NotBeNull();
            resp.Address.Should().NotBeNullOrWhiteSpace();
            resp.Address.Should().Be(keypath.Address.ToString());
        }

        [Fact]
        public async Task RequestWithdrawal_Success()
        {
            //Arrange
            var wallet = InitMockRequestWithdrawal(out var walletRepository,
                out var nbxplorerService,
                out _,
                out _,
                out _,
                out _,
                out _,
                out var bitcoinService,
                out var walletWithdrawalRequestRepository, out var mockScheduler);

            var mockNodeGuardService =
                new NodeGuardService(_logger.Object, null, walletRepository.Object,
                    _mockMapper, walletWithdrawalRequestRepository.Object,
                    bitcoinService.Object,
                    nbxplorerService.Object, mockScheduler);

            var requestWithdrawalRequest = new RequestWithdrawalRequest
            {
                WalletId = wallet.Id,
                Amount = 100,
                Description = $"Request Withdrawal Test {DateTime.Now}",
                Address = "bcrt1q590shaxaf5u08ml8jwlzghz99dup3z9592vxal",
            };

            //Act
            var resp = await mockNodeGuardService.RequestWithdrawal(requestWithdrawalRequest,
                TestServerCallContext.Create());

            //Assert
            resp.Should().NotBeNull();
            resp.Txid.Should().NotBeNullOrWhiteSpace();
            resp.Txid.Should().Be("f103e12f02ac1e5b8826831d4fc8fdb78a707bd00c4e1f191fe5d14458d63d5a");
        }

        [Fact]
        public async Task RequestWithdrawal_NoWallet()
        {
            //Arrange
            var wallet = InitMockRequestWithdrawal(out var walletRepository,
                out var nbxplorerService,
                out _,
                out _,
                out _,
                out _,
                out _,
                out var bitcoinService,
                out var walletWithdrawalRequestRepository,
                out _);

            walletRepository.Setup(x => x.GetById(It.IsAny<int>())).ReturnsAsync((Wallet) null);

            var requestWithdrawalRequest = new RequestWithdrawalRequest
            {
                WalletId = wallet.Id,
                Amount = 100,
                Description = $"Request Withdrawal Test {DateTime.Now}",
                Address = "bcrt1q590shaxaf5u08ml8jwlzghz99dup3z9592vxal",
            };

            var mockNodeGuardService =
                new NodeGuardService(_logger.Object, null, walletRepository.Object,
                    _mockMapper, walletWithdrawalRequestRepository.Object,
                    bitcoinService.Object,
                    nbxplorerService.Object, new Mock<ISchedulerFactory>().Object);

            //Act
            var resp = async () => await mockNodeGuardService.RequestWithdrawal(requestWithdrawalRequest,
                TestServerCallContext.Create());

            //Assert
            resp.Should().ThrowAsync<RpcException>();
        }

        [Fact]
        public async Task RequestWithdrawal_NoAvailableUTXOs()
        {
            //Arrange
            var wallet = InitMockRequestWithdrawal(out var walletRepository,
                out var nbxplorerService,
                out _,
                out var psbt,
                out _,
                out _,
                out _,
                out var bitcoinService,
                out var walletWithdrawalRequestRepository,
                out _);

            bitcoinService.Setup(x => x.GenerateTemplatePSBT(It.IsAny<WalletWithdrawalRequest>()))
                .ReturnsAsync(psbt);

            var requestWithdrawalRequest = new RequestWithdrawalRequest
            {
                WalletId = wallet.Id,
                Amount = 100,
                Description = $"Request Withdrawal Test {DateTime.Now}",
                Address = "bcrt1q590shaxaf5u08ml8jwlzghz99dup3z9592vxal",
            };

            var mockNodeGuardService =
                new NodeGuardService(_logger.Object, null, walletRepository.Object,
                    _mockMapper, walletWithdrawalRequestRepository.Object,
                    bitcoinService.Object,
                    nbxplorerService.Object, new Mock<ISchedulerFactory>().Object);

            //Act
            var resp = async () => await mockNodeGuardService.RequestWithdrawal(requestWithdrawalRequest,
                TestServerCallContext.Create());

            //Assert
            resp.Should().ThrowAsync<RpcException>();
        }

        [Fact]
        public async Task RequestWithdrawal_FailureRepoSave()
        {
            //Arrange
            var wallet = InitMockRequestWithdrawal(out var walletRepository,
                out var nbxplorerService,
                out _,
                out _,
                out _,
                out _,
                out _,
                out var bitcoinService,
                out var walletWithdrawalRequestRepository,
                out _);

            walletWithdrawalRequestRepository.Setup(x => x.AddAsync(It.IsAny<WalletWithdrawalRequest>()))
                .ReturnsAsync((false, "error"));

            var requestWithdrawalRequest = new RequestWithdrawalRequest
            {
                WalletId = wallet.Id,
                Amount = 100,
                Description = $"Request Withdrawal Test {DateTime.Now}",
                Address = "bcrt1q590shaxaf5u08ml8jwlzghz99dup3z9592vxal",
            };

            var mockNodeGuardService =
                new NodeGuardService(_logger.Object, null, walletRepository.Object,
                    _mockMapper, walletWithdrawalRequestRepository.Object,
                    bitcoinService.Object,
                    nbxplorerService.Object, new Mock<ISchedulerFactory>().Object);

            //Act
            var resp = async () => await mockNodeGuardService.RequestWithdrawal(requestWithdrawalRequest,
                TestServerCallContext.Create());

            //Assert
            resp.Should().ThrowAsync<RpcException>();
        }

        [Fact]
        public async Task RequestWithdrawal_NullTemplatePSBT()
        {
            //Arrange
            var wallet = InitMockRequestWithdrawal(out var walletRepository,
                out var nbxplorerService,
                out _,
                out _,
                out _,
                out _,
                out _,
                out var bitcoinService,
                out var walletWithdrawalRequestRepository,
                out _);

            bitcoinService.Setup(x => x.GenerateTemplatePSBT(It.IsAny<WalletWithdrawalRequest>()))
                .ThrowsAsync(exception: new Exception("error"));

            var requestWithdrawalRequest = new RequestWithdrawalRequest
            {
                WalletId = wallet.Id,
                Amount = 100,
                Description = $"Request Withdrawal Test {DateTime.Now}",
                Address = "bcrt1q590shaxaf5u08ml8jwlzghz99dup3z9592vxal",
            };

            var mockNodeGuardService =
                new NodeGuardService(_logger.Object, null, walletRepository.Object,
                    _mockMapper, walletWithdrawalRequestRepository.Object,
                    bitcoinService.Object,
                    nbxplorerService.Object, new Mock<ISchedulerFactory>().Object);

            //Act
            var resp = async () => await mockNodeGuardService.RequestWithdrawal(requestWithdrawalRequest,
                TestServerCallContext.Create());

            //Assert
            resp.Should().ThrowAsync<RpcException>();
        }

        private Wallet InitMockRequestWithdrawal(out Mock<IWalletRepository> walletRepository,
            out Mock<INBXplorerService> nbxplorerService,
            out KeyPathInformation keypath, out PSBT psbt, out UTXOChanges utxoChanges, out PSBTInput input,
            out List<UTXO> utxoList,
            out Mock<IBitcoinService> bitcoinService,
            out Mock<IWalletWithdrawalRequestRepository> walletWithdrawalRequestRepository,
            out ISchedulerFactory schedulerFactory)
        {
            var wallet = _fixture.Create<Wallet>();
            wallet.IsHotWallet = true;
            wallet.Keys = new List<Key>
            {
                new Key
                {
                    XPUB =
                        "tpubDCfM7v7fKZ31gTGGggNMycfCr5cDGinyijveRZ44RYSgAgEARwhaBd6PPpWst8kKbhEVoqNasgjHFWZKrEQoJ9pzPVEmNZDNe92hShzEMDy"
                }
            };

            walletRepository = new Mock<IWalletRepository>();

            walletRepository.Setup(x => x.GetById(It.IsAny<int>())).ReturnsAsync(wallet);
            nbxplorerService = new Mock<INBXplorerService>();

            keypath = new KeyPathInformation()
            {
                Address = BitcoinAddress.Create("bcrt1q590shaxaf5u08ml8jwlzghz99dup3z9592vxal", Network.RegTest),
            };

            nbxplorerService.Setup(x => x.GetUnusedAsync(It.IsAny<DerivationStrategyBase>(),
                    It.IsAny<DerivationFeature>(), It.IsAny<int>(), It.IsAny<bool>(), default))
                .ReturnsAsync(keypath);

            //Setup NbxplorerService for GetUTXOSAsync

            psbt = PSBT.Parse(
                "70736274ff01005e01000000013f8f745a7b40c6df77d5c558361509612d34a9fec5540d9e0ab10f1bc1d4eeec0000000000ffffffff0113601200000000002200200b977d20c92e6fb7efaf6286b58f00f4a40cbab98ce7e44fe704b9ac886e3a41000000004f01043587cf032f11242c80000001fc3bee423a018ceb6d226a0663288ed0fb5ee0fa9d790ab370d030e04730e6c803808e2e1cd482bdd07196943c5a8133d169af86022b6bb0381771644ec07444d7101fcce4de3000008001000080010000804f01043587cf0356ac03f480000001f908ae4a66b0ffa1ab14b55af0ce48f2a70225cb9010902d6901af90b93e8f810251af15e05bb8b7eac1895b3f5f47ffefecd8321a02ef22298f89d0a2037df0601060f3a0b33000008001000080010000804f01043587cf037db9468d80000001f52270c7687a3afb351ef4f1b9a30c10f3b6d392170683aea9488c139a11c998032223852338ea67fbfd8dc352ab2625e79a27764621236bf1c0213090d3489e9f10ed0210c83000008001000080010000800001012b8f62120000000000220020997a5fd26a72084c8a2d12bf902acec18669900dd09891d2ea24a8711ca3ec6e220203b0dbc01268f283bf00120c763686bd8984e7789442cf5c2802095f78b9b9ab9a47304402202e1820e8f1e3b8ad7d10117a9a83a3a8567537d73336d01b9f08859fda4856fe0220727a714631e4ffcd954ae4e011b3039232fa16e1ef99f7e6bd6f422fd3187b8502010304020000000105695221028761458f9cc5c051ed6baf4076df9d89ed21daf2d0f7570382c1bcf23c09878121035a4be5f58b8bbe9d399d9f2d8e004b42643649a5c75575fc3739dfac73264b2c2103b0dbc01268f283bf00120c763686bd8984e7789442cf5c2802095f78b9b9ab9a53ae2206028761458f9cc5c051ed6baf4076df9d89ed21daf2d0f7570382c1bcf23c0987811860f3a0b330000080010000800100008001000000050000002206035a4be5f58b8bbe9d399d9f2d8e004b42643649a5c75575fc3739dfac73264b2c18ed0210c83000008001000080010000800100000005000000220603b0dbc01268f283bf00120c763686bd8984e7789442cf5c2802095f78b9b9ab9a181fcce4de30000080010000800100008001000000050000000000",
                Network.RegTest);

            utxoChanges = new UTXOChanges();
            input = psbt.Inputs[0];
            utxoList = new List<UTXO>()
            {
                new()
                {
                    Outpoint = input.PrevOut,
                    Index = 0,
                    ScriptPubKey = input.WitnessUtxo.ScriptPubKey,
                    KeyPath = new KeyPath("0/0"),
                },
            };

            nbxplorerService.Setup(x => x.GetUTXOsAsync(It.IsAny<DerivationStrategyBase>(), default))
                .ReturnsAsync(utxoChanges);

            bitcoinService = new Mock<IBitcoinService>();

            utxoChanges.Confirmed = new UTXOChange() {UTXOs = utxoList};

            bitcoinService.Setup(x => x.GenerateTemplatePSBT(It.IsAny<WalletWithdrawalRequest>()))
                .ReturnsAsync(psbt);

            //Bitcoin Service Perform Withdrawal
            bitcoinService.Setup(x => x.PerformWithdrawal(It.IsAny<WalletWithdrawalRequest>()));

            walletWithdrawalRequestRepository = new Mock<IWalletWithdrawalRequestRepository>();

            walletWithdrawalRequestRepository.Setup(x => x.AddAsync(It.IsAny<WalletWithdrawalRequest>()))
                .ReturnsAsync((true, null));

            var withdrawalRequestFixture = _fixture.Create<WalletWithdrawalRequest>();

            //GetByIdMock
            walletWithdrawalRequestRepository.Setup(x => x.GetById(It.IsAny<int>()))
                .ReturnsAsync(withdrawalRequestFixture);

            var properties = new NameValueCollection
            {
                ["quartz.serializer.type"] = "json"
            };

            var sf = new StdSchedulerFactory(properties);
            var sched = Task.Run(() => sf.GetScheduler()).Result;
            var schedulerFactoryMock = new Mock<ISchedulerFactory>();
            schedulerFactoryMock.Setup(x => x.GetScheduler(default))
                .ReturnsAsync(sched);

            schedulerFactory = schedulerFactoryMock.Object;

            return wallet;
        }

        private readonly Random _random = new();

        private (Mock<IDbContextFactory<ApplicationDbContext>>, ApplicationDbContext) SetupDbContextFactory()
        {
            var dbContextFactory = new Mock<IDbContextFactory<ApplicationDbContext>>();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "WalletRepositoryTest" + _random.Next())
                .Options;
            var context = new ApplicationDbContext(options);
            dbContextFactory.Setup(x => x.CreateDbContext()).Returns(context);
            dbContextFactory.Setup(x => x.CreateDbContextAsync(default)).ReturnsAsync(context);
            return (dbContextFactory, context);
        }

        [Fact]
        public async void GetAvailableWallets_ReturnsAllWallets()
        {
            var (dbContextFactory, context) = SetupDbContextFactory();
            var scheduleFactory = new Mock<ISchedulerFactory>();

            var internalWallet = new InternalWallet()
            {
                DerivationPath = ""
            };
            context.Wallets.Add(new Wallet()
            {
                Name = "TestWallet1",
                IsFinalised = true,
                IsHotWallet = true,
                InternalWallet = internalWallet,
                Keys = new List<Key>(),
            });
            context.Wallets.Add(new Wallet()
            {
                Name = "TestWallet2",
                IsFinalised = true,
                IsHotWallet = false,
                InternalWallet = internalWallet,
                Keys = new List<Key>()
            });
            context.SaveChanges();

            var walletRepository = new WalletRepository(null, null, dbContextFactory.Object, null, null, null);

            var request = new GetAvailableWalletsRequest();
            var nodeGuardService = new NodeGuardService(null, null, walletRepository, null, null, null, null,
                scheduleFactory.Object);
            var result = await nodeGuardService.GetAvailableWallets(request, null);

            result.Wallets.ToList().Count().Should().Be(2);
        }

        [Fact]
        public async void GetAvailableWallets_ReturnsTypeHot()
        {
            var (dbContextFactory, context) = SetupDbContextFactory();
            var scheduleFactory = new Mock<ISchedulerFactory>();

            var internalWallet = new InternalWallet()
            {
                DerivationPath = ""
            };
            context.Wallets.Add(new Wallet()
            {
                Name = "TestWallet1",
                IsFinalised = true,
                IsHotWallet = true,
                InternalWallet = internalWallet,
                Keys = new List<Key>(),
            });
            context.Wallets.Add(new Wallet()
            {
                Name = "TestWallet2",
                IsFinalised = true,
                IsHotWallet = false,
                InternalWallet = internalWallet,
                Keys = new List<Key>()
            });
            context.SaveChanges();

            var walletRepository = new WalletRepository(null, null, dbContextFactory.Object, null, null, null);

            var request = new GetAvailableWalletsRequest()
            {
                WalletType = WALLET_TYPE.Hot
            };
            var nodeGuardService = new NodeGuardService(null, null, walletRepository, null, null, null, null,
                scheduleFactory.Object);
            var result = await nodeGuardService.GetAvailableWallets(request, null);

            result.Wallets.ToList().Count().Should().Be(1);
            result.Wallets.ToList().FirstOrDefault()!.IsHotWallet.Should().Be(true);
        }

        [Fact]
        public async void GetAvailableWallets_ReturnsTypeCold()
        {
            var (dbContextFactory, context) = SetupDbContextFactory();
            var scheduleFactory = new Mock<ISchedulerFactory>();

            var internalWallet = new InternalWallet()
            {
                DerivationPath = ""
            };
            context.Wallets.Add(new Wallet()
            {
                Name = "TestWallet1",
                IsFinalised = true,
                IsHotWallet = true,
                InternalWallet = internalWallet,
                Keys = new List<Key>(),
            });
            context.Wallets.Add(new Wallet()
            {
                Name = "TestWallet2",
                IsFinalised = true,
                IsHotWallet = false,
                InternalWallet = internalWallet,
                Keys = new List<Key>()
            });
            context.SaveChanges();

            var walletRepository = new WalletRepository(null, null, dbContextFactory.Object, null, null, null);

            var request = new GetAvailableWalletsRequest()
            {
                WalletType = WALLET_TYPE.Cold
            };
            var nodeGuardService = new NodeGuardService(null, null, walletRepository, null, null, null, null,
                scheduleFactory.Object);
            var result = await nodeGuardService.GetAvailableWallets(request, null);

            result.Wallets.ToList().Count().Should().Be(1);
            result.Wallets.ToList().FirstOrDefault()!.IsHotWallet.Should().Be(false);
        }

        [Fact]
        public async void GetAvailableWallets_ReturnsIds()
        {
            var (dbContextFactory, context) = SetupDbContextFactory();
            var scheduleFactory = new Mock<ISchedulerFactory>();

            var internalWallet = new InternalWallet()
            {
                DerivationPath = ""
            };
            context.Wallets.Add(new Wallet()
            {
                Id = 1,
                Name = "TestWallet1",
                IsFinalised = true,
                IsHotWallet = true,
                InternalWallet = internalWallet,
                Keys = new List<Key>(),
            });
            context.Wallets.Add(new Wallet()
            {
                Id = 2,
                Name = "TestWallet2",
                IsFinalised = true,
                IsHotWallet = false,
                InternalWallet = internalWallet,
                Keys = new List<Key>()
            });
            context.Wallets.Add(new Wallet()
            {
                Id = 3,
                Name = "TestWallet3",
                IsFinalised = true,
                IsHotWallet = false,
                InternalWallet = internalWallet,
                Keys = new List<Key>()
            });
            context.SaveChanges();

            var walletRepository = new WalletRepository(null, null, dbContextFactory.Object, null, null, null);

            var request = new GetAvailableWalletsRequest()
            {
                Id = {1, 3}
            };
            var nodeGuardService = new NodeGuardService(null, null, walletRepository, null, null, null, null,
                scheduleFactory.Object);
            var result = await nodeGuardService.GetAvailableWallets(request, null);

            result.Wallets.ToList().Count().Should().Be(2);
            result.Wallets.ToList().FirstOrDefault()!.Id.Should().Be(1);
            result.Wallets.ToList().LastOrDefault()!.Id.Should().Be(3);
        }

        [Fact]
        public async void GetAvailableWallets_CantPassTwoFilters()
        {
            var (dbContextFactory, context) = SetupDbContextFactory();
            var scheduleFactory = new Mock<ISchedulerFactory>();

            var request = new GetAvailableWalletsRequest()
            {
                WalletType = WALLET_TYPE.Hot,
                Id = {1, 3}
            };
            var nodeGuardService =
                new NodeGuardService(null, null, null, null, null, null, null, scheduleFactory.Object);
            var act = () => nodeGuardService.GetAvailableWallets(request, null);

            await act
                .Should()
                .ThrowAsync<RpcException>()
                .WithMessage(
                    "Status(StatusCode=\"Internal\", Detail=\"You can't select wallets by type and id at the same time\")");
        }
    }
}