using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HospitalAppointmentSystem.Models;

namespace HospitalAppointmentSystem.ViewModels
{
    public class PatientDashboardViewModel
    {
        public string PatientName { get; set; } = string.Empty;
        public int TotalAppointments { get; set; }
        public int UpcomingAppointmentsCount { get; set; }
        public int CompletedAppointmentsCount { get; set; }
        public int CancelledAppointmentsCount { get; set; }

        public Appointment? NextAppointment { get; set; }
        public List<Appointment> RecentAppointments { get; set; } = new List<Appointment>();
    }

    public class DoctorDashboardViewModel
    {
        public string DoctorName { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public int TodayAppointmentsCount { get; set; }
        public int PendingRequestsCount { get; set; }
        public int UpcomingAppointmentsCount { get; set; }
        public int TotalPatientsTreated { get; set; }

        public List<Appointment> TodayAppointments { get; set; } = new List<Appointment>();
        public List<Appointment> PendingRequests { get; set; } = new List<Appointment>();
    }

    public class AdminDashboardViewModel
    {
        public int TotalPatients { get; set; }
        public int TotalDoctors { get; set; }
        public int TotalDepartments { get; set; }
        public int TotalAppointments { get; set; }
        public int TodayAppointmentsCount { get; set; }
        public int PendingRequestsCount { get; set; }
        public int CompletedAppointmentsCount { get; set; }
        public int CancelledAppointmentsCount { get; set; }

        public List<Appointment> RecentAppointments { get; set; } = new List<Appointment>();
        public List<Doctor> TopDoctors { get; set; } = new List<Doctor>();
        public List<Department> Departments { get; set; } = new List<Department>();
    }

    public class DoctorFormViewModel
    {
        public int? DoctorId { get; set; }
        public int? UserId { get; set; }

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required.")]
        [Phone]
        public string Phone { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        public string? Password { get; set; } // Required on create, optional on edit

        [Required(ErrorMessage = "Department is required.")]
        public int DepartmentId { get; set; }

        [Required(ErrorMessage = "Qualification is required.")]
        public string Qualification { get; set; } = string.Empty;

        [Required(ErrorMessage = "Specialization is required.")]
        public string Specialization { get; set; } = string.Empty;

        [Range(0, 70)]
        public int Experience { get; set; }

        [Range(0, 100000)]
        public decimal ConsultationFee { get; set; }

        public string Biography { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public List<Department> Departments { get; set; } = new List<Department>();
    }

    public class PatientFormViewModel
    {
        public int? PatientId { get; set; }
        public int? UserId { get; set; }

        [Required(ErrorMessage = "Full Name is required.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required.")]
        [Phone]
        public string Phone { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        public string Gender { get; set; } = "Male";

        public string Address { get; set; } = string.Empty;

        public string EmergencyContact { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }

    public class DepartmentFormViewModel
    {
        public int? DepartmentId { get; set; }

        [Required(ErrorMessage = "Department name is required.")]
        [StringLength(100)]
        public string DepartmentName { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(50)]
        public string IconClass { get; set; } = "bi-hospital";

        public bool IsActive { get; set; } = true;
    }

    public class AvailabilityFormViewModel
    {
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Available date is required.")]
        [DataType(DataType.Date)]
        public DateTime AvailableDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Start time is required.")]
        public string StartTime { get; set; } = "09:00";

        [Required(ErrorMessage = "End time is required.")]
        public string EndTime { get; set; } = "17:00";

        [Range(10, 120)]
        public int SlotDuration { get; set; } = 30; // minutes

        public bool IsAvailable { get; set; } = true;

        public List<DoctorAvailability> ExistingAvailabilities { get; set; } = new List<DoctorAvailability>();
    }

    public class ReportsViewModel
    {
        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(-30);
        public DateTime EndDate { get; set; } = DateTime.Today;

        public int TotalBookings { get; set; }
        public int ConfirmedCount { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }
        public int RejectedCount { get; set; }
        public decimal EstimatedRevenue { get; set; }

        public List<Appointment> ReportAppointments { get; set; } = new List<Appointment>();
        public List<DepartmentReportItem> DepartmentBreakdown { get; set; } = new List<DepartmentReportItem>();
    }

    public class DepartmentReportItem
    {
        public string DepartmentName { get; set; } = string.Empty;
        public int AppointmentCount { get; set; }
        public decimal Revenue { get; set; }
    }
}
