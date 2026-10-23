
using Azure.Data.Tables;
using Azure;
using Ice_Task3_NotesAPI.Model;

namespace Ice_Task3_NotesAPI.Services;

public class UserService
{
    private readonly TableClient _usersTable;

    public UserService(TableServiceClient tableServiceClient)
    {
        _usersTable = tableServiceClient.GetTableClient("Users");
    }

    public async Task InitializeAsync()
    {
        await _usersTable.CreateIfNotExistsAsync();
    }
    public async Task<User?> GetByEmailAsync(string email)
    {
        await _usersTable.CreateIfNotExistsAsync();

        string normalizedEmail = email.Trim().ToLowerInvariant();

        await foreach (User user in _usersTable.QueryAsync<User>( filter: $"Email eq '{normalizedEmail.Replace("'", "''")}'"))
        {
            return user;
        }

        return null;
    }

    public async Task<User?> GetByIdAsync(string userId)
    {
        try
        {
            var response = await _usersTable.GetEntityAsync<User>("Users", userId);

            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
    public async Task CreateAsync(User user)
    {
        await _usersTable.CreateIfNotExistsAsync();

        user.Email = user.Email.Trim().ToLowerInvariant();
        user.PartitionKey = "Users";
        user.RowKey = user.UserId;

        await _usersTable.AddEntityAsync(user);
    }

}
