namespace Musoq.DataSources.SeparatedValues;

internal readonly record struct SeparatedValuesReadStrategyContext(
    long? FileSize,
    bool IsStream,
    int ProjectedColumnCount,
    int AllColumnCount,
    long? AcceptedTake,
    bool HasResidualWork,
    bool CanAvoidSecondHeaderOpen,
    bool ProjectionAccepted);
