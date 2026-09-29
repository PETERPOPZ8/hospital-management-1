using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HospitalAppointmentSystem.Models;

namespace HospitalAppointmentSystem.ViewModels
{
    public class TimeSlotViewModel
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string FormattedTime { get; set; } = string.Empty;
        public bool IsBooked { get; set; }
    }

    public class BookAppointmentViewModel
    {
        public int? SelectedDepartmentId { get; set; }

        [Required(ErrorMessage = "Please select a doctor.")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "Please select an appointment date.")]
        [DataType(DataType.Date)]
        public DateTime AppointmentDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Please select a time slot.")]
        public string SelectedSlot { get; set; } = string.Empty; // Format "HH:mm"

        [Required(ErrorMessage = "Please describe the reason for consultation.")]
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
        public string Reason { get; set; } = string.Empty;

        // View Helper Lists
        public List<Department> Departments { get; set; } = new List<Department>();
        public List<Doctor> Doctors { get; set; } = new List<Doctor>();
        public List<TimeSlotViewModel> AvailableSlots { get; set; } = new List<TimeSlotViewModel>();
        public Doctor? SelectedDoctor { get; set; }
    }

    public class RescheduleViewModel
    {
        public int AppointmentId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public DateTime OldDate { get; set; }
        public TimeSpan OldStartTime { get; set; }

        [Required(ErrorMessage = "Please select a new appointment date.")]
        [DataType(DataType.Date)]
        public DateTime NewDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Please select a new time slot.")]
        public string SelectedSlot { get; set; } = string.Empty;

        public List<TimeSlotViewModel> AvailableSlots { get; set; } = new List<TimeSlotViewModel>();
    }

    public class ConsultationNoteViewModel
    {
        public int AppointmentId { get; set; }
        public int DoctorId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientGender { get; set; } = string.Empty;
        public int? PatientAge { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Reason { get; set; } = string.Empty;

        [Required(ErrorMessage = "Consultation notes are required.")]
        [StringLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters.")]
        public string Notes { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Prescription cannot exceed 1000 characters.")]
        public string Prescription { get; set; } = string.Empty;
    }
}
