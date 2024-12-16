using DemoRedis.Database;
using DemoRedis.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;

namespace DemoRedis.Controllers;

[Route("api/stackexchange/employee")]
[ApiController]
public class EmployeeTestController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;

    public EmployeeTestController(AppDbContext dbContext, IConnectionMultiplexer redis)
    {
        _dbContext = dbContext;
        _redis = redis;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetEmployee(int id)
    {
        string cacheKey = $"employee:{id}";
        var db = GetRedisDatabase();

        var cachedEmployee = await CacheGetEmployeeAsync(cacheKey, db);
        if (!string.IsNullOrEmpty(cachedEmployee))
        {
            return Ok(System.Text.Json.JsonSerializer.Deserialize<Employee>(cachedEmployee));
        }

        var employee = await _dbContext.Employees.FindAsync(id);
        if (employee == null) return NotFound();

        await CacheSetEmployeeAsync(employee, db);
        return Ok(employee);
    }

    [HttpPost]
    public async Task<IActionResult> CreateEmployee(Employee employee)
    {
        var db = GetRedisDatabase();
        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync();

        await CacheSetEmployeeAsync(employee, db);

        return CreatedAtAction(nameof(GetEmployee), new { id = employee.Id }, employee);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEmployee(int id, Employee updatedEmployee)
    {
        var employee = await _dbContext.Employees.FindAsync(id);
        if (employee == null) return NotFound();

        employee.FirstName = updatedEmployee.FirstName;
        employee.LastName = updatedEmployee.LastName;
        employee.Email = updatedEmployee.Email;
        employee.Department = updatedEmployee.Department;
        employee.Salary = updatedEmployee.Salary;

        await _dbContext.SaveChangesAsync();

        var db = GetRedisDatabase();
        // Update Redis cache
        await CacheSetEmployeeAsync(employee, db);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEmployee(int id)
    {
        var employee = await _dbContext.Employees.FindAsync(id);
        if (employee == null) return NotFound();

        _dbContext.Employees.Remove(employee);
        await _dbContext.SaveChangesAsync();

        // Remove from Redis cache
        var db = GetRedisDatabase();
        string cacheKey = $"employee:{id}";
        await db.KeyDeleteAsync(cacheKey);

        return NoContent();
    }

    [NonAction]
    public async Task CacheSetEmployeeAsync(Employee employee, IDatabase db)
    {
        string cacheKey = $"employee:{employee.Id}";
        await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(employee), TimeSpan.FromMinutes(5));
    }

    [NonAction]
    public async Task<RedisValue> CacheGetEmployeeAsync(string cacheKey, IDatabase db)
    {
        var cachedEmployee = await db.StringGetAsync(cacheKey);
        return cachedEmployee;
    }

    [NonAction]
    public IDatabase GetRedisDatabase()
    {
        return _redis.GetDatabase();
    }
}
