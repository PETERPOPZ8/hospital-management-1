using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HospitalAppointmentSystem.Models
{
    public class Department
    {
        [Key]
        public int DepartmentId { get; set; }

        [Required]
        [StringLength(100)]
        public string DepartmentName { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(50)]
        public string IconClass { get; set; } = "bi-hospital";

        public bool IsActive { get; set; } = true;

        // Navigation Properties
        public virtual ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
    }
}
