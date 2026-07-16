using AutoMapper;
using WealthLedger.Contracts.Api.Requests;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;

namespace WealthLedger.Infrastructure.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<FinancialInstitutionRequestBody, FinancialInstitution>();
        CreateMap<FinancialInstitution, FinancialInstitutionResponse>();

        CreateMap<InvestmentRequestBody, Investment>();
        CreateMap<Investment, InvestmentResponse>();

        CreateMap<TaskItem, TaskItemResponse>();

        CreateMap<CashFlowScheduleItemRequestBody, CashFlowScheduleItem>();
        CreateMap<CashFlowScheduleItem, CashFlowScheduleItemResponse>();

        CreateMap<PassiveIncomeRequestBody, PassiveIncome>();
        CreateMap<PassiveIncome, PassiveIncomeResponse>();

        CreateMap<InvestmentGoalRequestBody, InvestmentGoal>();
        CreateMap<InvestmentGoal, InvestmentGoalResponse>();

        CreateMap<WatchlistItemRequestBody, WatchlistItem>();
        CreateMap<WatchlistItem, WatchlistItemResponse>();

        CreateMap<PortfolioSnapshot, PortfolioSnapshotResponse>();
    }
}
