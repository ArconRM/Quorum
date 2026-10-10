namespace Training.Application.Messages;

// Внутренние шаги сервиса. Через очередь — потому что долгие (GPU) и чтобы сбой
// повторялся брокером, а не терялся вместе с HTTP-запросом.

public sealed record RunTrainingJob(Guid JobId);

public sealed record EvaluateCandidate(Guid CycleId);
