using System;
using Launcher.Application.Accounts;
using Launcher.Domain.Models;
using StartRide.Core;

namespace StartRide.Services;

public sealed class StartRideOfflineIdService : IOfflineAccountUuidService
{
    public string CreateUuid(string accountName, OfflineUuidGenerationMode mode)
    {
        return CreateUuid(accountName, mode, null);
    }

    public string CreateUuid(string accountName, OfflineUuidGenerationMode mode, string? existingUuid)
    {
        if (mode == OfflineUuidGenerationMode.Manual && StartRidePlayerId.TryNormalize(existingUuid, out string kept))
        {
            return kept;
        }

        if (mode == OfflineUuidGenerationMode.Random)
        {

            return StartRidePlayerId.Create(Guid.NewGuid().ToString("N"));
        }

        return StartRidePlayerId.Create(accountName);
    }

    public bool TryNormalizeUuid(string text, out string uuid)
    {
        return StartRidePlayerId.TryNormalize(text, out uuid);
    }
}
