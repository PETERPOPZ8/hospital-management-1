using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HospitalAppointmentSystem.Data;
using HospitalAppointmentSystem.Models;
using HospitalAppointmentSystem.ViewModels;

namespace HospitalAppointmentSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Home Page
        public async Task<IActionResult> Index()
        {
            var featuredDoctors = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Department)
                .Where(d => d.IsActive && d.User.IsActive)
                .Take(6)
                .ToListAsync();

            var departments = await _context.Departments
                .Where(d => d.IsActive)
                .ToListAsync();

            ViewBag.FeaturedDoctors = featuredDoctors;
            ViewBag.Departments = departments;
            ViewBag.TotalDoctors = await _context.Doctors.CountAsync(d => d.IsActive);
            ViewBag.TotalPatients = await _context.Patients.CountAsync();
            ViewBag.TotalDepartments = await _context.Departments.CountAsync(d => d.IsActive);

            return View();
        }

        // 2. About Us
        public IActionResult About()
        {
            return View();
        }

        // 3. Contact Us
        public IActionResult Contact()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Contact(string name, string email, string subject, string message)
        {
            TempData["SuccessMessage"] = "Thank you for reaching out to Medicare Clinic. Your message has been received and our team will get back to you shortly!";
            return RedirectToAction(nameof(Contact));
        }

        // 4. Doctors Directory
        public async Task<IActionResult> Doctors(string? search, int? departmentId)
        {
            var query = _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Department)
                .Where(d => d.IsActive && d.User.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim().ToLower();
                query = query.Where(d => d.User.FullName.ToLower().Contains(searchLower) 
                                      || d.Specialization.ToLower().Contains(searchLower)
                                      || d.Qualification.ToLower().Contains(searchLower));
            }

            if (departmentId.HasValue && departmentId.Value > 0)
            {
                query = query.Where(d => d.DepartmentId == departmentId.Value);
            }

            ViewBag.Search = search;
            ViewBag.SelectedDepartmentId = departmentId;
            ViewBag.Departments = await _context.Departments.Where(d => d.IsActive).ToListAsync();

            var doctors = await query.ToListAsync();
            return View(doctors);
        }

        // 5. Doctor Details
        public async Task<IActionResult> DoctorDetails(int id)
        {
            var doctor = await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Department)
                .Include(d => d.Availabilities.Where(a => a.AvailableDate >= DateTime.Today && a.IsAvailable))
                .FirstOrDefaultAsync(d => d.DoctorId == id && d.IsActive);

            if (doctor == null)
            {
                return NotFound();
            }

            return View(doctor);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
