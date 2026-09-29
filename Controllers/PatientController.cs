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
    [Authorize(Roles = "Patient")]
    public class PatientController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppointmentService _appointmentService;
        private readonly IAuthService _authService;

        public PatientController(
            ApplicationDbContext context,
            IAppointmentService appointmentService,
            IAuthService authService)
        {
            _context = context;
            _appointmentService = appointmentService;
            _authService = authService;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<Patient?> GetCurrentPatientAsync()
        {
            return await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == CurrentUserId);
        }

        // 1. Patient Dashboard
        public async Task<IActionResult> Index()
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null) return RedirectToAction("Login", "Account");

            var appointments = await _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Department)
                .Where(a => a.PatientId == patient.PatientId)
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToListAsync();

            var today = DateTime.Today;
            var nowTime = DateTime.Now.TimeOfDay;

            var model = new PatientDashboardViewModel
            {
                PatientName = patient.User.FullName,
                TotalAppointments = appointments.Count,
                UpcomingAppointmentsCount = appointments.Count(a => 
                    (a.AppointmentDate > today || (a.AppointmentDate == today && a.StartTime >= nowTime)) 
                    && (a.Status == "Confirmed" || a.Status == "Pending")),
                CompletedAppointmentsCount = appointments.Count(a => a.Status == "Completed"),
                CancelledAppointmentsCount = appointments.Count(a => a.Status == "Cancelled" || a.Status == "Rejected"),
                NextAppointment = appointments
                    .Where(a => (a.AppointmentDate > today || (a.AppointmentDate == today && a.StartTime >= nowTime)) 
                             && (a.Status == "Confirmed" || a.Status == "Pending"))
                    .OrderBy(a => a.AppointmentDate)
                    .ThenBy(a => a.StartTime)
                    .FirstOrDefault(),
                RecentAppointments = appointments.Take(5).ToList()
            };

            return View(model);
        }

        // 2. Book Appointment Form
        [HttpGet]
        public async Task<IActionResult> Book(int? doctorId, int? departmentId)
        {
            var departments = await _context.Departments.Where(d => d.IsActive).ToListAsync();
            var doctorsQuery = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Department)
                .Where(d => d.IsActive && d.User.IsActive);

            if (departmentId.HasValue && departmentId.Value > 0)
            {
                doctorsQuery = doctorsQuery.Where(d => d.DepartmentId == departmentId.Value);
            }

            var doctors = await doctorsQuery.ToListAsync();

            Doctor? selectedDoctor = null;
            if (doctorId.HasValue)
            {
                selectedDoctor = doctors.FirstOrDefault(d => d.DoctorId == doctorId.Value);
            }

            var model = new BookAppointmentViewModel
            {
                SelectedDepartmentId = departmentId,
                DoctorId = doctorId ?? (doctors.FirstOrDefault()?.DoctorId ?? 0),
                AppointmentDate = DateTime.Today.AddDays(1),
                Departments = departments,
                Doctors = doctors,
                SelectedDoctor = selectedDoctor ?? doctors.FirstOrDefault()
            };

            if (model.DoctorId > 0)
            {
                model.AvailableSlots = await _appointmentService.GetAvailableSlotsAsync(model.DoctorId, model.AppointmentDate);
            }

            return View(model);
        }

        // POST: Book Appointment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Book(BookAppointmentViewModel model)
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null) return RedirectToAction("Login", "Account");

            if (string.IsNullOrEmpty(model.SelectedSlot))
            {
                ModelState.AddModelError("SelectedSlot", "Please select a time slot.");
            }

            TimeSpan startTime = TimeSpan.Zero;
            if (!TimeSpan.TryParse(model.SelectedSlot, out startTime))
            {
                ModelState.AddModelError("SelectedSlot", "Invalid time slot selected.");
            }

            if (!ModelState.IsValid)
            {
                model.Departments = await _context.Departments.Where(d => d.IsActive).ToListAsync();
                model.Doctors = await _context.Doctors
                    .Include(d => d.User)
                    .Include(d => d.Department)
                    .Where(d => d.IsActive && d.User.IsActive)
                    .ToListAsync();
                model.SelectedDoctor = model.Doctors.FirstOrDefault(d => d.DoctorId == model.DoctorId);
                model.AvailableSlots = await _appointmentService.GetAvailableSlotsAsync(model.DoctorId, model.AppointmentDate);
                return View(model);
            }

            TimeSpan endTime = startTime.Add(TimeSpan.FromMinutes(30)); // 30 mins duration

            var (success, message, appointment) = await _appointmentService.BookAppointmentAsync(
                patient.PatientId,
                model.DoctorId,
                model.AppointmentDate,
                startTime,
                endTime,
                model.Reason);

            if (!success || appointment == null)
            {
                ModelState.AddModelError(string.Empty, message);
                model.Departments = await _context.Departments.Where(d => d.IsActive).ToListAsync();
                model.Doctors = await _context.Doctors
                    .Include(d => d.User)
                    .Include(d => d.Department)
                    .Where(d => d.IsActive && d.User.IsActive)
                    .ToListAsync();
                model.SelectedDoctor = model.Doctors.FirstOrDefault(d => d.DoctorId == model.DoctorId);
                model.AvailableSlots = await _appointmentService.GetAvailableSlotsAsync(model.DoctorId, model.AppointmentDate);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Appointment requested successfully! Reference ID: #{appointment.AppointmentId}";
            return RedirectToAction(nameof(AppointmentDetails), new { id = appointment.AppointmentId });
        }

        // 3. My Appointments List
        public async Task<IActionResult> Appointments(string? status)
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null) return RedirectToAction("Login", "Account");

            var query = _context.Appointments
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Department)
                .Where(a => a.PatientId == patient.PatientId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(a => a.Status.ToLower() == status.ToLower());
            }

            var appointments = await query
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToListAsync();

            ViewBag.CurrentStatusFilter = status;
            return View(appointments);
        }

        // 4. Appointment Details View
        public async Task<IActionResult> AppointmentDetails(int id)
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null) return RedirectToAction("Login", "Account");

            var appointment = await _appointmentService.GetAppointmentDetailsAsync(id);
            if (appointment == null || appointment.PatientId != patient.PatientId)
            {
                return NotFound();
            }

            return View(appointment);
        }

        // 5. Reschedule Appointment
        [HttpGet]
        public async Task<IActionResult> Reschedule(int id)
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null) return RedirectToAction("Login", "Account");

            var appointment = await _appointmentService.GetAppointmentDetailsAsync(id);
            if (appointment == null || appointment.PatientId != patient.PatientId)
            {
                return NotFound();
            }

            if (appointment.Status == "Completed" || appointment.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = $"Cannot reschedule a {appointment.Status.ToLower()} appointment.";
                return RedirectToAction(nameof(Appointments));
            }

            var model = new RescheduleViewModel
            {
                AppointmentId = appointment.AppointmentId,
                DoctorName = appointment.Doctor.User.FullName,
                Specialization = appointment.Doctor.Specialization,
                OldDate = appointment.AppointmentDate,
                OldStartTime = appointment.StartTime,
                NewDate = appointment.AppointmentDate >= DateTime.Today ? appointment.AppointmentDate : DateTime.Today.AddDays(1),
                AvailableSlots = await _appointmentService.GetAvailableSlotsAsync(appointment.DoctorId, appointment.AppointmentDate)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reschedule(RescheduleViewModel model)
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null) return RedirectToAction("Login", "Account");

            TimeSpan startTime = TimeSpan.Zero;
            if (string.IsNullOrEmpty(model.SelectedSlot) || !TimeSpan.TryParse(model.SelectedSlot, out startTime))
            {
                ModelState.AddModelError("SelectedSlot", "Please select a valid time slot.");
            }

            if (!ModelState.IsValid)
            {
                var appt = await _appointmentService.GetAppointmentDetailsAsync(model.AppointmentId);
                model.DoctorName = appt?.Doctor.User.FullName ?? "";
                model.Specialization = appt?.Doctor.Specialization ?? "";
                model.AvailableSlots = appt != null ? await _appointmentService.GetAvailableSlotsAsync(appt.DoctorId, model.NewDate) : new();
                return View(model);
            }

            TimeSpan endTime = startTime.Add(TimeSpan.FromMinutes(30));
            var (success, message) = await _appointmentService.RescheduleAppointmentAsync(
                model.AppointmentId,
                patient.PatientId,
                model.NewDate,
                startTime,
                endTime);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                var appt = await _appointmentService.GetAppointmentDetailsAsync(model.AppointmentId);
                model.DoctorName = appt?.Doctor.User.FullName ?? "";
                model.Specialization = appt?.Doctor.Specialization ?? "";
                model.AvailableSlots = appt != null ? await _appointmentService.GetAvailableSlotsAsync(appt.DoctorId, model.NewDate) : new();
                return View(model);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(AppointmentDetails), new { id = model.AppointmentId });
        }

        // 6. Cancel Appointment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var (success, message) = await _appointmentService.CancelAppointmentAsync(id, CurrentUserId, "Patient");
            if (success)
            {
                TempData["SuccessMessage"] = message;
            }
            else
            {
                TempData["ErrorMessage"] = message;
            }

            return RedirectToAction(nameof(Appointments));
        }

        // 7. Profile Management
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null) return RedirectToAction("Login", "Account");

            var model = new PatientFormViewModel
            {
                PatientId = patient.PatientId,
                UserId = patient.UserId,
                FullName = patient.User.FullName,
                Email = patient.User.Email,
                Phone = patient.User.Phone,
                DateOfBirth = patient.DateOfBirth,
                Gender = patient.Gender,
                Address = patient.Address,
                EmergencyContact = patient.EmergencyContact
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(PatientFormViewModel model)
        {
            var patient = await GetCurrentPatientAsync();
            if (patient == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            patient.User.FullName = model.FullName;
            patient.User.Phone = model.Phone;
            patient.DateOfBirth = model.DateOfBirth;
            patient.Gender = model.Gender;
            patient.Address = model.Address ?? string.Empty;
            patient.EmergencyContact = model.EmergencyContact ?? string.Empty;

            _context.Users.Update(patient.User);
            _context.Patients.Update(patient);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Profile updated successfully!";
            return View(model);
        }

        // 8. Change Password
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (success, message) = await _authService.ChangePasswordAsync(CurrentUserId, model.CurrentPassword, model.NewPassword);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                return View(model);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Index));
        }
    }
}
