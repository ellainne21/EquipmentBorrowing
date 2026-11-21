using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EquipmentBorrowing.Contracts;

namespace EquipmentBorrowing.Desktop.Api;

// API-02, API-04, API-05, API-06
public sealed class StudentsApi
{
    private readonly ApiClient _api;

    public StudentsApi(ApiClient api) => _api = api;

    public Task<ApiResult<List<StudentDto>>> SearchAsync(string? search, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(search)
            ? "api/v1/students"
            : $"api/v1/students?search={Uri.EscapeDataString(search.Trim())}";
        return _api.GetAsync<List<StudentDto>>(url, ct);
    }

    public Task<ApiResult<StudentDto>> CreateAsync(string? studentNumber, string? fullName, string? contact)
        => _api.PostAsync<StudentDto>("api/v1/students",
            new CreateStudentRequest { StudentNumber = studentNumber, FullName = fullName, Contact = contact });

    public Task<ApiResult<StudentDto>> UpdateAsync(int id, string? studentNumber, string? fullName, string? contact)
        => _api.PutAsync<StudentDto>($"api/v1/students/{id}",
            new UpdateStudentRequest { StudentNumber = studentNumber, FullName = fullName, Contact = contact });

    public Task<ApiResult<StudentDto>> SetEligibilityAsync(int id, bool isEligible)
        => _api.PatchAsync<StudentDto>($"api/v1/students/{id}/eligibility",
            new SetEligibilityRequest { IsEligible = isEligible });
}