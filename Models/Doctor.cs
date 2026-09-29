using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HospitalAppointmentSystem.Models
{
    public class Doctor
    {
        [Key]
        public int DoctorId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [ForeignKey("Department")]
        public int DepartmentId { get; set; }

        [Required]
        [StringLength(100)]
        public string Qualification { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Specialization { get; set; } = string.Empty;

        [Range(0, 70)]
        public int Experience { get; set; } = 0; // years

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 100000)]
        public decimal ConsultationFee { get; set; } = 0.00m;

        [StringLength(1000)]
        public string Biography { get; set; } = string.Empty;

        [StringLength(255)]
        public string ProfileImage { get; set; } = "default-doctor.jpg";

        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public virtual ApplicationUser User { get; set; } = null!;
        public virtual Department Department { get; set; } = null!;
        public virtual ICollection<DoctorAvailability> Availabilities { get; set; } = new List<DoctorAvailability>();
        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
