using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Contracts;
using EquipmentBorrowing.Domain;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentBorrowing.Api.Controllers;

// API-10 to API-14. UC-03, 04, 05, 06, 09.
// The Lab 4 services decide every business rule inside ONE database transaction (BR-08 to BR-16).
// This controller only (1) checks the shape of the request, (2) checks that the referenced records exist,
// (3) calls the same service the desktop used in Lab 4, and (4) turns the result into an HTTP status.
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status500InternalServerError)]
public class BorrowingsController : ApiControllerBase
{
    private readonly IClock _clock;
    private readonly IStudentRepository _students;
    private readonly IEquipmentRepository _equipment;
    private readonly IBorrowingRepository _borrowings;
    private readonly IBorrowingLookup _lookup;
    private readonly BorrowingQueryService _query;
    private readonly BorrowEquipmentService _borrow;
    private readonly CheckOutMultipleEquipmentService _checkout;
    private readonly ReturnEquipmentService _return;

    public BorrowingsController(
        IClock clock,
        IStudentRepository students,
        IEquipmentRepository equipment,
        IBorrowingRepository borrowings,
        IBorrowingLookup lookup,
        BorrowingQueryService query,
        BorrowEquipmentService borrow,
        CheckOutMultipleEquipmentService checkout,
        ReturnEquipmentService returnService)
    {
        _clock = clock;
        _students = students;
        _equipment = equipment;
        _borrowings = borrowings;
        _lookup = lookup;
        _query = query;
        _borrow = borrow;
        _checkout = checkout;
        _return = returnService;
    }

