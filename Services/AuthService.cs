using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HospitalAppointmentSystem.Data;
using HospitalAppointmentSystem.Models;
using HospitalAppointmentSystem.ViewModels;

namespace HospitalAppointmentSystem.Services
{
    public interface IAuthService
    {
        Task<(bool Success, string Message, ApplicationUser? User)> ValidateUserAsync(string email, string password);
        Task SignInAsync(HttpContext httpContext, ApplicationUser user, bool rememberMe);
        Task SignOutAsync(HttpContext httpContext);
        Task<(bool Success, string Message)> RegisterPatientAsync(RegisterViewModel model);
        Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
    }

    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<ApplicationUser> _passwordHasher;

        public AuthService(ApplicationDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<ApplicationUser>();
        }

        public async Task<(bool Success, string Message, ApplicationUser? User)> ValidateUserAsync(string email, string password)
        {
            var user = await _context.Users
                .Include(u => u.Patient)
                .Include(u => u.Doctor)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

            if (user == null)
            {
                return (false, "Invalid email address or password.", null);
            }

            if (!user.IsActive)
            {
                return (false, "Your account has been deactivated. Please contact clinic support.", null);
            }

            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verification == PasswordVerificationResult.Failed)
            {
                return (false, "Invalid email address or password.", null);
            }

            return (true, "Login successful.", user);
        }

        public async Task SignInAsync(HttpContext httpContext, ApplicationUser user, bool rememberMe)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            if (user.Role == "Patient" && user.Patient != null)
            {
                claims.Add(new Claim("PatientId", user.Patient.PatientId.ToString()));
            }
            else if (user.Role == "Doctor" && user.Doctor != null)
            {
                claims.Add(new Claim("DoctorId", user.Doctor.DoctorId.ToString()));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(8)
            };

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }

        public async Task SignOutAsync(HttpContext httpContext)
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        public async Task<(bool Success, string Message)> RegisterPatientAsync(RegisterViewModel model)
        {
            var existingUser = await _context.Users.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower());
            if (existingUser)
            {
                return (false, "An account with this email address already exists.");
            }

            var user = new ApplicationUser
            {
                FullName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                Role = "Patient",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            var patient = new Patient
            {
                UserId = user.UserId,
                DateOfBirth = model.DateOfBirth,
                Gender = string.IsNullOrEmpty(model.Gender) ? "Other" : model.Gender,
                Address = model.Address ?? string.Empty,
                EmergencyContact = model.EmergencyContact ?? string.Empty
            };

            await _context.Patients.AddAsync(patient);
            await _context.SaveChangesAsync();

            return (true, "Registration successful! You can now log in.");
        }

        public async Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return (false, "User not found.");
            }

            var verify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
            if (verify == PasswordVerificationResult.Failed)
            {
                return (false, "Current password is incorrect.");
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            return (true, "Password updated successfully.");
        }
    }
}
