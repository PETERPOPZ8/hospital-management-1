using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HospitalAppointmentSystem.Data;
using HospitalAppointmentSystem.Models;
using HospitalAppointmentSystem.Services;
using HospitalAppointmentSystem.ViewModels;

namespace HospitalAppointmentSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordHasher<ApplicationUser> _passwordHasher;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<ApplicationUser>();
        }

        // 1. Dashboard Overview
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            var totalPatients = await _context.Patients.CountAsync();
            var totalDoctors = await _context.Doctors.CountAsync(d => d.IsActive);
            var totalDepartments = await _context.Departments.CountAsync(d => d.IsActive);
            var totalAppointments = await _context.Appointments.CountAsync();

            var appointments = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Department)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var model = new AdminDashboardViewModel
            {
                TotalPatients = totalPatients,
                TotalDoctors = totalDoctors,
                TotalDepartments = totalDepartments,
                TotalAppointments = totalAppointments,
                TodayAppointmentsCount = appointments.Count(a => a.AppointmentDate.Date == today),
                PendingRequestsCount = appointments.Count(a => a.Status == "Pending"),
                CompletedAppointmentsCount = appointments.Count(a => a.Status == "Completed"),
                CancelledAppointmentsCount = appointments.Count(a => a.Status == "Cancelled" || a.Status == "Rejected"),
                RecentAppointments = appointments.Take(10).ToList(),
                TopDoctors = await _context.Doctors.Include(d => d.User).Include(d => d.Department).Where(d => d.IsActive).Take(5).ToListAsync(),
                Departments = await _context.Departments.Include(d => d.Doctors).Where(d => d.IsActive).ToListAsync()
            };

            return View(model);
        }

        // 2. Doctor Management (CRUD)
        public async Task<IActionResult> Doctors(string? search)
        {
            var query = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Department)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower().Trim();
                query = query.Where(d => d.User.FullName.ToLower().Contains(s) 
                                      || d.Specialization.ToLower().Contains(s)
                                      || d.Department.DepartmentName.ToLower().Contains(s));
            }

            ViewBag.Search = search;
            var doctors = await query.ToListAsync();
            return View(doctors);
        }

        [HttpGet]
        public async Task<IActionResult> DoctorForm(int? id)
        {
            var departments = await _context.Departments.Where(d => d.IsActive).ToListAsync();

            if (id.HasValue && id.Value > 0)
            {
                var doctor = await _context.Doctors
                    .Include(d => d.User)
                    .FirstOrDefaultAsync(d => d.DoctorId == id.Value);

                if (doctor == null) return NotFound();

                var model = new DoctorFormViewModel
                {
                    DoctorId = doctor.DoctorId,
                    UserId = doctor.UserId,
                    FullName = doctor.User.FullName,
                    Email = doctor.User.Email,
                    Phone = doctor.User.Phone,
                    DepartmentId = doctor.DepartmentId,
                    Qualification = doctor.Qualification,
                    Specialization = doctor.Specialization,
                    Experience = doctor.Experience,
                    ConsultationFee = doctor.ConsultationFee,
                    Biography = doctor.Biography,
                    IsActive = doctor.IsActive && doctor.User.IsActive,
                    Departments = departments
                };

                return View(model);
            }

            return View(new DoctorFormViewModel { Departments = departments });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DoctorForm(DoctorFormViewModel model)
        {
            if (model.DoctorId == null || model.DoctorId == 0)
            {
                if (string.IsNullOrEmpty(model.Password))
                {
                    ModelState.AddModelError("Password", "Password is required for new doctor accounts.");
                }

                bool emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower());
                if (emailExists)
                {
                    ModelState.AddModelError("Email", "An account with this email address already exists.");
                }
            }

            if (!ModelState.IsValid)
            {
                model.Departments = await _context.Departments.Where(d => d.IsActive).ToListAsync();
                return View(model);
            }

            if (model.DoctorId.HasValue && model.DoctorId > 0)
            {
                // Update Doctor
                var doctor = await _context.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.DoctorId == model.DoctorId.Value);
                if (doctor == null) return NotFound();

                doctor.User.FullName = model.FullName;
                doctor.User.Phone = model.Phone;
                doctor.User.IsActive = model.IsActive;

                if (!string.IsNullOrEmpty(model.Password))
                {
                    doctor.User.PasswordHash = _passwordHasher.HashPassword(doctor.User, model.Password);
                }

                doctor.DepartmentId = model.DepartmentId;
                doctor.Qualification = model.Qualification;
                doctor.Specialization = model.Specialization;
                doctor.Experience = model.Experience;
                doctor.ConsultationFee = model.ConsultationFee;
                doctor.Biography = model.Biography ?? string.Empty;
                doctor.IsActive = model.IsActive;

                _context.Users.Update(doctor.User);
                _context.Doctors.Update(doctor);
                TempData["SuccessMessage"] = "Doctor record updated successfully.";
            }
            else
            {
                // Create Doctor
                var user = new ApplicationUser
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Phone = model.Phone,
                    Role = "Doctor",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                user.PasswordHash = _passwordHasher.HashPassword(user, model.Password!);

                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();

                var doctor = new Doctor
                {
                    UserId = user.UserId,
                    DepartmentId = model.DepartmentId,
                    Qualification = model.Qualification,
                    Specialization = model.Specialization,
                    Experience = model.Experience,
                    ConsultationFee = model.ConsultationFee,
                    Biography = model.Biography ?? string.Empty,
                    IsActive = true
                };

                await _context.Doctors.AddAsync(doctor);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "New Doctor added successfully.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Doctors));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDoctor(int id)
        {
            var doctor = await _context.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.DoctorId == id);
            if (doctor != null)
            {
                doctor.IsActive = false;
                doctor.User.IsActive = false;
                _context.Doctors.Update(doctor);
                _context.Users.Update(doctor.User);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Doctor status set to inactive.";
            }
            return RedirectToAction(nameof(Doctors));
        }

        // 3. Patient Management (CRUD)
        public async Task<IActionResult> Patients(string? search)
        {
            var query = _context.Patients
                .Include(p => p.User)
                .Include(p => p.Appointments)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower().Trim();
                query = query.Where(p => p.User.FullName.ToLower().Contains(s) || p.User.Email.ToLower().Contains(s) || p.User.Phone.Contains(s));
            }

            ViewBag.Search = search;
            var patients = await query.ToListAsync();
            return View(patients);
        }

        [HttpGet]
        public async Task<IActionResult> PatientForm(int? id)
        {
            if (id.HasValue && id.Value > 0)
            {
                var patient = await _context.Patients.Include(p => p.User).FirstOrDefaultAsync(p => p.PatientId == id.Value);
                if (patient == null) return NotFound();

                return View(new PatientFormViewModel
                {
                    PatientId = patient.PatientId,
                    UserId = patient.UserId,
                    FullName = patient.User.FullName,
                    Email = patient.User.Email,
                    Phone = patient.User.Phone,
                    DateOfBirth = patient.DateOfBirth,
                    Gender = patient.Gender,
                    Address = patient.Address,
                    EmergencyContact = patient.EmergencyContact,
                    IsActive = patient.User.IsActive
                });
            }

            return View(new PatientFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PatientForm(PatientFormViewModel model)
        {
            if (model.PatientId == null || model.PatientId == 0)
            {
                if (string.IsNullOrEmpty(model.Password))
                {
                    ModelState.AddModelError("Password", "Password is required for new patient accounts.");
                }

                if (await _context.Users.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower()))
                {
                    ModelState.AddModelError("Email", "An account with this email address already exists.");
                }
            }

            if (!ModelState.IsValid) return View(model);

            if (model.PatientId.HasValue && model.PatientId > 0)
            {
                var patient = await _context.Patients.Include(p => p.User).FirstOrDefaultAsync(p => p.PatientId == model.PatientId.Value);
                if (patient == null) return NotFound();

                patient.User.FullName = model.FullName;
                patient.User.Phone = model.Phone;
                patient.User.IsActive = model.IsActive;
                if (!string.IsNullOrEmpty(model.Password))
                {
                    patient.User.PasswordHash = _passwordHasher.HashPassword(patient.User, model.Password);
                }

                patient.DateOfBirth = model.DateOfBirth;
                patient.Gender = model.Gender;
                patient.Address = model.Address ?? string.Empty;
                patient.EmergencyContact = model.EmergencyContact ?? string.Empty;

                _context.Users.Update(patient.User);
                _context.Patients.Update(patient);
                TempData["SuccessMessage"] = "Patient record updated.";
            }
            else
            {
                var user = new ApplicationUser
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Phone = model.Phone,
                    Role = "Patient",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                user.PasswordHash = _passwordHasher.HashPassword(user, model.Password!);

                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();

                var patient = new Patient
                {
                    UserId = user.UserId,
                    DateOfBirth = model.DateOfBirth,
                    Gender = model.Gender,
                    Address = model.Address ?? string.Empty,
                    EmergencyContact = model.EmergencyContact ?? string.Empty
                };

                await _context.Patients.AddAsync(patient);
                TempData["SuccessMessage"] = "New Patient added.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Patients));
        }

        // 4. Department Management (CRUD)
        public async Task<IActionResult> Departments()
        {
            var departments = await _context.Departments
                .Include(d => d.Doctors)
                .ToListAsync();

            return View(departments);
        }

        [HttpGet]
        public async Task<IActionResult> DepartmentForm(int? id)
        {
            if (id.HasValue && id.Value > 0)
            {
                var dept = await _context.Departments.FindAsync(id.Value);
                if (dept == null) return NotFound();

                return View(new DepartmentFormViewModel
                {
                    DepartmentId = dept.DepartmentId,
                    DepartmentName = dept.DepartmentName,
                    Description = dept.Description,
                    IconClass = dept.IconClass,
                    IsActive = dept.IsActive
                });
            }

            return View(new DepartmentFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DepartmentForm(DepartmentFormViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            if (model.DepartmentId.HasValue && model.DepartmentId > 0)
            {
                var dept = await _context.Departments.FindAsync(model.DepartmentId.Value);
                if (dept == null) return NotFound();

                dept.DepartmentName = model.DepartmentName;
                dept.Description = model.Description ?? string.Empty;
                dept.IconClass = model.IconClass ?? "bi-hospital";
                dept.IsActive = model.IsActive;

                _context.Departments.Update(dept);
                TempData["SuccessMessage"] = "Department updated.";
            }
            else
            {
                var dept = new Department
                {
                    DepartmentName = model.DepartmentName,
                    Description = model.Description ?? string.Empty,
                    IconClass = model.IconClass ?? "bi-hospital",
                    IsActive = true
                };
                await _context.Departments.AddAsync(dept);
                TempData["SuccessMessage"] = "Department created.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Departments));
        }

        // 5. Appointment Management
        public async Task<IActionResult> Appointments(string? status)
        {
            var query = _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Department)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(a => a.Status.ToLower() == status.ToLower());
            }

            ViewBag.CurrentStatus = status;
            var appointments = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
            return View(appointments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAppointmentStatus(int appointmentId, string status)
        {
            var appt = await _context.Appointments.FindAsync(appointmentId);
            if (appt != null)
            {
                appt.Status = status;
                appt.UpdatedAt = DateTime.UtcNow;
                _context.Appointments.Update(appt);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Appointment #{appointmentId} status changed to '{status}'.";
            }
            return RedirectToAction(nameof(Appointments));
        }

        // 6. Reports Module
        public async Task<IActionResult> Reports(DateTime? startDate, DateTime? endDate)
        {
            var start = startDate ?? DateTime.Today.AddDays(-30);
            var end = endDate ?? DateTime.Today;

            var appointments = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Department)
                .Where(a => a.AppointmentDate.Date >= start.Date && a.AppointmentDate.Date <= end.Date)
                .ToListAsync();

            var deptBreakdown = appointments
                .GroupBy(a => a.Doctor.Department.DepartmentName)
                .Select(g => new DepartmentReportItem
                {
                    DepartmentName = g.Key,
                    AppointmentCount = g.Count(),
                    Revenue = g.Where(a => a.Status == "Completed" || a.Status == "Confirmed").Sum(a => a.Doctor.ConsultationFee)
                })
                .ToList();

            var model = new ReportsViewModel
            {
                StartDate = start,
                EndDate = end,
                TotalBookings = appointments.Count,
                ConfirmedCount = appointments.Count(a => a.Status == "Confirmed"),
                CompletedCount = appointments.Count(a => a.Status == "Completed"),
                CancelledCount = appointments.Count(a => a.Status == "Cancelled"),
                RejectedCount = appointments.Count(a => a.Status == "Rejected"),
                EstimatedRevenue = appointments.Where(a => a.Status == "Completed" || a.Status == "Confirmed").Sum(a => a.Doctor.ConsultationFee),
                ReportAppointments = appointments,
                DepartmentBreakdown = deptBreakdown
            };

            return View(model);
        }

        // 7. Clinic Settings Page
        public IActionResult Settings()
        {
            return View();
        }
    }
}
