using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EquipmentBorrowing.Contracts;

namespace EquipmentBorrowing.Desktop.Api;

// API-01, API-08, API-09
public sealed class EquipmentApi
{
    private readonly ApiClient _api;

    public EquipmentApi(ApiClient api) => _api = api;

    public Task<ApiResult<List<EquipmentDto>>> SearchAsync(string? search, bool availableOnly, CancellationToken ct = default)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) parts.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (availableOnly) parts.Add("available=true");

        var url = "api/v1/equipment" + (parts.Count == 0 ? "" : "?" + string.Join("&", parts));
        return _api.GetAsync<List<EquipmentDto>>(url, ct);
    }

    public Task<ApiResult<EquipmentDto>> CreateAsync(string? equipmentCode, string? name, string? description)
        => _api.PostAsync<EquipmentDto>("api/v1/equipment",
            new CreateEquipmentRequest { EquipmentCode = equipmentCode, Name = name, Description = description });

    public Task<ApiResult<EquipmentDto>> UpdateAsync(int id, string? equipmentCode, string? name, string? description)
        => _api.PutAsync<EquipmentDto>($"api/v1/equipment/{id}",
            new UpdateEquipmentRequest { EquipmentCode = equipmentCode, Name = name, Description = description });
}