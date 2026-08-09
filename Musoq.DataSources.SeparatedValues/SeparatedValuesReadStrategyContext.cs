namespace Musoq.DataSources.SeparatedValues;

internal readonly record struct SeparatedValuesReadStrategyContext(
    long FileSize,
    int ProjectedColumnCount,
    int AllColumnCount,
    long? AcceptedTake,
    bool HasResidualWork,
    bool ProjectionAccepted);
