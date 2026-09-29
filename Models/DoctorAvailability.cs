using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HospitalAppointmentSystem.Models
{
    public class DoctorAvailability
    {
        [Key]
        public int AvailabilityId { get; set; }

        [Required]
        [ForeignKey("Doctor")]
        public int DoctorId { get; set; }

        [DataType(DataType.Date)]
        public DateTime AvailableDate { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public int SlotDuration { get; set; } = 30; // minutes per slot

        public bool IsAvailable { get; set; } = true;

        // Navigation Property
        public virtual Doctor Doctor { get; set; } = null!;
    }
}
