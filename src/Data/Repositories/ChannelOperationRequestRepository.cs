﻿using FundsManager.Data.Models;
using FundsManager.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FundsManager.Data.Repositories
{
    public class ChannelOperationRequestRepository : IChannelOperationRequestRepository
    {
        private readonly IRepository<ChannelOperationRequest> _repository;
        private readonly ILogger<ChannelOperationRequestRepository> _logger;
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

        public ChannelOperationRequestRepository(IRepository<ChannelOperationRequest> repository,
            ILogger<ChannelOperationRequestRepository> logger,
            IDbContextFactory<ApplicationDbContext> dbContextFactory)
        {
            _repository = repository;
            _logger = logger;
            _dbContextFactory = dbContextFactory;
        }

        public async Task<ChannelOperationRequest?> GetById(int id)
        {
            await using var applicationDbContext = await _dbContextFactory.CreateDbContextAsync();

            return await applicationDbContext.ChannelOperationRequests.FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<List<ChannelOperationRequest>> GetAll()
        {
            await using var applicationDbContext = await _dbContextFactory.CreateDbContextAsync();

            return await applicationDbContext.ChannelOperationRequests.ToListAsync();
        }

        public async Task<List<ChannelOperationRequest>> GetPendingRequestsByUser(string userId)
        {
            await using var applicationDbContext = await _dbContextFactory.CreateDbContextAsync();

            return await applicationDbContext.ChannelOperationRequests
                .Where(request => request.Wallet.Keys.Any(key => key.User != null && key.User.Id == userId))
                .Include(request => request.Wallet)
                .Include(request => request.SourceNode)
                .Include(request => request.DestNode)
                .Include(request => request.ChannelOperationRequestSignatures)
                .ToListAsync();
        }
        
        public async Task<List<ChannelOperationRequest>> GetUnsignedPendingRequestsByUser(string userId)
        {
            await using var applicationDbContext = await _dbContextFactory.CreateDbContextAsync();

            return await applicationDbContext.ChannelOperationRequests
                .Where(request => request.Wallet.Keys.Any(key => key.User != null && key.User.Id == userId) && 
                                                                 request.ChannelOperationRequestSignatures.All(signature => signature.UserSignerId != userId))
                .Include(request => request.SourceNode)
                .Include(request => request.Wallet)
                .Include(request => request.DestNode)
                .Include(request => request.ChannelOperationRequestSignatures)
                .ToListAsync();
        }
        
        public async Task<(bool, string?)> AddAsync(ChannelOperationRequest type)
        {
            await using var applicationDbContext = await _dbContextFactory.CreateDbContextAsync();

            return await _repository.AddAsync(type, applicationDbContext);
        }

        public async Task<(bool, string?)> AddRangeAsync(List<ChannelOperationRequest> type)
        {
            await using var applicationDbContext = await _dbContextFactory.CreateDbContextAsync();

            return await _repository.AddRangeAsync(type, applicationDbContext);
        }

        public (bool, string?) Remove(ChannelOperationRequest type)
        {
            using var applicationDbContext = _dbContextFactory.CreateDbContext();

            return _repository.Remove(type, applicationDbContext);
        }

        public (bool, string?) RemoveRange(List<ChannelOperationRequest> types)
        {
            using var applicationDbContext = _dbContextFactory.CreateDbContext();

            return _repository.RemoveRange(types, applicationDbContext);
        }

        public (bool, string?) Update(ChannelOperationRequest type)
        {
            using var applicationDbContext = _dbContextFactory.CreateDbContext();

            type.Wallet = null;
            type.User = null;

            return _repository.Update(type, applicationDbContext);
        }
    }
}