using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Kernel.Blockchain.Application;
using AElf.Types;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;
using Xunit;

namespace AElf.Kernel.SmartContract.Parallel.Tests;

public class TransactionGrouperTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<GrouperOptions>(o =>
        {
            o.GroupingTimeOut = 200;
            o.MaxTransactions = 10;
        });
        context.Services.AddSingleton<ITransactionGrouper, TransactionGrouper>();
        context.Services.AddSingleton(
            _ =>
            {
                var mock = new Mock<IBlockchainService>();
                mock.Setup(s => s.GetChainAsync()).Returns(Task.FromResult(new Chain
                {
                    BestChainHash = Hash.Empty
                }));
                return mock.Object;
            });
        context.Services.AddSingleton<IResourceExtractionService, MockResourceExtractionService>();
    }
}

public class TransactionGrouperTest : AbpIntegratedTest<TransactionGrouperTestModule>
{
    private ITransactionGrouper Grouper => Application.ServiceProvider.GetRequiredService<ITransactionGrouper>();

    [Fact]
    public async Task Group_Test()
    {
        var group1Resources = new[] { (0, 1), (2, 1), (2, 4), (3, 2), (4, 5) };
        var group1 =
            group1Resources.Select(r => new { Resource = r, Transaction = GetTransaction("g1", r.Item1, r.Item2) })
                .ToList();
        var group2Resources = new[] { (6, 7), (8, 7) };
        var group2 =
            group2Resources.Select(r => new { Resource = r, Transaction = GetTransaction("g2", r.Item1, r.Item2) })
                .ToList();
        var group3Resources = new[] { (9, 10), (10, 11) };
        var group3 =
            group3Resources.Select(r => new { Resource = r, Transaction = GetTransaction("g3", r.Item1, r.Item2) })
                .ToList();
        var groups = new[] { group1, group2, group3 };
        var txLookup = groups.SelectMany(x => x).ToDictionary(x => x.Transaction.Params, x => x.Resource);
        var allTxns = groups.SelectMany(x => x).Select(x => x.Transaction).OrderBy(x => Guid.NewGuid()).ToList();

        var chainContext = new ChainContext
        {
            BlockHeight = 10,
            BlockHash = HashHelper.ComputeFrom("blockHash")
        };
        var grouped = await Grouper.GroupAsync(chainContext, allTxns);
        var groupedResources = grouped.Parallelizables.Select(g => g.Select(t => txLookup[t.Params]).ToList()).ToList();
        var expected = groups.Select(g => g.Select(x => x.Resource).ToList()).Select(StringRepresentation)
            .OrderBy(x => x);
        var actual = groupedResources.Select(StringRepresentation).OrderBy(x => x);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Group_With_OverMaxTransactions_Test()
    {
        var group1Resources = new[] { (0, 1), (2, 1), (2, 4), (3, 2), (4, 5) };
        var group1 =
            group1Resources.Select(r => new { Resource = r, Transaction = GetTransaction("g1", r.Item1, r.Item2) })
                .ToList();
        var group2Resources = new[] { (6, 7), (8, 7) };
        var group2 =
            group2Resources.Select(r => new { Resource = r, Transaction = GetTransaction("g2", r.Item1, r.Item2) })
                .ToList();
        var group3Resources = new[] { (9, 10), (10, 11) };
        var group3 =
            group3Resources.Select(r => new { Resource = r, Transaction = GetTransaction("g3", r.Item1, r.Item2) })
                .ToList();
        var group4Resources = new[] { (12, 13), (13, 15) };
        var group4 =
            group4Resources.Select(r => new { Resource = r, Transaction = GetTransaction("g4", r.Item1, r.Item2) })
                .ToList();

        var groups = new[] { group1, group2, group3, group4 };
        var allTxns = groups.SelectMany(x => x).Select(x => x.Transaction).OrderBy(x => Guid.NewGuid()).ToList();
        allTxns.Count.ShouldBeGreaterThan(10);

        var chainContext = new ChainContext
        {
            BlockHeight = 10,
            BlockHash = HashHelper.ComputeFrom("blockHash")
        };
        var grouped = await Grouper.GroupAsync(chainContext, allTxns);

        grouped.Parallelizables.Count.ShouldBeGreaterThanOrEqualTo(4);
        grouped.NonParallelizables.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Group_With_InvalidContractAddress_Test()
    {
        var groupResources = new[] { (0, 1) };
        var group =
            groupResources.Select(r => new
                {
                    Resource = r,
                    Transaction = GetTransaction("g1", r.Item1, r.Item2, ParallelType.InvalidContractAddress)
                })
                .ToList();
        var chainContext = new ChainContext
        {
            BlockHeight = 10,
            BlockHash = HashHelper.ComputeFrom("blockHash")
        };
        var grouped = await Grouper.GroupAsync(chainContext, group.Select(g => g.Transaction).ToList());

        grouped.TransactionsWithoutContract.Count.ShouldBe(1);
        grouped.Parallelizables.Count.ShouldBe(0);
        grouped.NonParallelizables.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Group_With_NonParallelizable_Test()
    {
        var groupResources = new[] { (0, 1) };
        var group =
            groupResources.Select(r => new
                {
                    Resource = r, Transaction = GetTransaction("g1", r.Item1, r.Item2, ParallelType.NonParallelizable)
                })
                .ToList();
        var chainContext = new ChainContext
        {
            BlockHeight = 10,
            BlockHash = HashHelper.ComputeFrom("blockHash")
        };
        var grouped = await Grouper.GroupAsync(chainContext, group.Select(g => g.Transaction).ToList());

        grouped.NonParallelizables.Count.ShouldBe(1);
        grouped.TransactionsWithoutContract.Count.ShouldBe(0);
        grouped.Parallelizables.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Group_Without_Paths_Test()
    {
        var groupResources = new[] { (0, 1) };
        var group =
            groupResources.Select(r => new
                    { Resource = r, Transaction = GetTransactionWithoutPaths("g1", r.Item1, r.Item2) })
                .ToList();
        var chainContext = new ChainContext
        {
            BlockHeight = 10,
            BlockHash = HashHelper.ComputeFrom("blockHash")
        };
        var grouped = await Grouper.GroupAsync(chainContext, group.Select(g => g.Transaction).ToList());

        grouped.NonParallelizables.Count.ShouldBe(1);
        grouped.TransactionsWithoutContract.Count.ShouldBe(0);
        grouped.Parallelizables.Count.ShouldBe(0);
    }

    private Transaction GetTransaction(string methodName, int from, int to,
        ParallelType parallelType = ParallelType.Parallelizable)
    {
        var tx = new Transaction
        {
            MethodName = methodName,
            Params = new TransactionResourceInfo
            {
                WritePaths =
                {
                    GetPath(from), GetPath(to)
                },
                ParallelType = parallelType
            }.ToByteString()
        };
        return tx;
    }

    private Transaction GetTransactionWithoutPaths(string methodName, int from, int to)
    {
        var tx = new Transaction
        {
            MethodName = methodName,
            Params = new TransactionResourceInfo().ToByteString()
        };
        return tx;
    }

    private string StringRepresentation(List<(int, int)> resources)
    {
        return string.Join(" ", resources.Select(r => r.ToString()).OrderBy(x => x));
    }

    private ScopedStatePath GetPath(int value)
    {
        return new ScopedStatePath
        {
            Path = new StatePath
            {
                Parts = { value.ToString() }
            }
        };
    }
}