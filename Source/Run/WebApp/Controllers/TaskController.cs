using System.Net;
using AutoMapper;
using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/tasks")]
public class TaskController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly IRepository<Investment> _investmentRepository;
    private readonly IRepository<FinancialInstitution> _institutionRepository;
    private readonly IMapper _mapper;

    public TaskController(
        ITaskService taskService,
        IRepository<Investment> investmentRepository,
        IRepository<FinancialInstitution> institutionRepository,
        IMapper mapper)
    {
        _taskService = taskService;
        _investmentRepository = investmentRepository;
        _institutionRepository = institutionRepository;
        _mapper = mapper;
    }

    [HttpGet("pending")]
    public async Task<Response<IEnumerable<TaskItemResponse>>> GetPendingAsync()
    {
        var tasks = await _taskService.GetPendingTasksAsync();
        var responses = new List<TaskItemResponse>();

        foreach (var task in tasks)
        {
            var response = _mapper.Map<TaskItemResponse>(task);
            var investment = await _investmentRepository.GetAsync(task.InvestmentId);
            if (investment != null)
                await EnrichTaskItemAsync(response, investment);
            responses.Add(response);
        }

        return new Response<IEnumerable<TaskItemResponse>>(responses);
    }

    [HttpPut("{id}/complete")]
    public async Task<Response<TaskItemResponse>> CompleteAsync(Guid id)
    {
        var task = await _taskService.CompleteTaskAsync(id);
        if (task == null)
            return new Response<TaskItemResponse>([new Error("Task not found")], HttpStatusCode.NotFound);

        var response = _mapper.Map<TaskItemResponse>(task);
        var investment = await _investmentRepository.GetAsync(task.InvestmentId);
        if (investment != null)
            await EnrichTaskItemAsync(response, investment);

        return response;
    }

    [HttpGet("completed")]
    public async Task<Response<IEnumerable<TaskItemResponse>>> GetCompletedAsync()
    {
        var tasks = await _taskService.GetCompletedTasksAsync();
        var responses = new List<TaskItemResponse>();

        foreach (var task in tasks)
        {
            var response = _mapper.Map<TaskItemResponse>(task);
            var investment = await _investmentRepository.GetAsync(task.InvestmentId);
            if (investment != null)
                await EnrichTaskItemAsync(response, investment);
            responses.Add(response);
        }

        return new Response<IEnumerable<TaskItemResponse>>(responses);
    }

    [HttpGet("future")]
    public async Task<Response<IEnumerable<FutureTaskResponse>>> GetFutureAsync([FromQuery] int monthsAhead = 36)
    {
        monthsAhead = Math.Clamp(monthsAhead, 1, 1200);
        var futureTasks = await _taskService.GetFutureTasksAsync(monthsAhead);
        return new Response<IEnumerable<FutureTaskResponse>>(futureTasks);
    }

    [HttpGet("pending-count")]
    public async Task<Response<int>> GetPendingCountAsync()
    {
        var count = await _taskService.GetPendingCountAsync();
        return new Response<int>(count);
    }

    private async Task EnrichTaskItemAsync(TaskItemResponse response, Investment investment)
    {
        response.InvestmentName = investment.Name;
        response.Currency = investment.Currency.ToString();
        var isMaturity = response.Title.StartsWith("Maturity:", StringComparison.OrdinalIgnoreCase);
        response.RequiredAmount = isMaturity ? investment.Amount : investment.MonthlyMovementAmount;
        if (isMaturity && investment.MaturityDate.HasValue)
        {
            response.DueDay = investment.MaturityDate.Value.Day;
        }
        var institution = await _institutionRepository.GetAsync(investment.FinancialInstitutionId);
        response.InstitutionName = institution?.Name;
    }
}
