namespace UCredit.Application.Execution;

public sealed record ExecutionActor(string? LegacyUserCode);

public interface IExecutionActorContext
{
    Task<ExecutionActor> GetAsync(CancellationToken cancellationToken = default);
}
