using Grpc.Core;
using Labeling.Application.Features.Runs.GetTrainingSet;
using MediatR;
using Quorum.Application.Exceptions;
using Quorum.Domain;
using Quorum.Grpc.Labeling;

namespace Labeling.Service.Grpc;

/// <summary>gRPC-сервер: принятые метки прогона для Training.</summary>
public sealed class TrainingSetQueryGrpcService(ISender sender) : TrainingSetQuery.TrainingSetQueryBase
{
    public override async Task<GetTrainingSetResponse> GetTrainingSet(GetTrainingSetRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.RunId, out var runId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "run_id must be a GUID"));

        try
        {
            var set = await sender.Send(new GetTrainingSetQuery(runId), context.CancellationToken);
            var response = new GetTrainingSetResponse { Task = set.Task };
            foreach (var item in set.Items)
            {
                var frame = new LabeledFrame { FrameId = item.FrameId.ToString(), Path = item.Path, Split = item.Split, Text = item.Text ?? string.Empty };
                frame.Boxes.AddRange(item.Boxes.Select(b => new LabelBox { X = b.X, Y = b.Y, W = b.W, H = b.H }));
                response.Frames.Add(frame);
            }
            return response;
        }
        catch (NotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (DomainException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
    }
}
