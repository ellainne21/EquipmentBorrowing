using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EquipmentBorrowing.Contracts;

namespace EquipmentBorrowing.Desktop.Api;

// API-10 to API-16
public sealed class BorrowingsApi
{
    private readonly ApiClient _api;

    public BorrowingsApi(ApiClient api) => _api = api;

    // status: All, Active or Returned. sort: borrowedDate or expectedReturnDate. direction: asc or desc.
    public Task<ApiResult<List<BorrowingDto>>> SearchAsync(
        string? search, string status, string sort, string direction, CancellationToken ct = default)
    {
        var parts = new List<string>
        {
            $"status={status}",
            $"sort={sort}",
            $"direction={direction}"
        };
        if (!string.IsNullOrWhiteSpace(search)) parts.Add($"search={Uri.EscapeDataString(search.Trim())}");

        return _api.GetAsync<List<BorrowingDto>>("api/v1/borrowings?" + string.Join("&", parts), ct);
    }

    // UC-04: active borrowings, earliest due date first
    public Task<ApiResult<List<BorrowingDto>>> ActiveAsync(string? search, CancellationToken ct = default)
        => SearchAsync(search, "Active", "expectedReturnDate", "asc", ct);

    public Task<ApiResult<List<BorrowingDto>>> StudentHistoryAsync(int studentId, CancellationToken ct = default)
        => _api.GetAsync<List<BorrowingDto>>($"api/v1/students/{studentId}/borrowings", ct);

    public Task<ApiResult<List<BorrowingDto>>> EquipmentHistoryAsync(int equipmentId, CancellationToken ct = default)
        => _api.GetAsync<List<BorrowingDto>>($"api/v1/equipment/{equipmentId}/borrowings", ct);

    // UC-03 (API-10)
    public Task<ApiResult<BorrowingDto>> BorrowAsync(int studentId, int equipmentId, DateOnly expectedReturnDate)
        => _api.PostAsync<BorrowingDto>("api/v1/borrowings",
            new CreateBorrowingRequest { StudentId = studentId, EquipmentId = equipmentId, ExpectedReturnDate = expectedReturnDate });

    // UC-06 (API-11): all or nothing
    public Task<ApiResult<CheckoutResponse>> CheckoutAsync(int studentId, List<int> equipmentIds, DateOnly expectedReturnDate)
        => _api.PostAsync<CheckoutResponse>("api/v1/checkout-requests",
            new CheckoutRequest { StudentId = studentId, EquipmentIds = equipmentIds, ExpectedReturnDate = expectedReturnDate });

    // UC-05 (API-12)
    public Task<ApiResult<BorrowingDto>> ReturnAsync(int borrowingId)
        => _api.PostAsync<BorrowingDto>($"api/v1/borrowings/{borrowingId}/return", new ReturnBorrowingRequest());
}