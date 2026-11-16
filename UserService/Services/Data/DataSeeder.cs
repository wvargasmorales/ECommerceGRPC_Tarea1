using UserService.Data;
using UserService.Models;
using BC = BCrypt.Net.BCrypt;

namespace UserService.Services.Data
{
    public static class DataSeeder
    {
        public static void SeedUsers(UserDbContext context)
        {
            var users = new List<User>
        {
            new User
            {
                Email = "juan.perez@empresa.es",
                Name = "Juan",
                LastName = "Pérez",
                PasswordHash = BC.HashPassword("Password123!")
            },
            new User
            {
                Email = "marie.dubois@societe.fr",
                Name = "Marie",
                LastName = "Dubois",
                PasswordHash = BC.HashPassword("Password123!")
            },
            new User
            {
                Email = "john.doe@company.com",
                Name = "John",
                LastName = "Doe",
                PasswordHash = BC.HashPassword("Password123!")
            },
            new User
            {
                Email = "admin@tienda.mx",
                Name = "Admin",
                LastName = "System",
                PasswordHash = BC.HashPassword("Admin123!")
            }
        };

            context.Users.AddRange(users);
            context.SaveChanges();
        }
    }
}
