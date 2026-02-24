using CdrBilling.Application.Abstractions;
using CdrBilling.Application.DTOs;
using MediatR;

namespace CdrBilling.Application.UseCases;

public sealed record GetCallDetailsQuery(
    Guid SessionId,
    string? PhoneNumber,
    int Page,
    int PageSize) : IRequest<PagedResult<CallRecordDetailDto>>;

public sealed class GetCallDetailsHandler(ICallRecordRepository repo)
    : IRequestHandler<GetCallDetailsQuery, PagedResult<CallRecordDetailDto>>
{
    public Task<PagedResult<CallRecordDetailDto>> Handle(
        GetCallDetailsQuery request,
        CancellationToken cancellationToken)
        => repo.GetDetailAsync(
            request.SessionId,
            request.PhoneNumber,
            request.Page,
            request.PageSize,
            cancellationToken);
}
