using DemoRedis.Database;
using DemoRedis.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;

namespace DemoRedis.Controllers;

[Route("api/distributed/employee")]
[ApiController]
public class EmployeeController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IDistributedCache _cache;

    public EmployeeController(AppDbContext dbContext, IDistributedCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    private async Task CacheEmployee(Employee employee)
    {
        string cacheKey = $"employee:{employee.Id}";
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(employee), options);
    }

    private async Task RemoveEmployeeFromCache(int id)
    {
        string cacheKey = $"employee:{id}";
        await _cache.RemoveAsync(cacheKey);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetEmployee(int id)
    {
        string cacheKey = $"employee:{id}";

        // Try getting from cache
        var cachedData = await _cache.GetStringAsync(cacheKey);
        if (cachedData != null)
        {
            var cachedEmployee = JsonSerializer.Deserialize<Employee>(cachedData);
            return Ok(cachedEmployee);
        }

        // Fetch from database
        var employee = await _dbContext.Employees.FindAsync(id);
        if (employee == null) return NotFound();

        // Cache result
        await CacheEmployee(employee);

        return Ok(employee);
    }

    [HttpPost]
    public async Task<IActionResult> CreateEmployee(Employee employee)
    {
        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync();

        // Cache new employee
        await CacheEmployee(employee);

        return CreatedAtAction(nameof(GetEmployee), new { id = employee.Id }, employee);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEmployee(int id, Employee updatedEmployee)
    {
        var employee = await _dbContext.Employees.FindAsync(id);
        if (employee == null) return NotFound();

        // Update employee
        employee.FirstName = updatedEmployee.FirstName;
        employee.LastName = updatedEmployee.LastName;
        employee.Email = updatedEmployee.Email;
        employee.Department = updatedEmployee.Department;
        employee.Salary = updatedEmployee.Salary;

        await _dbContext.SaveChangesAsync();

        // Update cache
        await CacheEmployee(employee);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEmployee(int id)
    {
        var employee = await _dbContext.Employees.FindAsync(id);
        if (employee == null) return NotFound();

        _dbContext.Employees.Remove(employee);
        await _dbContext.SaveChangesAsync();

        // Remove from cache
        await RemoveEmployeeFromCache(id);

        return NoContent();
    }
}
