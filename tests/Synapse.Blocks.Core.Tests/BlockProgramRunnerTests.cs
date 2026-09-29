using Synapse.Blocks.Models;
using Synapse.Blocks.Services;

namespace Synapse.Blocks.Core.Tests;

public sealed class BlockProgramRunnerTests
{
    private readonly BlockProgramRunner _runner = new();

    [Fact]
    public void Level_models_are_exposed_by_the_shared_core_assembly()
    {
        Assert.Equal("Synapse.Blocks.Core", typeof(LevelDefinition).Assembly.GetName().Name);
    }

    [Fact]
    public void RunAll_preserves_arithmetic_result_and_visited_path()
    {
        var input = new BlockNode { Kind = BlockKind.Input };
        var operation = new BlockNode { Kind = BlockKind.Operation, Config = "+ 2" };
        var output = new BlockNode { Kind = BlockKind.Output };
        var program = Connect((input, operation), (operation, output));

        var result = Assert.Single(_runner.RunAll(program, Level("3", "5")));

        AssertSuccess(result, "5", input.Id, operation.Id, output.Id);
    }

    [Fact]
    public void RunAll_preserves_condition_branch_result_and_visited_path()
    {
        var input = new BlockNode { Kind = BlockKind.Input };
        var condition = new BlockNode { Kind = BlockKind.Condition, Config = "> 0" };
        var positive = new BlockNode { Kind = BlockKind.Operation, Config = "set positive" };
        var negative = new BlockNode { Kind = BlockKind.Operation, Config = "set negative" };
        var output = new BlockNode { Kind = BlockKind.Output };
        var program = new BlockProgram
        {
            Nodes = [input, condition, positive, negative, output],
            Connections =
            [
                Edge(input, condition),
                new() { FromNodeId = condition.Id, ToNodeId = positive.Id, Port = OutputPort.True },
                new() { FromNodeId = condition.Id, ToNodeId = negative.Id, Port = OutputPort.False },
                Edge(positive, output),
                Edge(negative, output)
            ]
        };

        var result = Assert.Single(_runner.RunAll(program, Level("1", "positive")));

        AssertSuccess(result, "positive", input.Id, condition.Id, positive.Id, output.Id);
    }

    [Fact]
    public void RunAll_preserves_loop_result_and_visited_path()
    {
        var input = new BlockNode { Kind = BlockKind.Input };
        var loop = new BlockNode { Kind = BlockKind.Loop, Config = "3" };
        var operation = new BlockNode { Kind = BlockKind.Operation, Config = "+ 2" };
        var output = new BlockNode { Kind = BlockKind.Output };
        var program = new BlockProgram
        {
            Nodes = [input, loop, operation, output],
            Connections =
            [
                Edge(input, loop),
                new() { FromNodeId = loop.Id, ToNodeId = operation.Id, Port = OutputPort.Repeat },
                new() { FromNodeId = loop.Id, ToNodeId = output.Id, Port = OutputPort.Done }
            ]
        };

        var result = Assert.Single(_runner.RunAll(program, Level("0", "6")));

        Assert.True(result.Passed);
        Assert.Equal("6", result.Actual);
        Assert.Empty(result.Error);
        Assert.Equal([input.Id, loop.Id, operation.Id, loop.Id, operation.Id, loop.Id, operation.Id, loop.Id, output.Id], result.VisitedNodes);
    }

    [Fact]
    public void RunAll_preserves_variable_result_and_visited_path()
    {
        var input = new BlockNode { Kind = BlockKind.Input };
        var variable = new BlockNode { Kind = BlockKind.Variable, VariableName = "A", VariableInitialValue = "10" };
        var action = new BlockNode { Kind = BlockKind.VariableAction, VariableName = "A", VariableAction = VariableAction.Add };
        var output = new BlockNode { Kind = BlockKind.Output };
        var program = Connect((input, action), (action, output));
        program.Nodes.Insert(1, variable);

        var result = Assert.Single(_runner.RunAll(program, Level("3", "13")));

        AssertSuccess(result, "13", input.Id, action.Id, output.Id);
    }

    private static LevelDefinition Level(string input, string expected)
        => new()
        {
            Tests = [new LevelTestCase { Input = input, ExpectedOutput = expected }]
        };

    private static BlockProgram Connect(params (BlockNode From, BlockNode To)[] edges)
        => new()
        {
            Nodes = edges.SelectMany(edge => new[] { edge.From, edge.To }).DistinctBy(node => node.Id).ToList(),
            Connections = edges.Select(edge => Edge(edge.From, edge.To)).ToList()
        };

    private static BlockConnection Edge(BlockNode from, BlockNode to)
        => new() { FromNodeId = from.Id, ToNodeId = to.Id };

    private static void AssertSuccess(TestRunResult result, string expected, params Guid[] visited)
    {
        Assert.True(result.Passed);
        Assert.Equal(expected, result.Actual);
        Assert.Empty(result.Error);
        Assert.Equal(visited, result.VisitedNodes);
    }
}
