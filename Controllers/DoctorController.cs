using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HospitalAppointmentSystem.Data;
using HospitalAppointmentSystem.Models;
using HospitalAppointmentSystem.Services;
using HospitalAppointmentSystem.ViewModels;

namespace HospitalAppointmentSystem.Controllers
{
    [Authorize(Roles = "Doctor")]
    public class DoctorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppointmentService _appointmentService;
        private readonly IAuthService _authService;

        public DoctorController(
            ApplicationDbContext context,
            IAppointmentService appointmentService,
            IAuthService authService)
        {
            _context = context;
            _appointmentService = appointmentService;
            _authService = authService;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<Doctor?> GetCurrentDoctorAsync()
        {
            return await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Department)
                .FirstOrDefaultAsync(d => d.UserId == CurrentUserId);
        }

        // 1. Doctor Dashboard
        public async Task<IActionResult> Index()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return RedirectToAction("Login", "Account");

            var today = DateTime.Today;
            var appointments = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Where(a => a.DoctorId == doctor.DoctorId)
                .ToListAsync();

            var model = new DoctorDashboardViewModel
            {
                DoctorName = doctor.User.FullName,
                Specialization = doctor.Specialization,
                TodayAppointmentsCount = appointments.Count(a => a.AppointmentDate.Date == today && a.Status != "Cancelled" && a.Status != "Rejected"),
                PendingRequestsCount = appointments.Count(a => a.Status == "Pending"),
                UpcomingAppointmentsCount = appointments.Count(a => a.AppointmentDate.Date > today && (a.Status == "Confirmed" || a.Status == "Pending")),
                TotalPatientsTreated = appointments.Select(a => a.PatientId).Distinct().Count(),
                TodayAppointments = appointments.Where(a => a.AppointmentDate.Date == today && a.Status != "Cancelled" && a.Status != "Rejected")
                    .OrderBy(a => a.StartTime).ToList(),
                PendingRequests = appointments.Where(a => a.Status == "Pending")
                    .OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime).Take(5).ToList()
            };

            return View(model);
        }

        // 2. My Schedule / Daily Schedule View
        public async Task<IActionResult> Schedule(DateTime? date)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return RedirectToAction("Login", "Account");

            var targetDate = date ?? DateTime.Today;

            var appointments = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.ConsultationNote)
                .Where(a => a.DoctorId == doctor.DoctorId && a.AppointmentDate.Date == targetDate.Date)
                .OrderBy(a => a.StartTime)
                .ToListAsync();

            ViewBag.SelectedDate = targetDate;
            return View(appointments);
        }

        // 3. Manage Availability Slots
        [HttpGet]
        public async Task<IActionResult> Availability()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return RedirectToAction("Login", "Account");

            var availabilities = await _context.DoctorAvailabilities
                .Where(a => a.DoctorId == doctor.DoctorId && a.AvailableDate >= DateTime.Today)
                .OrderBy(a => a.AvailableDate)
                .ToListAsync();

            var model = new AvailabilityFormViewModel
            {
                DoctorId = doctor.DoctorId,
                AvailableDate = DateTime.Today.AddDays(1),
                StartTime = "09:00",
                EndTime = "17:00",
                SlotDuration = 30,
                ExistingAvailabilities = availabilities
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Availability(AvailabilityFormViewModel model)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return RedirectToAction("Login", "Account");

            TimeSpan startTime = TimeSpan.Zero;
            TimeSpan endTime = TimeSpan.Zero;
            if (!TimeSpan.TryParse(model.StartTime, out startTime) || !TimeSpan.TryParse(model.EndTime, out endTime))
            {
                ModelState.AddModelError(string.Empty, "Invalid time format provided.");
            }
            else if (startTime >= endTime)
            {
                ModelState.AddModelError(string.Empty, "Start time must be earlier than End time.");
            }

            if (!ModelState.IsValid)
            {
                model.ExistingAvailabilities = await _context.DoctorAvailabilities
                    .Where(a => a.DoctorId == doctor.DoctorId && a.AvailableDate >= DateTime.Today)
                    .OrderBy(a => a.AvailableDate)
                    .ToListAsync();
                return View(model);
            }

            var existing = await _context.DoctorAvailabilities
                .FirstOrDefaultAsync(a => a.DoctorId == doctor.DoctorId && a.AvailableDate.Date == model.AvailableDate.Date);

            if (existing != null)
            {
                existing.StartTime = startTime;
                existing.EndTime = endTime;
                existing.SlotDuration = model.SlotDuration;
                existing.IsAvailable = model.IsAvailable;
                _context.DoctorAvailabilities.Update(existing);
            }
            else
            {
                var availability = new DoctorAvailability
                {
                    DoctorId = doctor.DoctorId,
                    AvailableDate = model.AvailableDate.Date,
                    StartTime = startTime,
                    EndTime = endTime,
                    SlotDuration = model.SlotDuration,
                    IsAvailable = model.IsAvailable
                };
                await _context.DoctorAvailabilities.AddAsync(availability);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Availability schedule updated for {model.AvailableDate:MMM dd, yyyy}.";
            return RedirectToAction(nameof(Availability));
        }

        // 4. Pending Requests View
        public async Task<IActionResult> Requests()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return RedirectToAction("Login", "Account");

            var pendingRequests = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Where(a => a.DoctorId == doctor.DoctorId && a.Status == "Pending")
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.StartTime)
                .ToListAsync();

            return View(pendingRequests);
        }

        // 5. Approve Request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return RedirectToAction("Login", "Account");

            var (success, message) = await _appointmentService.UpdateStatusAsync(id, "Confirmed", CurrentUserId);
            if (success) TempData["SuccessMessage"] = "Appointment request approved!";
            else TempData["ErrorMessage"] = message;

            return RedirectToAction(nameof(Requests));
        }

        // 6. Reject Request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return RedirectToAction("Login", "Account");

            var (success, message) = await _appointmentService.UpdateStatusAsync(id, "Rejected", CurrentUserId);
            if (success) TempData["SuccessMessage"] = "Appointment request rejected.";
            else TempData["ErrorMessage"] = message;

            return RedirectToAction(nameof(Requests));
        }

        // 7. Patient Details
        public async Task<IActionResult> PatientDetails(int id)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return RedirectToAction("Login", "Account");

            var patient = await _context.Patients
                .Include(p => p.User)
                .Include(p => p.Appointments.Where(a => a.DoctorId == doctor.DoctorId))
                    .ThenInclude(a => a.ConsultationNote)
                .FirstOrDefaultAsync(p => p.PatientId == id);

            if (patient == null) return NotFound();

            return View(patient);
        }

        // 8. Add/Edit Consultation Notes
        [HttpGet]
        public async Task<IActionResult> AddNote(int appointmentId)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return RedirectToAction("Login", "Account");

            var appointment = await _appointmentService.GetAppointmentDetailsAsync(appointmentId);
            if (appointment == null || appointment.DoctorId != doctor.DoctorId)
            {
                return NotFound();
            }

            int? age = null;
            if (appointment.Patient.DateOfBirth.HasValue)
            {
                age = DateTime.Today.Year - appointment.Patient.DateOfBirth.Value.Year;
            }

            var model = new ConsultationNoteViewModel
            {
                AppointmentId = appointment.AppointmentId,
                DoctorId = doctor.DoctorId,
                PatientName = appointment.Patient.User.FullName,
                PatientGender = appointment.Patient.Gender,
                PatientAge = age,
                AppointmentDate = appointment.AppointmentDate,
                Reason = appointment.Reason,
                Notes = appointment.ConsultationNote?.Notes ?? "",
                Prescription = appointment.ConsultationNote?.Prescription ?? ""
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(ConsultationNoteViewModel model)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.DoctorId = doctor.DoctorId;
            var (success, message) = await _appointmentService.AddConsultationNoteAsync(model);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                return View(model);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Schedule));
        }

        // 9. Profile Management
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return RedirectToAction("Login", "Account");

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
                Departments = await _context.Departments.ToListAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(DoctorFormViewModel model)
        {
            var doctor = await GetCurrentDoctorAsync();
            if (doctor == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                model.Departments = await _context.Departments.ToListAsync();
                return View(model);
            }

            doctor.User.FullName = model.FullName;
            doctor.User.Phone = model.Phone;
            doctor.Qualification = model.Qualification;
            doctor.Specialization = model.Specialization;
            doctor.Experience = model.Experience;
            doctor.ConsultationFee = model.ConsultationFee;
            doctor.Biography = model.Biography ?? string.Empty;

            _context.Users.Update(doctor.User);
            _context.Doctors.Update(doctor);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Doctor profile updated successfully!";
            model.Departments = await _context.Departments.ToListAsync();
            return View(model);
        }
    }
}
