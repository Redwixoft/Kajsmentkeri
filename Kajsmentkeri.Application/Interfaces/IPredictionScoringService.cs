﻿namespace Kajsmentkeri.Application.Interfaces;

public interface IPredictionScoringService
{
    Task RecalculateForMatchAsync(Guid matchId);
}
