using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HospitalAppointmentSystem.Models
{
    public class ConsultationNote
    {
        [Key]
        public int NoteId { get; set; }

        [Required]
        [ForeignKey("Appointment")]
        public int AppointmentId { get; set; }

        [Required]
        [ForeignKey("Doctor")]
        public int DoctorId { get; set; }

        [Required]
        [StringLength(2000)]
        public string Notes { get; set; } = string.Empty;

        [StringLength(1000)]
        public string Prescription { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual Appointment Appointment { get; set; } = null!;
        public virtual Doctor Doctor { get; set; } = null!;
    }
}