    // API-14: search, filter and sort (UC-04, UC-09, BR-17). Defaults: status All, sort borrowedDate, direction asc.
    [HttpGet("borrowings")]
    [ProducesResponseType<List<BorrowingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? sort,
        [FromQuery] string? direction,
        CancellationToken cancellationToken)
    {
        BorrowingStatusFilter statusFilter;
        if (string.IsNullOrWhiteSpace(status) || status.Equals("All", StringComparison.OrdinalIgnoreCase))
            statusFilter = BorrowingStatusFilter.All;
        else if (status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            statusFilter = BorrowingStatusFilter.Active;
        else if (status.Equals("Returned", StringComparison.OrdinalIgnoreCase))
            statusFilter = BorrowingStatusFilter.Returned;
        else
            return BadInput("status must be Active, Returned or All.");

        BorrowingSortField sortField;
        if (string.IsNullOrWhiteSpace(sort) || sort.Equals("borrowedDate", StringComparison.OrdinalIgnoreCase))
            sortField = BorrowingSortField.CheckoutDate;
        else if (sort.Equals("expectedReturnDate", StringComparison.OrdinalIgnoreCase))
            sortField = BorrowingSortField.ExpectedReturnDate;
        else
            return BadInput("sort must be borrowedDate or expectedReturnDate.");

        bool descending;
        if (string.IsNullOrWhiteSpace(direction) || direction.Equals("asc", StringComparison.OrdinalIgnoreCase))
            descending = false;
        else if (direction.Equals("desc", StringComparison.OrdinalIgnoreCase))
            descending = true;
        else
            return BadInput("direction must be asc or desc.");

        var criteria = new BorrowingSearchCriteria(search, statusFilter, sortField, descending);
        var result = await _query.SearchAsync(criteria, cancellationToken);
        if (!result.IsSuccess) return ServerFailure(result.ErrorMessage);

        return Ok(result.Items.Select(r => r.ToDto()).ToList());
    }

    // API-13
    [HttpGet("borrowings/{id}")]
    [ProducesResponseType<BorrowingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var record = await _lookup.GetRecordAsync(id, cancellationToken);
        if (record is null) return Missing("The borrowing was not found.");

        return Ok(record.ToDto());
    }

    // API-10: issue ONE item (UC-03). The server decides the checkout date (BR-11).
    [HttpPost("borrowings")]
    [ProducesResponseType<BorrowingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateBorrowingRequest request, CancellationToken cancellationToken)
    {
        if (request.StudentId is null) return BadInput("studentId is required.");
        if (request.EquipmentId is null) return BadInput("equipmentId is required.");
        var dateProblem = CheckReturnDate(request.ExpectedReturnDate);
        if (dateProblem is not null) return BadInput(dateProblem);

        var studentId = request.StudentId.Value;
        var equipmentId = request.EquipmentId.Value;

        if (await _students.GetByIdAsync(studentId, cancellationToken) is null)
            return Missing("The student was not found.");
        if (await _equipment.GetByIdAsync(equipmentId, cancellationToken) is null)
            return Missing("The equipment item was not found.");

        var result = await _borrow.ExecuteAsync(
            studentId, equipmentId, request.ExpectedReturnDate!.Value.ToDateTime(TimeOnly.MinValue), cancellationToken);
        if (!result.IsSuccess) return FromOperation(result);

        return await AfterSave(async () =>
        {
            var created = await ReadActiveAsync(studentId, new[] { equipmentId }, cancellationToken);
            if (created.Count == 0) return ServerFailure("The borrowing was saved, but could not be read back. Refresh to see the current data.");

            return CreatedAtAction(nameof(Get), new { id = created[0].Id }, created[0]);
        });
    }

    // API-11: issue two or three items to one student, all or nothing (UC-06, BR-12, BR-15).
    [HttpPost("checkout-requests")]
    [ProducesResponseType<CheckoutResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken cancellationToken)
    {
        if (request.StudentId is null) return BadInput("studentId is required.");
        if (request.EquipmentIds is null || request.EquipmentIds.Count == 0)
            return BadInput("equipmentIds is required.");
        if (request.EquipmentIds.Count < 2)
            return BadInput("A multi-item checkout needs at least two different items.");
        if (request.EquipmentIds.Distinct().Count() != request.EquipmentIds.Count)
            return BadInput("The same item was listed more than once.");
        var dateProblem = CheckReturnDate(request.ExpectedReturnDate);
        if (dateProblem is not null) return BadInput(dateProblem);

        var studentId = request.StudentId.Value;
        var equipmentIds = request.EquipmentIds;

        if (await _students.GetByIdAsync(studentId, cancellationToken) is null)
            return Missing("The student was not found.");
        foreach (var equipmentId in equipmentIds)
        {
            if (await _equipment.GetByIdAsync(equipmentId, cancellationToken) is null)
                return Missing($"The equipment item with id {equipmentId} was not found. Nothing was checked out.");
        }

        var result = await _checkout.ExecuteAsync(
            studentId, equipmentIds, request.ExpectedReturnDate!.Value.ToDateTime(TimeOnly.MinValue), cancellationToken);
        if (!result.IsSuccess) return FromOperation(result);

        return await AfterSave(async () =>
        {
            var created = await ReadActiveAsync(studentId, equipmentIds, cancellationToken);
            if (created.Count != equipmentIds.Count)
                return ServerFailure("The checkout was saved, but the records could not be read back. Refresh to see the current data.");

            return StatusCode(StatusCodes.Status201Created, new CheckoutResponse { Borrowings = created });
        });
    }

    // API-12: return an active borrowing (UC-05). The record is kept and gets the return date (BR-13).
    [HttpPost("borrowings/{id}/return")]
    [ProducesResponseType<BorrowingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Return(int id, [FromBody] ReturnBorrowingRequest request, CancellationToken cancellationToken)
    {
        var record = await _lookup.GetRecordAsync(id, cancellationToken);
        if (record is null) return Missing("The borrowing was not found.");

        if (record.Status == BorrowingStatus.Returned)
            return ApiError(StatusCodes.Status409Conflict, ErrorCodes.Conflict, "This borrowing was already returned. No change was made.");

        var result = await _return.ExecuteAsync(id, cancellationToken);
        if (!result.IsSuccess) return FromOperation(result);

        return await AfterSave(async () =>
        {
            var returned = await _lookup.GetRecordAsync(id, cancellationToken);
            return returned is null ? Missing("The borrowing was not found.") : Ok(returned.ToDto());
        });
    }

    // The expected return date must be a real date later than the checkout date (BR-11).
    private string? CheckReturnDate(DateOnly? date)
    {
        if (date is null) return "expectedReturnDate is required (format YYYY-MM-DD).";

        var today = DateOnly.FromDateTime(_clock.Today);
        if (date.Value <= today)
            return $"expectedReturnDate must be later than the checkout date ({today:yyyy-MM-dd}).";

        return null;
    }

    // Reads the just-created active borrowings of one student for the given items, in the order of the request.
    // An item can have at most one active borrowing (BR-10), so these are exactly the new records.
    private async Task<List<BorrowingDto>> ReadActiveAsync(
        int studentId, IReadOnlyCollection<int> equipmentIds, CancellationToken cancellationToken)
    {
        var active = await _borrowings.SearchAsync(
            new BorrowingSearchCriteria(Status: BorrowingStatusFilter.Active, StudentId: studentId),
            cancellationToken);

        return equipmentIds
            .Select(id => active.FirstOrDefault(r => r.EquipmentId == id))
            .Where(r => r is not null)
            .Select(r => r!.ToDto())
            .ToList();
    }
}
