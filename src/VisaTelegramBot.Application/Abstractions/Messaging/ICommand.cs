using MediatR;
using VisaTelegramBot.Application.Common.Results;

namespace VisaTelegramBot.Application.Abstractions.Messaging;

/// <summary>
/// CQRS: Command sistemin durumunu degistirir. Query ise sadece okur.
/// Bu arayuzler MediatR'i sarmalar; handler'lar her zaman Result doner.
/// </summary>
public interface IBaseCommand;

public interface ICommand : IRequest<Result>, IBaseCommand;

public interface ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand;

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;
